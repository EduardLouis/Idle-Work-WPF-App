// [v0.1: MainWindow] Code-behind for MainWindow shell and Milestones modal launcher
using System;
using System.ComponentModel;
using System.Windows;
using IdleWork.App.ViewModels;
using IdleWork.App.Views;

namespace IdleWork.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _mainViewModel;

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
            Closing += MainWindow_Closing;
            Loaded += (s, e) => App.LogStartup("MainWindow: Window Loaded event triggered.");
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
            App.LogStartup("MainWindow: Closing and disposing services...");
            _mainViewModel.Dispose();
            App.LogStartup("MainWindow: Disposed.");
        }
    }
}