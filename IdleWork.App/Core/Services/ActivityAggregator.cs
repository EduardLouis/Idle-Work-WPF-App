// [v0.1: ActivityAggregator] Dwell debouncer and continuous time-span chunker
using System;
using System.Threading.Tasks;
using System.Timers;
using IdleWork.App.Core.Models;
using IdleWork.App.Plugins;

namespace IdleWork.App.Core.Services
{
    public class ActivityAggregator : IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly RuleClassifierService _classifierService;
        private readonly WindowTrackerService _windowTracker;
        private readonly IdleDetectionService _idleDetector;
        private readonly PluginManager? _pluginManager;

        private readonly System.Timers.Timer _tickTimer;
        private ActivityTimeSpan? _currentSpan;
        private DateTime _spanStartTime;
        private DateTime _intervalStartTime;
        private DateTime _lastFocusChangeTime;
        private string _pendingProcess = string.Empty;
        private string _pendingTitle = string.Empty;
        private string _pendingDoc = string.Empty;
        private int _pendingMonitor = 0;
        private List<MonitorWindowSnapshot> _pendingTopWindows = new List<MonitorWindowSnapshot>();
        private List<SoftwarePriority> _softwarePriorities = new List<SoftwarePriority>();

        public double DwellDebounceSeconds { get; set; } = 2.5;
        public ActivityTimeSpan? CurrentActivity => _currentSpan;

        public event EventHandler<ActivityTimeSpan>? ActivityUpdated;
        public event EventHandler<ActivityTimeSpan>? ActivityCommitted;

        public ActivityAggregator(
            DatabaseService databaseService,
            RuleClassifierService classifierService,
            WindowTrackerService windowTracker,
            IdleDetectionService idleDetector,
            PluginManager? pluginManager = null)
        {
            _databaseService = databaseService;
            _classifierService = classifierService;
            _windowTracker = windowTracker;
            _idleDetector = idleDetector;
            _pluginManager = pluginManager;

            _lastFocusChangeTime = DateTime.Now;
            _spanStartTime = DateTime.Now;
            _intervalStartTime = DateTime.Now;

            // [v0.2: MultiMonitor] Subscribe to multi-monitor window transitions
            _windowTracker.MultiMonitorWindowChanged += WindowTracker_MultiMonitorWindowChanged;
            _idleDetector.StateChanged += IdleDetector_StateChanged;

            // Load software priorities
            _ = RefreshPrioritiesAsync();

            // [v0.2: OfflineTracking] Check and record any untracked period when the application was closed/inactive
            _ = DetectAndRecordAppClosedGapAsync();

            _tickTimer = new System.Timers.Timer(1000); // 1-second pulse
            _tickTimer.Elapsed += TickTimer_Elapsed;
            _tickTimer.Start();
        }

        public async Task RefreshPrioritiesAsync()
        {
            _softwarePriorities = await _databaseService.GetSoftwarePrioritiesAsync().ConfigureAwait(false);
        }

