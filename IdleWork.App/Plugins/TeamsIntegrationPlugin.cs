// [v0.1: TeamsPlugin] Microsoft Teams meeting duration and active context correlation plugin
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Plugins
{
    public class TeamsIntegrationPlugin : IIdleWorkPlugin
    {
        public string Id => "microsoft.teams";
        public string Name => "Microsoft Teams Correlation";
        public string Description => "Extracts conversation titles, detects active calls/meetings, and enriches meeting time spans.";
        public string Version => "0.1";
        public bool IsEnabled { get; set; } = true;

        private string _lastCallTopic = string.Empty;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task EnrichActivityAsync(ActivityTimeSpan activity)
        {
            if (!IsEnabled)
                return Task.CompletedTask;

            // Check if active window is Teams or title indicates an active meeting
            bool isTeams = activity.ProcessName.Contains("Teams", StringComparison.OrdinalIgnoreCase)
                        || activity.ProcessName.Contains("ms-teams", StringComparison.OrdinalIgnoreCase);

            if (isTeams)
            {
                // Teams title patterns: "Meeting with Project Alpha | Microsoft Teams", "Call with John Doe | Microsoft Teams"
                var match = Regex.Match(activity.WindowTitle, @"^(?:Meeting with|Call with|Chat \|)\s*(.*?)(?:\s*\|.*)?$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    _lastCallTopic = match.Groups[1].Value.Trim();
                }

                if (activity.WindowTitle.Contains("Meeting", StringComparison.OrdinalIgnoreCase) ||
                    activity.WindowTitle.Contains("Call", StringComparison.OrdinalIgnoreCase))
                {
                    activity.State = "Meeting";
                    if (string.IsNullOrEmpty(activity.ProjectName))
                    {
                        activity.ProjectName = "Meetings & Calls";
                    }
                    activity.Category = "Teams Meeting";
                    if (!string.IsNullOrEmpty(_lastCallTopic))
                    {
                        activity.DocumentName = $"Teams: {_lastCallTopic}";
                    }
                }
            }
            else if (activity.State == "Meeting" && !string.IsNullOrEmpty(_lastCallTopic))
            {
                // Cross-app correlation: user is looking at another window (e.g. Revit/PDF) while in a Teams meeting
                activity.Category = "Meeting & Active Context";
                if (!string.IsNullOrEmpty(activity.Tags) && !activity.Tags.Contains("Teams"))
                    activity.Tags += ", Teams Call";
                else if (string.IsNullOrEmpty(activity.Tags))
                    activity.Tags = "Teams Call";
            }

            return Task.CompletedTask;
        }

        public Task OnTimeSpanCompletedAsync(ActivityTimeSpan activity)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}
