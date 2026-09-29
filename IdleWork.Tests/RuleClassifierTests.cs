// [v0.1: Test] Unit tests for RuleClassifierService and smart rule learning
using System;
using System.IO;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;
using Xunit;

namespace IdleWork.Tests
{
    public class RuleClassifierTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DatabaseService _dbService;
        private readonly RuleClassifierService _classifier;

        public RuleClassifierTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_rules_{Guid.NewGuid():N}.db");
            _dbService = new DatabaseService(_tempDbPath);
            _classifier = new RuleClassifierService(_dbService);
        }

        [Fact]
        public async Task ClassifyActivity_MatchesProcessName_AssignsProjectAndTags()
        {
            await _classifier.RefreshRulesAsync();

            var activity = new ActivityTimeSpan
            {
                ProcessName = "Revit",
                WindowTitle = "Autodesk Revit 2025 - [Hospital_Tower.rvt]",
                State = "Active"
            };

            _classifier.ClassifyActivity(activity);

            Assert.Equal("BIM & CAD Modeling", activity.ProjectName);
            Assert.Equal("BIM", activity.Category);
            Assert.Contains("Revit", activity.Tags);
        }

        [Fact]
        public async Task ClassifyActivity_RespectsRulePriority()
        {
            // High priority rule (priority 5) vs lower priority rule (priority 10)
            var highPriorityRule = new AutoTagRule
            {
                RuleName = "Specific Hospital Project",
                ProcessFilter = "Revit",
                TitlePattern = "Hospital",
                TargetProject = "Project Alpha - Hospital",
                Priority = 5,
                IsEnabled = true
            };

            await _dbService.SaveRuleAsync(highPriorityRule);
            await _classifier.RefreshRulesAsync();

            var activity = new ActivityTimeSpan
            {
                ProcessName = "Revit",
                WindowTitle = "Autodesk Revit 2025 - [Hospital_Tower.rvt]",
                State = "Active"
            };

            _classifier.ClassifyActivity(activity);

            Assert.Equal("Project Alpha - Hospital", activity.ProjectName);
        }

        [Fact]
        public async Task AssignAndRememberRuleAsync_PersistsRuleAndAppliesRetroactively()
        {
            // Create an unassigned activity from 2 days ago
            var oldActivity = new ActivityTimeSpan
            {
                StartTime = DateTime.Today.AddDays(-2),
                EndTime = DateTime.Today.AddDays(-2).AddMinutes(30),
                DurationSeconds = 1800,
                ProcessName = "SpecialCadApp",
                WindowTitle = "Drawing_Bridge402.dwg",
                DocumentName = "Bridge402",
                State = "Active"
            };
            await _dbService.SaveActivityAsync(oldActivity);

            // Execute 1-Click "Assign & Remember"
            int updatedCount = await _classifier.AssignAndRememberRuleAsync(
                oldActivity,
                projectName: "Bridge Replacement",
                category: "Infrastructure",
                tags: "CAD, Bridge",
                matchProcess: true,
                matchDocumentTitle: false,
                applyRetroactively: true);

            Assert.True(updatedCount >= 1);

            // Verify the old activity in database was updated
            var reloaded = await _dbService.GetActivitiesForDateRangeAsync(DateTime.Today.AddDays(-3), DateTime.Today);
            var updated = reloaded.Find(a => a.ProcessName == "SpecialCadApp");

            Assert.NotNull(updated);
            Assert.Equal("Bridge Replacement", updated.ProjectName);
            Assert.Equal("Infrastructure", updated.Category);
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
