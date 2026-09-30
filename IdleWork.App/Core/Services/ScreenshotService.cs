// [v0.003: ScreenshotService] Native GDI-accelerated screen capture and JPEG compression engine
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using IdleWork.App.Core.Native;

namespace IdleWork.App.Core.Services
{
    public class ScreenshotService
    {
        private static ScreenshotService? _instance;
        public static ScreenshotService Instance => _instance ??= new ScreenshotService();

        public bool EnableScreenshots { get; set; } = true;
        public bool CaptureOnWindowSwitch { get; set; } = true;
        public int ScreenshotIntervalMinutes { get; set; } = 5;
        // [v0.004: ScreenshotRetention] Retention period in days (default 14 days, 0 = keep forever)
        public int RetentionDays { get; set; } = 14;

        private readonly string _baseScreenshotsFolder;
        private readonly object _lock = new object();

        public string BaseScreenshotsFolder => _baseScreenshotsFolder;

        public ScreenshotService(string? customFolder = null)
        {
            if (!string.IsNullOrWhiteSpace(customFolder))
            {
                _baseScreenshotsFolder = customFolder;
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _baseScreenshotsFolder = Path.Combine(localAppData, "IdleWork", "Screenshots");
            }

            try
            {
                if (!Directory.Exists(_baseScreenshotsFolder))
                    Directory.CreateDirectory(_baseScreenshotsFolder);
            }
            catch
            {
            }

            // [v0.004: ScreenshotRetention] Run background retention purge on startup
            Task.Run(() => PurgeOldScreenshots());
        }

        public string GetTodayFolder()
        {
            string todayFolder = Path.Combine(_baseScreenshotsFolder, DateTime.Now.ToString("yyyy-MM-dd"));
            if (!Directory.Exists(todayFolder))
            {
                Directory.CreateDirectory(todayFolder);
            }
            return todayFolder;
        }

