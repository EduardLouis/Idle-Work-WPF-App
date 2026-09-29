// [v0.1: LiveTrackerVM] Real-time tracking dashboard ViewModel
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;

namespace IdleWork.App.ViewModels
{
    public class LiveTrackerViewModel : ObservableObject
    {
        private readonly WindowTrackerService _windowTracker;
        private readonly IdleDetectionService _idleDetector;
        private readonly AudioLevelService _audioService;
        private readonly ActivityAggregator _aggregator;
        private readonly DatabaseService _databaseService;
        private readonly Dispatcher _dispatcher;

        private string _activeProcess = "Initializing...";
        private string _activeTitle = "Detecting active window...";
        private string _activeDoc = "";
        private string _activeState = "Active";
        private float _micLevel = 0f;
        private string _idleTimeText = "0s";
        private string _stateColor = "#10B981";
        private string _activeDurationText = "00:00";

        public string ActiveProcess
        {
            get => _activeProcess;
            set => SetProperty(ref _activeProcess, value);
        }

        public string ActiveTitle
        {
            get => _activeTitle;
            set => SetProperty(ref _activeTitle, value);
        }

        public string ActiveDoc
        {
            get => _activeDoc;
            set => SetProperty(ref _activeDoc, value);
        }

        public string ActiveState
        {
            get => _activeState;
            set => SetProperty(ref _activeState, value);
        }

        public float MicLevel
        {
            get => _micLevel;
            set => SetProperty(ref _micLevel, value);
        }

        public string IdleTimeText
        {
            get => _idleTimeText;
            set => SetProperty(ref _idleTimeText, value);
        }

        public string StateColor
        {
            get => _stateColor;
            set => SetProperty(ref _stateColor, value);
        }

        public string ActiveDurationText
        {
            get => _activeDurationText;
            set => SetProperty(ref _activeDurationText, value);
        }

        // [v0.2: MultiMonitor] Sub-activity for secondary monitor window
        private string? _subProcess;
        public string? SubProcess
        {
            get => _subProcess;
            set
            {
                if (SetProperty(ref _subProcess, value))
                    OnPropertyChanged(nameof(HasSubActivity));
            }
        }

        private string? _subTitle;
        public string? SubTitle
        {
            get => _subTitle;
            set => SetProperty(ref _subTitle, value);
        }

        public bool HasSubActivity => !string.IsNullOrEmpty(_subProcess);

        public ObservableCollection<MonitorWindowSnapshot> TopWindows { get; } = new ObservableCollection<MonitorWindowSnapshot>();
        public ObservableCollection<ActivityTimeSpan> RecentActivities { get; } = new ObservableCollection<ActivityTimeSpan>();
        public ObservableCollection<Project> AvailableProjects { get; } = new ObservableCollection<Project>();

        public LiveTrackerViewModel(
            WindowTrackerService windowTracker,
            IdleDetectionService idleDetector,
            AudioLevelService audioService,
            ActivityAggregator aggregator,
            DatabaseService databaseService)
        {
            _windowTracker = windowTracker;
            _idleDetector = idleDetector;
            _audioService = audioService;
            _aggregator = aggregator;
            _databaseService = databaseService;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _audioService.PeakLevelChanged += (s, peak) =>
            {
                _dispatcher.InvokeAsync(() => MicLevel = peak);
            };

            _idleDetector.StateChanged += (s, state) =>
            {
                _dispatcher.InvokeAsync(() =>
                {
                    ActiveState = state;
                    StateColor = state switch
                    {
                        "Active" => "#10B981",
                        "Meeting" => "#F59E0B",
                        "Idle" => "#64748B",
                        _ => "#94A3B8"
                    };
                });
            };

            _aggregator.ActivityUpdated += (s, act) =>
            {
                _dispatcher.InvokeAsync(() =>
                {
                    ActiveProcess = act.ProcessName;
                    ActiveTitle = act.WindowTitle;
                    ActiveDoc = act.DocumentName;
                    SubProcess = act.SubProcessName;
                    SubTitle = act.SubWindowTitle;
                    ActiveDurationText = act.DisplayDuration;
                    IdleTimeText = $"{(int)_idleDetector.IdleDuration.TotalSeconds}s";

                    // Update top windows list
                    TopWindows.Clear();
                    foreach (var tw in _windowTracker.CurrentTopWindows)
                    {
                        TopWindows.Add(tw);
                    }
                });
            };

            _aggregator.ActivityCommitted += (s, act) =>
            {
                _dispatcher.InvokeAsync(() =>
                {
                    RecentActivities.Insert(0, act);
                    if (RecentActivities.Count > 25)
                        RecentActivities.RemoveAt(RecentActivities.Count - 1);
                });
            };

            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            var projects = await _databaseService.GetProjectsAsync();
            foreach (var p in projects)
                AvailableProjects.Add(p);

            var recents = await _databaseService.GetRecentActivitiesAsync(15);
            foreach (var r in recents)
                RecentActivities.Add(r);
        }
    }
}
