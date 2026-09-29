// [v0.2: AutoTagRuleModel] Persistent rule definition for automatic project/tag assignment with observable change notifications
using SQLite;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Core.Models
{
    [Table("AutoTagRules")]
    public class AutoTagRule : ObservableObject
    {
        private int _id;
        private string _ruleName = string.Empty;
        private string? _processFilter;
        private string? _titlePattern;
        private bool _isRegex = false;
        private string _targetProject = string.Empty;
        private string? _targetCategory;
        private string? _targetTags;
        private int _priority = 100;
        private bool _isEnabled = true;

        [PrimaryKey, AutoIncrement]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string RuleName
        {
            get => _ruleName;
            set => SetProperty(ref _ruleName, value);
        }

        // Matches process name (e.g. "Revit", "acad", "devenv", "chrome")
        [Indexed]
        public string? ProcessFilter
        {
            get => _processFilter;
            set => SetProperty(ref _processFilter, value);
        }

        // Matches window title (regex or keyword, e.g. "Hospital", "Tower")
        public string? TitlePattern
        {
            get => _titlePattern;
            set => SetProperty(ref _titlePattern, value);
        }

        public bool IsRegex
        {
            get => _isRegex;
            set => SetProperty(ref _isRegex, value);
        }

        [Indexed]
        public string TargetProject
        {
            get => _targetProject;
            set => SetProperty(ref _targetProject, value);
        }

        public string? TargetCategory
        {
            get => _targetCategory;
            set => SetProperty(ref _targetCategory, value);
        }

        public string? TargetTags
        {
            get => _targetTags;
            set => SetProperty(ref _targetTags, value);
        }

        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }
}