        public string? CaptureWindowScreenshot(IntPtr hWnd, int monitorIndex = 0, string? activityKey = null)
        {
            if (!EnableScreenshots)
                return null;

            lock (_lock)
            {
                IntPtr hdcScreen = IntPtr.Zero;
                IntPtr hdcMem = IntPtr.Zero;
                IntPtr hBitmap = IntPtr.Zero;
                IntPtr hOld = IntPtr.Zero;
                BitmapSource? source = null;

                try
                {
                    // 1. Determine target bounding rectangle from monitor or window
                    int x = 0, y = 0, width = 1920, height = 1080;
                    bool gotBounds = false;

                    if (hWnd != IntPtr.Zero)
                    {
                        IntPtr hMonitor = User32.MonitorFromWindow(hWnd, User32.MONITOR_DEFAULTTONEAREST);
                        if (hMonitor != IntPtr.Zero)
                        {
                            var mi = new User32.MONITORINFOEX();
                            mi.cbSize = Marshal.SizeOf(typeof(User32.MONITORINFOEX));
                            if (User32.GetMonitorInfo(hMonitor, ref mi))
                            {
                                // [v0.004: MultiMonitor] Correctly support negative virtual display coordinates
                                x = mi.rcMonitor.Left;
                                y = mi.rcMonitor.Top;
                                width = Math.Max(100, mi.rcMonitor.Right - mi.rcMonitor.Left);
                                height = Math.Max(100, mi.rcMonitor.Bottom - mi.rcMonitor.Top);
                                gotBounds = true;
                            }
                        }
                    }

                    if (!gotBounds)
                    {
                        // Fallback to primary virtual screen dimensions
                        width = (int)SystemParameters.PrimaryScreenWidth;
                        height = (int)SystemParameters.PrimaryScreenHeight;
                        if (width <= 0) width = 1920;
                        if (height <= 0) height = 1080;
                    }

                    // 2. Perform GDI BitBlt capture inside leak-proof try/finally
                    hdcScreen = User32.GetDC(IntPtr.Zero);
                    if (hdcScreen == IntPtr.Zero)
                        return null;

                    hdcMem = Gdi32.CreateCompatibleDC(hdcScreen);
                    if (hdcMem == IntPtr.Zero)
                        return null;

                    hBitmap = Gdi32.CreateCompatibleBitmap(hdcScreen, width, height);
                    if (hBitmap == IntPtr.Zero)
                        return null;

                    hOld = Gdi32.SelectObject(hdcMem, hBitmap);
                    bool success = Gdi32.BitBlt(hdcMem, 0, 0, width, height, hdcScreen, x, y, Gdi32.SRCCOPY);

                    if (!success)
                        return null;

                    // 3. Convert HBitmap to WPF BitmapSource before releasing GDI objects
                    source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    source.Freeze();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScreenshotService] Capture error: {ex.Message}");
                    return null;
                }
                finally
                {
                    // [v0.004: GdiLeakFix] Unconditionally release all GDI device contexts and bitmap handles
                    if (hdcMem != IntPtr.Zero)
                    {
                        if (hOld != IntPtr.Zero)
                            Gdi32.SelectObject(hdcMem, hOld);
                        Gdi32.DeleteDC(hdcMem);
                    }
                    if (hBitmap != IntPtr.Zero)
                    {
                        Gdi32.DeleteObject(hBitmap);
                    }
                    if (hdcScreen != IntPtr.Zero)
                    {
                        User32.ReleaseDC(IntPtr.Zero, hdcScreen);
                    }
                }

                if (source == null)
                    return null;

                try
                {
                    // 4. Save as compressed JPEG
                    string sanitizedKey = string.IsNullOrWhiteSpace(activityKey)
                        ? "win"
                        : string.Concat(activityKey.Split(Path.GetInvalidFileNameChars())).Trim();

                    if (sanitizedKey.Length > 24)
                        sanitizedKey = sanitizedKey.Substring(0, 24);

                    string folder = GetTodayFolder();
                    string fileName = $"act_{sanitizedKey}_{DateTime.Now:yyyyMMdd_HHmmssfff}.jpg";
                    string filePath = Path.Combine(folder, fileName);

                    var encoder = new JpegBitmapEncoder { QualityLevel = 75 };
                    encoder.Frames.Add(BitmapFrame.Create(source));

                    using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        encoder.Save(fs);
                    }

                    return filePath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScreenshotService] Save error: {ex.Message}");
                    return null;
                }
            }
        }

        // [v0.004: ScreenshotRetention] Automatically purges screenshot folders older than retention period
        public int PurgeOldScreenshots(int? days = null)
        {
            int retention = days ?? RetentionDays;
            if (retention <= 0) return 0; // 0 means keep forever

            int deletedCount = 0;
            try
            {
                if (!Directory.Exists(_baseScreenshotsFolder)) return 0;

                DateTime cutoff = DateTime.Today.AddDays(-retention);
                var dirs = Directory.GetDirectories(_baseScreenshotsFolder);

                foreach (var dir in dirs)
                {
                    string dirName = Path.GetFileName(dir);
                    if (DateTime.TryParseExact(dirName, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var folderDate))
                    {
                        if (folderDate < cutoff)
                        {
                            try
                            {
                                var files = Directory.GetFiles(dir);
                                deletedCount += files.Length;
                                Directory.Delete(dir, true);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ScreenshotService] Failed to purge {dir}: {ex.Message}");
                            }
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    LoggingService.Instance.LogInfo("Screenshots", $"Purged {deletedCount} old screenshots older than {retention} days.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogWarn("Screenshots", $"PurgeOldScreenshots error: {ex.Message}");
            }
            return deletedCount;
        }

        // [v0.004: ScreenshotRetention] Calculates total disk usage of screenshots
        public (int TotalFiles, long TotalBytes) GetStorageStats()
        {
            try
            {
                if (!Directory.Exists(_baseScreenshotsFolder)) return (0, 0);

                var dirInfo = new DirectoryInfo(_baseScreenshotsFolder);
                var files = dirInfo.GetFiles("*.jpg", SearchOption.AllDirectories);
                long bytes = 0;
                foreach (var f in files)
                {
                    bytes += f.Length;
                }
                return (files.Length, bytes);
            }
            catch
            {
                return (0, 0);
            }
        }

        // [v0.004: ScreenshotRetention] Clears all screenshots on demand
        public (int FilesDeleted, long BytesFreed) ClearAllScreenshots()
        {
            int filesDeleted = 0;
            long bytesFreed = 0;
            try
            {
                if (!Directory.Exists(_baseScreenshotsFolder)) return (0, 0);

                var dirInfo = new DirectoryInfo(_baseScreenshotsFolder);
                var files = dirInfo.GetFiles("*.jpg", SearchOption.AllDirectories);
                filesDeleted = files.Length;
                foreach (var f in files)
                {
                    bytesFreed += f.Length;
                }

                var dirs = Directory.GetDirectories(_baseScreenshotsFolder);
                foreach (var d in dirs)
                {
                    try { Directory.Delete(d, true); } catch { }
                }

                LoggingService.Instance.LogInfo("Screenshots", $"User cleared screenshot storage: {filesDeleted} files, {bytesFreed / (1024 * 1024.0):F1} MB freed.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Screenshots", $"ClearAllScreenshots error: {ex.Message}", ex);
            }
            return (filesDeleted, bytesFreed);
        }

        public Task<string?> CaptureWindowScreenshotAsync(IntPtr hWnd, int monitorIndex = 0, string? activityKey = null)
        {
            return Task.Run(() => CaptureWindowScreenshot(hWnd, monitorIndex, activityKey));
        }
    }
}
