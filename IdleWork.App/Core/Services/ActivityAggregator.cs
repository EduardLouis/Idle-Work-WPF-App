// [v0.1: ActivityAggregator] Dwell debouncer and continuous time-span chunker
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Win32;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Native;
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
        private readonly ScreenshotService _screenshotService;

        // [v0.004: ConcurrencyLock] Serialize database writes to prevent SQLite lock contention and race conditions
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private DateTime _suspendTime = DateTime.MinValue;

        private readonly System.Timers.Timer _tickTimer;
        private ActivityTimeSpan? _currentSpan;
        private DateTime _spanStartTime;
        private DateTime _intervalStartTime;
        private DateTime _lastFocusChangeTime;
        private DateTime _lastPeriodicScreenshotTime = DateTime.MinValue;
        private string _pendingProcess = string.Empty;
        private string _pendingTitle = string.Empty;
        private string _pendingDoc = string.Empty;
        private int _pendingMonitor = 0;
        private List<MonitorWindowSnapshot> _pendingTopWindows = new List<MonitorWindowSnapshot>();
        private List<SoftwarePriority> _softwarePriorities = new List<SoftwarePriority>();

        public double DwellDebounceSeconds { get; set; } = 30.0;
        public ActivityTimeSpan? CurrentActivity => _currentSpan;

        public event EventHandler<ActivityTimeSpan>? ActivityUpdated;
        public event EventHandler<ActivityTimeSpan>? ActivityCommitted;

        public ActivityAggregator(
            DatabaseService databaseService,
            RuleClassifierService classifierService,
            WindowTrackerService windowTracker,
            IdleDetectionService idleDetector,
            PluginManager? pluginManager = null,
            ScreenshotService? screenshotService = null)
        {
            _databaseService = databaseService;
            _classifierService = classifierService;
            _windowTracker = windowTracker;
            _idleDetector = idleDetector;
            _pluginManager = pluginManager;
            _screenshotService = screenshotService ?? ScreenshotService.Instance;

            _lastFocusChangeTime = DateTime.Now;
            _spanStartTime = DateTime.Now;
            _intervalStartTime = DateTime.Now;

            // [v0.2: MultiMonitor] Subscribe to multi-monitor window transitions
            _windowTracker.MultiMonitorWindowChanged += WindowTracker_MultiMonitorWindowChanged;
            _idleDetector.StateChanged += IdleDetector_StateChanged;

            // [v0.004: PowerMode] Listen for OS sleep/wake cycles to prevent false active spans
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

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

        private bool _gapDetectionDone;

        // [v0.2: OfflineTracking] Detects and logs periods when the application was closed or inactive
        public async Task DetectAndRecordAppClosedGapAsync()
        {
            if (_gapDetectionDone) return;
            _gapDetectionDone = true;

            try
            {
                var lastAct = await _databaseService.GetMostRecentActivityAsync().ConfigureAwait(false);
                if (lastAct == null) return;

                // [v0.003: OfflineDeduplication] If the most recent activity is already an offline gap within the last minute, skip
                if (lastAct.State == "Offline" && (DateTime.Now - lastAct.EndTime).TotalSeconds < 60)
                    return;

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

            // [v0.003: ExcludeSelfAndOverlays] Skip ignored processes (e.g. IdleWork, SnippingTool)
            if (WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, mainProc, mainTitle))
            {
                return;
            }

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
                // Main app is the same! If sub-activity changed (e.g., user clicked between PDF and CAD on secondary monitor), register sub-activity
                if (_currentSpan.SubProcessName != subProc || _currentSpan.SubWindowTitle != subTitle)
                {
                    // [v0.003: SubActivityIntervals] Save discrete interval for previous sub-activity session
                    double intervalDuration = (now - _intervalStartTime).TotalSeconds;
                    if (intervalDuration >= 1.0 && _currentSpan.Id > 0)
                    {
                        var interval = new ActivityInterval
                        {
                            ActivityId = _currentSpan.Id,
                            StartTime = _intervalStartTime,
                            EndTime = now,
                            DurationSeconds = intervalDuration,
                            SubProcessName = _currentSpan.SubProcessName,
                            SubWindowTitle = _currentSpan.SubWindowTitle,
                            ScreenshotPath = _currentSpan.ScreenshotPath
                        };
                        _ = _databaseService.SaveIntervalAsync(interval);
                    }

                    _currentSpan.SubProcessName = subProc;
                    _currentSpan.SubWindowTitle = subTitle;
                    _currentSpan.SubDocumentName = subDoc;
                    _intervalStartTime = now;

                    // Capture screenshot of newly focused secondary program if enabled
                    if (_screenshotService.EnableScreenshots && _screenshotService.CaptureOnWindowSwitch && !string.IsNullOrEmpty(subProc))
                    {
                        IntPtr hWnd = _windowTracker.ActiveWindowHandle;
                        string? shot = _screenshotService.CaptureWindowScreenshot(hWnd, rawFgMon, subProc);
                        if (!string.IsNullOrEmpty(shot))
                        {
                            _currentSpan.ScreenshotPath = shot;
                        }
                    }
                }

                // Update current span duration
                _currentSpan.EndTime = now;
                _currentSpan.DurationSeconds = (now - _currentSpan.StartTime).TotalSeconds;
                ActivityUpdated?.Invoke(this, _currentSpan);

                // [v0.003: PeriodicScreenshot] Periodic capture while user is actively working
                int intervalMin = _screenshotService.ScreenshotIntervalMinutes;
                if (_screenshotService.EnableScreenshots && intervalMin > 0 && currentState == "Active")
                {
                    if ((now - _lastPeriodicScreenshotTime).TotalMinutes >= intervalMin)
                    {
                        IntPtr hWnd = _windowTracker.ActiveWindowHandle;
                        string? shotPath = _screenshotService.CaptureWindowScreenshot(hWnd, _currentSpan.MonitorIndex, _currentSpan.ProcessName);
                        if (!string.IsNullOrEmpty(shotPath))
                        {
                            _currentSpan.ScreenshotPath = shotPath;
                            _lastPeriodicScreenshotTime = now;
                        }
                    }
                }

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
            if (topWindows == null || topWindows.Count == 0)
            {
                return (fgProc, fgTitle, fgDoc, fgMon, null, null, null);
            }

            // Find top windows matching enabled priorities (Revit, AutoCAD, etc.)
            MonitorWindowSnapshot? bestPriorityWindow = null;
            int bestPriorityRank = int.MaxValue;

            if (_softwarePriorities.Count > 0)
            {
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

            // [v0.003: MultiMonitorSubActivity] Fallback check:
            // If current main activity is active and its window is still visible in topWindows on its monitor,
            // and the user interacts with a window on a different monitor, do NOT change the main activity:
            // retain the main activity and register the focused window as SubActivity!
            if (_currentSpan != null && !string.IsNullOrEmpty(_currentSpan.ProcessName) &&
                !WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, _currentSpan.ProcessName, _currentSpan.WindowTitle))
            {
                var currentSpanWindow = topWindows.FirstOrDefault(w =>
                    w.MonitorIndex == _currentSpan.MonitorIndex &&
                    (w.ProcessName.Equals(_currentSpan.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                     w.WindowTitle.Equals(_currentSpan.WindowTitle, StringComparison.OrdinalIgnoreCase)));

                if (currentSpanWindow != null && fgMon != _currentSpan.MonitorIndex &&
                    (!fgProc.Equals(_currentSpan.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                     !fgTitle.Equals(_currentSpan.WindowTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    return (_currentSpan.ProcessName, _currentSpan.WindowTitle, _currentSpan.DocumentName, _currentSpan.MonitorIndex, fgProc, fgTitle, fgDoc);
                }
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
            // [v0.003: ExcludeSelfAndOverlays] Never create activity spans for ignored processes
            if (!string.IsNullOrEmpty(process) && WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, process, title ?? ""))
            {
                return;
            }

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

                // [v0.003: LoggingService] Record activity span to local log and cloud telemetry
                LoggingService.Instance.LogActivity(spanToSave);

                // [v0.004: ConcurrencyLock] Serialize DB writes and ActivityCommitted invocation
                _ = Task.Run(async () =>
                {
                    await _saveLock.WaitAsync().ConfigureAwait(false);
                    try
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
                            if (!string.IsNullOrEmpty(spanToSave.ScreenshotPath))
                            {
                                existing.ScreenshotPath = spanToSave.ScreenshotPath;
                            }
                            if (string.IsNullOrEmpty(existing.AppDescription) && !string.IsNullOrEmpty(spanToSave.AppDescription))
                            {
                                existing.AppDescription = spanToSave.AppDescription;
                                existing.ExecutablePath = spanToSave.ExecutablePath;
                                existing.AppCompany = spanToSave.AppCompany;
                                existing.AppVersion = spanToSave.AppVersion;
                                existing.WindowClassName = spanToSave.WindowClassName;
                            }
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
                            SubWindowTitle = spanToSave.SubWindowTitle,
                            ScreenshotPath = spanToSave.ScreenshotPath
                        };
                        await _databaseService.SaveIntervalAsync(interval).ConfigureAwait(false);

                        if (_pluginManager != null)
                            await _pluginManager.OnTimeSpanCompletedAsync(spanToSave).ConfigureAwait(false);

                        // [v0.004: ConcurrencyLock] Dispatch ActivityCommitted strictly after DB persistence completes
                        ActivityCommitted?.Invoke(this, spanToSave);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("ActivityAggregator", $"Error persisting committed span: {ex.Message}", ex);
                    }
                    finally
                    {
                        _saveLock.Release();
                    }
                });
            }

            // Start new span
            string proc = process ?? _windowTracker.ActiveProcessName;
            string winTitle = title ?? _windowTracker.ActiveWindowTitle;
            string docName = doc ?? _windowTracker.ActiveDocumentName;

            // [v0.003: RichMetadata] Extract process metadata and window class
            IntPtr currentHwnd = _windowTracker.ActiveWindowHandle;
            var meta = Shell32.GetRichProcessInfo(currentHwnd);

            // [v0.003: Screenshot] Capture screenshot on window switch if enabled
            string? switchShot = null;
            if (_screenshotService.EnableScreenshots && _screenshotService.CaptureOnWindowSwitch && state == "Active")
            {
                switchShot = _screenshotService.CaptureWindowScreenshot(currentHwnd, monitor, proc);
                _lastPeriodicScreenshotTime = now;
            }

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
                SubDocumentName = subDoc,
                AppDescription = meta.AppDescription,
                ExecutablePath = meta.ExePath,
                AppCompany = meta.AppCompany,
                AppVersion = meta.AppVersion,
                WindowClassName = meta.WindowClassName,
                ScreenshotPath = switchShot
            };

            _classifierService.ClassifyActivity(_currentSpan);
            _spanStartTime = now;
            _intervalStartTime = now;

            // [v0.003: SubActivityIntervals] Pre-save newly initiated span to generate DB primary key for child intervals
            var currentSpanToInit = _currentSpan;
            _ = Task.Run(async () =>
            {
                await _saveLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _databaseService.SaveActivityAsync(currentSpanToInit).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("ActivityAggregator", $"Error pre-saving active span: {ex.Message}", ex);
                }
                finally
                {
                    _saveLock.Release();
                }
            });
        }

        // [v0.004: PowerModeChanged] Handle laptop sleep and resume seamlessly without phantom active work spans
        private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                _suspendTime = DateTime.Now;
                LoggingService.Instance.LogInfo("ActivityAggregator", "System entering Suspend/Sleep. Finalizing active span.");
                CommitAndStartNewSpan("Offline", "System", "System Suspended", "Power Suspend");
            }
            else if (e.Mode == PowerModes.Resume)
            {
                DateTime resumeTime = DateTime.Now;
                LoggingService.Instance.LogInfo("ActivityAggregator", "System Resumed from Sleep/Suspend.");
                if (_suspendTime > DateTime.MinValue)
                {
                    double sleepDuration = (resumeTime - _suspendTime).TotalSeconds;
                    if (sleepDuration >= 60.0)
                    {
                        var sleepSpan = new ActivityTimeSpan
                        {
                            StartTime = _suspendTime,
                            EndTime = resumeTime,
                            DurationSeconds = sleepDuration,
                            ProcessName = "System",
                            WindowTitle = "System Sleep / Standby",
                            DocumentName = "Power Sleep",
                            Category = "Untracked",
                            ProjectName = "Untracked",
                            State = "Offline",
                            IsManualEdit = false
                        };
                        _ = Task.Run(async () =>
                        {
                            await _saveLock.WaitAsync().ConfigureAwait(false);
                            try
                            {
                                await _databaseService.SaveActivityAsync(sleepSpan).ConfigureAwait(false);
                                ActivityCommitted?.Invoke(this, sleepSpan);
                            }
                            catch (Exception ex)
                            {
                                LoggingService.Instance.LogError("ActivityAggregator", $"Error saving sleep span: {ex.Message}", ex);
                            }
                            finally
                            {
                                _saveLock.Release();
                            }
                        });
                    }
                    _suspendTime = DateTime.MinValue;
                }

                _lastFocusChangeTime = resumeTime;
                _spanStartTime = resumeTime;
                _intervalStartTime = resumeTime;
                CommitAndStartNewSpan("Active");
            }
        }

        public void Dispose()
        {
            // [v0.004: Cleanup] Unsubscribe OS power events to prevent memory leaks
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
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

            _saveLock.Dispose();
        }
    }
}
