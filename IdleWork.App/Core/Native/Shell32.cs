// [v0.1: Shell32] Helper methods for process name and window handle resolution
using System;
using System.Diagnostics;
using System.Text;

namespace IdleWork.App.Core.Native
{
    public static class Shell32
    {
        // [v0.004: ProcessCache] High-speed cache for process metadata to eliminate CPU spikes in EnumWindows
        private class CachedProcessInfo
        {
            public DateTime Expiry { get; set; }
            public string ProcessName { get; set; } = string.Empty;
            public string ExePath { get; set; } = string.Empty;
            public string AppDescription { get; set; } = string.Empty;
            public string AppCompany { get; set; } = string.Empty;
            public string AppVersion { get; set; } = string.Empty;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, CachedProcessInfo> _processCache = new();

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder lpExeName, ref int lpdwSize);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private static CachedProcessInfo ResolveProcess(uint processId)
        {
            if (_processCache.TryGetValue(processId, out var cached) && DateTime.UtcNow < cached.Expiry)
            {
                return cached;
            }

            string procName = string.Empty;
            string exePath = string.Empty;
            string appDescription = string.Empty;
            string appCompany = string.Empty;
            string appVersion = string.Empty;

            // 1. Fast path: Query via OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    {
                        exePath = sb.ToString();
                        procName = System.IO.Path.GetFileNameWithoutExtension(exePath);
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }

            // 2. Fallback to Process.GetProcessById if Win32 query was unsuccessful
            if (string.IsNullOrEmpty(procName))
            {
                try
                {
                    using var proc = Process.GetProcessById((int)processId);
                    procName = proc.ProcessName;
                    try
                    {
                        exePath = proc.MainModule?.FileName ?? string.Empty;
                    }
                    catch { }
                }
                catch
                {
                    procName = "Unknown";
                }
            }

            // 3. Extract FileVersionInfo if exePath is available
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    if (System.IO.File.Exists(exePath))
                    {
                        var vi = FileVersionInfo.GetVersionInfo(exePath);
                        appDescription = !string.IsNullOrWhiteSpace(vi.FileDescription)
                            ? vi.FileDescription
                            : (!string.IsNullOrWhiteSpace(vi.ProductName) ? vi.ProductName : procName);
                        appCompany = vi.CompanyName ?? string.Empty;
                        appVersion = !string.IsNullOrWhiteSpace(vi.FileVersion)
                            ? vi.FileVersion
                            : (vi.ProductVersion ?? string.Empty);
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(appDescription))
                appDescription = procName;

            var newEntry = new CachedProcessInfo
            {
                Expiry = DateTime.UtcNow.AddSeconds(30),
                ProcessName = procName,
                ExePath = exePath,
                AppDescription = appDescription,
                AppCompany = appCompany,
                AppVersion = appVersion
            };

            _processCache[processId] = newEntry;
            return newEntry;
        }

        public static (string ProcessName, string ExePath) GetProcessInfo(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return ("Unknown", string.Empty);

            User32.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0)
                return ("Unknown", string.Empty);

            var info = ResolveProcess(processId);
            return (info.ProcessName, info.ExePath);
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

        // [v0.003: RichMetadata] Extract rich executable and window class metadata
        public static AppMetadata GetRichProcessInfo(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return new AppMetadata("Unknown", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0);

            User32.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0)
                return new AppMetadata("Unknown", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0);

            var info = ResolveProcess(processId);

            string className = string.Empty;
            try
            {
                var sb = new StringBuilder(256);
                int len = User32.GetClassName(hWnd, sb, sb.Capacity);
                if (len > 0)
                    className = sb.ToString();
            }
            catch
            {
            }

            return new AppMetadata(
                info.ProcessName,
                info.ExePath,
                info.AppDescription,
                info.AppCompany,
                info.AppVersion,
                className,
                (int)processId
            );
        }

        // [v0.003: SystemTray] Shell_NotifyIcon constants and P/Invoke
        public const uint NIM_ADD = 0x00000000;
        public const uint NIM_MODIFY = 0x00000001;
        public const uint NIM_DELETE = 0x00000002;
        public const uint NIM_SETVERSION = 0x00000004;

        public const uint NIF_MESSAGE = 0x00000001;
        public const uint NIF_ICON = 0x00000002;
        public const uint NIF_TIP = 0x00000004;
        public const uint NIF_STATE = 0x00000008;
        public const uint NIF_INFO = 0x00000010;
        public const uint NIF_SHOWTIP = 0x00000080;

        public const uint NIIF_NONE = 0x00000000;
        public const uint NIIF_INFO = 0x00000001;
        public const uint NIIF_WARNING = 0x00000002;
        public const uint NIIF_ERROR = 0x00000003;

        public const uint NOTIFYICON_VERSION_4 = 4;

        public const int WM_USER = 0x0400;
        public const int WM_TRAYICON = WM_USER + 2048;

        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_CONTEXTMENU = 0x007B;

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        // [v0.003: SystemTray] Win32 NOTIFYICONDATA structure for Windows taskbar notification area
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }
    }

    // [v0.003: RichMetadata] Detailed process and window metadata container
    public record AppMetadata(
        string ProcessName,
        string ExePath,
        string AppDescription,
        string AppCompany,
        string AppVersion,
        string WindowClassName,
        int ProcessId
    );
}
