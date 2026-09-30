// [v0.1: TimesheetService] Daily timeline and weekly timesheet matrix aggregation with CSV export
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Core.Services
{
    public class WeeklyTimesheetRow
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#3B82F6";
        public double[] DayHours { get; set; } = new double[7]; // Monday to Sunday (indices 0..6)
        public double TotalHours => DayHours.Sum();

        public string GetFormattedDay(int dayIndex)
        {
            if (dayIndex < 0 || dayIndex >= 7) return "0:00";
            var ts = TimeSpan.FromHours(DayHours[dayIndex]);
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
        }

        public string FormattedTotal
        {
            get
            {
                var ts = TimeSpan.FromHours(TotalHours);
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
            }
        }
    }

    public class WeeklyTimesheetData
    {
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public List<WeeklyTimesheetRow> Rows { get; set; } = new List<WeeklyTimesheetRow>();
        public double[] DailyActiveHours { get; set; } = new double[7];
        public double[] DailyMeetingHours { get; set; } = new double[7];
        public double[] DailyIdleHours { get; set; } = new double[7];

        public double TotalActiveHours => DailyActiveHours.Sum();
        public double TotalMeetingHours => DailyMeetingHours.Sum();
        public double TotalIdleHours => DailyIdleHours.Sum();
        public double GrandTotalWorkHours => TotalActiveHours + TotalMeetingHours;
    }

    public class TimesheetService
    {
        private readonly DatabaseService _databaseService;

        public TimesheetService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<WeeklyTimesheetData> GetWeeklyTimesheetAsync(DateTime referenceDate)
        {
            // Calculate Monday of the reference week
            int diff = (7 + (referenceDate.Date.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = referenceDate.Date.AddDays(-1 * diff);
            DateTime sundayEnd = monday.AddDays(7).AddTicks(-1);

            var activities = await _databaseService.GetActivitiesForDateRangeAsync(monday, sundayEnd);
            var projects = await _databaseService.GetProjectsAsync();

            var data = new WeeklyTimesheetData
            {
                WeekStartDate = monday,
                WeekEndDate = monday.AddDays(6)
            };

            // Map projects into rows
            var projectDict = new Dictionary<string, WeeklyTimesheetRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in projects)
            {
                projectDict[p.Name] = new WeeklyTimesheetRow
                {
                    ProjectName = p.Name,
                    ColorHex = p.ColorHex
                };
            }

            // Also ensure "Unassigned" row exists if needed
            string unassignedKey = "Unassigned / Other";
            projectDict[unassignedKey] = new WeeklyTimesheetRow
            {
                ProjectName = unassignedKey,
                ColorHex = "#94A3B8"
            };

            foreach (var act in activities)
            {
                int dayIndex = (int)act.StartTime.DayOfWeek;
                // Convert DayOfWeek: Monday = 0, ..., Sunday = 6
                int mondayIndex = dayIndex == 0 ? 6 : dayIndex - 1;

                double hours = act.DurationSeconds / 3600.0;

                if (act.State == "Active")
                {
                    data.DailyActiveHours[mondayIndex] += hours;

                    string projName = !string.IsNullOrWhiteSpace(act.ProjectName) ? act.ProjectName : unassignedKey;
                    if (!projectDict.TryGetValue(projName, out var row))
                    {
                        row = new WeeklyTimesheetRow { ProjectName = projName, ColorHex = "#38BDF8" };
                        projectDict[projName] = row;
                    }
                    row.DayHours[mondayIndex] += hours;
                }
                else if (act.State == "Meeting")
                {
                    data.DailyMeetingHours[mondayIndex] += hours;

                    string projName = !string.IsNullOrWhiteSpace(act.ProjectName) ? act.ProjectName : "Meetings & Calls";
                    if (!projectDict.TryGetValue(projName, out var row))
                    {
                        row = new WeeklyTimesheetRow { ProjectName = projName, ColorHex = "#F59E0B" };
                        projectDict[projName] = row;
                    }
                    row.DayHours[mondayIndex] += hours;
                }
                else if (act.State == "Idle")
                {
                    data.DailyIdleHours[mondayIndex] += hours;
                }
            }

            // Only return rows that have hours or are registered active projects
            data.Rows = projectDict.Values
                .Where(r => r.TotalHours > 0.01 || projects.Any(p => p.Name.Equals(r.ProjectName, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(r => r.TotalHours)
                .ToList();

            return data;
        }

        public string ExportWeeklyTimesheetToCsv(WeeklyTimesheetData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Idle-Work Weekly Timesheet");
            sb.AppendLine($"Week: {data.WeekStartDate:yyyy-MM-dd} to {data.WeekEndDate:yyyy-MM-dd}");
            sb.AppendLine();

            // Headers
            sb.AppendLine("Project,Mon,Tue,Wed,Thu,Fri,Sat,Sun,Total Hours");

            foreach (var row in data.Rows)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "\"{0}\",{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2}",
                    row.ProjectName.Replace("\"", "\"\""),
                    row.DayHours[0], row.DayHours[1], row.DayHours[2],
                    row.DayHours[3], row.DayHours[4], row.DayHours[5], row.DayHours[6],
                    row.TotalHours));
            }

            sb.AppendLine();
            // Daily Active Work Summary
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "\"Daily Active Work\",{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2}",
                data.DailyActiveHours[0], data.DailyActiveHours[1], data.DailyActiveHours[2],
                data.DailyActiveHours[3], data.DailyActiveHours[4], data.DailyActiveHours[5], data.DailyActiveHours[6],
                data.TotalActiveHours));

            // Daily Meetings Summary
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "\"Daily Meetings\",{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2}",
                data.DailyMeetingHours[0], data.DailyMeetingHours[1], data.DailyMeetingHours[2],
                data.DailyMeetingHours[3], data.DailyMeetingHours[4], data.DailyMeetingHours[5], data.DailyMeetingHours[6],
                data.TotalMeetingHours));

            // Daily Idle / Break Summary
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "\"Daily Idle / Break\",{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2}",
                data.DailyIdleHours[0], data.DailyIdleHours[1], data.DailyIdleHours[2],
                data.DailyIdleHours[3], data.DailyIdleHours[4], data.DailyIdleHours[5], data.DailyIdleHours[6],
                data.TotalIdleHours));

            return sb.ToString();
        }

        // [v0.004: MarkdownExport] Clean Markdown table export for Jira, Slack, Teams, and daily standups
        public string ExportWeeklyTimesheetToMarkdown(WeeklyTimesheetData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### ⏱️ Idle-Work Timesheet Summary ({data.WeekStartDate:MMM dd} - {data.WeekEndDate:MMM dd, yyyy})");
            sb.AppendLine();
            sb.AppendLine("| Project / Category | Mon | Tue | Wed | Thu | Fri | Sat | Sun | Total |");
            sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

            foreach (var row in data.Rows)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| **{0}** | {1} | {2} | {3} | {4} | {5} | {6} | {7} | **{8}** |",
                    row.ProjectName,
                    row.GetFormattedDay(0), row.GetFormattedDay(1), row.GetFormattedDay(2),
                    row.GetFormattedDay(3), row.GetFormattedDay(4), row.GetFormattedDay(5), row.GetFormattedDay(6),
                    row.FormattedTotal));
            }

            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "| **Total Active Work** | {0:F1}h | {1:F1}h | {2:F1}h | {3:F1}h | {4:F1}h | {5:F1}h | {6:F1}h | **{7:F1}h** |",
                data.DailyActiveHours[0], data.DailyActiveHours[1], data.DailyActiveHours[2],
                data.DailyActiveHours[3], data.DailyActiveHours[4], data.DailyActiveHours[5], data.DailyActiveHours[6],
                data.TotalActiveHours));

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "| **Total Meetings** | {0:F1}h | {1:F1}h | {2:F1}h | {3:F1}h | {4:F1}h | {5:F1}h | {6:F1}h | **{7:F1}h** |",
                data.DailyMeetingHours[0], data.DailyMeetingHours[1], data.DailyMeetingHours[2],
                data.DailyMeetingHours[3], data.DailyMeetingHours[4], data.DailyMeetingHours[5], data.DailyMeetingHours[6],
                data.TotalMeetingHours));

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "| **Grand Total (Work+Meetings)** | {0:F1}h | {1:F1}h | {2:F1}h | {3:F1}h | {4:F1}h | {5:F1}h | {6:F1}h | **{7:F1}h** |",
                data.DailyActiveHours[0] + data.DailyMeetingHours[0],
                data.DailyActiveHours[1] + data.DailyMeetingHours[1],
                data.DailyActiveHours[2] + data.DailyMeetingHours[2],
                data.DailyActiveHours[3] + data.DailyMeetingHours[3],
                data.DailyActiveHours[4] + data.DailyMeetingHours[4],
                data.DailyActiveHours[5] + data.DailyMeetingHours[5],
                data.DailyActiveHours[6] + data.DailyMeetingHours[6],
                data.GrandTotalWorkHours));

            return sb.ToString();
        }
    }
}
