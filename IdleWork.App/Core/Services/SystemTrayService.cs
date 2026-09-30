// [v0.003: SystemTray] Win32-backed System Tray Notification Icon Service for Windows Taskbar
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using IdleWork.App.Core.Native;

namespace IdleWork.App.Core.Services
{
    public class SystemTrayService : IDisposable
    {
        private static SystemTrayService? _instance;
        public static SystemTrayService Instance => _instance ??= new SystemTrayService();

        private Window? _mainWindow;
        private Action<string>? _navigationHandler;
        private HwndSource? _hwndSource;
        private IntPtr _hIcon = IntPtr.Zero;
        private Shell32.NOTIFYICONDATA _notifyData;
        private bool _isInitialized;
        private bool _hasShownFirstMinimizeNotice;
        private ContextMenu? _contextMenu;

        public bool MinimizeToTray { get; set; } = true;
        public bool CloseToTray { get; set; } = true;

        public event EventHandler? RequestExit;

        public SystemTrayService()
        {
        }

        public void Initialize(Window mainWindow, Action<string>? navigationHandler = null)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _navigationHandler = navigationHandler;

            var helper = new WindowInteropHelper(mainWindow);
            IntPtr hWnd = helper.Handle;

            if (hWnd == IntPtr.Zero)
            {
                // If handle is not yet created, hook into SourceInitialized
                mainWindow.SourceInitialized += (s, e) =>
                {
                    var h = new WindowInteropHelper(mainWindow).Handle;
                    SetupTrayIcon(h);
                };
            }
            else
            {
                SetupTrayIcon(hWnd);
            }
        }

