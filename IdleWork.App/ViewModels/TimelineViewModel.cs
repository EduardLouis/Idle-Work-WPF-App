// [v0.1: TimelineVM] Daily interactive timeline ViewModel with 1-Click "Assign & Remember"
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using IdleWork.App.Core.Helpers;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;

namespace IdleWork.App.ViewModels
{
    // [v0.2: TimelineSorting] Sorting options for consolidated daily activities
    public enum ActivitySortMode
    {
        Percentage = 0,
        LastActive = 1,
        OldActivity = 2
    }

    // [v0.003: VisualEvidenceGrid] Model for discrete screenshot evidence items shown in multi-column table/grid
    public class ActivityScreenshotItem
    {
        public string ScreenshotPath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string TimeDisplayText { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string? AppDescription { get; set; }
    }

    public class TimelineViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly RuleClassifierService _classifierService;

        private DateTime _selectedDate = DateTime.Today;
        private ActivityTimeSpan? _selectedActivity;
        private Project? _selectedProjectForAssign;
        private bool _rememberRule = true;
        private bool _applyRetroactively = true;
        private string _totalActiveText = "0h 00m";
        private string _totalMeetingText = "0h 00m";
        private string _totalIdleText = "0h 00m";
        private string _statusMessage = "";

        // [v0.003: VisualEvidenceGrid] Collection of all visual screenshots for the selected activity
        public ObservableCollection<ActivityScreenshotItem> SelectedActivityScreenshots { get; } = new ObservableCollection<ActivityScreenshotItem>();
        public bool HasAnyScreenshots => SelectedActivityScreenshots.Count > 0;
        public string ScreenshotCountBadgeText => SelectedActivityScreenshots.Count > 0 ? $"{SelectedActivityScreenshots.Count}" : "";

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    _ = LoadTimelineAsync();
                }
            }
        }

        public string DisplayDateText => SelectedDate.Date == DateTime.Today
            ? $"Today ({SelectedDate:dddd, MMM d, yyyy})"
            : SelectedDate.ToString("dddd, MMM d, yyyy");

        public ActivityTimeSpan? SelectedActivity
        {
            get => _selectedActivity;
            set
            {
                if (SetProperty(ref _selectedActivity, value))
                {
                    UpdateSelectedActivityScreenshots();
                    PopulateAssignmentChoices();
                    OnPropertyChanged(nameof(NoRulesMessageText));
                }
            }
        }

        public void UpdateSelectedActivityScreenshots()
        {
            SelectedActivityScreenshots.Clear();
            if (SelectedActivity == null)
            {
                OnPropertyChanged(nameof(HasAnyScreenshots));
                OnPropertyChanged(nameof(ScreenshotCountBadgeText));
                return;
            }

            var seenPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Primary activity screenshot (if valid on disk)
            if (!string.IsNullOrEmpty(SelectedActivity.ScreenshotPath) && System.IO.File.Exists(SelectedActivity.ScreenshotPath))
            {
                seenPaths.Add(SelectedActivity.ScreenshotPath);
                SelectedActivityScreenshots.Add(new ActivityScreenshotItem
                {
                    ScreenshotPath = SelectedActivity.ScreenshotPath,
                    Timestamp = SelectedActivity.StartTime,
                    TimeDisplayText = SelectedActivity.StartTime.ToString("HH:mm:ss"),
                    Label = "Main Window",
                    ProcessName = SelectedActivity.ProcessName,
                    WindowTitle = SelectedActivity.WindowTitle,
                    AppDescription = SelectedActivity.AppDescription
                });
            }

            // 2. Child intervals screenshots (from multi-monitor sub-activities or periodic interval captures)
            if (SelectedActivity.Intervals != null && SelectedActivity.Intervals.Count > 0)
            {
                foreach (var interval in SelectedActivity.Intervals)
                {
                    if (!string.IsNullOrEmpty(interval.ScreenshotPath) &&
                        System.IO.File.Exists(interval.ScreenshotPath) &&
                        seenPaths.Add(interval.ScreenshotPath))
                    {
                        string subProc = interval.SubProcessName ?? SelectedActivity.ProcessName;
                        string subTitle = interval.SubWindowTitle ?? SelectedActivity.WindowTitle;
                        string label = !string.IsNullOrEmpty(interval.SubProcessName)
                            ? $"Sub: {interval.SubProcessName}"
                            : $"{interval.TimeRangeText}";

                        SelectedActivityScreenshots.Add(new ActivityScreenshotItem
                        {
                            ScreenshotPath = interval.ScreenshotPath,
                            Timestamp = interval.StartTime,
                            TimeDisplayText = interval.StartTime.ToString("HH:mm:ss"),
                            Label = label,
                            ProcessName = subProc,
                            WindowTitle = subTitle,
                            AppDescription = SelectedActivity.AppDescription
                        });
                    }
                }
            }

            OnPropertyChanged(nameof(HasAnyScreenshots));
            OnPropertyChanged(nameof(ScreenshotCountBadgeText));
        }

        // [v0.2: DualAssign] Mode toggle: Classification Rule vs Direct Project
        private bool _isAssignByRule = true;
        public bool IsAssignByRule
        {
            get => _isAssignByRule;
            set
            {
                if (SetProperty(ref _isAssignByRule, value))
                {
                    OnPropertyChanged(nameof(IsAssignByProject));
                    OnPropertyChanged(nameof(DropdownLabelText));
                    PopulateAssignmentChoices();
                }
            }
        }

        public bool IsAssignByProject
        {
            get => !_isAssignByRule;
            set => IsAssignByRule = !value;
        }

        public string DropdownLabelText => IsAssignByRule ? "Select Applicable Rule" : "Select Target Project";

        private bool _hasNoApplicableRules;
        public bool HasNoApplicableRules
        {
            get => _hasNoApplicableRules;
            set => SetProperty(ref _hasNoApplicableRules, value);
        }

        public string NoRulesMessageText => $"No classification rules defined for '{SelectedActivity?.ProcessName}'. Switch to 'Direct Project' to assign a project and automatically learn a new rule.";

        public ObservableCollection<AssignmentChoice> AssignmentChoices { get; } = new ObservableCollection<AssignmentChoice>();
        public object AssignmentChoicesLock { get; } = new object();

        // [v0.003: ThreadSafeSnapshot] Return safe snapshot of assignment choices
        public List<AssignmentChoice> GetAssignmentChoicesSnapshot()
        {
            lock (AssignmentChoicesLock)
            {
                return AssignmentChoices.ToList();
            }
        }

        // [v0.2: QuickCreate] Event to request opening dialog from UI
        public event EventHandler<bool>? RequestCreateNew;
        public ICommand CreateNewCommand { get; }

        private AssignmentChoice? _selectedChoice;
        private AssignmentChoice? _lastValidChoice;
        private bool _isPopulatingChoices;

        public AssignmentChoice? SelectedChoice
        {
            get => _selectedChoice;
            set
            {
                // [v0.2: QuickCreate] Never fire dialog creation when populating choices during grid row selection
                if (_isPopulatingChoices)
                {
                    SetProperty(ref _selectedChoice, value);
                    return;
                }

                if (value != null && value.IsCreateAction)
                {
                    _selectedChoice = value;
                    OnPropertyChanged(nameof(SelectedChoice));
                    // Request dialog (true for Rule, false for Project) ONLY on explicit user selection
                    RequestCreateNew?.Invoke(this, value.IsRule);
                    return;
                }

                if (SetProperty(ref _selectedChoice, value))
                {
                    if (value != null && !value.IsCreateAction)
                    {
                        _lastValidChoice = value;
                    }
                }
            }
        }

        private List<AutoTagRule> _cachedAllRules = new List<AutoTagRule>();

        public Project? SelectedProjectForAssign
        {
            get => _selectedProjectForAssign;
            set => SetProperty(ref _selectedProjectForAssign, value);
        }

        public bool RememberRule
        {
            get => _rememberRule;
            set => SetProperty(ref _rememberRule, value);
        }

        public bool ApplyRetroactively
        {
            get => _applyRetroactively;
            set => SetProperty(ref _applyRetroactively, value);
        }

        public string TotalActiveText
        {
            get => _totalActiveText;
            set => SetProperty(ref _totalActiveText, value);
        }

        public string TotalMeetingText
        {
            get => _totalMeetingText;
            set => SetProperty(ref _totalMeetingText, value);
        }

        public string TotalIdleText
        {
            get => _totalIdleText;
            set => SetProperty(ref _totalIdleText, value);
        }

        // [v0.2: OfflineTracking] Total duration when app was closed/offline
        private string _totalOfflineText = "0h 00m";
        public string TotalOfflineText
        {
            get => _totalOfflineText;
            set => SetProperty(ref _totalOfflineText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<ActivityTimeSpan> Activities { get; } = new ObservableCollection<ActivityTimeSpan>();
        public ObservableCollection<Project> AvailableProjects { get; } = new ObservableCollection<Project>();

        // [v0.2: TimelineBands] Visual ribbon segments inspired by Trackabi / ActivityWatch / ManicTime
        public ObservableCollection<TimelineSegment> UsageSegments { get; } = new ObservableCollection<TimelineSegment>();
        public ObservableCollection<TimelineSegment> AppSegments { get; } = new ObservableCollection<TimelineSegment>();
        public ObservableCollection<TimelineSegment> DocSegments { get; } = new ObservableCollection<TimelineSegment>();

        public ICommand PreviousDayCommand { get; }
        public ICommand NextDayCommand { get; }
        public ICommand TodayCommand { get; }
        public ICommand AssignProjectCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SetSortModeCommand { get; }
        public ICommand PreviewScreenshotCommand { get; }
        public ICommand PreviewIntervalScreenshotCommand { get; }
        public event EventHandler<ActivityTimeSpan>? RequestPreviewScreenshot;

        // [v0.2: TimelineSorting] Sorting options for consolidated daily activities
        private ActivitySortMode _selectedSortMode = ActivitySortMode.Percentage;
        private readonly List<ActivityTimeSpan> _allConsolidatedActivities = new List<ActivityTimeSpan>();

        public ActivitySortMode SelectedSortMode
        {
            get => _selectedSortMode;
            set
            {
                if (SetProperty(ref _selectedSortMode, value))
                {
                    OnPropertyChanged(nameof(SelectedSortIndex));
                    ApplyActivitySorting();
                }
            }
        }

        public int SelectedSortIndex
        {
            get => (int)_selectedSortMode;
            set
            {
                if (value >= 0 && value <= 2 && (int)_selectedSortMode != value)
                {
                    _selectedSortMode = (ActivitySortMode)value;
                    OnPropertyChanged(nameof(SelectedSortIndex));
                    OnPropertyChanged(nameof(SelectedSortMode));
                    ApplyActivitySorting();
                }
            }
        }

        public TimelineViewModel(DatabaseService databaseService, RuleClassifierService classifierService)
        {
            _databaseService = databaseService;
            _classifierService = classifierService;

            PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1));
            TodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today);
            RefreshCommand = new RelayCommand(async () => await LoadTimelineAsync());
            AssignProjectCommand = new RelayCommand(async () => await AssignProjectAsync());
            CreateNewCommand = new RelayCommand(() => RequestCreateNew?.Invoke(this, IsAssignByRule));
            SetSortModeCommand = new RelayCommand(param =>
            {
                if (param is ActivitySortMode mode) SelectedSortMode = mode;
                else if (param is string s && int.TryParse(s, out int idx)) SelectedSortIndex = idx;
            });

            // [v0.003: VisualEvidence] Screenshot preview commands
            PreviewScreenshotCommand = new RelayCommand(param =>
            {
                if (param is ActivityScreenshotItem item && !string.IsNullOrEmpty(item.ScreenshotPath))
                {
                    var dummySpan = new ActivityTimeSpan
                    {
                        ProcessName = item.ProcessName,
                        WindowTitle = item.WindowTitle,
                        AppDescription = item.AppDescription ?? SelectedActivity?.AppDescription,
                        StartTime = item.Timestamp,
                        ScreenshotPath = item.ScreenshotPath
                    };
                    RequestPreviewScreenshot?.Invoke(this, dummySpan);
                }
                else if (SelectedActivityScreenshots.Count > 0)
                {
                    var first = SelectedActivityScreenshots[0];
                    var dummySpan = new ActivityTimeSpan
                    {
                        ProcessName = first.ProcessName,
                        WindowTitle = first.WindowTitle,
                        AppDescription = first.AppDescription ?? SelectedActivity?.AppDescription,
                        StartTime = first.Timestamp,
                        ScreenshotPath = first.ScreenshotPath
                    };
                    RequestPreviewScreenshot?.Invoke(this, dummySpan);
                }
                else if (SelectedActivity != null && SelectedActivity.HasScreenshot)
                {
                    RequestPreviewScreenshot?.Invoke(this, SelectedActivity);
                }
            });

            PreviewIntervalScreenshotCommand = new RelayCommand(param =>
            {
                if (param is ActivityInterval interval && !string.IsNullOrEmpty(interval.ScreenshotPath))
                {
                    var dummySpan = new ActivityTimeSpan
                    {
                        ProcessName = interval.SubProcessName ?? SelectedActivity?.ProcessName ?? "Activity",
                        WindowTitle = interval.SubWindowTitle ?? SelectedActivity?.WindowTitle ?? "",
                        AppDescription = SelectedActivity?.AppDescription,
                        StartTime = interval.StartTime,
                        ScreenshotPath = interval.ScreenshotPath
                    };
                    RequestPreviewScreenshot?.Invoke(this, dummySpan);
                }
            });

            // [v0.004: AsyncRefactoring] Safe fire-and-forget initializations
            LoadProjectsAsync().SafeFireAndForget("TimelineVM_Projects");
            LoadRulesCacheAsync().SafeFireAndForget("TimelineVM_Rules");
            LoadCategoriesAndTagsCacheAsync().SafeFireAndForget("TimelineVM_Categories");
            LoadTimelineAsync().SafeFireAndForget("TimelineVM_Timeline");
        }

        // [v0.2: TimelineSorting] Sorts consolidated activities by Percentage, Last Active, or Old Activity
        public void ApplyActivitySorting()
        {
            void Apply()
            {
                if (_allConsolidatedActivities == null || _allConsolidatedActivities.Count == 0)
                    return;

                var prevSelected = SelectedActivity;
                IEnumerable<ActivityTimeSpan> sorted;

                switch (_selectedSortMode)
                {
                    case ActivitySortMode.Percentage:
                        // Highest percentage / duration first
                        sorted = _allConsolidatedActivities.OrderByDescending(a => a.DurationSeconds);
                        break;
                    case ActivitySortMode.LastActive:
                        // Most recently active first (latest EndTime first)
                        sorted = _allConsolidatedActivities.OrderByDescending(a => a.EndTime);
                        break;
                    case ActivitySortMode.OldActivity:
                        // Oldest activity first (earliest StartTime first)
                        sorted = _allConsolidatedActivities.OrderBy(a => a.StartTime);
                        break;
                    default:
                        sorted = _allConsolidatedActivities.OrderByDescending(a => a.DurationSeconds);
                        break;
                }

                Activities.Clear();
                foreach (var item in sorted)
                {
                    Activities.Add(item);
                }

                if (prevSelected != null)
                {
                    SelectedActivity = Activities.FirstOrDefault(a => a.Id == prevSelected.Id ||
                        (a.ProcessName == prevSelected.ProcessName && a.WindowTitle == prevSelected.WindowTitle))
                        ?? Activities.FirstOrDefault();
                }
            }

            if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
            {
                App.Current.Dispatcher.Invoke(Apply);
            }
            else
            {
                Apply();
            }
        }

        private List<WorkCategory> _cachedCategories = new List<WorkCategory>();
        private List<WorkTag> _cachedTags = new List<WorkTag>();
        public IReadOnlyList<WorkCategory> CachedCategories => _cachedCategories;
        public IReadOnlyList<WorkTag> CachedTags => _cachedTags;

        public async Task LoadCategoriesAndTagsCacheAsync()
        {
            var categories = await _databaseService.GetCategoriesAsync();
            _cachedCategories = categories ?? new List<WorkCategory>();
            var tags = await _databaseService.GetTagsAsync();
            _cachedTags = tags ?? new List<WorkTag>();
        }

        public async Task LoadRulesCacheAsync()
        {
            var rules = await _databaseService.GetRulesAsync();
            _cachedAllRules = rules ?? new List<AutoTagRule>();
            PopulateAssignmentChoices();
        }

        public void PopulateAssignmentChoices()
        {
            lock (AssignmentChoicesLock)
            {
                _isPopulatingChoices = true;
                try
                {
                    AssignmentChoices.Clear();

                    if (IsAssignByRule)
                    {
                        if (SelectedActivity != null && !string.IsNullOrWhiteSpace(SelectedActivity.ProcessName))
                        {
                            string proc = SelectedActivity.ProcessName.Trim();
                            var rulesSnapshot = _cachedAllRules.ToList();
                            // [v0.2: RuleFilter] Filter rules strictly to this application or generic rules
                            var matchingRules = rulesSnapshot
                                .Where(r => r.IsEnabled && (string.IsNullOrWhiteSpace(r.ProcessFilter) ||
                                                            proc.Contains(r.ProcessFilter.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                                            r.ProcessFilter.Trim().Contains(proc, StringComparison.OrdinalIgnoreCase)))
                                .OrderBy(r => r.Priority)
                                .ToList();

                            foreach (var rule in matchingRules)
                            {
                                AssignmentChoices.Add(new AssignmentChoice
                                {
                                    Title = rule.RuleName,
                                    Subtitle = $"→ Target Project: {rule.TargetProject}",
                                    TargetProject = rule.TargetProject,
                                    TargetCategory = rule.TargetCategory,
                                    TargetTags = rule.TargetTags,
                                    IsRule = true,
                                    IsCreateAction = false,
                                    Rule = rule
                                });
                            }
                        }

                        HasNoApplicableRules = AssignmentChoices.Count == 0 && SelectedActivity != null;

                        // [v0.2: QuickCreate] Append Create New Rule action row
                        AssignmentChoices.Add(new AssignmentChoice
                        {
                            Title = "➕ Create New Rule...",
                            Subtitle = SelectedActivity != null && !string.IsNullOrEmpty(SelectedActivity.ProcessName)
                                ? $"Create rule for {SelectedActivity.ProcessName}"
                                : "Configure a new classification rule",
                            IsRule = true,
                            IsCreateAction = true
                        });
                    }
                    else
                    {
                        HasNoApplicableRules = false;
                        var projectsSnapshot = AvailableProjects.ToList();
                        foreach (var proj in projectsSnapshot)
                        {
                            AssignmentChoices.Add(new AssignmentChoice
                            {
                                Title = proj.Name,
                                Subtitle = string.IsNullOrEmpty(proj.Code) ? "" : $"Code: {proj.Code}",
                                TargetProject = proj.Name,
                                IsRule = false,
                                IsCreateAction = false,
                                Project = proj
                            });
                        }

                        // [v0.2: QuickCreate] Append Create New Project action row
                        AssignmentChoices.Add(new AssignmentChoice
                        {
                            Title = "➕ Create New Project...",
                            Subtitle = "Register and configure a new project account",
                            IsRule = false,
                            IsCreateAction = true
                        });
                    }

                    if (AssignmentChoices.Count > 0)
                    {
                        // Select matching project/rule or first non-create action. Never auto-select create action.
                        SelectedChoice = AssignmentChoices.FirstOrDefault(c => !c.IsCreateAction && c.TargetProject == SelectedActivity?.ProjectName)
                                         ?? AssignmentChoices.FirstOrDefault(c => !c.IsCreateAction);
                    }
                    else
                    {
                        SelectedChoice = null;
                    }
                }
                finally
                {
                    _isPopulatingChoices = false;
                }
            }
        }

        // [v0.2: QuickCreate] Revert selection if user canceled creation dialog
        public void CancelCreation()
        {
            if (_selectedChoice != null && _selectedChoice.IsCreateAction)
            {
                SelectedChoice = _lastValidChoice ?? AssignmentChoices.FirstOrDefault(c => !c.IsCreateAction);
            }
        }

        public IReadOnlyList<AutoTagRule> CachedRules => _cachedAllRules;

        // [v0.2: QuickCreate] Save and auto-select newly created project, linking any correlated rules
        public async Task OnNewProjectCreatedAsync(Project newProject, IEnumerable<AutoTagRule>? correlatedRules = null)
        {
            await _databaseService.SaveProjectAsync(newProject).ConfigureAwait(false);

            if (correlatedRules != null)
            {
                foreach (var rule in correlatedRules)
                {
                    rule.TargetProject = newProject.Name;
                    await _databaseService.SaveRuleAsync(rule).ConfigureAwait(false);
                }
                var rules = await _databaseService.GetRulesAsync().ConfigureAwait(false);
                _cachedAllRules = rules ?? new List<AutoTagRule>();
            }

            var projects = await _databaseService.GetProjectsAsync().ConfigureAwait(false);

            void UpdateProjectsUi()
            {
                AvailableProjects.Clear();
                foreach (var p in projects)
                    AvailableProjects.Add(p);

                SelectedProjectForAssign = AvailableProjects.FirstOrDefault(p => p.Name == newProject.Name) ?? AvailableProjects.FirstOrDefault();
                PopulateAssignmentChoices();

                SelectedChoice = AssignmentChoices.FirstOrDefault(c => !c.IsCreateAction && c.TargetProject == newProject.Name);
                StatusMessage = $"Created project '{newProject.Name}'.";
            }

            if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
            {
                App.Current.Dispatcher.Invoke(UpdateProjectsUi);
            }
            else
            {
                UpdateProjectsUi();
            }
        }

        // [v0.2: QuickCreate] Save and auto-select newly created rule
        public async Task OnNewRuleCreatedAsync(AutoTagRule newRule)
        {
            await _databaseService.SaveRuleAsync(newRule).ConfigureAwait(false);
            var rules = await _databaseService.GetRulesAsync().ConfigureAwait(false);

            void UpdateRulesUi()
            {
                _cachedAllRules = rules ?? new List<AutoTagRule>();
                PopulateAssignmentChoices();

                SelectedChoice = AssignmentChoices.FirstOrDefault(c => !c.IsCreateAction && c.Rule?.RuleName == newRule.RuleName);
                StatusMessage = $"Created rule '{newRule.RuleName}'.";
            }

            if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
            {
                App.Current.Dispatcher.Invoke(UpdateRulesUi);
            }
            else
            {
                UpdateRulesUi();
            }
        }

        // [v0.004: AsyncRefactoring] Refactored from async void to async Task
        public async Task LoadProjectsAsync()
        {
            var projects = await _databaseService.GetProjectsAsync();
            AvailableProjects.Clear();
            foreach (var p in projects)
                AvailableProjects.Add(p);

            SelectedProjectForAssign = AvailableProjects.FirstOrDefault();
            PopulateAssignmentChoices();
        }

        private System.Threading.CancellationTokenSource? _loadCts;
        private readonly object _loadLock = new object();

        public async Task LoadTimelineAsync()
        {
            System.Threading.CancellationToken token;
            lock (_loadLock)
            {
                _loadCts?.Cancel();
                _loadCts?.Dispose();
                _loadCts = new System.Threading.CancellationTokenSource();
                token = _loadCts.Token;
            }

            DateTime start = SelectedDate.Date;
            DateTime end = start.AddDays(1).AddTicks(-1);

            List<ActivityTimeSpan> items;
            try
            {
                items = await _databaseService.GetActivitiesForDateRangeAsync(start, end);
            }
            catch
            {
                return;
            }

            if (token.IsCancellationRequested || start != SelectedDate.Date)
                return;

            Activities.Clear();
            UsageSegments.Clear();
            AppSegments.Clear();
            DocSegments.Clear();

            double activeSec = 0;
            double meetingSec = 0;
            double idleSec = 0;
            double offlineSec = 0;

            foreach (var act in items)
            {
                if (act.State == "Active")
                    activeSec += act.DurationSeconds;
                else if (act.State == "Meeting")
                    meetingSec += act.DurationSeconds;
                else if (act.State == "Idle")
                    idleSec += act.DurationSeconds;
                else if (act.State == "Offline")
                    offlineSec += act.DurationSeconds;
            }

            double totalSec = activeSec + meetingSec;
            var tsActive = TimeSpan.FromSeconds(activeSec);
            var tsMeeting = TimeSpan.FromSeconds(meetingSec);
            var tsIdle = TimeSpan.FromSeconds(idleSec);
            var tsOffline = TimeSpan.FromSeconds(offlineSec);

            TotalActiveText = $"{(int)tsActive.TotalHours}h {tsActive.Minutes:D2}m";
            TotalMeetingText = $"{(int)tsMeeting.TotalHours}h {tsMeeting.Minutes:D2}m";
            TotalIdleText = $"{(int)tsIdle.TotalHours}h {tsIdle.Minutes:D2}m";
            TotalOfflineText = $"{(int)tsOffline.TotalHours}h {tsOffline.Minutes:D2}m";

            // [v0.2: SessionConsolidation] Group recurring activities by Process, Title, and Project
            var grouped = items
                .Where(a => a.State != "Idle")
                .GroupBy(a => $"{a.ProcessName}@@{a.WindowTitle}@@{a.ProjectName}")
                .ToList();

            // Color palette helper
            var appColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Revit", "#8B5CF6" },
                { "acad", "#EF4444" },
                { "devenv", "#06B6D4" },
                { "WINWORD", "#3B82F6" },
                { "EXCEL", "#10B981" },
                { "Teams", "#F59E0B" }
            };

            // [v0.004: PerformanceNPlus1] Pre-fetch all child intervals in a single batch query
            var allChunkIds = grouped.SelectMany(g => g.Select(x => x.Id)).Where(id => id > 0).Distinct().ToList();
            var allLoadedIntervals = await _databaseService.GetIntervalsForActivitiesAsync(allChunkIds);
            var intervalsByActivityId = allLoadedIntervals
                .GroupBy(i => i.ActivityId)
                .ToDictionary(g => g.Key, g => g.ToList());

            _allConsolidatedActivities.Clear();
            foreach (var grp in grouped)
            {
                var rep = grp.First();
                // Find first valid screenshot path from chunks if available
                string? firstChunkScreenshot = grp.Select(x => x.ScreenshotPath)
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p) && System.IO.File.Exists(p));

                double consolidatedSec = grp.Sum(x => x.DurationSeconds);

                var consolidated = new ActivityTimeSpan
                {
                    Id = rep.Id,
                    ProcessName = rep.ProcessName,
                    WindowTitle = rep.WindowTitle,
                    DocumentName = rep.DocumentName,
                    ProjectName = rep.ProjectName,
                    Category = rep.Category,
                    Tags = rep.Tags,
                    State = rep.State,
                    StartTime = grp.Min(x => x.StartTime),
                    EndTime = grp.Max(x => x.EndTime),
                    DurationSeconds = consolidatedSec,
                    SubProcessName = rep.SubProcessName,
                    SubWindowTitle = rep.SubWindowTitle,
                    AppDescription = rep.AppDescription,
                    ExecutablePath = rep.ExecutablePath,
                    AppCompany = rep.AppCompany,
                    AppVersion = rep.AppVersion,
                    WindowClassName = rep.WindowClassName,
                    ScreenshotPath = firstChunkScreenshot,
                    PercentageOfDay = totalSec > 0 ? (consolidatedSec / totalSec) * 100 : 0
                };

                // Populate all discrete intervals for this consolidated group via memory dictionary lookup
                var allIntervals = new List<ActivityInterval>();
                foreach (var chunk in grp)
                {
                    if (intervalsByActivityId.TryGetValue(chunk.Id, out var dbIntervals) && dbIntervals.Count > 0)
                    {
                        allIntervals.AddRange(dbIntervals);
                    }
                    else
                    {
                        allIntervals.Add(new ActivityInterval
                        {
                            ActivityId = chunk.Id,
                            StartTime = chunk.StartTime,
                            EndTime = chunk.EndTime,
                            DurationSeconds = chunk.DurationSeconds,
                            SubProcessName = chunk.SubProcessName,
                            SubWindowTitle = chunk.SubWindowTitle,
                            ScreenshotPath = chunk.ScreenshotPath
                        });
                    }
                }

                // If consolidated screenshot wasn't on the chunk, check intervals
                if (string.IsNullOrEmpty(consolidated.ScreenshotPath))
                {
                    consolidated.ScreenshotPath = allIntervals
                        .Select(i => i.ScreenshotPath)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p) && System.IO.File.Exists(p));
                }

                consolidated.Intervals = allIntervals.OrderBy(i => i.StartTime).ToList();
                _allConsolidatedActivities.Add(consolidated);
            }

            ApplyActivitySorting();

            // [v0.2: TimelineRibbons] Generate 24-hour visual ribbon segments (seconds from midnight)
            const double secondsInDay = 86400.0;
            foreach (var act in items)
            {
                double startSecFromMidnight = (act.StartTime - start).TotalSeconds;
                if (startSecFromMidnight < 0) startSecFromMidnight = 0;
                double widthSec = act.DurationSeconds;
                if (widthSec < 60) widthSec = 60; // minimum visible segment

                double leftRatio = Math.Clamp(startSecFromMidnight / secondsInDay, 0.0, 1.0);
                double widthRatio = Math.Clamp(widthSec / secondsInDay, 0.002, 1.0 - leftRatio);

                // Usage Band (Active = green, Meeting = amber, Offline = slate, Idle = red)
                string usageColor = act.State switch
                {
                    "Active" => "#10B981",
                    "Meeting" => "#F59E0B",
                    "Offline" => "#475569", // Dark Slate for Untracked / App Closed
                    _ => "#EF4444"
                };

                string label = act.State switch
                {
                    "Offline" => "Untracked (Closed)",
                    _ => act.State
                };

                UsageSegments.Add(new TimelineSegment
                {
                    LeftRatio = leftRatio,
                    WidthRatio = widthRatio,
                    ColorHex = usageColor,
                    Label = label,
                    ToolTipText = act.State == "Offline"
                        ? $"Untracked (App Closed): {act.StartTime:HH:mm} - {act.EndTime:HH:mm} ({act.DisplayDuration})"
                        : $"{act.State}: {act.StartTime:HH:mm} - {act.EndTime:HH:mm} ({act.DisplayDuration})"
                });

                if (act.State != "Idle")
                {
                    // App Band
                    string appColor = appColors.TryGetValue(act.ProcessName, out var col) ? col : "#38BDF8";
                    AppSegments.Add(new TimelineSegment
                    {
                        LeftRatio = leftRatio,
                        WidthRatio = widthRatio,
                        ColorHex = appColor,
                        Label = act.ProcessName,
                        ToolTipText = $"{act.ProcessName}: {act.DisplayDuration} ({act.StartTime:HH:mm} - {act.EndTime:HH:mm})"
                    });

                    // Doc Band
                    string docName = string.IsNullOrEmpty(act.DocumentName) ? act.WindowTitle : act.DocumentName;
                    DocSegments.Add(new TimelineSegment
                    {
                        LeftRatio = leftRatio,
                        WidthRatio = widthRatio,
                        ColorHex = "#A855F7",
                        Label = docName,
                        ToolTipText = $"{docName}: {act.DisplayDuration}"
                    });
                }
            }

            OnPropertyChanged(nameof(DisplayDateText));
        }

        private async Task AssignProjectAsync()
        {
            if (SelectedActivity == null)
            {
                StatusMessage = "Please select an activity from the timeline first.";
                return;
            }

            if (SelectedChoice == null)
            {
                StatusMessage = IsAssignByRule
                    ? "No classification rule selected. Switch to 'Direct Project' if no rule exists for this app."
                    : "Please select a target project.";
                return;
            }

            if (SelectedChoice.IsRule && SelectedChoice.Rule != null)
            {
                var rule = SelectedChoice.Rule;
                SelectedActivity.ProjectName = rule.TargetProject;
                if (!string.IsNullOrWhiteSpace(rule.TargetCategory))
                    SelectedActivity.Category = rule.TargetCategory;
                if (!string.IsNullOrWhiteSpace(rule.TargetTags))
                    SelectedActivity.Tags = rule.TargetTags;
                SelectedActivity.IsManualEdit = true;
                await _databaseService.SaveActivityAsync(SelectedActivity);

                if (ApplyRetroactively)
                {
                    int updated = await _databaseService.ApplyRuleRetroactivelyAsync(rule);
                    StatusMessage = $"Applied rule '{rule.RuleName}'! Updated {updated} matching activities in the past 7 days.";
                }
                else
                {
                    StatusMessage = $"Applied rule '{rule.RuleName}' (Target: {rule.TargetProject}) to selected activity.";
                }
            }
            else
            {
                SelectedActivity.ProjectName = SelectedChoice.TargetProject;
                SelectedActivity.IsManualEdit = true;
                await _databaseService.SaveActivityAsync(SelectedActivity);

                if (RememberRule)
                {
                    int count = await _classifierService.AssignAndRememberRuleAsync(
                        SelectedActivity,
                        SelectedChoice.TargetProject,
                        SelectedActivity.Category,
                        SelectedActivity.Tags,
                        matchProcess: true,
                        matchDocumentTitle: !string.IsNullOrEmpty(SelectedActivity.DocumentName),
                        applyRetroactively: ApplyRetroactively);

                    StatusMessage = ApplyRetroactively
                        ? $"New rule created & saved! Assigned to {count} activities this week."
                        : "New rule created & saved for future activities!";

                    await LoadRulesCacheAsync();
                }
                else
                {
                    StatusMessage = $"Assigned directly to '{SelectedChoice.TargetProject}'.";
                }
            }

            await LoadTimelineAsync();
        }
    }
}
