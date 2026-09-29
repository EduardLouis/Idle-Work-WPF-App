// [v0.1: AppSettingsModel] User configurable settings for idle timeouts, debounce, and audio thresholds
namespace IdleWork.App.Core.Models
{
    public class AppSettings
    {
        public int IdleTimeoutSeconds { get; set; } = 180; // 3 minutes default
        public float MicThreshold { get; set; } = 0.05f; // 5% peak audio volume
        public double DwellDebounceSeconds { get; set; } = 2.5; // 2.5s minimum dwell time
        public bool EnableMicMonitoring { get; set; } = true;
        public bool AutoStartWithWindows { get; set; } = false;
        public string Theme { get; set; } = "Dark"; // "Dark" or "Light"
    }
}
