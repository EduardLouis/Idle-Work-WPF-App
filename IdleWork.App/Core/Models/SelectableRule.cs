// [v0.2: SelectableRule] Model for selecting and associating rules with projects
using IdleWork.App.ViewModels;

namespace IdleWork.App.Core.Models
{
    public class SelectableRule : ObservableObject
    {
        private bool _isSelected;
        public AutoTagRule Rule { get; }
        public bool IsNew { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string RuleName => Rule.RuleName;
        public string ProcessFilter => Rule.ProcessFilter ?? "Any";
        public string TitlePattern => Rule.TitlePattern ?? "*";
        public string SummaryText => $"{ProcessFilter} | {TitlePattern}";
        public string CurrentTargetProject => Rule.TargetProject ?? "";

        public SelectableRule(AutoTagRule rule, bool isSelected = false, bool isNew = false)
        {
            Rule = rule;
            _isSelected = isSelected;
            IsNew = isNew;
        }
    }
}
