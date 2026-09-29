// [v0.2: Categories] Predefined category entity for activity classification
using SQLite;

namespace IdleWork.App.Core.Models
{
    [Table("Categories")]
    public class WorkCategory : ObservableObject
    {
        private int _id;
        private string _name = "";
        private string? _description;
        private string _colorHex = "#3B82F6";

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

        public string ColorHex
        {
            get => _colorHex;
            set => SetProperty(ref _colorHex, value);
        }
    }
}
