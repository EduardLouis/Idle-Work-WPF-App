// [v0.003: Tests] Unit tests for Outlook & Teams interop services and enrichment plugins
using System;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;
using IdleWork.App.Plugins;
using Xunit;

namespace IdleWork.Tests
{
    public class OutlookAndTeamsPluginTests
    {
        [Fact]
        public void OutlookInteropService_ParseFromWindowTitle_ParsesInboxAndAccount()
        {
            var info = new OutlookEmailInfo();
            OutlookInteropService.ParseFromWindowTitle("Inbox - Eduard Louis - Outlook", info);

            Assert.Equal("Inbox", info.Folder);
            Assert.Equal("Eduard Louis", info.Account);
            Assert.Null(info.Subject);
            Assert.Equal("Outlook: Inbox (Eduard Louis)", info.FormatDocumentSummary());
        }

        [Fact]
        public void OutlookInteropService_ParseFromWindowTitle_ParsesSubjectAndSender()
        {
            var info = new OutlookEmailInfo();
            OutlookInteropService.ParseFromWindowTitle("Project Status Meeting - John Doe - Outlook", info);

            Assert.Equal("Project Status Meeting", info.Subject);
            Assert.Equal("John Doe", info.SenderName);
            Assert.True(info.IsEmailOpen);
            Assert.Equal("Email: Project Status Meeting (From: John Doe)", info.FormatDocumentSummary());
        }

        [Fact]
        public void OutlookInteropService_ParseFromWindowTitle_ParsesStandaloneSubject()
        {
            var info = new OutlookEmailInfo();
            OutlookInteropService.ParseFromWindowTitle("Q4 Financial Budget Review - Outlook", info);

            Assert.Equal("Q4 Financial Budget Review", info.Subject);
            Assert.True(info.IsEmailOpen);
            Assert.Equal("Email: Q4 Financial Budget Review", info.FormatDocumentSummary());
        }

        [Fact]
        public async Task OutlookCalendarPlugin_EnrichActivityAsync_EnrichesNewOutlookOlk()
        {
            var plugin = new OutlookCalendarPlugin();
            var activity = new ActivityTimeSpan
            {
                ProcessName = "olk",
                WindowTitle = "Structural Engineering Update - Sarah Connor - Outlook",
                StartTime = DateTime.Now.AddMinutes(-5),
                EndTime = DateTime.Now
            };

            await plugin.EnrichActivityAsync(activity);

            Assert.Contains("Structural Engineering Update", activity.DocumentName);
            Assert.Contains("Sarah Connor", activity.DocumentName);
            Assert.Equal("Email & Communication", activity.Category);
        }

        [Fact]
        public void TeamsInteropService_GetCurrentConversationInfo_ParsesCallWithParticipant()
        {
            var info = TeamsInteropService.GetCurrentConversationInfo("Call with Jane Doe | Microsoft Teams");

            Assert.Equal("Call", info.ConversationType);
            Assert.Equal("Jane Doe", info.Participant);
            Assert.True(info.IsActiveMeetingOrCall);
            Assert.Equal("Teams Call with Jane Doe", info.FormatDocumentSummary());
        }

        [Fact]
        public void TeamsInteropService_GetCurrentConversationInfo_ParsesDirectChat()
        {
            var info = TeamsInteropService.GetCurrentConversationInfo("Chat | Alex Vance | Microsoft Teams");

            Assert.Equal("Chat", info.ConversationType);
            Assert.Equal("Alex Vance", info.Participant);
            Assert.False(info.IsActiveMeetingOrCall);
            Assert.Equal("Teams Chat: Alex Vance", info.FormatDocumentSummary());
        }

        [Fact]
        public void TeamsInteropService_GetCurrentConversationInfo_ParsesNamedMeeting()
        {
            var info = TeamsInteropService.GetCurrentConversationInfo("Meeting | BIM Coordination Weekly | Microsoft Teams");

            Assert.Equal("Meeting", info.ConversationType);
            Assert.Equal("BIM Coordination Weekly", info.Title);
            Assert.True(info.IsActiveMeetingOrCall);
            Assert.Equal("Teams Meeting: BIM Coordination Weekly", info.FormatDocumentSummary());
        }

        [Fact]
        public void TeamsInteropService_GetCurrentConversationInfo_ParsesChannelAndTeam()
        {
            var info = TeamsInteropService.GetCurrentConversationInfo("General | Structural Engineering Team | Microsoft Teams");

            Assert.Equal("Channel", info.ConversationType);
            Assert.Equal("General", info.ChannelName);
            Assert.Equal("Structural Engineering Team", info.TeamName);
            Assert.Equal("Teams: Structural Engineering Team > General", info.FormatDocumentSummary());
        }

        [Fact]
        public async Task TeamsIntegrationPlugin_EnrichActivityAsync_EnrichesMeetingAndCategory()
        {
            var plugin = new TeamsIntegrationPlugin();
            var activity = new ActivityTimeSpan
            {
                ProcessName = "ms-teams",
                WindowTitle = "Meeting | Project Sprint Planning | Microsoft Teams",
                StartTime = DateTime.Now.AddMinutes(-15),
                EndTime = DateTime.Now
            };

            await plugin.EnrichActivityAsync(activity);

            Assert.Equal("Meeting", activity.State);
            Assert.Equal("Teams Meeting", activity.Category);
            Assert.Equal("Meetings & Calls", activity.ProjectName);
            Assert.Equal("Teams Meeting: Project Sprint Planning", activity.DocumentName);
        }
    }
}
