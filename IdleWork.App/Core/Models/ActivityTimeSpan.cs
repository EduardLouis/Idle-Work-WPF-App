// [v0.1: ActivityModel] Core entity representing a chunk of tracked user activity
using System;
using SQLite;

namespace IdleWork.App.Core.Models
{
    [Table("ActivityTimeSpans")]
    public class ActivityTimeSpan : IdleWork.App.ViewModels.ObservableObject
    {
        private bool _isExpanded;
        private double _percentageOfDay;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public double DurationSeconds { get; set; }

        [Indexed]
        public string ProcessName { get; set; } = string.Empty;

        public string WindowTitle { get; set; } = string.Empty;

        public string DocumentName { get; set; } = string.Empty;

        public int MonitorIndex { get; set; } = 0;

        public string MonitorBounds { get; set; } = string.Empty;

        [Indexed]
        public string State { get; set; } = "Active"; // "Active", "Idle", "Meeting"

        [Indexed]
        public string? ProjectName { get; set; }

        public string? Category { get; set; }

        public string? Tags { get; set; }

        public bool IsManualEdit { get; set; }
        
        // [v0.2: MultiMonitor] Concurrent secondary/focused window while working in primary app
        public string? SubProcessName { get; set; }
        public string? SubWindowTitle { get; set; }
        public string? SubDocumentName { get; set; }

        // [v0.2: MultiMonitor] JSON serialized snapshot of top visible windows across all monitors
        public string? TopWindowsJson { get; set; }

        // [v0.2: SessionConsolidation] In-memory or joined discrete session intervals
        [Ignore]
        public System.Collections.Generic.List<ActivityInterval> Intervals { get; set; } = new System.Collections.Generic.List<ActivityInterval>();

        // [v0.2: MultiMonitor] Helpers for Main and Sub activity display in feeds
        [Ignore]
        public bool HasSubActivity => !string.IsNullOrEmpty(SubProcessName);

        [Ignore]
        public string SubActivityDisplay => !string.IsNullOrEmpty(SubProcessName)
            ? $"{SubProcessName} {(string.IsNullOrEmpty(SubDocumentName) ? SubWindowTitle : SubDocumentName)}"
            : string.Empty;

        [Ignore]
        public string SubActivityDetailDisplay => !string.IsNullOrEmpty(SubProcessName)
            ? (!string.IsNullOrEmpty(SubDocumentName) ? SubDocumentName : (SubWindowTitle ?? SubProcessName))
            : string.Empty;

        [Ignore]
        public int IntervalCount => Intervals.Count > 0 ? Intervals.Count : 1;

        [Ignore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        [Ignore]
        public double PercentageOfDay
        {
            get => _percentageOfDay;
            set => SetProperty(ref _percentageOfDay, value);
        }

        [Ignore]
        public string PercentageText => $"{PercentageOfDay:F1}%";

        [Ignore]
        public string TimeSpanRangeText => $"{StartTime:HH:mm} - {EndTime:HH:mm}";

        [Ignore]
        public System.Windows.Input.ICommand ToggleExpandCommand => new IdleWork.App.ViewModels.RelayCommand(() => IsExpanded = !IsExpanded);

        [Ignore]
        public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

        [Ignore]
        public string DisplayDuration => Duration.TotalHours >= 1
            ? $"{(int)Duration.TotalHours}h {Duration.Minutes:D2}m {Duration.Seconds:D2}s"
            : $"{Duration.Minutes}m {Duration.Seconds:D2}s";

        [Ignore]
        public string StatusColorHex => State switch
        {
            "Active" => "#10B981",   // Emerald green
            "Meeting" => "#F59E0B",  // Amber gold
            "Idle" => "#64748B",     // Slate grey
            _ => "#94A3B8"
        };
    }
}
