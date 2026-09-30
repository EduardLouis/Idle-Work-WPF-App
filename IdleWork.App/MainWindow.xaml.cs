// [v0.1: MainWindow] Code-behind for MainWindow shell and Milestones modal launcher
using System;
using System.ComponentModel;
using System.Windows;
using IdleWork.App.Core.Services;
using IdleWork.App.ViewModels;
using IdleWork.App.Views;

namespace IdleWork.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _mainViewModel;
        private bool _isExplicitExit;

        public MainWindow()
        {
            App.LogStartup("MainWindow: Calling InitializeComponent...");
            InitializeComponent();
            App.LogStartup("MainWindow: InitializeComponent completed.");

            App.LogStartup("MainWindow: Initializing MainViewModel...");
            _mainViewModel = new MainViewModel();
            DataContext = _mainViewModel;
            App.LogStartup("MainWindow: MainViewModel assigned to DataContext.");

            _mainViewModel.RequestShowMilestones += MainViewModel_RequestShowMilestones;
            
            // [v0.003: SystemTray] Initialize system tray icon and event listeners
            SystemTrayService.Instance.RequestExit += SystemTrayService_RequestExit;
            SystemTrayService.Instance.Initialize(this, OnTrayNavigate);

            // [v0.004: LiveTrayTooltip] Update tray tooltip dynamically with active app, elapsed duration, and status
            _mainViewModel.Aggregator.ActivityUpdated += (s, act) =>
            {
                if (act == null) return;
                var duration = TimeSpan.FromSeconds(act.DurationSeconds);
                string timeStr = duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes}m" : $"{duration.Minutes}m {duration.Seconds:D2}s";
                string tip = $"Idle-Work: {act.ProcessName} ({timeStr}) [{act.State}]";
                SystemTrayService.Instance.UpdateTooltip(tip);
            };

            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            Loaded += (s, e) => App.LogStartup("MainWindow: Window Loaded event triggered.");
        }

        private void OnTrayNavigate(string destination)
        {
            switch (destination)
            {
                case "LiveTracker":
                    _mainViewModel.NavigateLiveTrackerCommand.Execute(null);
                    break;
                case "Timeline":
                    _mainViewModel.NavigateTimelineCommand.Execute(null);
                    break;
                case "WeeklyTimesheet":
                    _mainViewModel.NavigateWeeklyTimesheetCommand.Execute(null);
                    break;
                case "Rules":
                    _mainViewModel.NavigateRulesCommand.Execute(null);
                    break;
                case "Projects":
                    _mainViewModel.NavigateProjectsCommand.Execute(null);
                    break;
                case "Settings":
                    _mainViewModel.NavigateSettingsCommand.Execute(null);
                    break;
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // [v0.003: SystemTray] When minimizing window, hide to system tray near the clock
            if (WindowState == WindowState.Minimized && SystemTrayService.Instance.MinimizeToTray)
            {
                Hide();
                ShowInTaskbar = false;
                SystemTrayService.Instance.NotifyMinimizedToTray();
            }
        }

        private void SystemTrayService_RequestExit(object? sender, EventArgs e)
        {
            _isExplicitExit = true;
            Close();
        }

        private void MainViewModel_RequestShowMilestones(object? sender, EventArgs e)
        {
            var milestonesWin = new MilestonesWindow
            {
                Owner = this
            };
            milestonesWin.ShowDialog();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // [v0.003: SystemTray] Close button (X) minimizes to tray instead of quitting if CloseToTray is active
            if (!_isExplicitExit && SystemTrayService.Instance.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                SystemTrayService.Instance.NotifyMinimizedToTray();
                return;
            }

            App.LogStartup("MainWindow: Closing and disposing services...");
            SystemTrayService.Instance.Dispose();
            _mainViewModel.Dispose();
            App.LogStartup("MainWindow: Disposed.");
        }
    }
}