// [v0.003: OutlookPlugin] Microsoft Outlook deep email inspection and calendar synchronization plugin
using System;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Native;

namespace IdleWork.App.Plugins
{
    public class OutlookCalendarPlugin : IIdleWorkPlugin
    {
        public string Id => "microsoft.outlook";
        public string Name => "Outlook Email & Calendar Integration";
        public string Description => "Extracts current open/selected email subject, sender, and folder details from Classic Outlook (COM) and New Outlook (olk.exe).";
        public string Version => "0.003";
        public bool IsEnabled { get; set; } = true;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task EnrichActivityAsync(ActivityTimeSpan activity)
        {
            if (!IsEnabled)
                return Task.CompletedTask;

            // Check if active or sub activity is Outlook (Classic 'OUTLOOK' or New 'olk')
            bool isOutlookMain = activity.ProcessName.Equals("OUTLOOK", StringComparison.OrdinalIgnoreCase) ||
                                 activity.ProcessName.Equals("olk", StringComparison.OrdinalIgnoreCase);

            bool isOutlookSub = !string.IsNullOrEmpty(activity.SubProcessName) &&
                                (activity.SubProcessName.Equals("OUTLOOK", StringComparison.OrdinalIgnoreCase) ||
                                 activity.SubProcessName.Equals("olk", StringComparison.OrdinalIgnoreCase));

            if (isOutlookMain)
            {
                var fgHwnd = User32.GetForegroundWindow();
                var emailInfo = OutlookInteropService.GetCurrentEmailInfo(activity.ProcessName, activity.WindowTitle, fgHwnd);

                if (emailInfo.HasDetails)
                {
                    string summary = emailInfo.FormatDocumentSummary();
                    if (!string.IsNullOrEmpty(summary))
                    {
                        activity.DocumentName = summary;
                    }

                    if (!string.IsNullOrEmpty(emailInfo.Subject))
                    {
                        // If window title was generic "Inbox - Eduard Louis - Outlook" or "olk", provide rich context
                        if (emailInfo.IsEmailOpen || activity.WindowTitle.Contains("Inbox", StringComparison.OrdinalIgnoreCase))
                        {
                            activity.WindowTitle = $"{emailInfo.Subject} (From: {emailInfo.SenderDisplay})";
                        }
                    }
                }

                if (string.IsNullOrEmpty(activity.Category))
                {
                    activity.Category = "Email & Communication";
                }
            }
            else if (isOutlookSub)
            {
                var emailInfo = OutlookInteropService.GetCurrentEmailInfo(activity.SubProcessName!, activity.SubWindowTitle ?? string.Empty, IntPtr.Zero);
                if (emailInfo.HasDetails)
                {
                    activity.SubDocumentName = emailInfo.FormatDocumentSummary();
                }
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
