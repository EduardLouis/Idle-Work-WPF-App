// [v0.2: Models] User-defined software priority ranking for primary activity determination with dynamic UI notifications
using SQLite;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Core.Models
{
    [Table("SoftwarePriorities")]
    public class SoftwarePriority : ObservableObject
    {
        private int _id;
        private string _processFilter = string.Empty;
        private string _displayName = string.Empty;
        private int _priority;
        private bool _isEnabled = true;

        [PrimaryKey, AutoIncrement]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [Indexed]
        public string ProcessFilter
        {
            get => _processFilter;
            set => SetProperty(ref _processFilter, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        [Indexed]
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
