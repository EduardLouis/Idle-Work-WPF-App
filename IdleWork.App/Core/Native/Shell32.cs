// [v0.1: Shell32] Helper methods for process name and window handle resolution
using System;
using System.Diagnostics;
using System.Text;

namespace IdleWork.App.Core.Native
{
    public static class Shell32
    {
        public static (string ProcessName, string ExePath) GetProcessInfo(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return ("Unknown", string.Empty);

            User32.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0)
                return ("Unknown", string.Empty);

            try
            {
                using var proc = Process.GetProcessById((int)processId);
                string procName = proc.ProcessName;
                string exePath = string.Empty;
                try
                {
                    exePath = proc.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    // Access denied for system processes is normal
                }
                return (procName, exePath);
            }
            catch
            {
                return ("Unknown", string.Empty);
            }
        }

        public static string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return string.Empty;

            int length = User32.GetWindowTextLength(hWnd);
            if (length == 0)
                return string.Empty;

            var sb = new StringBuilder(length + 1);
            User32.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
