// [v0.2: Models] Snapshot of the top-most visible window on a display monitor
using System;

namespace IdleWork.App.Core.Models
{
    public class MonitorWindowSnapshot
    {
        public int MonitorIndex { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public IntPtr Hwnd { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public bool IsFocused { get; set; }

        public string DisplayText => $"{ProcessName}: {(string.IsNullOrEmpty(DocumentName) ? WindowTitle : DocumentName)}";
    }
}
