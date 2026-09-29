// [v0.1: OutlookPlugin] Microsoft Outlook calendar synchronization and appointment matching plugin
using System;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Plugins
{
    public class OutlookCalendarPlugin : IIdleWorkPlugin
    {
        public string Id => "microsoft.outlook";
        public string Name => "Outlook Calendar Sync";
        public string Description => "Matches scheduled calendar meetings against tracked time blocks.";
        public string Version => "0.1";
        public bool IsEnabled { get; set; } = true;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task EnrichActivityAsync(ActivityTimeSpan activity)
        {
            if (!IsEnabled)
                return Task.CompletedTask;

            bool isOutlook = activity.ProcessName.Contains("OUTLOOK", StringComparison.OrdinalIgnoreCase);
            if (isOutlook && string.IsNullOrEmpty(activity.Category))
            {
                activity.Category = "Email & Calendar";
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
