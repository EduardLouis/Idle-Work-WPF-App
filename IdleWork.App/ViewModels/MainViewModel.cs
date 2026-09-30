// [v0.1: MainVM] Primary navigation and service lifecycle coordinator
using System;
using System.Windows.Input;
using IdleWork.App.Core.Helpers;
using IdleWork.App.Core.Services;
using IdleWork.App.Plugins;

namespace IdleWork.App.ViewModels
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        public DatabaseService Database { get; }
        public AudioLevelService AudioService { get; }
        public IdleDetectionService IdleDetector { get; }
        public WindowTrackerService WindowTracker { get; }
        public RuleClassifierService Classifier { get; }
        public PluginManager Plugins { get; }
        public ActivityAggregator Aggregator { get; }
        public TimesheetService Timesheet { get; }

        public LiveTrackerViewModel LiveTrackerVM { get; }
        public TimelineViewModel TimelineVM { get; }
        public WeeklyTimesheetViewModel WeeklyTimesheetVM { get; }
        public RulesManagerViewModel RulesManagerVM { get; }
        public ProjectsViewModel ProjectsVM { get; }
        public SettingsViewModel SettingsVM { get; }

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        private string _currentPageTitle = "Live Tracker";
        public string CurrentPageTitle
        {
            get => _currentPageTitle;
            set => SetProperty(ref _currentPageTitle, value);
        }

        public string AppTitle => AppVersionHelper.AppTitle;
        public string VersionTag => $"v{AppVersionHelper.Version}";
        public string BuildStamp => AppVersionHelper.BuildTimestamp;

        public ICommand NavigateLiveTrackerCommand { get; }
        public ICommand NavigateTimelineCommand { get; }
        public ICommand NavigateWeeklyTimesheetCommand { get; }
        public ICommand NavigateRulesCommand { get; }
        public ICommand NavigateProjectsCommand { get; }
        public ICommand NavigateSettingsCommand { get; }
        public ICommand ShowMilestonesCommand { get; }

        public event EventHandler? RequestShowMilestones;

        public MainViewModel()
        {
            Database = DatabaseService.Instance;
            AudioService = new AudioLevelService();
            IdleDetector = new IdleDetectionService(AudioService);
            WindowTracker = new WindowTrackerService();
            Classifier = new RuleClassifierService(Database);
            Plugins = new PluginManager();
            Aggregator = new ActivityAggregator(Database, Classifier, WindowTracker, IdleDetector, Plugins);
            Timesheet = new TimesheetService(Database);

            LiveTrackerVM = new LiveTrackerViewModel(WindowTracker, IdleDetector, AudioService, Aggregator, Database);
            TimelineVM = new TimelineViewModel(Database, Classifier);
            WeeklyTimesheetVM = new WeeklyTimesheetViewModel(Timesheet);
            RulesManagerVM = new RulesManagerViewModel(Database, Classifier, Aggregator);
            ProjectsVM = new ProjectsViewModel(Database);
            SettingsVM = new SettingsViewModel(Database, Aggregator, IdleDetector, AudioService);

            _currentView = LiveTrackerVM;

            NavigateLiveTrackerCommand = new RelayCommand(() =>
            {
                CurrentView = LiveTrackerVM;
                CurrentPageTitle = "Live Real-Time Tracker";
            });

            NavigateTimelineCommand = new RelayCommand(() =>
            {
                CurrentView = TimelineVM;
                CurrentPageTitle = "Daily Visual Timeline";
                // [v0.004: CompilerWarnings] Eliminate CS4014 via SafeFireAndForget
                TimelineVM.LoadTimelineAsync().SafeFireAndForget("NavigateTimeline");
            });

            NavigateWeeklyTimesheetCommand = new RelayCommand(() =>
            {
                CurrentView = WeeklyTimesheetVM;
                CurrentPageTitle = "Weekly Timesheet Matrix";
                // [v0.004: CompilerWarnings] Eliminate CS4014 via SafeFireAndForget
                WeeklyTimesheetVM.LoadWeeklyDataAsync().SafeFireAndForget("NavigateWeeklyTimesheet");
            });

            NavigateRulesCommand = new RelayCommand(() =>
            {
                CurrentView = RulesManagerVM;
                CurrentPageTitle = "Smart Rules & Software Priority";
                RulesManagerVM.RefreshAll();
            });

            NavigateProjectsCommand = new RelayCommand(() =>
            {
                CurrentView = ProjectsVM;
                CurrentPageTitle = "Project Management";
                // [v0.004: CompilerWarnings] Eliminate CS4014 via SafeFireAndForget
                ProjectsVM.LoadProjectsAsync().SafeFireAndForget("NavigateProjects");
            });

            NavigateSettingsCommand = new RelayCommand(() =>
            {
                CurrentView = SettingsVM;
                CurrentPageTitle = "Preferences & Diagnostics";
            });

            ShowMilestonesCommand = new RelayCommand(() =>
            {
                RequestShowMilestones?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Dispose()
        {
            Aggregator.Dispose();
            WindowTracker.Dispose();
            IdleDetector.Dispose();
            AudioService.Dispose();
            Plugins.ShutdownAsync().Wait();
        }
    }
}
