// [v0.2: Models] Discrete session interval belonging to a consolidated daily activity
using System;
using SQLite;

namespace IdleWork.App.Core.Models
{
    [Table("ActivityIntervals")]
    public class ActivityInterval
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int ActivityId { get; set; }

        [Indexed]
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public double DurationSeconds { get; set; }

        public string? SubProcessName { get; set; }

        public string? SubWindowTitle { get; set; }

        [Ignore]
        public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

        [Ignore]
        public string DisplayDuration => Duration.TotalHours >= 1
            ? $"{(int)Duration.TotalHours}h {Duration.Minutes:D2}m {Duration.Seconds:D2}s"
            : $"{Duration.Minutes}m {Duration.Seconds:D2}s";

        [Ignore]
        public string TimeRangeText => $"{StartTime:HH:mm:ss} - {EndTime:HH:mm:ss}";
    }
}
