// [v0.003: Views] Modal dialog to preview full resolution activity screenshot
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IdleWork.App.Views
{
    public partial class ScreenshotPreviewDialog : Window
    {
        private readonly string _filePath;

        public ScreenshotPreviewDialog(string filePath, string appName, string windowTitle, DateTime? timestamp = null)
        {
            InitializeComponent();
            _filePath = filePath;

            TxtAppDescription.Text = string.IsNullOrWhiteSpace(appName) ? "Application" : appName;
            TxtWindowTitle.Text = windowTitle;
            TxtTimestamp.Text = timestamp.HasValue ? $" • {timestamp.Value:yyyy-MM-dd HH:mm:ss}" : "";
            TxtFilePath.Text = filePath;

            LoadScreenshot(filePath);

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                }
            };
        }

        private bool _is100PercentMode = false;

        private void LoadScreenshot(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ImgPreviewFit.Source = bitmap;
                    ImgPreviewOriginal.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenshotPreviewDialog] Error loading image: {ex.Message}");
            }
        }

        private void ToggleViewMode_Click(object sender, RoutedEventArgs e)
        {
            _is100PercentMode = !_is100PercentMode;
            if (_is100PercentMode)
            {
                ImgPreviewFit.Visibility = Visibility.Collapsed;
                ScrollOriginal.Visibility = Visibility.Visible;
                BtnToggleViewMode.Content = "📐 Fit to Window";
            }
            else
            {
                ScrollOriginal.Visibility = Visibility.Collapsed;
                ImgPreviewFit.Visibility = Visibility.Visible;
                BtnToggleViewMode.Content = "🔍 1:1 Actual Size";
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Maximize_Click(sender, e);
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void OpenInDefaultViewer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? folder = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
