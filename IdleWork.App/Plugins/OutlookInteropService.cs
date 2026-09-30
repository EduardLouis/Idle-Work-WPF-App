// [v0.003: OutlookInterop] Deep email extraction service for Classic Outlook (COM) and New Outlook (olk.exe / UIA)
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using IdleWork.App.Core.Services;

namespace IdleWork.App.Plugins
{
    public class OutlookEmailInfo
    {
        public string? Subject { get; set; }
        public string? SenderName { get; set; }
        public string? SenderEmail { get; set; }
        public string? Folder { get; set; }
        public string? Account { get; set; }
        public bool IsEmailOpen { get; set; }

        public bool HasDetails => !string.IsNullOrEmpty(Subject) || !string.IsNullOrEmpty(SenderName);

        public string SenderDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(SenderName) && !string.IsNullOrEmpty(SenderEmail) &&
                    !SenderName.Equals(SenderEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{SenderName} <{SenderEmail}>";
                }
                return !string.IsNullOrEmpty(SenderName) ? SenderName : (SenderEmail ?? string.Empty);
            }
        }

        public string FormatDocumentSummary()
        {
            if (!string.IsNullOrEmpty(Subject) && !string.IsNullOrEmpty(SenderDisplay))
            {
                return $"Email: {Subject} (From: {SenderDisplay})";
            }
            if (!string.IsNullOrEmpty(Subject))
            {
                return $"Email: {Subject}";
            }
            if (!string.IsNullOrEmpty(Folder) && !string.IsNullOrEmpty(Account))
            {
                return $"Outlook: {Folder} ({Account})";
            }
            if (!string.IsNullOrEmpty(Folder))
            {
                return $"Outlook: {Folder}";
            }
            return string.Empty;
        }
    }

    public static class OutlookInteropService
    {
        /// <summary>
        /// Attempts to extract rich email details (Subject, Sender, Folder) from active Outlook instance.
        /// Supports Classic Outlook (via COM Automation), New Outlook olk.exe (via Window Title regex &amp; UI Automation).
        /// </summary>
        public static OutlookEmailInfo GetCurrentEmailInfo(string processName, string windowTitle, IntPtr hWnd)
        {
            var info = new OutlookEmailInfo();

            bool isClassicOutlook = processName.Equals("OUTLOOK", StringComparison.OrdinalIgnoreCase);
            bool isNewOutlook = processName.Equals("olk", StringComparison.OrdinalIgnoreCase);

            // 1. Try Classic Outlook COM Automation first if process is OUTLOOK
            if (isClassicOutlook)
            {
                try
                {
                    if (TryExtractViaCom(out var comInfo) && comInfo.HasDetails)
                    {
                        return comInfo;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogDebug($"Classic Outlook COM extraction skipped: {ex.Message}");
                }
            }

            // 2. Parse Window Title (applicable to both Classic and New Outlook olk.exe)
            ParseFromWindowTitle(windowTitle, info);

            // 3. If missing sender and hWnd is valid, attempt UI Automation inspection for New Outlook / WebView2
            if (hWnd != IntPtr.Zero && (string.IsNullOrEmpty(info.SenderName) || string.IsNullOrEmpty(info.Subject)))
            {
                try
                {
                    TryExtractViaUiAutomation(hWnd, info);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogDebug($"Outlook UI Automation extraction skipped: {ex.Message}");
                }
            }

            return info;
        }

        /// <summary>
        /// Dynamic late-bound COM automation for Classic Outlook without requiring compile-time PIA references.
        /// </summary>
        private static bool TryExtractViaCom(out OutlookEmailInfo info)
        {
            info = new OutlookEmailInfo();

            try
            {
                object? appObj = null;
                try
                {
                    // Marshal.GetActiveObject("Outlook.Application") via COM CLSID
                    var clsid = new Guid("0006F03A-0000-0000-C000-000000000046");
                    GetActiveObject(ref clsid, IntPtr.Zero, out var pUnk);
                    appObj = pUnk;
                }
                catch
                {
                    // Outlook COM server not active in ROT
                    return false;
                }

                if (appObj == null)
                    return false;

                dynamic app = appObj;

                // Priority A: Check ActiveInspector (email opened in a separate window)
                dynamic? inspector = null;
                try { inspector = app.ActiveInspector; } catch { }

                if (inspector != null)
                {
                    dynamic? currentItem = null;
                    try { currentItem = inspector.CurrentItem; } catch { }

                    if (currentItem != null && ExtractFromMailItem(currentItem, info))
                    {
                        info.IsEmailOpen = true;
                        return true;
                    }
                }

                // Priority B: Check ActiveExplorer (email highlighted in reading pane)
                dynamic? explorer = null;
                try { explorer = app.ActiveExplorer; } catch { }

                if (explorer != null)
                {
                    dynamic? selection = null;
                    try { selection = explorer.Selection; } catch { }

                    // [v0.004: CompilerWarnings] Safe type-checked count to eliminate CS8602
                    if (selection is not null)
                    {
                        int count = 0;
                        try { count = (int)selection.Count; } catch { }
                        if (count > 0)
                        {
                            dynamic? item = null;
                            try { item = selection[1]; } catch { }
                            if (item is not null && ExtractFromMailItem(item, info))
                            {
                                info.IsEmailOpen = false;
                                SafeReleaseCom(item);
                                SafeReleaseCom(selection);
                                SafeReleaseCom(explorer);
                                return true;
                            }
                            SafeReleaseCom(item);
                        }
                    }
                    SafeReleaseCom(selection);
                    SafeReleaseCom(explorer);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogDebug($"TryExtractViaCom exception: {ex.Message}");
            }

            return info.HasDetails;
        }

        private static void SafeReleaseCom(object? comObj)
        {
            // [v0.004: ComRelease] Clean release of Outlook COM automation handles
            if (comObj != null && Marshal.IsComObject(comObj))
            {
                try { Marshal.ReleaseComObject(comObj); } catch { }
            }
        }

        private static bool ExtractFromMailItem(dynamic item, OutlookEmailInfo info)
        {
            try
            {
                string? subject = null;
                try { subject = (string?)item.Subject; } catch { }

                string? senderName = null;
                try { senderName = (string?)item.SenderName; } catch { }

                string? senderEmail = null;
                try { senderEmail = (string?)item.SenderEmailAddress; } catch { }

                // Exchange email resolution if SenderEmailAddress is EX (/o=Exchange...)
                string? emailType = null;
                try { emailType = (string?)item.SenderEmailType; } catch { }

                if (string.Equals(emailType, "EX", StringComparison.OrdinalIgnoreCase))
                {
                    dynamic? sender = null;
                    dynamic? exUser = null;
                    try
                    {
                        sender = item.Sender;
                        if (sender is not null)
                        {
                            exUser = sender.GetExchangeUser();
                            // [v0.004: CompilerWarnings] Null-safe property extraction eliminating CS8602
                            if (exUser is not null)
                            {
                                string? smtp = null;
                                try { smtp = (string?)exUser.PrimarySmtpAddress; } catch { }
                                if (!string.IsNullOrEmpty(smtp))
                                {
                                    senderEmail = smtp;
                                }
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        SafeReleaseCom(exUser);
                        SafeReleaseCom(sender);
                    }
                }

                info.Subject = subject;
                info.SenderName = senderName;
                info.SenderEmail = senderEmail;
                return info.HasDetails;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses window title for email subject and folder context.
        /// Examples:
        /// - "Inbox - Eduard Louis - Outlook" -> Folder: Inbox, Account: Eduard Louis
        /// - "Project Status Meeting - John Doe - Outlook" -> Subject: Project Status Meeting, Sender: John Doe
        /// - "Q4 Financial Budget - Outlook" -> Subject: Q4 Financial Budget
        /// - "RE: Architectural Drawings v2 - Outlook" -> Subject: RE: Architectural Drawings v2
        /// </summary>
        public static void ParseFromWindowTitle(string title, OutlookEmailInfo info)
        {
            if (string.IsNullOrWhiteSpace(title))
                return;

            string cleaned = title.Trim();

            // Match "Inbox - Eduard Louis - Outlook"
            var folderMatch = Regex.Match(cleaned, @"^(Inbox|Sent Items|Drafts|Deleted Items|Archive|Junk Email|Outbox)\s*-\s*([^-]+?)\s*-\s*Outlook$", RegexOptions.IgnoreCase);
            if (folderMatch.Success)
            {
                info.Folder = folderMatch.Groups[1].Value.Trim();
                info.Account = folderMatch.Groups[2].Value.Trim();
                return;
            }

            // Match single folder: "Inbox - Outlook"
            var singleFolderMatch = Regex.Match(cleaned, @"^(Inbox|Sent Items|Drafts|Deleted Items|Archive|Junk Email|Outbox)\s*-\s*Outlook$", RegexOptions.IgnoreCase);
            if (singleFolderMatch.Success)
            {
                info.Folder = singleFolderMatch.Groups[1].Value.Trim();
                return;
            }

            // Match Subject with Sender/Account: "{Subject} - {Sender or Account} - Outlook"
            var subjectSenderMatch = Regex.Match(cleaned, @"^(.*?)\s*-\s*([^-]+?)\s*-\s*Outlook$", RegexOptions.IgnoreCase);
            if (subjectSenderMatch.Success)
            {
                info.Subject = subjectSenderMatch.Groups[1].Value.Trim();
                info.SenderName = subjectSenderMatch.Groups[2].Value.Trim();
                info.IsEmailOpen = true;
                return;
            }

            // Match Subject: "{Subject} - Outlook" or "{Subject} - Message (HTML)"
            var subjectMatch = Regex.Match(cleaned, @"^(.*?)\s*-\s*(?:Outlook|Message(?:\s*\(.*?\))?)$", RegexOptions.IgnoreCase);
            if (subjectMatch.Success)
            {
                info.Subject = subjectMatch.Groups[1].Value.Trim();
                info.IsEmailOpen = true;
                return;
            }
        }

        /// <summary>
        /// UI Automation fallback for New Outlook (olk.exe) and WebView2 reading pane.
        /// </summary>
        private static void TryExtractViaUiAutomation(IntPtr hWnd, OutlookEmailInfo info)
        {
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root == null)
                    return;

                // Look for elements containing "From:" or sender button in reading pane
                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Header),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)
                );

                var elements = root.FindAll(TreeScope.Children, condition);
                foreach (AutomationElement el in elements)
                {
                    try
                    {
                        string name = el.Current.Name;
                        if (string.IsNullOrEmpty(name))
                            continue;

                        if (string.IsNullOrEmpty(info.SenderName) && name.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
                        {
                            info.SenderName = name.Substring(5).Trim();
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                // UIA may fail or timeout on non-responsive windows, fail safely
            }
        }

        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
    }
}
