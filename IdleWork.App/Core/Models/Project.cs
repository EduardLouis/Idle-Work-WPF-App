// [v0.1: ProjectModel] Project entity for categorizing activities and timesheets
using SQLite;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Core.Models
{
    [Table("Projects")]
    public class Project : ObservableObject
    {
        private int _id;
        private string _name = string.Empty;
        private string _code = string.Empty;
        private string _colorHex = "#3B82F6";
        private string _description = string.Empty;
        private bool _isActive = true;

        [PrimaryKey, AutoIncrement]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [Indexed(Unique = true)]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value);
        }

        public string ColorHex
        {
            get => _colorHex;
            set => SetProperty(ref _colorHex, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
    }
}
