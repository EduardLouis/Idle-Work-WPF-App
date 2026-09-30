// [v0.1: WindowTrackerService] Win32 event hook and multi-monitor window tracker
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Timers;
using IdleWork.App.Core.Helpers;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Native;

namespace IdleWork.App.Core.Services
{
    public class WindowTrackerService : IDisposable
    {
        private IntPtr _hHook = IntPtr.Zero;
        private readonly User32.WinEventDelegate _winEventDelegate;
        private readonly System.Timers.Timer _pollTimer;
        private IntPtr _lastHwnd = IntPtr.Zero;
        private string _lastTitle = string.Empty;

        public string ActiveProcessName { get; private set; } = string.Empty;
        public string ActiveWindowTitle { get; private set; } = string.Empty;
        public string ActiveDocumentName { get; private set; } = string.Empty;
        public int ActiveMonitorIndex { get; private set; } = 0;
        public IntPtr ActiveWindowHandle => _lastHwnd != IntPtr.Zero ? _lastHwnd : User32.GetForegroundWindow();
        public List<MonitorWindowSnapshot> CurrentTopWindows { get; private set; } = new List<MonitorWindowSnapshot>();
        public List<MonitorInfo> AllMonitors { get; private set; } = new List<MonitorInfo>();

        public event EventHandler<(string ProcessName, string Title, string Doc, int Monitor)>? WindowChanged;
        public event EventHandler<(string ProcessName, string Title, string Doc, int Monitor, List<MonitorWindowSnapshot> TopWindows)>? MultiMonitorWindowChanged;

        public WindowTrackerService()
        {
            _winEventDelegate = new User32.WinEventDelegate(WinEventProc);
            _hHook = User32.SetWinEventHook(
                User32.EVENT_SYSTEM_FOREGROUND,
                User32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);

            _pollTimer = new System.Timers.Timer(1000); // 1-second poll for internal title/tab changes
            _pollTimer.Elapsed += (s, e) => CheckForegroundWindow();
            _pollTimer.Start();

            RefreshMonitors();
            CheckForegroundWindow();
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            CheckForegroundWindow(hwnd);
        }

        // [v0.003: ExcludeSelfAndOverlays] Determines if a window belongs to this program itself or a transient screen capture overlay
        public static bool IsIgnoredWindow(IntPtr hwnd, string processName, string title)
        {
            if (hwnd != IntPtr.Zero)
            {
                User32.GetWindowThreadProcessId(hwnd, out uint procId);
                if (procId != 0 && procId == Environment.ProcessId)
                    return true;
            }

            if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(title))
                return true;

