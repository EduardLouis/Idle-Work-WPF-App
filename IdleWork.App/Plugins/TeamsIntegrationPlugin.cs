// [v0.003: TeamsPlugin] Microsoft Teams meeting, call, and chat context correlation plugin
using System;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Native;

namespace IdleWork.App.Plugins
{
    public class TeamsIntegrationPlugin : IIdleWorkPlugin
    {
        public string Id => "microsoft.teams";
        public string Name => "Microsoft Teams Correlation";
        public string Description => "Extracts conversation titles, participants, active calls/meetings, and enriches meeting time spans.";
        public string Version => "0.003";
        public bool IsEnabled { get; set; } = true;

        private string _lastCallTopic = string.Empty;
        private string _lastParticipant = string.Empty;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task EnrichActivityAsync(ActivityTimeSpan activity)
        {
            if (!IsEnabled)
                return Task.CompletedTask;

            // Check if active window is Teams (ms-teams.exe or Teams.exe)
            bool isTeamsMain = activity.ProcessName.Contains("Teams", StringComparison.OrdinalIgnoreCase)
                            || activity.ProcessName.Contains("ms-teams", StringComparison.OrdinalIgnoreCase);

            bool isTeamsSub = !string.IsNullOrEmpty(activity.SubProcessName) &&
                             (activity.SubProcessName.Contains("Teams", StringComparison.OrdinalIgnoreCase) ||
                              activity.SubProcessName.Contains("ms-teams", StringComparison.OrdinalIgnoreCase));

            if (isTeamsMain)
            {
                var fgHwnd = User32.GetForegroundWindow();
                var convInfo = TeamsInteropService.GetCurrentConversationInfo(activity.WindowTitle, fgHwnd);

                if (convInfo.HasDetails)
                {
                    activity.DocumentName = convInfo.FormatDocumentSummary();

                    if (!string.IsNullOrEmpty(convInfo.Title))
                        _lastCallTopic = convInfo.Title;
                    if (!string.IsNullOrEmpty(convInfo.Participant))
                        _lastParticipant = convInfo.Participant;

                    if (convInfo.IsActiveMeetingOrCall)
                    {
                        activity.State = "Meeting";
                        activity.Category = "Teams Meeting";
                        if (string.IsNullOrEmpty(activity.ProjectName))
                        {
                            activity.ProjectName = "Meetings & Calls";
                        }
                    }
                    else if (convInfo.ConversationType == "Chat")
                    {
                        activity.Category = "Communication & Chat";
                    }
                    else if (convInfo.ConversationType == "Channel")
                    {
                        activity.Category = "Team Collaboration";
                    }
                }
            }
            else if (isTeamsSub)
            {
                var convInfo = TeamsInteropService.GetCurrentConversationInfo(activity.SubWindowTitle ?? string.Empty, IntPtr.Zero);
                if (convInfo.HasDetails)
                {
                    activity.SubDocumentName = convInfo.FormatDocumentSummary();
                }
            }
            else if (activity.State == "Meeting" && (!string.IsNullOrEmpty(_lastCallTopic) || !string.IsNullOrEmpty(_lastParticipant)))
            {
                // Cross-app correlation: user is looking at another window (e.g. Revit/PDF) while in a Teams meeting
                activity.Category = "Meeting & Active Context";
                string contextLabel = !string.IsNullOrEmpty(_lastCallTopic) ? _lastCallTopic : _lastParticipant;
                if (!string.IsNullOrEmpty(activity.Tags) && !activity.Tags.Contains("Teams"))
                    activity.Tags += $", Teams: {contextLabel}";
                else if (string.IsNullOrEmpty(activity.Tags))
                    activity.Tags = $"Teams: {contextLabel}";
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
