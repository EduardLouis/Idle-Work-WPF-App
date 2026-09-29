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
        public const double DefaultDwellDebounceSeconds = 2.5; // 2.5 seconds
        public const float DefaultMicSensitivity = 0.05f; // 5% peak audio volume

        private readonly DatabaseService _databaseService;
        private readonly ActivityAggregator _aggregator;
        private readonly IdleDetectionService _idleDetector;
        private readonly AudioLevelService _audioService;

        private int _idleTimeoutSeconds = DefaultIdleTimeoutSeconds;
        private double _dwellDebounceSeconds = DefaultDwellDebounceSeconds;
        private float _micSensitivity = DefaultMicSensitivity;
        private string _databasePath = "";
        private string _statusMessage = "";

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
                if (SetProperty(ref _dwellDebounceSeconds, value))
                {
                    _aggregator.DwellDebounceSeconds = value;
                }
            }
        }

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

        // [v0.2: Preferences] Reset to default commands for all sliders
        public ICommand ResetIdleTimeoutCommand { get; }
        public ICommand ResetDwellDebounceCommand { get; }
        public ICommand ResetMicSensitivityCommand { get; }
        public ICommand OpenDatabaseFolderCommand { get; }

        public SettingsViewModel(
            DatabaseService databaseService,
            ActivityAggregator aggregator,
            IdleDetectionService idleDetector,
            AudioLevelService audioService)
        {
            _databaseService = databaseService;
            _aggregator = aggregator;
            _idleDetector = idleDetector;
            _audioService = audioService;

            IdleTimeoutSeconds = _idleDetector.IdleTimeoutSeconds;
            DwellDebounceSeconds = _aggregator.DwellDebounceSeconds;
            MicSensitivity = _audioService.SensitivityThreshold;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            DatabasePath = Path.Combine(localAppData, "IdleWork", "idlework.db");

            // [v0.2: Preferences] Initialize reset commands
            ResetIdleTimeoutCommand = new RelayCommand(() => IdleTimeoutSeconds = DefaultIdleTimeoutSeconds);
            ResetDwellDebounceCommand = new RelayCommand(() => DwellDebounceSeconds = DefaultDwellDebounceSeconds);
            ResetMicSensitivityCommand = new RelayCommand(() => MicSensitivity = DefaultMicSensitivity);

            OpenDatabaseFolderCommand = new RelayCommand(OpenDatabaseFolder);
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