            // 1. Skip self / this program itself
            if (processName.Equals("IdleWork", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("IdleWork.App", StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. Skip Snipping Tool and screen capture overlays
            if (processName.Equals("SnippingTool", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("ScreenClippingHost", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("SnippingToolApp", StringComparison.OrdinalIgnoreCase))
                return true;

            if (title.Contains("Snipping Tool", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Screen Clipping", StringComparison.OrdinalIgnoreCase))
                return true;

            // 3. Skip system shell overlays
            if (processName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("SearchHost", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("LockApp", StringComparison.OrdinalIgnoreCase))
                return true;

            if (title == "Program Manager" || title == "Windows Shell Experience Host" || title == "Task Switching")
                return true;

            if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(title) || title == "Taskbar"))
                return true;

            return false;
        }

        public void CheckForegroundWindow(IntPtr? specificHwnd = null)
        {
            IntPtr hwnd = specificHwnd ?? User32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return;

            string title = Shell32.GetWindowTitle(hwnd);
            var (processName, _) = Shell32.GetProcessInfo(hwnd);

            // [v0.003: ExcludeSelfAndOverlays] If the focused window is this program itself or a screen clipping overlay,
            // ignore it so we never interrupt or replace the active work session
            if (IsIgnoredWindow(hwnd, processName, title))
                return;

            // [v0.2: MultiMonitor] Query topmost visible windows across all monitors
            var topWindows = GetTopWindowsForAllMonitors(hwnd);
            CurrentTopWindows = topWindows;

            if (hwnd != _lastHwnd || title != _lastTitle)
            {
                _lastHwnd = hwnd;
                _lastTitle = title;

                ActiveProcessName = processName;
                ActiveWindowTitle = title;
                ActiveDocumentName = TitleParser.ExtractDocumentName(processName, title);
                ActiveMonitorIndex = GetMonitorIndexForWindow(hwnd);

                WindowChanged?.Invoke(this, (ActiveProcessName, ActiveWindowTitle, ActiveDocumentName, ActiveMonitorIndex));
                MultiMonitorWindowChanged?.Invoke(this, (ActiveProcessName, ActiveWindowTitle, ActiveDocumentName, ActiveMonitorIndex, CurrentTopWindows));
            }
        }

        // [v0.2: MultiMonitor] Enumerate windows in Z-order and identify the top visible window per monitor
        public List<MonitorWindowSnapshot> GetTopWindowsForAllMonitors(IntPtr foregroundHwnd)
        {
            if (AllMonitors.Count == 0)
                RefreshMonitors();

            var result = new Dictionary<int, MonitorWindowSnapshot>();
            int currentPid = Environment.ProcessId;

            User32.EnumWindows((hWnd, lParam) =>
            {
                // Must be visible and not minimized
                if (!User32.IsWindowVisible(hWnd) || User32.IsIconic(hWnd))
                    return true;

                // Check dimensions
                if (!User32.GetWindowRect(hWnd, out var rect))
                    return true;

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width < 100 || height < 100)
                    return true;

                // Skip cloaked windows (hidden Store apps, background virtual desktop windows)
                if (User32.DwmGetWindowAttribute(hWnd, User32.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                    return true;

                // Get process info and skip self / overlays
                User32.GetWindowThreadProcessId(hWnd, out uint procId);
                if (procId == currentPid || procId == 0)
                    return true;

                string title = Shell32.GetWindowTitle(hWnd);
                var (procName, _) = Shell32.GetProcessInfo(hWnd);

                if (IsIgnoredWindow(hWnd, procName, title))
                    return true;

                // Determine which monitor this window is primarily on
                int monIndex = GetMonitorIndexForRect(rect);
                if (!result.ContainsKey(monIndex))
                {
                    result[monIndex] = new MonitorWindowSnapshot
                    {
                        MonitorIndex = monIndex,
                        DeviceName = monIndex < AllMonitors.Count ? AllMonitors[monIndex].DeviceName : $"Monitor {monIndex}",
                        Hwnd = hWnd,
                        ProcessName = procName,
                        WindowTitle = title,
                        DocumentName = TitleParser.ExtractDocumentName(procName, title),
                        IsFocused = (hWnd == foregroundHwnd)
                    };
                }

                // If we found a top window for every active monitor, stop early
                return result.Count < AllMonitors.Count;
            }, IntPtr.Zero);

            return new List<MonitorWindowSnapshot>(result.Values);
        }

        // [v0.004: MultiMonitor] Robust rectangle intersection area calculation supporting negative virtual desktop coordinates
        private int GetMonitorIndexForRect(User32.RECT rect)
        {
            if (AllMonitors.Count == 0) return 0;

            int bestIndex = 0;
            long maxOverlapArea = -1;

            for (int i = 0; i < AllMonitors.Count; i++)
            {
                var m = AllMonitors[i];
                int monRight = m.Left + m.Width;
                int monBottom = m.Top + m.Height;

                int overlapLeft = Math.Max(rect.Left, m.Left);
                int overlapRight = Math.Min(rect.Right, monRight);
                int overlapTop = Math.Max(rect.Top, m.Top);
                int overlapBottom = Math.Min(rect.Bottom, monBottom);

                int overlapWidth = Math.Max(0, overlapRight - overlapLeft);
                int overlapHeight = Math.Max(0, overlapBottom - overlapTop);
                long overlapArea = (long)overlapWidth * overlapHeight;

                if (overlapArea > maxOverlapArea)
                {
                    maxOverlapArea = overlapArea;
                    bestIndex = i;
                }
            }

            // Fallback to center point if no area overlap (e.g., zero-size or minimized window)
            if (maxOverlapArea <= 0)
            {
                int centerX = (rect.Left + rect.Right) / 2;
                int centerY = (rect.Top + rect.Bottom) / 2;
                for (int i = 0; i < AllMonitors.Count; i++)
                {
                    var m = AllMonitors[i];
                    if (centerX >= m.Left && centerX < (m.Left + m.Width) &&
                        centerY >= m.Top && centerY < (m.Top + m.Height))
                    {
                        return i;
                    }
                }
            }

            return bestIndex;
        }

        public void RefreshMonitors()
        {
            var monitors = new List<MonitorInfo>();
            int idx = 0;

            User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref User32.RECT rc, IntPtr data) =>
            {
                var mi = new User32.MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(mi);
                if (User32.GetMonitorInfo(hMon, ref mi))
                {
                    monitors.Add(new MonitorInfo
                    {
                        Index = idx++,
                        DeviceName = mi.szDevice,
                        Left = mi.rcMonitor.Left,
                        Top = mi.rcMonitor.Top,
                        Width = mi.rcMonitor.Right - mi.rcMonitor.Left,
                        Height = mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                        IsPrimary = (mi.dwFlags & User32.MONITORINFOF_PRIMARY) != 0
                    });
                }
                return true;
            }, IntPtr.Zero);

            AllMonitors = monitors;
        }

        private int GetMonitorIndexForWindow(IntPtr hwnd)
        {
            if (User32.GetWindowRect(hwnd, out var rect))
            {
                return GetMonitorIndexForRect(rect);
            }
            return 0;
        }

        public void Dispose()
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();

            if (_hHook != IntPtr.Zero)
            {
                User32.UnhookWinEvent(_hHook);
                _hHook = IntPtr.Zero;
            }
        }
    }
}
