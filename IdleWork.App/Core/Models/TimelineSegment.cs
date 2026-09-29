// [v0.2: Models] Visual segment for the multi-band daily timeline ribbon (Usage, Applications, Documents)
namespace IdleWork.App.Core.Models
{
    public class TimelineSegment
    {
        public double LeftRatio { get; set; } // 0.0 to 1.0 across the timeline width
        public double WidthRatio { get; set; } // 0.0 to 1.0 width
        public string ColorHex { get; set; } = "#3B82F6";
        public string Label { get; set; } = string.Empty;
        public string ToolTipText { get; set; } = string.Empty;
    }
}