        private void SetupTrayIcon(IntPtr hWnd)
        {
            if (_isInitialized || hWnd == IntPtr.Zero) return;

            _hwndSource = HwndSource.FromHwnd(hWnd);
            _hwndSource?.AddHook(WndProc);

            _hIcon = GetOrCreateAppIcon();

            _notifyData = new Shell32.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(Shell32.NOTIFYICONDATA)),
                hWnd = hWnd,
                uID = 1001,
                uFlags = Shell32.NIF_MESSAGE | Shell32.NIF_ICON | Shell32.NIF_TIP,
                uCallbackMessage = (uint)Shell32.WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "Idle-Work - Activity & Timesheets (Tracking Active)"
            };

            bool success = Shell32.Shell_NotifyIcon(Shell32.NIM_ADD, ref _notifyData);
            if (success)
            {
                _isInitialized = true;
                BuildContextMenu();
                LoggingService.Instance.LogInfo("SystemTray", "System tray icon registered successfully next to Windows clock.");
            }
            else
            {
                LoggingService.Instance.LogWarn("SystemTray", "Failed to register system tray icon via Shell_NotifyIcon.");
            }
        }

        private IntPtr GetOrCreateAppIcon()
        {
            // 1. Try extracting executable icon
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    uint count = Shell32.ExtractIconEx(exePath, 0, out IntPtr hLarge, out IntPtr hSmall, 1);
                    if (count > 0 && hSmall != IntPtr.Zero)
                    {
                        if (hLarge != IntPtr.Zero) User32.DestroyIcon(hLarge);
                        return hSmall;
                    }
                    if (count > 0 && hLarge != IntPtr.Zero)
                    {
                        return hLarge;
                    }
                }
            }
            catch
            {
            }

            // 2. Generate a custom 32x32 vector badge icon in memory (Dark Navy background + Cyan Accent ring + Clock hands)
            try
            {
                const int width = 32;
                const int height = 32;
                uint[] pixels = new uint[width * height];

                const float centerX = 15.5f;
                const float centerY = 15.5f;
                const float outerRadius = 14.5f;
                const float innerRingMin = 11.5f;
                const float innerRingMax = 14.0f;

                // Color constants (ARGB)
                const uint cTransparent = 0x00000000;
                const uint cDarkNavy = 0xFF0F172A;   // #0F172A background
                const uint cCyan = 0xFF38BDF8;       // #38BDF8 accent ring and hands
                const uint cGreenDot = 0xFF10B981;   // #10B981 active pulse center

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (dist > outerRadius)
                        {
                            pixels[y * width + x] = cTransparent;
                        }
                        else if (dist >= innerRingMin && dist <= innerRingMax)
                        {
                            pixels[y * width + x] = cCyan;
                        }
                        else
                        {
                            // Inner face of clock
                            // Draw hour and minute hands
                            bool isCenter = (Math.Abs(dx) <= 1.5f && Math.Abs(dy) <= 1.5f);
                            bool isHourHand = (Math.Abs(dx) <= 1.0f && dy <= 0 && dy >= -8.0f);
                            bool isMinuteHand = (Math.Abs(dy) <= 1.0f && dx >= 0 && dx <= 7.0f);

                            if (isCenter)
                            {
                                pixels[y * width + x] = cGreenDot;
                            }
                            else if (isHourHand || isMinuteHand)
                            {
                                pixels[y * width + x] = cCyan;
                            }
                            else
                            {
                                pixels[y * width + x] = cDarkNavy;
                            }
                        }
                    }
                }

                GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                IntPtr pPixels = handle.AddrOfPinnedObject();
                IntPtr hbmColor = Gdi32.CreateBitmap(width, height, 1, 32, pPixels);
                handle.Free();

                // Mask bitmap (1-bit monochrome mask: all zeroes means transparent is handled by 32-bit alpha)
                byte[] maskBytes = new byte[width * height / 8];
                GCHandle maskHandle = GCHandle.Alloc(maskBytes, GCHandleType.Pinned);
                IntPtr hbmMask = Gdi32.CreateBitmap(width, height, 1, 1, maskHandle.AddrOfPinnedObject());
                maskHandle.Free();

                var iconInfo = new User32.ICONINFO
                {
                    fIcon = true,
                    xHotspot = 0,
                    yHotspot = 0,
                    hbmColor = hbmColor,
                    hbmMask = hbmMask
                };

                IntPtr createdIcon = User32.CreateIconIndirect(ref iconInfo);

                Gdi32.DeleteObject(hbmColor);
                Gdi32.DeleteObject(hbmMask);

                if (createdIcon != IntPtr.Zero)
                {
                    return createdIcon;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarn("SystemTray", $"Failed to generate vector tray icon: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenu();

            // Header title item
            var headerItem = new MenuItem
            {
                Header = "⚡ Idle-Work Activity Tracker",
                FontWeight = FontWeights.Bold,
                IsEnabled = false,
                Foreground = (Brush)_mainWindow!.FindResource("AccentBrush")
            };
            _contextMenu.Items.Add(headerItem);

            _contextMenu.Items.Add(new Separator());

            // Open Dashboard
            var openItem = new MenuItem
            {
                Header = "⚡ Open Live Dashboard",
                FontWeight = FontWeights.SemiBold
            };
            openItem.Click += (s, e) =>
            {
                RestoreWindow();
                _navigationHandler?.Invoke("LiveTracker");
            };
            _contextMenu.Items.Add(openItem);

            // Daily Timeline
            var timelineItem = new MenuItem { Header = "📅 Daily Timeline" };
            timelineItem.Click += (s, e) =>
            {
                RestoreWindow();
                _navigationHandler?.Invoke("Timeline");
            };
            _contextMenu.Items.Add(timelineItem);

            // Weekly Timesheet
            var timesheetItem = new MenuItem { Header = "📊 Weekly Timesheet" };
            timesheetItem.Click += (s, e) =>
            {
                RestoreWindow();
                _navigationHandler?.Invoke("WeeklyTimesheet");
            };
            _contextMenu.Items.Add(timesheetItem);

            // Smart Rules
            var rulesItem = new MenuItem { Header = "🏷️ Smart Rules" };
            rulesItem.Click += (s, e) =>
            {
                RestoreWindow();
                _navigationHandler?.Invoke("Rules");
            };
            _contextMenu.Items.Add(rulesItem);

            // Preferences
            var settingsItem = new MenuItem { Header = "⚙️ Preferences..." };
            settingsItem.Click += (s, e) =>
            {
                RestoreWindow();
                _navigationHandler?.Invoke("Settings");
            };
            _contextMenu.Items.Add(settingsItem);

            _contextMenu.Items.Add(new Separator());

            // Exit Application
            var exitItem = new MenuItem
            {
                Header = "🚪 Exit Application",
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)) // Soft red #F87171
            };
            exitItem.Click += (s, e) =>
            {
                RequestExit?.Invoke(this, EventArgs.Empty);
            };
            _contextMenu.Items.Add(exitItem);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Shell32.WM_TRAYICON)
            {
                int mouseMsg = (int)lParam;

                if (mouseMsg == Shell32.WM_LBUTTONUP || mouseMsg == Shell32.WM_LBUTTONDBLCLK)
                {
                    RestoreWindow();
                    handled = true;
                }
                else if (mouseMsg == Shell32.WM_RBUTTONUP || mouseMsg == Shell32.WM_CONTEXTMENU)
                {
                    ShowContextMenu();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        public void RestoreWindow()
        {
            if (_mainWindow == null) return;

            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }

            _mainWindow.ShowInTaskbar = true;

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            _mainWindow.Focus();

            if (_contextMenu != null)
            {
                _contextMenu.IsOpen = false;
            }
        }

        public void ShowContextMenu()
        {
            if (_contextMenu == null || _mainWindow == null) return;

            var helper = new WindowInteropHelper(_mainWindow);
            User32.SetForegroundWindow(helper.Handle);

            _contextMenu.Placement = PlacementMode.MousePoint;
            _contextMenu.IsOpen = true;
        }

        public void NotifyMinimizedToTray()
        {
            if (!_hasShownFirstMinimizeNotice && _isInitialized)
            {
                _hasShownFirstMinimizeNotice = true;
                ShowNotification("Idle-Work Running", "Idle-Work is tracking in the background. Click the icon near the clock to restore.");
            }
        }

        public void ShowNotification(string title, string text, uint infoFlags = Shell32.NIIF_INFO)
        {
            if (!_isInitialized) return;

            try
            {
                _notifyData.uFlags |= Shell32.NIF_INFO;
                _notifyData.szInfoTitle = title.Length > 63 ? title.Substring(0, 63) : title;
                _notifyData.szInfo = text.Length > 255 ? text.Substring(0, 255) : text;
                _notifyData.dwInfoFlags = infoFlags;

                Shell32.Shell_NotifyIcon(Shell32.NIM_MODIFY, ref _notifyData);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarn("SystemTray", $"ShowNotification failed: {ex.Message}");
            }
        }

        public void UpdateTooltip(string tooltip)
        {
            if (!_isInitialized) return;

            try
            {
                _notifyData.uFlags = Shell32.NIF_TIP;
                _notifyData.szTip = tooltip.Length > 127 ? tooltip.Substring(0, 127) : tooltip;
                Shell32.Shell_NotifyIcon(Shell32.NIM_MODIFY, ref _notifyData);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                try
                {
                    Shell32.Shell_NotifyIcon(Shell32.NIM_DELETE, ref _notifyData);
                }
                catch
                {
                }
                _isInitialized = false;
            }

            if (_hIcon != IntPtr.Zero)
            {
                try
                {
                    User32.DestroyIcon(_hIcon);
                }
                catch
                {
                }
                _hIcon = IntPtr.Zero;
            }

            if (_hwndSource != null)
            {
                try
                {
                    _hwndSource.RemoveHook(WndProc);
                }
                catch
                {
                }
                _hwndSource = null;
            }
        }
    }
}
