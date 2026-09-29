// [v0.1: TimelineVM] Daily interactive timeline ViewModel with 1-Click "Assign & Remember"
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;

namespace IdleWork.App.ViewModels
{
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

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    LoadTimelineAsync();
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
                    PopulateAssignmentChoices();
                    OnPropertyChanged(nameof(NoRulesMessageText));
                }
            }
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

        public TimelineViewModel(DatabaseService databaseService, RuleClassifierService classifierService)
        {
            _databaseService = databaseService;
            _classifierService = classifierService;

            PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1));
            TodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today);
            RefreshCommand = new RelayCommand(() => LoadTimelineAsync());
            AssignProjectCommand = new RelayCommand(async () => await AssignProjectAsync());
            CreateNewCommand = new RelayCommand(() => RequestCreateNew?.Invoke(this, IsAssignByRule));

            LoadProjectsAsync();
            _ = LoadRulesCacheAsync();
            LoadTimelineAsync();
        }

        public async Task LoadRulesCacheAsync()
        {
            var rules = await _databaseService.GetRulesAsync();
            _cachedAllRules = rules ?? new List<AutoTagRule>();
            PopulateAssignmentChoices();
        }

        public void PopulateAssignmentChoices()
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
                        // [v0.2: RuleFilter] Filter rules strictly to this application or generic rules
                        var matchingRules = _cachedAllRules
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
                    foreach (var proj in AvailableProjects)
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

        public async void LoadProjectsAsync()
        {
            var projects = await _databaseService.GetProjectsAsync();
            AvailableProjects.Clear();
            foreach (var p in projects)
                AvailableProjects.Add(p);

            SelectedProjectForAssign = AvailableProjects.FirstOrDefault();
            PopulateAssignmentChoices();
        }

        public async void LoadTimelineAsync()
        {
            DateTime start = SelectedDate.Date;
            DateTime end = start.AddDays(1).AddTicks(-1);

            var items = await _databaseService.GetActivitiesForDateRangeAsync(start, end);

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

            foreach (var grp in grouped.OrderByDescending(g => g.Sum(x => x.DurationSeconds)))
            {
                var rep = grp.First();
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
                    PercentageOfDay = totalSec > 0 ? (consolidatedSec / totalSec) * 100 : 0
                };

                // Populate all discrete intervals for this consolidated group
                var allIntervals = new List<ActivityInterval>();
                foreach (var chunk in grp)
                {
                    var dbIntervals = await _databaseService.GetIntervalsForActivityAsync(chunk.Id);
                    if (dbIntervals != null && dbIntervals.Count > 0)
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
                            SubWindowTitle = chunk.SubWindowTitle
                        });
                    }
                }

                consolidated.Intervals = allIntervals.OrderBy(i => i.StartTime).ToList();
                Activities.Add(consolidated);
            }

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

            LoadTimelineAsync();
        }
    }
}
