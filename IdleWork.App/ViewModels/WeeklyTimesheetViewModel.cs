// [v0.1: WeeklyTimesheetVM] Weekly timesheet matrix ViewModel with CSV export
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using IdleWork.App.Core.Services;

namespace IdleWork.App.ViewModels
{
    public class WeeklyTimesheetViewModel : ObservableObject
    {
        private readonly TimesheetService _timesheetService;
        private DateTime _referenceDate = DateTime.Today;
        private WeeklyTimesheetData? _currentData;
        private string _weekRangeText = "";
        private string _totalWorkHoursText = "0.0h";
        private string _statusMessage = "";

        public DateTime ReferenceDate
        {
            get => _referenceDate;
            set
            {
                if (SetProperty(ref _referenceDate, value))
                {
                    LoadWeeklyDataAsync();
                }
            }
        }

        public string WeekRangeText
        {
            get => _weekRangeText;
            set => SetProperty(ref _weekRangeText, value);
        }

        public string TotalWorkHoursText
        {
            get => _totalWorkHoursText;
            set => SetProperty(ref _totalWorkHoursText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<WeeklyTimesheetRow> Rows { get; } = new ObservableCollection<WeeklyTimesheetRow>();

        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }
        public ICommand CurrentWeekCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand RefreshCommand { get; }

        public WeeklyTimesheetViewModel(TimesheetService timesheetService)
        {
            _timesheetService = timesheetService;

            PreviousWeekCommand = new RelayCommand(() => ReferenceDate = ReferenceDate.AddDays(-7));
            NextWeekCommand = new RelayCommand(() => ReferenceDate = ReferenceDate.AddDays(7));
            CurrentWeekCommand = new RelayCommand(() => ReferenceDate = DateTime.Today);
            RefreshCommand = new RelayCommand(() => LoadWeeklyDataAsync());
            ExportCsvCommand = new RelayCommand(ExportCsv);

            LoadWeeklyDataAsync();
        }

        public async void LoadWeeklyDataAsync()
        {
            _currentData = await _timesheetService.GetWeeklyTimesheetAsync(ReferenceDate);

            WeekRangeText = $"{_currentData.WeekStartDate:MMM d, yyyy} – {_currentData.WeekEndDate:MMM d, yyyy}";
            TotalWorkHoursText = $"{_currentData.GrandTotalWorkHours:F1} hrs";

            Rows.Clear();
            foreach (var r in _currentData.Rows)
            {
                Rows.Add(r);
            }
        }

        private void ExportCsv()
        {
            if (_currentData == null) return;

            try
            {
                string csv = _timesheetService.ExportWeeklyTimesheetToCsv(_currentData);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string fileName = $"IdleWork_Timesheet_{_currentData.WeekStartDate:yyyyMMdd}_{_currentData.WeekEndDate:yyyyMMdd}.csv";
                string fullPath = Path.Combine(desktop, fileName);

                File.WriteAllText(fullPath, csv);
                StatusMessage = $"Exported successfully to Desktop: {fileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }
}