        // [v0.2: OfflineTracking] Detects and logs periods when the application was closed or inactive
        public async Task DetectAndRecordAppClosedGapAsync()
        {
            try
            {
                var lastAct = await _databaseService.GetMostRecentActivityAsync().ConfigureAwait(false);
                if (lastAct == null) return;

                DateTime now = DateTime.Now;
                DateTime lastEnd = lastAct.EndTime;

                if (lastEnd >= now) return;

                // Check gap duration
                double gapSeconds = (now - lastEnd).TotalSeconds;
                if (gapSeconds >= 60) // Threshold: minimum 1 minute gap
                {
                    // If the gap crosses midnight (e.g. overnight or machine was turned off yesterday)
                    if (lastEnd.Date < now.Date)
                    {
                        // 1. Record gap for previous day(s) up to midnight
                        DateTime prevDayEnd = lastEnd.Date.AddDays(1).AddTicks(-1);
                        double prevDuration = (prevDayEnd - lastEnd).TotalSeconds;
                        if (prevDuration >= 60)
                        {
                            var prevOfflineSpan = new ActivityTimeSpan
                            {
                                StartTime = lastEnd,
                                EndTime = prevDayEnd,
                                DurationSeconds = prevDuration,
                                ProcessName = "App Inactive",
                                WindowTitle = "App Closed / Untracked",
                                DocumentName = "System Offline",
                                Category = "Untracked",
                                ProjectName = "Untracked",
                                State = "Offline",
                                IsManualEdit = false
                            };
                            await _databaseService.SaveActivityAsync(prevOfflineSpan).ConfigureAwait(false);
                        }

                        // 2. Record gap from midnight today to start moment
                        DateTime todayStart = now.Date;
                        double todayDuration = (now - todayStart).TotalSeconds;
                        if (todayDuration >= 60)
                        {
                            var todayOfflineSpan = new ActivityTimeSpan
                            {
                                StartTime = todayStart,
                                EndTime = now,
                                DurationSeconds = todayDuration,
                                ProcessName = "App Inactive",
                                WindowTitle = "App Closed / Untracked",
                                DocumentName = "System Offline",
                                Category = "Untracked",
                                ProjectName = "Untracked",
                                State = "Offline",
                                IsManualEdit = false
                            };
                            await _databaseService.SaveActivityAsync(todayOfflineSpan).ConfigureAwait(false);
                            ActivityCommitted?.Invoke(this, todayOfflineSpan);
                        }
                    }
                    else
                    {
                        // Gap is within today
                        var offlineSpan = new ActivityTimeSpan
                        {
                            StartTime = lastEnd,
                            EndTime = now,
                            DurationSeconds = gapSeconds,
                            ProcessName = "App Inactive",
                            WindowTitle = "App Closed / Untracked",
                            DocumentName = "System Offline",
                            Category = "Untracked",
                            ProjectName = "Untracked",
                            State = "Offline",
                            IsManualEdit = false
                        };
                        await _databaseService.SaveActivityAsync(offlineSpan).ConfigureAwait(false);
                        ActivityCommitted?.Invoke(this, offlineSpan);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ActivityAggregator] DetectAndRecordAppClosedGapAsync error: {ex.Message}");
            }
        }

        private void WindowTracker_MultiMonitorWindowChanged(
            object? sender,
            (string ProcessName, string Title, string Doc, int Monitor, List<MonitorWindowSnapshot> TopWindows) e)
        {
            _pendingProcess = e.ProcessName;
            _pendingTitle = e.Title;
            _pendingDoc = e.Doc;
            _pendingMonitor = e.Monitor;
            _pendingTopWindows = e.TopWindows;
            _lastFocusChangeTime = DateTime.Now;
        }

        private void IdleDetector_StateChanged(object? sender, string state)
        {
            // Immediate state change boundary (e.g. going Idle or into Meeting Mode)
            CommitAndStartNewSpan(state);
        }

        private void TickTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            string currentState = _idleDetector.CurrentState;

            // [v0.2: MultiMonitor] Resolve hierarchy using pending top windows or current top windows
            var topWindows = _pendingTopWindows.Count > 0 ? _pendingTopWindows : _windowTracker.CurrentTopWindows;
            string rawFgProc = string.IsNullOrEmpty(_pendingProcess) ? _windowTracker.ActiveProcessName : _pendingProcess;
            string rawFgTitle = string.IsNullOrEmpty(_pendingTitle) ? _windowTracker.ActiveWindowTitle : _pendingTitle;
            string rawFgDoc = string.IsNullOrEmpty(_pendingDoc) ? _windowTracker.ActiveDocumentName : _pendingDoc;
            int rawFgMon = _pendingMonitor;

            var (mainProc, mainTitle, mainDoc, mainMon, subProc, subTitle, subDoc) =
                ResolveHierarchy(rawFgProc, rawFgTitle, rawFgDoc, rawFgMon, topWindows);

            // Check if main activity has changed
            if (!string.IsNullOrEmpty(mainProc) &&
                (_currentSpan == null || _currentSpan.ProcessName != mainProc || _currentSpan.WindowTitle != mainTitle))
            {
                double dwellElapsed = (now - _lastFocusChangeTime).TotalSeconds;
                if (dwellElapsed >= DwellDebounceSeconds)
                {
                    CommitAndStartNewSpan(currentState, mainProc, mainTitle, mainDoc, mainMon, subProc, subTitle, subDoc, topWindows);
                    _pendingProcess = string.Empty;
                }
            }
            else if (_currentSpan != null)
            {
                // Main app is the same! If sub-activity changed (e.g., user clicked between PDF and CAD), update sub-activity
                if (_currentSpan.SubProcessName != subProc || _currentSpan.SubWindowTitle != subTitle)
                {
                    _currentSpan.SubProcessName = subProc;
                    _currentSpan.SubWindowTitle = subTitle;
                    _currentSpan.SubDocumentName = subDoc;
                }

                // Update current span duration
                _currentSpan.EndTime = now;
                _currentSpan.DurationSeconds = (now - _currentSpan.StartTime).TotalSeconds;
                ActivityUpdated?.Invoke(this, _currentSpan);

                // Auto-save every 30 seconds to safeguard data
                if (_currentSpan.DurationSeconds > 30 && ((int)_currentSpan.DurationSeconds % 30 == 0))
                {
                    _ = _databaseService.SaveActivityAsync(_currentSpan);
                }
            }
            else
            {
                CommitAndStartNewSpan(currentState, mainProc, mainTitle, mainDoc, mainMon, subProc, subTitle, subDoc, topWindows);
            }
        }

