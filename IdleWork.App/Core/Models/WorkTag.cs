// [v0.2: Tags] Predefined tag entity for multi-tagging activities
using SQLite;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Core.Models
{
    [Table("Tags")]
    public class WorkTag : ObservableObject
    {
        private int _id;
        private string _name = "";
        private string? _description;

        [PrimaryKey, AutoIncrement]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
}
