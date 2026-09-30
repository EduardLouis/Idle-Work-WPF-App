// [v0.003: TeamsInterop] Context extraction service for Microsoft Teams (ms-teams.exe / Teams.exe)
using System;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using IdleWork.App.Core.Services;

namespace IdleWork.App.Plugins
{
    public class TeamsConversationInfo
    {
        public string ConversationType { get; set; } = "General"; // "Chat", "Call", "Meeting", "Channel", "Calendar"
        public string? Title { get; set; }
        public string? Participant { get; set; }
        public string? TeamName { get; set; }
        public string? ChannelName { get; set; }
        public bool IsActiveMeetingOrCall { get; set; }

        public bool HasDetails => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Participant) || !string.IsNullOrEmpty(ChannelName);

        public string FormatDocumentSummary()
        {
            if (IsActiveMeetingOrCall)
            {
                if (!string.IsNullOrEmpty(Title))
                    return $"Teams Meeting: {Title}";
                if (!string.IsNullOrEmpty(Participant))
                    return $"Teams Call with {Participant}";
                return "Teams Meeting (In Call)";
            }

            if (ConversationType == "Chat" && !string.IsNullOrEmpty(Participant))
            {
                return $"Teams Chat: {Participant}";
            }

            if (ConversationType == "Channel" && !string.IsNullOrEmpty(ChannelName))
            {
                return !string.IsNullOrEmpty(TeamName)
                    ? $"Teams: {TeamName} > {ChannelName}"
                    : $"Teams Channel: {ChannelName}";
            }

            if (!string.IsNullOrEmpty(Title))
            {
                return $"Teams: {Title}";
            }

            return "Microsoft Teams";
        }
    }

    public static class TeamsInteropService
    {
        /// <summary>
        /// Analyzes Microsoft Teams window title and UI elements to extract active meeting, call, or chat info.
        /// </summary>
        public static TeamsConversationInfo GetCurrentConversationInfo(string windowTitle, IntPtr hWnd = default)
        {
            var info = new TeamsConversationInfo();

            if (string.IsNullOrWhiteSpace(windowTitle))
                return info;

            string cleaned = windowTitle.Trim();

            // 1. Direct Calls: "Call with Jane Doe | Microsoft Teams"
            var callMatch = Regex.Match(cleaned, @"^Call with\s+(.*?)(?:\s*\|\s*Microsoft Teams)?$", RegexOptions.IgnoreCase);
            if (callMatch.Success)
            {
                info.ConversationType = "Call";
                info.Participant = callMatch.Groups[1].Value.Trim();
                info.IsActiveMeetingOrCall = true;
                return info;
            }

            // 2. Direct Chats: "Chat | John Doe | Microsoft Teams"
            var chatMatch = Regex.Match(cleaned, @"^Chat\s*\|\s*(.*?)(?:\s*\|\s*Microsoft Teams)?$", RegexOptions.IgnoreCase);
            if (chatMatch.Success)
            {
                info.ConversationType = "Chat";
                info.Participant = chatMatch.Groups[1].Value.Trim();
                return info;
            }

            // 3. Named Meetings: "Meeting | Q3 Architectural Review | Microsoft Teams" or "Meeting with Project Alpha | Microsoft Teams"
            var meetingMatch = Regex.Match(cleaned, @"^(?:Meeting\s*\|\s*|Meeting with\s+)(.*?)(?:\s*\|\s*Microsoft Teams)?$", RegexOptions.IgnoreCase);
            if (meetingMatch.Success)
            {
                info.ConversationType = "Meeting";
                info.Title = meetingMatch.Groups[1].Value.Trim();
                info.IsActiveMeetingOrCall = true;
                return info;
            }

            // 4. Channel Discussions: "General | Revit Developers Team | Microsoft Teams"
            var channelMatch = Regex.Match(cleaned, @"^(.*?)\s*\|\s*(.*?)\s*\|\s*Microsoft Teams$", RegexOptions.IgnoreCase);
            if (channelMatch.Success)
            {
                info.ConversationType = "Channel";
                info.ChannelName = channelMatch.Groups[1].Value.Trim();
                info.TeamName = channelMatch.Groups[2].Value.Trim();
                return info;
            }

            // 5. In-call indicator in title: "... (in a call)" or "In Call"
            if (cleaned.IndexOf("in a call", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cleaned.IndexOf("in call", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.IsActiveMeetingOrCall = true;
                info.ConversationType = "Call";
            }

            // 6. Generic Title Strip: "{Custom Topic} | Microsoft Teams"
            var genericStrip = Regex.Match(cleaned, @"^(.*?)\s*\|\s*Microsoft Teams$", RegexOptions.IgnoreCase);
            if (genericStrip.Success)
            {
                string topic = genericStrip.Groups[1].Value.Trim();
                if (!topic.Equals("Calendar", StringComparison.OrdinalIgnoreCase) &&
                    !topic.Equals("Activity", StringComparison.OrdinalIgnoreCase) &&
                    !topic.Equals("Files", StringComparison.OrdinalIgnoreCase))
                {
                    info.Title = topic;
                    if (topic.IndexOf("Meeting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        topic.IndexOf("Sync", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        topic.IndexOf("Standup", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        info.IsActiveMeetingOrCall = true;
                        info.ConversationType = "Meeting";
                    }
                }
            }

            // 7. UI Automation inspection fallback for meeting banner if hWnd is available
            if (hWnd != IntPtr.Zero && !info.IsActiveMeetingOrCall)
            {
                try
                {
                    TryDetectMeetingViaUiAutomation(hWnd, info);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogDebug($"Teams UI Automation inspection skipped: {ex.Message}");
                }
            }

            return info;
        }

        private static void TryDetectMeetingViaUiAutomation(IntPtr hWnd, TeamsConversationInfo info)
        {
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root == null)
                    return;

                // Look for elements with "Leave call", "Mute mic", or "Call controls"
                var callCondition = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Leave"),
                    new PropertyCondition(AutomationElement.NameProperty, "Leave call"),
                    new PropertyCondition(AutomationElement.NameProperty, "Mute"),
                    new PropertyCondition(AutomationElement.NameProperty, "Unmute"),
                    new PropertyCondition(AutomationElement.NameProperty, "Call controls")
                );

                var match = root.FindFirst(TreeScope.Descendants, callCondition);
                if (match != null)
                {
                    info.IsActiveMeetingOrCall = true;
                    if (info.ConversationType == "General")
                    {
                        info.ConversationType = "Meeting";
                    }
                }
            }
            catch
            {
                // Non-blocking fallback
            }
        }
    }
}
