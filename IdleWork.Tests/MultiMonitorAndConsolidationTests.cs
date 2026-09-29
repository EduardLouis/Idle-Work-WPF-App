// [v0.2: Test] Unit tests for Multi-Monitor Software Priority Hierarchy and Session Consolidation
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;
using IdleWork.App.Core.Helpers;
using IdleWork.App.ViewModels;
using Xunit;

namespace IdleWork.Tests
{
    public class MultiMonitorAndConsolidationTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DatabaseService _dbService;

        public MultiMonitorAndConsolidationTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_multimon_{Guid.NewGuid():N}.db");
            _dbService = new DatabaseService(_tempDbPath);
        }

        [Fact]
        public async Task ResolveHierarchy_RevitOnMon0_AcadOnMon1_KeepsRevitAsMainAndAcadAsSub()
        {
            await _dbService.EnsureInitializedAsync();

            var classifier = new RuleClassifierService(_dbService);
            var winTracker = new WindowTrackerService();
            var audio = new AudioLevelService();
            var idle = new IdleDetectionService(audio);
            var aggregator = new ActivityAggregator(_dbService, classifier, winTracker, idle);

            await aggregator.RefreshPrioritiesAsync();

            // Simulate multi-monitor topology:
            // Monitor 0 has Revit visible
            // Monitor 1 has AutoCAD visible
            var topWindows = new List<MonitorWindowSnapshot>
            {
                new MonitorWindowSnapshot
                {
                    MonitorIndex = 0,
                    DeviceName = "Display 1",
                    ProcessName = "Revit",
                    WindowTitle = "Autodesk Revit 2025 - [Hospital_BIM.rvt]",
                    DocumentName = "Hospital_BIM.rvt",
                    IsFocused = false
                },
                new MonitorWindowSnapshot
                {
                    MonitorIndex = 1,
                    DeviceName = "Display 2",
                    ProcessName = "acad",
                    WindowTitle = "AutoCAD 2025 - [Detail_Sheet.dwg]",
                    DocumentName = "Detail_Sheet.dwg",
                    IsFocused = true
                }
            };

            // User switches focus to AutoCAD on Monitor 1
            var (mainProc, mainTitle, mainDoc, mainMon, subProc, subTitle, subDoc) =
                aggregator.ResolveHierarchy("acad", "AutoCAD 2025 - [Detail_Sheet.dwg]", "Detail_Sheet.dwg", 1, topWindows);

            // Revit has priority 0 while AutoCAD has priority 1
            // Assert that Main Activity stays Revit!
            Assert.Equal("Revit", mainProc);
            Assert.Equal("Hospital_BIM.rvt", mainDoc);
            Assert.Equal(0, mainMon);

            // Assert that AutoCAD is captured as Sub-Activity!
            Assert.Equal("acad", subProc);
            Assert.Equal("Detail_Sheet.dwg", subDoc);
        }

        [Fact]
        public async Task ResolveHierarchy_WhenRevitMinimized_PromotesAutoCADToMain()
        {
            await _dbService.EnsureInitializedAsync();

            var classifier = new RuleClassifierService(_dbService);
            var winTracker = new WindowTrackerService();
            var audio = new AudioLevelService();
            var idle = new IdleDetectionService(audio);
            var aggregator = new ActivityAggregator(_dbService, classifier, winTracker, idle);

            await aggregator.RefreshPrioritiesAsync();

            // Revit is minimized or closed; only AutoCAD and Adobe Reader (PDF) are visible
            var topWindows = new List<MonitorWindowSnapshot>
            {
                new MonitorWindowSnapshot
                {
                    MonitorIndex = 0,
                    DeviceName = "Display 1",
                    ProcessName = "AcroRd32",
                    WindowTitle = "Adobe Acrobat - [Specification.pdf]",
                    DocumentName = "Specification.pdf",
                    IsFocused = false
                },
                new MonitorWindowSnapshot
                {
                    MonitorIndex = 1,
                    DeviceName = "Display 2",
                    ProcessName = "acad",
                    WindowTitle = "AutoCAD 2025 - [Detail_Sheet.dwg]",
                    DocumentName = "Detail_Sheet.dwg",
                    IsFocused = true
                }
            };

            // Focused on AutoCAD
            var (mainProc, mainTitle, _, mainMon, subProc, _, _) =
                aggregator.ResolveHierarchy("acad", "AutoCAD 2025 - [Detail_Sheet.dwg]", "Detail_Sheet.dwg", 1, topWindows);

            // AutoCAD is now the highest priority visible app!
            Assert.Equal("acad", mainProc);
            Assert.Null(subProc); // since user is focused on AutoCAD itself
        }

        [Fact]
        public async Task SoftwarePriorities_Reordering_PersistsCorrectOrder()
        {
            await _dbService.EnsureInitializedAsync();

            var list = await _dbService.GetSoftwarePrioritiesAsync();
            Assert.True(list.Count >= 2);

            // Swap first two priorities
            var first = list[0];
            var second = list[1];

            list[0] = second;
            list[1] = first;

            await _dbService.SaveSoftwarePrioritiesOrderAsync(list);

            var reloaded = await _dbService.GetSoftwarePrioritiesAsync();
            Assert.Equal(second.ProcessFilter, reloaded[0].ProcessFilter);
            Assert.Equal(0, reloaded[0].Priority);
            Assert.Equal(first.ProcessFilter, reloaded[1].ProcessFilter);
            Assert.Equal(1, reloaded[1].Priority);
        }

        [Fact]
        public async Task SessionConsolidation_AppendsIntervalToExistingActivity()
        {
            await _dbService.EnsureInitializedAsync();

            var today = DateTime.Today.AddHours(9);

            var originalActivity = new ActivityTimeSpan
            {
                StartTime = today,
                EndTime = today.AddMinutes(45),
                DurationSeconds = 2700,
                ProcessName = "Revit",
                WindowTitle = "Autodesk Revit - [Hospital.rvt]",
                DocumentName = "Hospital.rvt",
                ProjectName = "BIM & CAD Modeling",
                State = "Active"
            };

            await _dbService.SaveActivityAsync(originalActivity);

            // Save initial interval
            var int1 = new ActivityInterval
            {
                ActivityId = originalActivity.Id,
                StartTime = originalActivity.StartTime,
                EndTime = originalActivity.EndTime,
                DurationSeconds = 2700
            };
            await _dbService.SaveIntervalAsync(int1);

            // Later on the same day (14:00 PM), user works on the same activity for 30 min
            var existing = await _dbService.FindTodayActivityAsync("Revit", "Autodesk Revit - [Hospital.rvt]", today);
            Assert.NotNull(existing);

            // Update existing with additional duration
            existing.EndTime = today.AddHours(5).AddMinutes(30);
            existing.DurationSeconds += 1800;
            await _dbService.SaveActivityAsync(existing);

            // Add second interval
            var int2 = new ActivityInterval
            {
                ActivityId = existing.Id,
                StartTime = today.AddHours(5),
                EndTime = today.AddHours(5).AddMinutes(30),
                DurationSeconds = 1800,
                SubProcessName = "AcroRd32",
                SubWindowTitle = "Review.pdf"
            };
            await _dbService.SaveIntervalAsync(int2);

            // Verify consolidated record
            var intervals = await _dbService.GetIntervalsForActivityAsync(existing.Id);
            Assert.Equal(2, intervals.Count);
            Assert.Equal(4500, existing.DurationSeconds); // 2700 + 1800 = 4500s (1h 15m)
            Assert.Equal("AcroRd32", intervals[1].SubProcessName);
        }

        [Fact]
        public async Task TimelineViewModel_DualAssignment_FiltersRulesByActivityApplication()
        {
            // [v0.2: DualAssign] Verify applicable rules are filtered by activity app and projects show on toggle
            await _dbService.EnsureInitializedAsync();

            var classifier = new RuleClassifierService(_dbService);
            var vm = new TimelineViewModel(_dbService, classifier);

            // Wait for VM cache to load
            await vm.LoadRulesCacheAsync();

            // Set selected activity in Revit
            var revitAct = new ActivityTimeSpan
            {
                ProcessName = "Revit",
                WindowTitle = "Autodesk Revit 2024 - [Hospital.rvt]",
                DocumentName = "Hospital.rvt"
            };
            vm.SelectedActivity = revitAct;

            // 1. By default or when IsAssignByRule is true, only Revit rules appear plus Create New Rule action row
            vm.IsAssignByRule = true;
            Assert.NotEmpty(vm.AssignmentChoices);
            Assert.Contains(vm.AssignmentChoices, c => c.IsCreateAction && c.Title.Contains("Create New Rule"));
            Assert.All(vm.AssignmentChoices.Where(c => !c.IsCreateAction), choice =>
            {
                Assert.True(choice.IsRule);
                // Must be either empty filter or contain Revit
                string filter = choice.Rule?.ProcessFilter ?? "";
                Assert.True(string.IsNullOrEmpty(filter) || filter.Contains("Revit", StringComparison.OrdinalIgnoreCase));
            });
            // Ensure AutoCAD rule is NOT in the choices
            Assert.DoesNotContain(vm.AssignmentChoices, c => c.Title.Contains("AutoCAD", StringComparison.OrdinalIgnoreCase));

            // 2. When switching to Direct Project, available projects appear plus Create New Project action row
            vm.IsAssignByProject = true;
            Assert.NotEmpty(vm.AssignmentChoices);
            Assert.Contains(vm.AssignmentChoices, c => c.IsCreateAction && c.Title.Contains("Create New Project"));
            Assert.All(vm.AssignmentChoices.Where(c => !c.IsCreateAction), choice =>
            {
                Assert.False(choice.IsRule);
                Assert.NotNull(choice.Project);
            });
        }

        [Fact]
        public async Task ActivityAggregator_DetectAndRecordAppClosedGap_CreatesOfflineActivity()
        {
            // [v0.2: OfflineTracking] Verify app-closed gap creates Offline State activity
            await _dbService.EnsureInitializedAsync();

            var now = DateTime.Now;
            var pastEnd = now.AddHours(-2); // 2 hours ago

            // Seed an activity that ended 2 hours ago
            var previousActivity = new ActivityTimeSpan
            {
                StartTime = pastEnd.AddHours(-1),
                EndTime = pastEnd,
                DurationSeconds = 3600,
                ProcessName = "acad",
                WindowTitle = "AutoCAD 2025 - [Layout.dwg]",
                DocumentName = "Layout.dwg",
                State = "Active"
            };
            await _dbService.SaveActivityAsync(previousActivity);

            var classifier = new RuleClassifierService(_dbService);
            var winTracker = new WindowTrackerService();
            var audio = new AudioLevelService();
            var idle = new IdleDetectionService(audio);
            var aggregator = new ActivityAggregator(_dbService, classifier, winTracker, idle);

            // Execute gap detection
            await aggregator.DetectAndRecordAppClosedGapAsync();

            // Fetch activities for today
            var activities = await _dbService.GetRecentActivitiesAsync(10);
            var offlineAct = activities.Find(a => a.State == "Offline");

            Assert.NotNull(offlineAct);
            Assert.Equal("App Inactive", offlineAct.ProcessName);
            Assert.Equal("App Closed / Untracked", offlineAct.WindowTitle);
            Assert.Equal("Untracked", offlineAct.Category);
            // Gap should be roughly 7200 seconds (2 hours)
            Assert.True(offlineAct.DurationSeconds >= 7100 && offlineAct.DurationSeconds <= 7300);
        }

        [Fact]
        public void SettingsViewModel_IdleTimeout_SnappingAndDisplayText_WorksCorrectly()
        {
            // [v0.2: Preferences] Verify idle timeout snaps to 30s, 60s, then strictly 1-min intervals, displaying minutes above 60s
            var audio = new AudioLevelService();
            var idle = new IdleDetectionService(audio);
            var classifier = new RuleClassifierService(_dbService);
            var winTracker = new WindowTrackerService();
            var aggregator = new ActivityAggregator(_dbService, classifier, winTracker, idle);
            var vm = new SettingsViewModel(_dbService, aggregator, idle, audio);

            // 1. 30 seconds
            vm.IdleTimeoutSeconds = 30;
            Assert.Equal(30, vm.IdleTimeoutSeconds);
            Assert.Equal("30 seconds", vm.IdleTimeoutDisplayText);

            // 2. 60 seconds (1 min)
            vm.IdleTimeoutSeconds = 60;
            Assert.Equal(60, vm.IdleTimeoutSeconds);
            Assert.Equal("1 minute", vm.IdleTimeoutDisplayText);

            // 3. Above 60 seconds - snaps to 1-minute multiples (e.g. 150 -> 120 or 180, never 150)
            vm.IdleTimeoutSeconds = 175; // Near 180 (3 min)
            Assert.Equal(180, vm.IdleTimeoutSeconds);
            Assert.Equal("3 minutes", vm.IdleTimeoutDisplayText);

            vm.IdleTimeoutSeconds = 600; // 10 min
            Assert.Equal(600, vm.IdleTimeoutSeconds);
            Assert.Equal("10 minutes", vm.IdleTimeoutDisplayText);

            vm.IdleTimeoutSeconds = 900; // 15 min
            Assert.Equal(900, vm.IdleTimeoutSeconds);
            Assert.Equal("15 minutes", vm.IdleTimeoutDisplayText);
        }

        [Fact]
        public void SettingsViewModel_ResetCommands_RestoreDefaults()
        {
            // [v0.2: Preferences] Verify reset commands restore defaults for all sliders
            var audio = new AudioLevelService();
            var idle = new IdleDetectionService(audio);
            var classifier = new RuleClassifierService(_dbService);
            var winTracker = new WindowTrackerService();
            var aggregator = new ActivityAggregator(_dbService, classifier, winTracker, idle);
            var vm = new SettingsViewModel(_dbService, aggregator, idle, audio);

            // Modify values
            vm.IdleTimeoutSeconds = 30;
            vm.DwellDebounceSeconds = 4.5;
            vm.MicSensitivity = 0.20f;

            // Execute reset commands
            vm.ResetIdleTimeoutCommand.Execute(null);
            Assert.Equal(SettingsViewModel.DefaultIdleTimeoutSeconds, vm.IdleTimeoutSeconds);
            Assert.Equal("3 minutes", vm.IdleTimeoutDisplayText);

            vm.ResetDwellDebounceCommand.Execute(null);
            Assert.Equal(SettingsViewModel.DefaultDwellDebounceSeconds, vm.DwellDebounceSeconds);

            vm.ResetMicSensitivityCommand.Execute(null);
            Assert.Equal(SettingsViewModel.DefaultMicSensitivity, vm.MicSensitivity);
        }

        [Fact]
        public async Task TimelineViewModel_QuickCreate_TriggersEvent_AndAppendsNewProject()
        {
            // [v0.2: QuickCreate] Verify QuickCreate triggers creation event and updates choices
            await _dbService.EnsureInitializedAsync();

            var classifier = new RuleClassifierService(_dbService);
            var vm = new TimelineViewModel(_dbService, classifier);
            await vm.LoadRulesCacheAsync();

            bool eventFired = false;
            bool eventIsRule = false;
            vm.RequestCreateNew += (s, isRule) =>
            {
                eventFired = true;
                eventIsRule = isRule;
            };

            // Switch to Project mode and select Create New Project action row
            vm.IsAssignByProject = true;
            var createProjChoice = vm.AssignmentChoices.FirstOrDefault(c => c.IsCreateAction);
            Assert.NotNull(createProjChoice);
            Assert.False(createProjChoice.IsRule);

            // Selecting the create action fires RequestCreateNew
            vm.SelectedChoice = createProjChoice;
            Assert.True(eventFired);
            Assert.False(eventIsRule);

            // Simulate saving a new project
            var newProject = new Project { Name = "Super Tower 2026", Code = "ST-26", ColorHex = "#10B981" };
            await vm.OnNewProjectCreatedAsync(newProject);

            // Verify the new project is now in AvailableProjects and in AssignmentChoices
            var addedChoice = vm.AssignmentChoices.FirstOrDefault(c => c.TargetProject == "Super Tower 2026");
            Assert.NotNull(addedChoice);
            Assert.False(addedChoice.IsCreateAction);
        }

        [Fact]
        public async Task TimelineViewModel_SelectingRowInGrid_DoesNotFireRequestCreateNew_EvenWithNoRules()
        {
            // [v0.2: Timeline] Verify clicking/selecting a row in the grid never opens the create dialog
            await _dbService.EnsureInitializedAsync();

            var classifier = new RuleClassifierService(_dbService);
            var vm = new TimelineViewModel(_dbService, classifier);
            await vm.LoadRulesCacheAsync();

            bool eventFired = false;
            vm.RequestCreateNew += (s, isRule) =>
            {
                eventFired = true;
            };

            // Set SelectedActivity to an application that has zero rules defined
            vm.IsAssignByRule = true;
            vm.SelectedActivity = new ActivityTimeSpan
            {
                ProcessName = "CompletelyUnknownAppWithNoRules",
                WindowTitle = "Unclassified Document",
                DurationSeconds = 120
            };

            // Event MUST NOT fire simply because user clicked the row in the grid
            Assert.False(eventFired);
            Assert.Null(vm.SelectedChoice);
            Assert.True(vm.HasNoApplicableRules);

            // But clicking CreateNewCommand (or selecting create action) DOES fire it
            vm.CreateNewCommand.Execute(null);
            Assert.True(eventFired);
        }

        [Fact]
        public async Task ProjectsViewModel_SelectProject_SetsUpdateMode_AndUpdatesDescriptionAndRules()
        {
            // [v0.2: ProjectEdit] Verify selecting project sets Update Project text, updates description in list, and correlates rules
            await _dbService.EnsureInitializedAsync();

            var project = new Project
            {
                Name = "Airport Terminal",
                Code = "APT",
                ColorHex = "#3B82F6",
                Description = "Initial description",
                IsActive = true
            };
            await _dbService.SaveProjectAsync(project);

            var rule = new AutoTagRule
            {
                RuleName = "Airport Revit Rule",
                ProcessFilter = "Revit",
                TargetProject = "Other Project",
                IsEnabled = true
            };
            await _dbService.SaveRuleAsync(rule);

            var vm = new ProjectsViewModel(_dbService);
            await vm.LoadProjectsAsync();
            await vm.LoadRulesAsync();

            // Initial state: no project selected -> "+ Create Project"
            Assert.Equal("+ Create Project", vm.SaveButtonText);

            // 1. Select the project in list
            var found = vm.Projects.FirstOrDefault(p => p.Name == "Airport Terminal");
            Assert.NotNull(found);
            vm.SelectedProject = found;

            // Save button dynamically updates to Update Project
            Assert.Equal("💾 Update Project", vm.SaveButtonText);
            Assert.Equal("Initial description", vm.ProjectDescription);

            // 2. Modify description and correlate rule
            vm.ProjectDescription = "Updated expansion description 2026";
            var ruleItem = vm.AvailableRules.FirstOrDefault(r => r.RuleName == "Airport Revit Rule");
            Assert.NotNull(ruleItem);
            ruleItem.IsSelected = true;

            // Save
            await vm.SaveProjectAsync();

            // Verify project object in Projects collection has updated description
            Assert.Equal("Updated expansion description 2026", found.Description);

            // Verify rule in DB has TargetProject updated to Airport Terminal
            var dbRules = await _dbService.GetRulesAsync();
            var updatedDbRule = dbRules.FirstOrDefault(r => r.RuleName == "Airport Revit Rule");
            Assert.NotNull(updatedDbRule);
            Assert.Equal("Airport Terminal", updatedDbRule.TargetProject);
        }

        [Fact]
        public async Task RulesManagerViewModel_MultiSelect_DeleteAndToggleAndDuplicate_WorksOnAllSelected()
        {
            // [v0.2: SmartRulesMultiSelect] Verify delete, toggle, and duplicate on multiple selected rows
            await _dbService.EnsureInitializedAsync();
            var classifier = new RuleClassifierService(_dbService);
            var vm = new RulesManagerViewModel(_dbService, classifier);

            var rule1 = new AutoTagRule { RuleName = "Rule A", ProcessFilter = "Revit", TargetProject = "P1", IsEnabled = true };
            var rule2 = new AutoTagRule { RuleName = "Rule B", ProcessFilter = "acad", TargetProject = "P2", IsEnabled = true };
            var rule3 = new AutoTagRule { RuleName = "Rule C", ProcessFilter = "devenv", TargetProject = "P3", IsEnabled = false };

            await _dbService.SaveRuleAsync(rule1);
            await _dbService.SaveRuleAsync(rule2);
            await _dbService.SaveRuleAsync(rule3);

            vm.LoadRulesAsync();
            // Wait for collection to populate
            await Task.Delay(100);
            int initialCount = vm.Rules.Count;
            Assert.True(initialCount >= 3);

            var ruleA = vm.Rules.First(r => r.RuleName == "Rule A");
            var ruleB = vm.Rules.First(r => r.RuleName == "Rule B");
            var selectedList = new System.Collections.ArrayList { ruleA, ruleB };

            // 1. Test Toggle on multiple rules: since both are true, toggling should disable them
            await vm.ToggleSelectedRulesEnabledAsync(selectedList);
            Assert.False(ruleA.IsEnabled);
            Assert.False(ruleB.IsEnabled);

            // Toggle again: now both are false, toggling should enable them
            await vm.ToggleSelectedRulesEnabledAsync(selectedList);
            Assert.True(ruleA.IsEnabled);
            Assert.True(ruleB.IsEnabled);

            // 2. Test Duplicate on multiple rules
            await vm.DuplicateSelectedRulesAsync(selectedList);
            Assert.Equal(initialCount + 2, vm.Rules.Count);
            Assert.Contains(vm.Rules, r => r.RuleName == "Rule A (Copy)");
            Assert.Contains(vm.Rules, r => r.RuleName == "Rule B (Copy)");

            // 3. Test Delete on multiple rules (delete the copies)
            var copy1 = vm.Rules.First(r => r.RuleName == "Rule A (Copy)");
            var copy2 = vm.Rules.First(r => r.RuleName == "Rule B (Copy)");
            var toDeleteList = new System.Collections.ArrayList { copy1, copy2 };

            await vm.DeleteSelectedRulesAsync(toDeleteList);
            Assert.Equal(initialCount, vm.Rules.Count);
            Assert.DoesNotContain(vm.Rules, r => r.RuleName == "Rule A (Copy)");
            Assert.DoesNotContain(vm.Rules, r => r.RuleName == "Rule B (Copy)");

            // Verify in DB
            var rulesInDb = await _dbService.GetRulesAsync();
            Assert.Equal(initialCount, rulesInDb.Count);
        }

        [Fact]
        public async Task RulesManagerViewModel_SingleRowSelection_SwitchesToUpdateMode_AndMultiSelectionResets()
        {
            // [v0.2: SmartRulesAutoEdit] Verify selecting 1 row sets Update Rule mode, and multi-selection resets
            await _dbService.EnsureInitializedAsync();
            var classifier = new RuleClassifierService(_dbService);
            var vm = new RulesManagerViewModel(_dbService, classifier);

            var rule = new AutoTagRule
            {
                RuleName = "Test Edit Rule",
                ProcessFilter = "acad",
                TitlePattern = "FloorPlan",
                TargetProject = "Tower B",
                IsEnabled = true
            };
            await _dbService.SaveRuleAsync(rule);
            vm.LoadRulesAsync();
            await Task.Delay(100);

            // Initially: no rule selected -> "+ Add Persistent Rule"
            Assert.Equal("+ Add Persistent Rule", vm.SaveButtonText);

            var found = vm.Rules.First(r => r.RuleName == "Test Edit Rule");

            // 1. Select exactly 1 row via OnRuleSelectionChanged
            var singleList = new System.Collections.ArrayList { found };
            vm.OnRuleSelectionChanged(singleList);

            Assert.Equal("💾 Update Rule", vm.SaveButtonText);
            Assert.Equal("Test Edit Rule", vm.NewRuleName);
            Assert.Equal("acad", vm.NewProcessFilter);
            Assert.Equal("FloorPlan", vm.NewTitlePattern);

            // 2. Select multiple rows
            var multiList = new System.Collections.ArrayList { found, vm.Rules[0] };
            vm.OnRuleSelectionChanged(multiList);

            // Button reverts to Add Persistent Rule in batch mode
            Assert.Equal("+ Add Persistent Rule", vm.SaveButtonText);

            // 3. Clear/Deselect
            vm.CancelEditCommand.Execute(null);
            Assert.Equal("+ Add Persistent Rule", vm.SaveButtonText);
            Assert.Equal("", vm.NewRuleName);
        }

        [Fact]
        public void AppVersionHelper_MajorMinor_BoundedWithin0To999()
        {
            // [v0.2: Versioning] Verify version string formats as Major.Minor and components are within 0-999
            Assert.False(string.IsNullOrWhiteSpace(AppVersionHelper.Version));
            Assert.InRange(AppVersionHelper.Major, 0, 999);
            Assert.InRange(AppVersionHelper.Minor, 0, 999);
            Assert.Equal($"{AppVersionHelper.Major}.{AppVersionHelper.Minor}", AppVersionHelper.Version);
            Assert.Contains($"v{AppVersionHelper.Version}", AppVersionHelper.AppTitle);
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
