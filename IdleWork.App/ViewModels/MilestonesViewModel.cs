// [v0.1: MilestonesVM] In-app Milestones and version history ViewModel
using System;
using System.IO;
using IdleWork.App.Core.Helpers;

namespace IdleWork.App.ViewModels
{
    public class MilestonesViewModel : ObservableObject
    {
        public string Version => AppVersionHelper.Version;
        public string BuildTimestamp => AppVersionHelper.BuildTimestamp;
        public string InformationalVersion => AppVersionHelper.InformationalVersion;
        public string FullTitle => AppVersionHelper.AppTitle;

        private string _milestoneContent = "";
        public string MilestoneContent
        {
            get => _milestoneContent;
            set => SetProperty(ref _milestoneContent, value);
        }

        public MilestonesViewModel()
        {
            LoadMilestones();
        }

        private void LoadMilestones()
        {
            try
            {
                // Try reading MILESTONES.md from application root or working directory
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string? current = appDir;
                string milestonesPath = "";

                for (int i = 0; i < 5; i++)
                {
                    if (string.IsNullOrEmpty(current)) break;
                    string candidate = Path.Combine(current, "MILESTONES.md");
                    if (File.Exists(candidate))
                    {
                        milestonesPath = candidate;
                        break;
                    }
                    current = Directory.GetParent(current)?.FullName;
                }

                if (!string.IsNullOrEmpty(milestonesPath) && File.Exists(milestonesPath))
                {
                    MilestoneContent = File.ReadAllText(milestonesPath);
                }
                else
                {
                    MilestoneContent = $"# Idle-Work Activity Tracker\n\nActive Version: v{Version}\nBuild: {BuildTimestamp}\n\nFeatures:\n- Multi-Monitor Window Tracking\n- 3-Input Idle Sensing (Keyboard, Mouse, Mic)\n- Meeting Mode Detection\n- Smart Persistent Rules\n- Daily Timeline & Weekly Timesheet Matrix";
                }
            }
            catch (Exception ex)
            {
                MilestoneContent = $"Version: v{Version} (Build {BuildTimestamp})\n\nUnable to load MILESTONES.md: {ex.Message}";
            }
        }
    }
}
