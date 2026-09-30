// [v0.1: SettingsVM] Configuration and tuning settings ViewModel
using System;
using System.IO;
using System.Windows.Input;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;

namespace IdleWork.App.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        // [v0.2: Preferences] Defaults for all configurable tracking sliders
        public const int DefaultIdleTimeoutSeconds = 180; // 3 minutes
        public const double DefaultDwellDebounceSeconds = 30.0; // 30 seconds default (30s to 180s range)
        public const float DefaultMicSensitivity = 0.05f; // 5% peak audio volume

        // [v0.003: Preferences] Screenshot capture defaults
        public const bool DefaultEnableScreenshots = true;
        public const bool DefaultCaptureOnWindowSwitch = true;
        public const int DefaultScreenshotIntervalMinutes = 5; // 5 minutes

        // [v0.003: SystemTray] System Tray preferences defaults
        public const bool DefaultMinimizeToTray = true;
        public const bool DefaultCloseToTray = true;

        private readonly DatabaseService _databaseService;
        private readonly ActivityAggregator _aggregator;
        private readonly IdleDetectionService _idleDetector;
        private readonly AudioLevelService _audioService;
        private readonly ScreenshotService _screenshotService;

        private int _idleTimeoutSeconds = DefaultIdleTimeoutSeconds;
        private double _dwellDebounceSeconds = DefaultDwellDebounceSeconds;
        private float _micSensitivity = DefaultMicSensitivity;
        private bool _enableScreenshots = DefaultEnableScreenshots;
        private bool _captureOnWindowSwitch = DefaultCaptureOnWindowSwitch;
        private int _screenshotIntervalMinutes = DefaultScreenshotIntervalMinutes;
        private bool _minimizeToTray = DefaultMinimizeToTray;
        private bool _closeToTray = DefaultCloseToTray;
        private string _databasePath = "";
        private string _statusMessage = "";
        private string _cloudEndpointUrl = "";
        private bool _enableCloudTelemetry = true;

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                if (SetProperty(ref _minimizeToTray, value))
                {
                    SystemTrayService.Instance.MinimizeToTray = value;
                    _ = _databaseService.SetSettingAsync("MinimizeToTray", value.ToString());
                }
            }
        }

        public bool CloseToTray
        {
            get => _closeToTray;
            set
            {
                if (SetProperty(ref _closeToTray, value))
                {
                    SystemTrayService.Instance.CloseToTray = value;
                    _ = _databaseService.SetSettingAsync("CloseToTray", value.ToString());
                }
            }
        }

        public string CloudEndpointUrl
        {
            get => _cloudEndpointUrl;
            set
            {
                if (SetProperty(ref _cloudEndpointUrl, value))
                {
                    LoggingService.Instance.CloudEndpointUrl = value?.Trim() ?? "";
                    _ = _databaseService.SetSettingAsync("CloudEndpointUrl", value?.Trim() ?? "");
                }
            }
        }

        public bool EnableCloudTelemetry
        {
            get => _enableCloudTelemetry;
            set
            {
                if (SetProperty(ref _enableCloudTelemetry, value))
                {
                    LoggingService.Instance.EnableCloudTelemetry = value;
                    _ = _databaseService.SetSettingAsync("EnableCloudTelemetry", value.ToString());
                }
            }
        }

        public int IdleTimeoutSeconds
        {
            get => _idleTimeoutSeconds;
            set
            {
                // [v0.2: Preferences] 30s, 60s (1 min), then 1-minute steps (no 30s steps above 60s)
                int snappedValue;
                if (value <= 45)
                {
                    snappedValue = 30;
                }
                else if (value < 90)
                {
                    snappedValue = 60;
                }
                else
                {
                    int minutes = (int)Math.Round((double)value / 60.0);
                    snappedValue = minutes * 60;
                }

                if (SetProperty(ref _idleTimeoutSeconds, snappedValue))
                {
                    _idleDetector.IdleTimeoutSeconds = snappedValue;
                    OnPropertyChanged(nameof(IdleTimeoutDisplayText));
                }
            }
        }

        // [v0.2: Preferences] Time display: 30 seconds, 1 minute, then strictly minutes (never seconds above 60s)
        public string IdleTimeoutDisplayText
        {
            get
            {
                if (_idleTimeoutSeconds <= 30)
                    return "30 seconds";
                if (_idleTimeoutSeconds <= 60)
                    return "1 minute";

                int minutes = (int)Math.Round((double)_idleTimeoutSeconds / 60.0);
                return $"{minutes} minutes";
            }
        }

        public double DwellDebounceSeconds
        {
            get => _dwellDebounceSeconds;
            set
            {
                // [v0.003: DwellRange] Snapped in 30-second steps from 30s to 180s (3 min)
                double snappedValue = Math.Round(value / 30.0) * 30.0;
                if (snappedValue < 30.0) snappedValue = 30.0;
                if (snappedValue > 180.0) snappedValue = 180.0;

                if (SetProperty(ref _dwellDebounceSeconds, snappedValue))
                {
                    _aggregator.DwellDebounceSeconds = snappedValue;
                    OnPropertyChanged(nameof(DwellDebounceDisplay));
                }
            }
        }

        public string DwellDebounceDisplay =>
            _dwellDebounceSeconds switch
            {
                <= 30.0 => "30 seconds",
                60.0 => "1 minute",
                90.0 => "1m 30s",
                120.0 => "2 minutes",
                150.0 => "2m 30s",
                >= 180.0 => "3 minutes",
                _ => $"{_dwellDebounceSeconds:F0} seconds"
            };

        public float MicSensitivity
        {
            get => _micSensitivity;
            set
            {
                if (SetProperty(ref _micSensitivity, value))
                {
                    _audioService.SensitivityThreshold = value;
                }
            }
        }

        public string DatabasePath
        {
            get => _databasePath;
            set => SetProperty(ref _databasePath, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // [v0.003: Preferences] Screenshot settings properties
        public bool EnableScreenshots
        {
            get => _enableScreenshots;
            set
            {
                if (SetProperty(ref _enableScreenshots, value))
                {
                    _screenshotService.EnableScreenshots = value;
                }
            }
        }

        public bool CaptureOnWindowSwitch
        {
            get => _captureOnWindowSwitch;
            set
            {
                if (SetProperty(ref _captureOnWindowSwitch, value))
                {
                    _screenshotService.CaptureOnWindowSwitch = value;
                }
            }
        }

        public int ScreenshotIntervalMinutes
        {
            get => _screenshotIntervalMinutes;
            set
            {
                // Snap to valid interval: 0 (Off), 1, 2, 5, 10, 15, 30
                int snapped;
                if (value <= 0) snapped = 0;
                else if (value <= 1) snapped = 1;
                else if (value <= 3) snapped = 2;
                else if (value <= 7) snapped = 5;
                else if (value <= 12) snapped = 10;
                else if (value <= 22) snapped = 15;
                else snapped = 30;

                if (SetProperty(ref _screenshotIntervalMinutes, snapped))
                {
                    _screenshotService.ScreenshotIntervalMinutes = snapped;
                    OnPropertyChanged(nameof(ScreenshotIntervalDisplayText));
                }
            }
        }

        public string ScreenshotIntervalDisplayText
        {
            get
            {
                if (_screenshotIntervalMinutes == 0)
                    return "Off (No periodic screenshots)";
                if (_screenshotIntervalMinutes == 1)
                    return "1 minute";
                return $"{_screenshotIntervalMinutes} minutes";
            }
        }

        // [v0.004: ScreenshotRetention] Retention period configuration (0=Forever, 7, 14, 30, 60, 90 days)
        public const int DefaultScreenshotRetentionDays = 14;
        private int _screenshotRetentionDays = DefaultScreenshotRetentionDays;

        public int ScreenshotRetentionDays
        {
            get => _screenshotRetentionDays;
            set
            {
                int snapped;
                if (value <= 3) snapped = 0; // 0 = Forever
                else if (value <= 10) snapped = 7;
                else if (value <= 20) snapped = 14;
                else if (value <= 45) snapped = 30;
                else if (value <= 75) snapped = 60;
                else snapped = 90;

                if (SetProperty(ref _screenshotRetentionDays, snapped))
                {
                    _screenshotService.RetentionDays = snapped;
                    _ = _databaseService.SetSettingAsync("ScreenshotRetentionDays", snapped.ToString());
                    OnPropertyChanged(nameof(ScreenshotRetentionDisplayText));
                }
            }
        }

        public string ScreenshotRetentionDisplayText =>
            _screenshotRetentionDays switch
            {
                0 => "Keep Forever (No automatic purge)",
                7 => "7 days (1 week)",
                14 => "14 days (2 weeks - Default)",
                30 => "30 days (1 month)",
                60 => "60 days (2 months)",
                90 => "90 days (3 months)",
                _ => $"{_screenshotRetentionDays} days"
            };

        public string ScreenshotStorageStatsText
        {
            get
            {
                var (files, bytes) = _screenshotService.GetStorageStats();
                return $"{files} captures ({bytes / (1024.0 * 1024.0):F1} MB)";
            }
        }

        public void RefreshScreenshotStats()
        {
            OnPropertyChanged(nameof(ScreenshotStorageStatsText));
        }

        // [v0.2: Preferences] Reset to default commands for all sliders
        public ICommand ResetIdleTimeoutCommand { get; }
        public ICommand ResetDwellDebounceCommand { get; }
        public ICommand ResetMicSensitivityCommand { get; }
        public ICommand ResetScreenshotSettingsCommand { get; }
        public ICommand ResetCloudSettingsCommand { get; }
        public ICommand ResetTraySettingsCommand { get; }
        public ICommand MinimizeToTrayNowCommand { get; }
        public ICommand TestTrayNotificationCommand { get; }
        public ICommand OpenDatabaseFolderCommand { get; }
        public ICommand OpenScreenshotsFolderCommand { get; }
        public ICommand OpenLogsFolderCommand { get; }
        // [v0.004: ScreenshotRetention] Management commands
        public ICommand PurgeOldScreenshotsNowCommand { get; }
        public ICommand ClearAllScreenshotsCommand { get; }

        public SettingsViewModel(
            DatabaseService databaseService,
            ActivityAggregator aggregator,
            IdleDetectionService idleDetector,
            AudioLevelService audioService,
            ScreenshotService? screenshotService = null)
        {
            _databaseService = databaseService;
            _aggregator = aggregator;
            _idleDetector = idleDetector;
            _audioService = audioService;
            _screenshotService = screenshotService ?? ScreenshotService.Instance;

            IdleTimeoutSeconds = _idleDetector.IdleTimeoutSeconds;
            DwellDebounceSeconds = _aggregator.DwellDebounceSeconds;
            MicSensitivity = _audioService.SensitivityThreshold;
            EnableScreenshots = _screenshotService.EnableScreenshots;
            CaptureOnWindowSwitch = _screenshotService.CaptureOnWindowSwitch;
            ScreenshotIntervalMinutes = _screenshotService.ScreenshotIntervalMinutes;
            MinimizeToTray = SystemTrayService.Instance.MinimizeToTray;
            CloseToTray = SystemTrayService.Instance.CloseToTray;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            DatabasePath = Path.Combine(localAppData, "IdleWork", "idlework.db");

            // [v0.2: Preferences] Initialize reset commands
            ResetIdleTimeoutCommand = new RelayCommand(() => IdleTimeoutSeconds = DefaultIdleTimeoutSeconds);
            ResetDwellDebounceCommand = new RelayCommand(() => DwellDebounceSeconds = DefaultDwellDebounceSeconds);
            ResetMicSensitivityCommand = new RelayCommand(() => MicSensitivity = DefaultMicSensitivity);
            ResetScreenshotSettingsCommand = new RelayCommand(() =>
            {
                EnableScreenshots = DefaultEnableScreenshots;
                CaptureOnWindowSwitch = DefaultCaptureOnWindowSwitch;
                ScreenshotIntervalMinutes = DefaultScreenshotIntervalMinutes;
                ScreenshotRetentionDays = DefaultScreenshotRetentionDays;
            });
            ResetCloudSettingsCommand = new RelayCommand(() =>
            {
                CloudEndpointUrl = "";
                EnableCloudTelemetry = true;
            });
            ResetTraySettingsCommand = new RelayCommand(() =>
            {
                MinimizeToTray = DefaultMinimizeToTray;
                CloseToTray = DefaultCloseToTray;
            });

            MinimizeToTrayNowCommand = new RelayCommand(() =>
            {
                if (System.Windows.Application.Current?.MainWindow != null)
                {
                    System.Windows.Application.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized;
                }
            });

            TestTrayNotificationCommand = new RelayCommand(() =>
            {
                SystemTrayService.Instance.ShowNotification(
                    "Idle-Work Active",
                    "System tray icon is online! Click to restore Idle-Work.");
            });

            OpenDatabaseFolderCommand = new RelayCommand(OpenDatabaseFolder);
            OpenScreenshotsFolderCommand = new RelayCommand(OpenScreenshotsFolder);
            OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);

            // [v0.004: ScreenshotRetention] Commands
            PurgeOldScreenshotsNowCommand = new RelayCommand(() =>
            {
                int purged = _screenshotService.PurgeOldScreenshots();
                RefreshScreenshotStats();
                StatusMessage = purged > 0 ? $"Cleaned {purged} old screenshot files." : "No old screenshots needed cleanup.";
            });

            ClearAllScreenshotsCommand = new RelayCommand(() =>
            {
                var (files, bytes) = _screenshotService.ClearAllScreenshots();
                RefreshScreenshotStats();
                StatusMessage = $"Cleared all screenshots: {files} files ({bytes / (1024.0 * 1024.0):F1} MB freed).";
            });

            _ = LoadTelemetrySettingsAsync();
        }

        private async Task LoadTelemetrySettingsAsync()
        {
            try
            {
                string? url = await _databaseService.GetSettingAsync("CloudEndpointUrl").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(url))
                {
                    _cloudEndpointUrl = url;
                    LoggingService.Instance.CloudEndpointUrl = url;
                    OnPropertyChanged(nameof(CloudEndpointUrl));
                }

                string? enabled = await _databaseService.GetSettingAsync("EnableCloudTelemetry").ConfigureAwait(false);
                if (bool.TryParse(enabled, out bool isEnabled))
                {
                    _enableCloudTelemetry = isEnabled;
                    LoggingService.Instance.EnableCloudTelemetry = isEnabled;
                    OnPropertyChanged(nameof(EnableCloudTelemetry));
                }

                // [v0.003: SystemTray] Load system tray preferences
                string? minToTray = await _databaseService.GetSettingAsync("MinimizeToTray").ConfigureAwait(false);
                if (bool.TryParse(minToTray, out bool isMinToTray))
                {
                    _minimizeToTray = isMinToTray;
                    SystemTrayService.Instance.MinimizeToTray = isMinToTray;
                    OnPropertyChanged(nameof(MinimizeToTray));
                }

                string? closeToTray = await _databaseService.GetSettingAsync("CloseToTray").ConfigureAwait(false);
                if (bool.TryParse(closeToTray, out bool isCloseToTray))
                {
                    _closeToTray = isCloseToTray;
                    SystemTrayService.Instance.CloseToTray = isCloseToTray;
                    OnPropertyChanged(nameof(CloseToTray));
                }

                // [v0.004: ScreenshotRetention] Load retention days preference
                string? retDays = await _databaseService.GetSettingAsync("ScreenshotRetentionDays").ConfigureAwait(false);
                if (int.TryParse(retDays, out int rDays))
                {
                    _screenshotRetentionDays = rDays;
                    _screenshotService.RetentionDays = rDays;
                    OnPropertyChanged(nameof(ScreenshotRetentionDays));
                    OnPropertyChanged(nameof(ScreenshotRetentionDisplayText));
                }
                RefreshScreenshotStats();
            }
            catch { }
        }

        private void OpenLogsFolder()
        {
            try
            {
                string folder = LoggingService.Instance.LogDirectory;
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Unable to open logs folder: {ex.Message}";
            }
        }

        private void OpenScreenshotsFolder()
        {
            try
            {
                string folder = _screenshotService.BaseScreenshotsFolder;
                if (Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Unable to open screenshots folder: {ex.Message}";
            }
        }

        private void OpenDatabaseFolder()
        {
            try
            {
                string? folder = Path.GetDirectoryName(DatabasePath);
                if (folder != null && Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Unable to open folder: {ex.Message}";
            }
        }
    }
}
