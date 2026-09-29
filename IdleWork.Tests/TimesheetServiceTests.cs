// [v0.1: Test] Unit tests for TimesheetService and CSV export
using System;
using System.IO;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;
using Xunit;

namespace IdleWork.Tests
{
    public class TimesheetServiceTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DatabaseService _dbService;
        private readonly TimesheetService _timesheetService;

        public TimesheetServiceTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_timesheet_{Guid.NewGuid():N}.db");
            _dbService = new DatabaseService(_tempDbPath);
            _timesheetService = new TimesheetService(_dbService);
        }

        [Fact]
        public async Task GetWeeklyTimesheetAsync_AggregatesHoursByDayAndProject()
        {
            // Pick reference date: Wednesday, Sep 30, 2026
            var refDate = new DateTime(2026, 9, 30);
            int diff = (7 + (refDate.Date.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = refDate.Date.AddDays(-1 * diff);

            // Add activities for Monday and Wednesday
            var actMon = new ActivityTimeSpan
            {
                StartTime = monday.AddHours(9),
                EndTime = monday.AddHours(11),
                DurationSeconds = 7200, // 2 hours
                ProcessName = "Revit",
                ProjectName = "BIM & CAD Modeling",
                State = "Active"
            };

            var actWed = new ActivityTimeSpan
            {
                StartTime = monday.AddDays(2).AddHours(14),
                EndTime = monday.AddDays(2).AddHours(15).AddMinutes(30),
                DurationSeconds = 5400, // 1.5 hours
                ProcessName = "Teams",
                ProjectName = "Meetings & Calls",
                State = "Meeting"
            };

            await _dbService.SaveActivityAsync(actMon);
            await _dbService.SaveActivityAsync(actWed);

            var weeklyData = await _timesheetService.GetWeeklyTimesheetAsync(refDate);

            Assert.NotNull(weeklyData);
            Assert.Equal(2.0, weeklyData.DailyActiveHours[0], 2); // Monday active
            Assert.Equal(1.5, weeklyData.DailyMeetingHours[2], 2); // Wednesday meeting
            Assert.Equal(3.5, weeklyData.GrandTotalWorkHours, 2);

            // Verify CSV export
            string csv = _timesheetService.ExportWeeklyTimesheetToCsv(weeklyData);
            Assert.Contains("BIM & CAD Modeling", csv);
            Assert.Contains("Meetings & Calls", csv);
            Assert.Contains("Daily Active Work", csv);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_tempDbPath))
                    File.Delete(_tempDbPath);
            }
            catch
            {
            }
        }
    }
}
