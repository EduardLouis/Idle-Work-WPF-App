// [v0.1: MonitorInfoModel] Multi-monitor display bounds and top-window state
namespace IdleWork.App.Core.Models
{
    public class MonitorInfo
    {
        public int Index { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsPrimary { get; set; }
        public string? TopWindowTitle { get; set; }
        public string? TopProcessName { get; set; }

        public string BoundsText => $"{Width}x{Height} at ({Left},{Top})";
    }
}