        // [v0.2: PriorityHierarchy] Evaluates all visible top windows against SoftwarePriority rules
        public (string MainProc, string MainTitle, string MainDoc, int MainMon, string? SubProc, string? SubTitle, string? SubDoc)
            ResolveHierarchy(string fgProc, string fgTitle, string fgDoc, int fgMon, List<MonitorWindowSnapshot> topWindows)
        {
            if (topWindows == null || topWindows.Count == 0 || _softwarePriorities.Count == 0)
            {
                return (fgProc, fgTitle, fgDoc, fgMon, null, null, null);
            }

            // Find top windows matching enabled priorities
            MonitorWindowSnapshot? bestPriorityWindow = null;
            int bestPriorityRank = int.MaxValue;

            foreach (var win in topWindows)
            {
                if (string.IsNullOrWhiteSpace(win.ProcessName))
                    continue;

                foreach (var prio in _softwarePriorities)
                {
                    if (!prio.IsEnabled)
                        continue;

                    if (win.ProcessName.Contains(prio.ProcessFilter, StringComparison.OrdinalIgnoreCase) ||
                        prio.ProcessFilter.Contains(win.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (prio.Priority < bestPriorityRank)
                        {
                            bestPriorityRank = prio.Priority;
                            bestPriorityWindow = win;
                        }
                        break;
                    }
                }
            }

            // If a prioritized work software is visible on top of any monitor
            if (bestPriorityWindow != null)
            {
                string mainProc = bestPriorityWindow.ProcessName;
                string mainTitle = bestPriorityWindow.WindowTitle;
                string mainDoc = bestPriorityWindow.DocumentName;
                int mainMon = bestPriorityWindow.MonitorIndex;

                // If user is focused on a different window, assign it as SubActivity
                if (fgProc != mainProc || fgTitle != mainTitle)
                {
                    return (mainProc, mainTitle, mainDoc, mainMon, fgProc, fgTitle, fgDoc);
                }

                return (mainProc, mainTitle, mainDoc, mainMon, null, null, null);
            }

            // Default fallback: focused foreground window
            return (fgProc, fgTitle, fgDoc, fgMon, null, null, null);
        }

        private void CommitAndStartNewSpan(
            string state,
            string? process = null,
            string? title = null,
            string? doc = null,
            int monitor = 0,
            string? subProc = null,
            string? subTitle = null,
            string? subDoc = null,
            List<MonitorWindowSnapshot>? topWindows = null)
        {
            var now = DateTime.Now;

            // Finalize previous span if valid
            if (_currentSpan != null && _currentSpan.DurationSeconds >= 1.0)
            {
                _currentSpan.EndTime = now;
                _currentSpan.DurationSeconds = (_currentSpan.EndTime - _currentSpan.StartTime).TotalSeconds;

                // Classify via persistent smart rules
                _classifierService.ClassifyActivity(_currentSpan);

                var spanToSave = _currentSpan;
                var intervalStart = _intervalStartTime;
                _ = Task.Run(async () =>
                {
                    // Enrich via plugins
                    if (_pluginManager != null)
                        await _pluginManager.EnrichActivityAsync(spanToSave).ConfigureAwait(false);

                    // [v0.2: SessionConsolidation] Check if an activity for this process & title already exists today
                    var existing = await _databaseService.FindTodayActivityAsync(spanToSave.ProcessName, spanToSave.WindowTitle, now).ConfigureAwait(false);
                    int targetActivityId = spanToSave.Id;

                    if (existing != null && existing.Id != spanToSave.Id)
                    {
                        // Append duration to existing activity
                        existing.EndTime = now;
                        existing.DurationSeconds += spanToSave.DurationSeconds;
                        await _databaseService.SaveActivityAsync(existing).ConfigureAwait(false);
                        targetActivityId = existing.Id;
                    }
                    else
                    {
                        await _databaseService.SaveActivityAsync(spanToSave).ConfigureAwait(false);
                        targetActivityId = spanToSave.Id;
                    }

                    // Save discrete interval record
                    var interval = new ActivityInterval
                    {
                        ActivityId = targetActivityId,
                        StartTime = intervalStart,
                        EndTime = now,
                        DurationSeconds = (now - intervalStart).TotalSeconds,
                        SubProcessName = spanToSave.SubProcessName,
                        SubWindowTitle = spanToSave.SubWindowTitle
                    };
                    await _databaseService.SaveIntervalAsync(interval).ConfigureAwait(false);

                    if (_pluginManager != null)
                        await _pluginManager.OnTimeSpanCompletedAsync(spanToSave).ConfigureAwait(false);
                });

                ActivityCommitted?.Invoke(this, spanToSave);
            }

            // Start new span
            string proc = process ?? _windowTracker.ActiveProcessName;
            string winTitle = title ?? _windowTracker.ActiveWindowTitle;
            string docName = doc ?? _windowTracker.ActiveDocumentName;

            _currentSpan = new ActivityTimeSpan
            {
                StartTime = now,
                EndTime = now,
                DurationSeconds = 0,
                ProcessName = string.IsNullOrEmpty(proc) ? "System" : proc,
                WindowTitle = winTitle,
                DocumentName = docName,
                MonitorIndex = monitor,
                State = state,
                SubProcessName = subProc,
                SubWindowTitle = subTitle,
                SubDocumentName = subDoc
            };

            _classifierService.ClassifyActivity(_currentSpan);
            _spanStartTime = now;
            _intervalStartTime = now;
        }

        public void Dispose()
        {
            _tickTimer.Stop();
            _tickTimer.Dispose();

            // Final commit on exit
            if (_currentSpan != null && _currentSpan.DurationSeconds > 1.0)
            {
                _currentSpan.EndTime = DateTime.Now;
                _currentSpan.DurationSeconds = (_currentSpan.EndTime - _currentSpan.StartTime).TotalSeconds;
                _classifierService.ClassifyActivity(_currentSpan);
                try
                {
                    _databaseService.SaveActivityAsync(_currentSpan).Wait(1000);
                }
                catch
                {
                }
            }
        }
    }
}
