// [v0.1: DatabaseService] Local SQLite persistence with non-blocking async initialization
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;
using SQLite;

namespace IdleWork.App.Core.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _db;
        private readonly Task _initTask;
        private static DatabaseService? _instance;
        public static DatabaseService Instance => _instance ??= new DatabaseService();

        public DatabaseService(string? customDbPath = null)
        {
            // [v0.1: SQLite] Ensure native provider is initialized
            SQLitePCL.Batteries_V2.Init();

            string dbPath;
            if (!string.IsNullOrWhiteSpace(customDbPath))
            {
                dbPath = customDbPath;
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(localAppData, "IdleWork");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                dbPath = Path.Combine(folder, "idlework.db");
            }

            _db = new SQLiteAsyncConnection(dbPath);
            // Non-blocking initialization task (never block the UI thread constructor)
            _initTask = InitializeAsync();
        }

        public async Task EnsureInitializedAsync()
        {
            await _initTask.ConfigureAwait(false);
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _db.CreateTableAsync<ActivityTimeSpan>().ConfigureAwait(false);
                await _db.CreateTableAsync<Project>().ConfigureAwait(false);
                await _db.CreateTableAsync<AutoTagRule>().ConfigureAwait(false);
                // [v0.2: Tables] Software priority, interval, category, and tag records
                await _db.CreateTableAsync<SoftwarePriority>().ConfigureAwait(false);
                await _db.CreateTableAsync<ActivityInterval>().ConfigureAwait(false);
                await _db.CreateTableAsync<WorkCategory>().ConfigureAwait(false);
                await _db.CreateTableAsync<WorkTag>().ConfigureAwait(false);

                // Seed default projects if none exist
                var projectCount = await _db.Table<Project>().CountAsync().ConfigureAwait(false);
                if (projectCount == 0)
                {
                    var defaults = new List<Project>
                    {
                        new Project { Name = "General Work", Code = "GEN", ColorHex = "#3B82F6", Description = "Daily development and admin tasks" },
                        new Project { Name = "Client Projects", Code = "CLI", ColorHex = "#10B981", Description = "Direct client billable deliverables" },
                        new Project { Name = "BIM & CAD Modeling", Code = "BIM", ColorHex = "#8B5CF6", Description = "Revit, AutoCAD, Navisworks modeling" },
                        new Project { Name = "Meetings & Calls", Code = "MTG", ColorHex = "#F59E0B", Description = "Teams, Zoom, and phone discussions" },
                        new Project { Name = "Internal / Admin", Code = "ADM", ColorHex = "#06B6D4", Description = "Timesheets, emails, scheduling" }
                    };
                    await _db.InsertAllAsync(defaults).ConfigureAwait(false);
                }

                // Seed default smart rules if none exist
                var ruleCount = await _db.Table<AutoTagRule>().CountAsync().ConfigureAwait(false);
                if (ruleCount == 0)
                {
                    var defaultRules = new List<AutoTagRule>
                    {
                        new AutoTagRule { RuleName = "Revit Modeling", ProcessFilter = "Revit", TargetProject = "BIM & CAD Modeling", TargetCategory = "BIM", TargetTags = "Revit, 3D", Priority = 10 },
                        new AutoTagRule { RuleName = "AutoCAD Drafting", ProcessFilter = "acad", TargetProject = "BIM & CAD Modeling", TargetCategory = "CAD", TargetTags = "AutoCAD, 2D", Priority = 20 },
                        new AutoTagRule { RuleName = "Teams Calls", ProcessFilter = "Teams", TargetProject = "Meetings & Calls", TargetCategory = "Communication", TargetTags = "Teams, Meeting", Priority = 30 },
                        new AutoTagRule { RuleName = "Outlook Email", ProcessFilter = "OUTLOOK", TargetProject = "Internal / Admin", TargetCategory = "Admin", TargetTags = "Email", Priority = 40 },
                        new AutoTagRule { RuleName = "Development Work", ProcessFilter = "devenv", TargetProject = "General Work", TargetCategory = "Coding", TargetTags = "Visual Studio, C#", Priority = 50 }
                    };
                    await _db.InsertAllAsync(defaultRules).ConfigureAwait(false);
                }

                // [v0.2: Seed] Seed default software priorities if none exist
                var priorityCount = await _db.Table<SoftwarePriority>().CountAsync().ConfigureAwait(false);
                if (priorityCount == 0)
                {
                    var defaultPriorities = new List<SoftwarePriority>
                    {
                        new SoftwarePriority { ProcessFilter = "Revit", DisplayName = "Autodesk Revit", Priority = 0, IsEnabled = true },
                        new SoftwarePriority { ProcessFilter = "acad", DisplayName = "AutoCAD", Priority = 1, IsEnabled = true },
                        new SoftwarePriority { ProcessFilter = "devenv", DisplayName = "Visual Studio", Priority = 2, IsEnabled = true },
                        new SoftwarePriority { ProcessFilter = "WINWORD", DisplayName = "Microsoft Word", Priority = 3, IsEnabled = true },
                        new SoftwarePriority { ProcessFilter = "EXCEL", DisplayName = "Microsoft Excel", Priority = 4, IsEnabled = true }
                    };
                    await _db.InsertAllAsync(defaultPriorities).ConfigureAwait(false);
                }

                // [v0.2: Seed] Seed default categories if none exist
                var categoryCount = await _db.Table<WorkCategory>().CountAsync().ConfigureAwait(false);
                if (categoryCount == 0)
                {
                    var defaultCategories = new List<WorkCategory>
                    {
                        new WorkCategory { Name = "BIM", ColorHex = "#8B5CF6", Description = "Building Information Modeling & CAD" },
                        new WorkCategory { Name = "Development", ColorHex = "#3B82F6", Description = "Software engineering, scripting & coding" },
                        new WorkCategory { Name = "Design", ColorHex = "#EC4899", Description = "Architectural & graphic design" },
                        new WorkCategory { Name = "Admin", ColorHex = "#06B6D4", Description = "Emails, billing, timesheets & paperwork" },
                        new WorkCategory { Name = "Meeting", ColorHex = "#F59E0B", Description = "Client calls, syncs & discussions" },
                        new WorkCategory { Name = "Research", ColorHex = "#10B981", Description = "Specifications, codes, documentation & learning" },
                        new WorkCategory { Name = "QA/QC", ColorHex = "#EF4444", Description = "Quality assurance, review & auditing" }
                    };
                    await _db.InsertAllAsync(defaultCategories).ConfigureAwait(false);
                }

                // [v0.2: Seed] Seed default tags if none exist
                var tagCount = await _db.Table<WorkTag>().CountAsync().ConfigureAwait(false);
                if (tagCount == 0)
                {
                    var defaultTags = new List<WorkTag>
                    {
                        new WorkTag { Name = "Revit", Description = "Autodesk Revit modeling & drafting" },
                        new WorkTag { Name = "AutoCAD", Description = "AutoCAD 2D/3D drafting" },
                        new WorkTag { Name = "3D", Description = "3D modeling and visualization" },
                        new WorkTag { Name = "2D", Description = "2D plan drafting & details" },
                        new WorkTag { Name = "Teams", Description = "Microsoft Teams meetings and chats" },
                        new WorkTag { Name = "Email", Description = "Outlook correspondence" },
                        new WorkTag { Name = "Code", Description = "Programming and development" },
                        new WorkTag { Name = "Billable", Description = "Client billable time" },
                        new WorkTag { Name = "Internal", Description = "Internal company operations" },
                        new WorkTag { Name = "Documentation", Description = "Project documentation & specs" }
                    };
                    await _db.InsertAllAsync(defaultTags).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Init error: {ex.Message}");
            }
        }

        // Activities
        public async Task<int> SaveActivityAsync(ActivityTimeSpan activity)
        {
            await _initTask.ConfigureAwait(false);
            if (activity.Id == 0)
                return await _db.InsertAsync(activity).ConfigureAwait(false);
            else
                return await _db.UpdateAsync(activity).ConfigureAwait(false);
        }

        public async Task<List<ActivityTimeSpan>> GetActivitiesForDateRangeAsync(DateTime start, DateTime end)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<ActivityTimeSpan>()
                .Where(a => a.StartTime >= start && a.StartTime <= end)
                .OrderBy(a => a.StartTime)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<ActivityTimeSpan>> GetRecentActivitiesAsync(int count = 50)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<ActivityTimeSpan>()
                .OrderByDescending(a => a.StartTime)
                .Take(count)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        // [v0.2: OfflineTracking] Retrieve the most recent recorded activity to detect app-closed gaps
        public async Task<ActivityTimeSpan?> GetMostRecentActivityAsync()
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<ActivityTimeSpan>()
                .OrderByDescending(a => a.EndTime)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> DeleteActivityAsync(int id)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.DeleteAsync<ActivityTimeSpan>(id).ConfigureAwait(false);
        }

        // [v0.2: SessionConsolidation] Find existing activity for the current day to append intervals
        public async Task<ActivityTimeSpan?> FindTodayActivityAsync(string processName, string windowTitle, DateTime date)
        {
            await _initTask.ConfigureAwait(false);
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            return await _db.Table<ActivityTimeSpan>()
                .Where(a => a.StartTime >= startOfDay && a.StartTime < endOfDay && a.ProcessName == processName && a.WindowTitle == windowTitle)
                .OrderByDescending(a => a.StartTime)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        // Projects
        public async Task<List<Project>> GetProjectsAsync()
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<Project>().Where(p => p.IsActive).ToListAsync().ConfigureAwait(false);
        }

        public async Task<int> SaveProjectAsync(Project project)
        {
            await _initTask.ConfigureAwait(false);
            if (project.Id == 0)
                return await _db.InsertAsync(project).ConfigureAwait(false);
            else
                return await _db.UpdateAsync(project).ConfigureAwait(false);
        }

        public async Task<int> DeleteProjectAsync(int id)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.DeleteAsync<Project>(id).ConfigureAwait(false);
        }

        // Rules
        public async Task<List<AutoTagRule>> GetRulesAsync()
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<AutoTagRule>().OrderBy(r => r.Priority).ToListAsync().ConfigureAwait(false);
        }

        public async Task<int> SaveRuleAsync(AutoTagRule rule)
        {
            await _initTask.ConfigureAwait(false);
            if (rule.Id == 0)
                return await _db.InsertAsync(rule).ConfigureAwait(false);
            else
                return await _db.UpdateAsync(rule).ConfigureAwait(false);
        }

        public async Task<int> DeleteRuleAsync(int id)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.DeleteAsync<AutoTagRule>(id).ConfigureAwait(false);
        }

        // Retroactive application of rules across a date range
        public async Task<int> ApplyRuleRetroactivelyAsync(AutoTagRule rule, DateTime? startDate = null)
        {
            await _initTask.ConfigureAwait(false);
            var start = startDate ?? DateTime.Today.AddDays(-7);
            var activities = await _db.Table<ActivityTimeSpan>()
                .Where(a => a.StartTime >= start)
                .ToListAsync()
                .ConfigureAwait(false);

            int updatedCount = 0;
            foreach (var act in activities)
            {
                if (MatchesRule(act, rule))
                {
                    act.ProjectName = rule.TargetProject;
                    if (!string.IsNullOrWhiteSpace(rule.TargetCategory))
                        act.Category = rule.TargetCategory;
                    if (!string.IsNullOrWhiteSpace(rule.TargetTags))
                        act.Tags = rule.TargetTags;

                    await _db.UpdateAsync(act).ConfigureAwait(false);
                    updatedCount++;
                }
            }

            return updatedCount;
        }

        private bool MatchesRule(ActivityTimeSpan act, AutoTagRule rule)
        {
            if (!rule.IsEnabled)
                return false;

            bool appMatch = true;
            if (!string.IsNullOrWhiteSpace(rule.ProcessFilter))
            {
                appMatch = act.ProcessName.Contains(rule.ProcessFilter, StringComparison.OrdinalIgnoreCase);
            }

            bool titleMatch = true;
            if (!string.IsNullOrWhiteSpace(rule.TitlePattern))
            {
                if (rule.IsRegex)
                {
                    try
                    {
                        titleMatch = Regex.IsMatch(act.WindowTitle, rule.TitlePattern, RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        titleMatch = false;
                    }
                }
                else
                {
                    titleMatch = act.WindowTitle.Contains(rule.TitlePattern, StringComparison.OrdinalIgnoreCase);
                }
            }

            return appMatch && titleMatch;
        }

        // [v0.2: SoftwarePriorities] Methods for managing application priority ranking
        public async Task<List<SoftwarePriority>> GetSoftwarePrioritiesAsync()
        {
            await _initTask.ConfigureAwait(false);
            var list = await _db.Table<SoftwarePriority>().OrderBy(p => p.Priority).ThenBy(p => p.Id).ToListAsync().ConfigureAwait(false);
            // [v0.2: Normalize] Ensure priorities are strictly sequential (0, 1, 2...) without gaps or duplicates
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Priority != i)
                {
                    list[i].Priority = i;
                    await _db.UpdateAsync(list[i]).ConfigureAwait(false);
                }
            }
            return list;
        }

        public async Task<int> SaveSoftwarePriorityAsync(SoftwarePriority priority)
        {
            await _initTask.ConfigureAwait(false);
            if (priority.Id == 0)
                return await _db.InsertAsync(priority).ConfigureAwait(false);
            else
                return await _db.UpdateAsync(priority).ConfigureAwait(false);
        }

        public async Task<int> DeleteSoftwarePriorityAsync(int id)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.DeleteAsync<SoftwarePriority>(id).ConfigureAwait(false);
        }

        public async Task SaveSoftwarePrioritiesOrderAsync(List<SoftwarePriority> list)
        {
            await _initTask.ConfigureAwait(false);
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Priority = i;
                await _db.UpdateAsync(list[i]).ConfigureAwait(false);
            }
        }

        public async Task SaveRulesOrderAsync(List<AutoTagRule> list)
        {
            await _initTask.ConfigureAwait(false);
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Priority = (i + 1) * 10;
                await _db.UpdateAsync(list[i]).ConfigureAwait(false);
            }
        }

        // [v0.2: Intervals] Discrete session interval persistence
        public async Task<int> SaveIntervalAsync(ActivityInterval interval)
        {
            await _initTask.ConfigureAwait(false);
            if (interval.Id == 0)
                return await _db.InsertAsync(interval).ConfigureAwait(false);
            else
                return await _db.UpdateAsync(interval).ConfigureAwait(false);
        }

        public async Task<List<ActivityInterval>> GetIntervalsForActivityAsync(int activityId)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<ActivityInterval>()
                .Where(i => i.ActivityId == activityId)
                .OrderBy(i => i.StartTime)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<ActivityInterval>> GetIntervalsForDateRangeAsync(DateTime start, DateTime end)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<ActivityInterval>()
                .Where(i => i.StartTime >= start && i.StartTime <= end)
                .OrderBy(i => i.StartTime)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        // [v0.2: Categories] Category CRUD operations
        public async Task<List<WorkCategory>> GetCategoriesAsync()
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<WorkCategory>()
                .OrderBy(c => c.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> SaveCategoryAsync(WorkCategory category)
        {
            await _initTask.ConfigureAwait(false);
            if (category.Id == 0)
                return await _db.InsertAsync(category).ConfigureAwait(false);
            else
                return await _db.UpdateAsync(category).ConfigureAwait(false);
        }

        public async Task<int> DeleteCategoryAsync(int id)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.DeleteAsync<WorkCategory>(id).ConfigureAwait(false);
        }

        // [v0.2: Tags] Tag CRUD operations
        public async Task<List<WorkTag>> GetTagsAsync()
        {
            await _initTask.ConfigureAwait(false);
            return await _db.Table<WorkTag>()
                .OrderBy(t => t.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> SaveTagAsync(WorkTag tag)
        {
            await _initTask.ConfigureAwait(false);
            if (tag.Id == 0)
                return await _db.InsertAsync(tag).ConfigureAwait(false);
            else
                return await _db.UpdateAsync(tag).ConfigureAwait(false);
        }

        public async Task<int> DeleteTagAsync(int id)
        {
            await _initTask.ConfigureAwait(false);
            return await _db.DeleteAsync<WorkTag>(id).ConfigureAwait(false);
        }
    }
}
