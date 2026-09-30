// [v0.003: AppSettingsModel] User configurable settings for idle timeouts, debounce, audio thresholds, and cloud telemetry
using SQLite;

namespace IdleWork.App.Core.Models
{
    public class AppSettings
    {
        public int IdleTimeoutSeconds { get; set; } = 180; // 3 minutes default
        public float MicThreshold { get; set; } = 0.05f; // 5% peak audio volume
        public double DwellDebounceSeconds { get; set; } = 30.0; // 30s minimum dwell time default (30s - 180s range)
        public bool EnableMicMonitoring { get; set; } = true;
        public bool AutoStartWithWindows { get; set; } = false;
        public string Theme { get; set; } = "Dark"; // "Dark" or "Light"
        public string CloudEndpointUrl { get; set; } = "";
    }

    [Table("AppConfigEntries")]
    public class AppConfigEntry
    {
        [PrimaryKey]
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
