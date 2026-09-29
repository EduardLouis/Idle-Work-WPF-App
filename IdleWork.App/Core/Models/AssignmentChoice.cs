// [v0.2: Models] Dual assignment choice (Classification Rule or Direct Project) for daily timeline
namespace IdleWork.App.Core.Models
{
    public class AssignmentChoice
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string TargetProject { get; set; } = string.Empty;
        public string? TargetCategory { get; set; }
        public string? TargetTags { get; set; }
        public bool IsRule { get; set; }
        public bool IsCreateAction { get; set; } = false;
        public AutoTagRule? Rule { get; set; }
        public Project? Project { get; set; }

        public override string ToString() => Title;
    }
}
