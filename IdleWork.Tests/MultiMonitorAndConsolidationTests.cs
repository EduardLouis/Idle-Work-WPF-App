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
            var ruleChoices = vm.GetAssignmentChoicesSnapshot();
            Assert.NotEmpty(ruleChoices);
            Assert.Contains(ruleChoices, c => c.IsCreateAction && c.Title.Contains("Create New Rule"));
            Assert.All(ruleChoices.Where(c => !c.IsCreateAction), choice =>
            {
                Assert.True(choice.IsRule);
                // Must be either empty filter or contain Revit
                string filter = choice.Rule?.ProcessFilter ?? "";
                Assert.True(string.IsNullOrEmpty(filter) || filter.Contains("Revit", StringComparison.OrdinalIgnoreCase));
            });
            // Ensure AutoCAD rule is NOT in the choices
            Assert.DoesNotContain(ruleChoices, c => c.Title.Contains("AutoCAD", StringComparison.OrdinalIgnoreCase));

            // 2. When switching to Direct Project, available projects appear plus Create New Project action row
            vm.IsAssignByProject = true;
            var projChoices = vm.GetAssignmentChoicesSnapshot();
            Assert.NotEmpty(projChoices);
            Assert.Contains(projChoices, c => c.IsCreateAction && c.Title.Contains("Create New Project"));
            Assert.All(projChoices.Where(c => !c.IsCreateAction), choice =>
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
            vm.DwellDebounceSeconds = 90.0;
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
        public async Task ProjectsViewModel_MultiCheckRulesDropdown_SummaryTextAndNewRuleCreated()
        {
            // [v0.003: MultiCheckDropdown] Verify multi-check rules dropdown summary text and NewRuleDialog integration
            await _dbService.EnsureInitializedAsync();

            var vm = new ProjectsViewModel(_dbService);
            await vm.LoadProjectsAsync();
            await vm.LoadRulesAsync();

            // Default state: no rules selected
            vm.ClearAllRulesCommand.Execute(null);
            Assert.Equal("No rules linked (Click to select)", vm.SelectedRulesSummaryText);

            // Select one rule
            if (vm.AvailableRules.Count > 0)
            {
                vm.AvailableRules[0].IsSelected = true;
                Assert.Contains("1 rule linked", vm.SelectedRulesSummaryText);
            }

            // Select all rules
            vm.SelectAllRulesCommand.Execute(null);
            Assert.Contains("rules linked", vm.SelectedRulesSummaryText);
            Assert.True(vm.AvailableRules.All(r => r.IsSelected));

            // Clear all rules
            vm.ClearAllRulesCommand.Execute(null);
            Assert.Equal("No rules linked (Click to select)", vm.SelectedRulesSummaryText);
            Assert.True(vm.AvailableRules.All(r => !r.IsSelected));

            // OnNewRuleCreated from NewRuleDialog
            var created = new AutoTagRule
            {
                RuleName = "Custom Hospital Rule",
                ProcessFilter = "Revit",
                TitlePattern = "Hospital",
                TargetProject = "Medical Center"
            };
            vm.OnNewRuleCreated(created);

            var added = vm.AvailableRules.FirstOrDefault(r => r.RuleName == "Custom Hospital Rule");
            Assert.NotNull(added);
            Assert.True(added.IsSelected);
            Assert.Contains("Custom Hospital Rule", vm.SelectedRulesSummaryText);
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

            await vm.LoadRulesAsync();
            int initialCount = vm.Rules.Count;
            Assert.True(initialCount >= 3);

            var ruleA = vm.Rules.ToList().First(r => r.RuleName == "Rule A");
            var ruleB = vm.Rules.ToList().First(r => r.RuleName == "Rule B");
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
            var copy1 = vm.Rules.ToList().First(r => r.RuleName == "Rule A (Copy)");
            var copy2 = vm.Rules.ToList().First(r => r.RuleName == "Rule B (Copy)");
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
            await vm.LoadRulesAsync();

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
            // [v0.004: Versioning] Verify version string formats as Major.Minor and components are within 0-999
            Assert.False(string.IsNullOrWhiteSpace(AppVersionHelper.Version));
            Assert.InRange(AppVersionHelper.Major, 0, 999);
            Assert.InRange(AppVersionHelper.Minor, 0, 999);
            Assert.Equal("0.004", AppVersionHelper.Version);
            Assert.Contains($"v{AppVersionHelper.Version}", AppVersionHelper.AppTitle);
        }

        [Fact]
        public async Task CategoriesAndTags_DatabaseService_CrudOperations()
        {
            // [v0.2: Categories & Tags] Verify seeding, insertion, retrieval, and deletion
            await _dbService.EnsureInitializedAsync();

            var categories = await _dbService.GetCategoriesAsync();
            Assert.NotEmpty(categories);
            Assert.Contains(categories, c => c.Name == "BIM");
            Assert.Contains(categories, c => c.Name == "Development");

            var tags = await _dbService.GetTagsAsync();
            Assert.NotEmpty(tags);
            Assert.Contains(tags, t => t.Name == "Revit");
            Assert.Contains(tags, t => t.Name == "AutoCAD");

            // Create new category
            var customCat = new WorkCategory { Name = "Structural Engineering", Description = "Calculations and framing", ColorHex = "#10B981" };
            await _dbService.SaveCategoryAsync(customCat);

            var updatedCats = await _dbService.GetCategoriesAsync();
            var savedCat = updatedCats.FirstOrDefault(c => c.Name == "Structural Engineering");
            Assert.NotNull(savedCat);
            Assert.Equal("#10B981", savedCat.ColorHex);

            // Update category
            savedCat.Description = "Updated framing";
            await _dbService.SaveCategoryAsync(savedCat);
            var reloadedCats = await _dbService.GetCategoriesAsync();
            Assert.Equal("Updated framing", reloadedCats.First(c => c.Id == savedCat.Id).Description);

            // Delete category
            await _dbService.DeleteCategoryAsync(savedCat.Id);
            var afterDeleteCats = await _dbService.GetCategoriesAsync();
            Assert.DoesNotContain(afterDeleteCats, c => c.Id == savedCat.Id);

            // Create and delete new tag
            var customTag = new WorkTag { Name = "Navisworks", Description = "Clash detection" };
            await _dbService.SaveTagAsync(customTag);

            var updatedTags = await _dbService.GetTagsAsync();
            var savedTag = updatedTags.FirstOrDefault(t => t.Name == "Navisworks");
            Assert.NotNull(savedTag);

            await _dbService.DeleteTagAsync(savedTag.Id);
            var afterDeleteTags = await _dbService.GetTagsAsync();
            Assert.DoesNotContain(afterDeleteTags, t => t.Id == savedTag.Id);
        }

        [Fact]
        public async Task ProjectsViewModel_CategoriesAndTags_FormAndSelectionOperations()
        {
            // [v0.2: Categories & Tags] Verify ProjectsViewModel category and tag subpages
            await _dbService.EnsureInitializedAsync();
            var vm = new ProjectsViewModel(_dbService);
            await vm.InitializeAsync();

            Assert.NotEmpty(vm.Categories);
            Assert.NotEmpty(vm.Tags);

            // Default state
            Assert.Equal("+ Add Category", vm.SaveCategoryButtonText);
            Assert.Equal("+ Add Tag", vm.SaveTagButtonText);

            // Select a category
            var bimCat = vm.Categories.First(c => c.Name == "BIM");
            vm.SelectedCategory = bimCat;
            Assert.Equal("💾 Update Category", vm.SaveCategoryButtonText);
            Assert.Equal("BIM", vm.CategoryName);

            // Clear category form
            vm.ClearCategoryFormCommand.Execute(null);
            Assert.Equal("+ Add Category", vm.SaveCategoryButtonText);
            Assert.Equal("", vm.CategoryName);

            // Select a tag
            var revitTag = vm.Tags.First(t => t.Name == "Revit");
            vm.SelectedTag = revitTag;
            Assert.Equal("💾 Update Tag", vm.SaveTagButtonText);
            Assert.Equal("Revit", vm.TagName);

            // Clear tag form
            vm.ClearTagFormCommand.Execute(null);
            Assert.Equal("+ Add Tag", vm.SaveTagButtonText);
            Assert.Equal("", vm.TagName);
        }

        [Fact]
        public async Task RulesManagerViewModel_CategoriesAndTags_SubpageOperations()
        {
            // [v0.2: Categories & Tags in RulesManager] Verify subpages in Smart Rules
            await _dbService.EnsureInitializedAsync();
            var classifier = new RuleClassifierService(_dbService);
            var vm = new RulesManagerViewModel(_dbService, classifier);

            // Wait a tick for async loaders
            await Task.Delay(50);

            Assert.NotEmpty(vm.Categories);
            Assert.NotEmpty(vm.Tags);

            // Default state
            Assert.Equal("+ Add Category", vm.SaveCategoryButtonText);
            Assert.Equal("+ Add Tag", vm.SaveTagButtonText);

            // Select a category
            var devCat = vm.Categories.First(c => c.Name == "Development");
            vm.SelectedCategory = devCat;
            Assert.Equal("💾 Update Category", vm.SaveCategoryButtonText);
            Assert.Equal("Development", vm.CategoryName);

            // Clear category form
            vm.ClearCategoryFormCommand.Execute(null);
            Assert.Equal("+ Add Category", vm.SaveCategoryButtonText);
            Assert.Equal("", vm.CategoryName);

            // Select a tag
            var acadTag = vm.Tags.First(t => t.Name == "AutoCAD");
            vm.SelectedTag = acadTag;
            Assert.Equal("💾 Update Tag", vm.SaveTagButtonText);
            Assert.Equal("AutoCAD", vm.TagName);

            // Clear tag form
            vm.ClearTagFormCommand.Execute(null);
            Assert.Equal("+ Add Tag", vm.SaveTagButtonText);
            Assert.Equal("", vm.TagName);
        }

        [Fact]
        public async Task TimelineViewModel_ConsolidatedActivities_SortModes_Percentage_LastActive_OldActivity()
        {
            // [v0.2: TimelineSorting] Verify sorting by Percentage, Last Active, and Old Activity
            await _dbService.EnsureInitializedAsync();

            var testDate = new DateTime(2027, 5, 20);
            // Activity 1: Early morning, 1 hour (oldest start)
            var act1 = new ActivityTimeSpan
            {
                ProcessName = "Revit",
                WindowTitle = "Project A - Level 1",
                StartTime = testDate.AddHours(9),
                EndTime = testDate.AddHours(10),
                DurationSeconds = 3600,
                State = "Active"
            };
            // Activity 2: Mid-day, 4 hours (highest percentage)
            var act2 = new ActivityTimeSpan
            {
                ProcessName = "acad",
                WindowTitle = "Project B - Details",
                StartTime = testDate.AddHours(11),
                EndTime = testDate.AddHours(15),
                DurationSeconds = 14400,
                State = "Active"
            };
            // Activity 3: Late afternoon, 30 min (latest end time)
            var act3 = new ActivityTimeSpan
            {
                ProcessName = "devenv",
                WindowTitle = "Project C - Solution",
                StartTime = testDate.AddHours(16),
                EndTime = testDate.AddHours(16.5),
                DurationSeconds = 1800,
                State = "Active"
            };

            await _dbService.SaveActivityAsync(act1);
            await _dbService.SaveActivityAsync(act2);
            await _dbService.SaveActivityAsync(act3);

            var classifier = new RuleClassifierService(_dbService);
            var vm = new TimelineViewModel(_dbService, classifier);
            vm.SelectedDate = testDate;
            await vm.LoadTimelineAsync();
            Assert.Equal(3, vm.Activities.Count);

            // 1. Sort by Percentage (Index 0): Highest duration/percentage first
            vm.SelectedSortIndex = 0;
            Assert.Equal(ActivitySortMode.Percentage, vm.SelectedSortMode);
            Assert.Equal("acad", vm.Activities[0].ProcessName);    // 14400s
            Assert.Equal("Revit", vm.Activities[1].ProcessName);   // 3600s
            Assert.Equal("devenv", vm.Activities[2].ProcessName);  // 1800s

            // 2. Sort by Last Active (Index 1): Latest EndTime first
            vm.SelectedSortIndex = 1;
            Assert.Equal(ActivitySortMode.LastActive, vm.SelectedSortMode);
            Assert.Equal("devenv", vm.Activities[0].ProcessName);  // End 16:30
            Assert.Equal("acad", vm.Activities[1].ProcessName);    // End 15:00
            Assert.Equal("Revit", vm.Activities[2].ProcessName);   // End 10:00

            // 3. Sort by Old Activity (Index 2): Earliest StartTime first
            vm.SelectedSortIndex = 2;
            Assert.Equal(ActivitySortMode.OldActivity, vm.SelectedSortMode);
            Assert.Equal("Revit", vm.Activities[0].ProcessName);   // Start 09:00
            Assert.Equal("acad", vm.Activities[1].ProcessName);    // Start 11:00
            Assert.Equal("devenv", vm.Activities[2].ProcessName);  // Start 16:00
        }

        [Fact]
        public void AppVersionHelper_MajorMinor_Resolves_0_003()
        {
            // [v0.003: Versioning] Verify AppVersionHelper extracts version components
            Assert.NotNull(AppVersionHelper.Version);
            Assert.Equal(0, AppVersionHelper.Major);
            Assert.True(AppVersionHelper.Minor >= 0 && AppVersionHelper.Minor <= 999);
            Assert.StartsWith("Idle-Work v0.", AppVersionHelper.AppTitle);
        }

        [Fact]
        public void SettingsViewModel_ScreenshotSettings_Snapping_AndResetCommand()
        {
            // [v0.003: Preferences] Verify Screenshot settings snapping, display text, and reset command
            var classifier = new RuleClassifierService(_dbService);
            var windowTracker = new WindowTrackerService();
            var audioService = new AudioLevelService();
            var idleDetector = new IdleDetectionService(audioService);
            var tempFolder = Path.Combine(Path.GetTempPath(), "IdleWork_Test_Screenshots_" + Guid.NewGuid().ToString("N"));
            var screenshotService = new ScreenshotService(tempFolder);
            var aggregator = new ActivityAggregator(_dbService, classifier, windowTracker, idleDetector, null, screenshotService);
            var vm = new SettingsViewModel(_dbService, aggregator, idleDetector, audioService, screenshotService);

            // Defaults
            Assert.True(vm.EnableScreenshots);
            Assert.True(vm.CaptureOnWindowSwitch);
            Assert.Equal(5, vm.ScreenshotIntervalMinutes);
            Assert.Equal("5 minutes", vm.ScreenshotIntervalDisplayText);

            // Snapping tests:
            vm.ScreenshotIntervalMinutes = 0;
            Assert.Equal(0, vm.ScreenshotIntervalMinutes);
            Assert.Equal("Off (No periodic screenshots)", vm.ScreenshotIntervalDisplayText);

            vm.ScreenshotIntervalMinutes = 1;
            Assert.Equal(1, vm.ScreenshotIntervalMinutes);
            Assert.Equal("1 minute", vm.ScreenshotIntervalDisplayText);

            vm.ScreenshotIntervalMinutes = 3;
            Assert.Equal(2, vm.ScreenshotIntervalMinutes);
            Assert.Equal("2 minutes", vm.ScreenshotIntervalDisplayText);

            vm.ScreenshotIntervalMinutes = 6;
            Assert.Equal(5, vm.ScreenshotIntervalMinutes);

            vm.ScreenshotIntervalMinutes = 11;
            Assert.Equal(10, vm.ScreenshotIntervalMinutes);

            vm.ScreenshotIntervalMinutes = 20;
            Assert.Equal(15, vm.ScreenshotIntervalMinutes);

            vm.ScreenshotIntervalMinutes = 28;
            Assert.Equal(30, vm.ScreenshotIntervalMinutes);

            // Reset command restores defaults
            vm.EnableScreenshots = false;
            vm.CaptureOnWindowSwitch = false;
            vm.ResetScreenshotSettingsCommand.Execute(null);

            Assert.True(vm.EnableScreenshots);
            Assert.True(vm.CaptureOnWindowSwitch);
            Assert.Equal(5, vm.ScreenshotIntervalMinutes);

            // Cleanup
            try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
        }

        [Fact]
        public void ActivityTimeSpan_RichMetadata_And_Screenshot_DisplayProperties()
        {
            // [v0.003: RichMetadata] Verify DisplayAppDescription fallback and HasScreenshot evaluation
            var span = new ActivityTimeSpan
            {
                ProcessName = "Revit",
                WindowTitle = "Floor Plan: Level 1 - Hospital.rvt",
                AppDescription = "Autodesk Revit 2025",
                ExecutablePath = @"C:\Program Files\Autodesk\Revit 2025\Revit.exe",
                AppCompany = "Autodesk, Inc.",
                AppVersion = "25.0.0.0",
                WindowClassName = "Afx:00400000:8",
                ScreenshotPath = null
            };

            Assert.Equal("Autodesk Revit 2025", span.DisplayAppDescription);
            Assert.False(span.HasScreenshot);

            // Fallback when AppDescription is empty
            span.AppDescription = "";
            Assert.Equal("Revit", span.DisplayAppDescription);

            // Screenshot detection
            var tempFile = Path.GetTempFileName();
            try
            {
                span.ScreenshotPath = tempFile;
                Assert.True(span.HasScreenshot);

                span.ScreenshotPath = @"C:\NonExistent_Fake_Dir\test.jpg";
                Assert.False(span.HasScreenshot);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void SettingsViewModel_DwellDebounce_SnappingAndDisplay()
        {
            // [v0.003: DwellRange30s] Verify 30-180s range, 30s snapping, and display text formatting
            var audio = new AudioLevelService();
            var idle = new IdleDetectionService(audio);
            var classifier = new RuleClassifierService(_dbService);
            var winTracker = new WindowTrackerService();
            var aggregator = new ActivityAggregator(_dbService, classifier, winTracker, idle);
            var vm = new SettingsViewModel(_dbService, aggregator, idle, audio);

            // Default
            Assert.Equal(30.0, vm.DwellDebounceSeconds);
            Assert.Equal("30 seconds", vm.DwellDebounceDisplay);

            // Snapping test
            vm.DwellDebounceSeconds = 15.0; // Clamped and snapped to 30s
            Assert.Equal(30.0, vm.DwellDebounceSeconds);
            Assert.Equal("30 seconds", vm.DwellDebounceDisplay);

            vm.DwellDebounceSeconds = 48.0; // Snapped to 60s
            Assert.Equal(60.0, vm.DwellDebounceSeconds);
            Assert.Equal("1 minute", vm.DwellDebounceDisplay);

            vm.DwellDebounceSeconds = 85.0; // Snapped to 90s
            Assert.Equal(90.0, vm.DwellDebounceSeconds);
            Assert.Equal("1m 30s", vm.DwellDebounceDisplay);

            vm.DwellDebounceSeconds = 122.0; // Snapped to 120s
            Assert.Equal(120.0, vm.DwellDebounceSeconds);
            Assert.Equal("2 minutes", vm.DwellDebounceDisplay);

            vm.DwellDebounceSeconds = 150.0;
            Assert.Equal(150.0, vm.DwellDebounceSeconds);
            Assert.Equal("2m 30s", vm.DwellDebounceDisplay);

            vm.DwellDebounceSeconds = 200.0; // Clamped to 180s
            Assert.Equal(180.0, vm.DwellDebounceSeconds);
            Assert.Equal("3 minutes", vm.DwellDebounceDisplay);
        }

        [Fact]
        public void WindowTrackerService_IsIgnoredWindow_FiltersSelfAndOverlays()
        {
            // [v0.003: ExcludeSelfAndOverlays] Verify self app and screen clipping overlays are ignored
            Assert.True(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "IdleWork", "IdleWork - Time Tracker"));
            Assert.True(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "IdleWork.App", "Settings"));
            Assert.True(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "SnippingTool", "Snipping Tool Overlay"));
            Assert.True(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "ScreenClippingHost", ""));
            Assert.True(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "ShellExperienceHost", "Windows Shell Experience Host"));
            Assert.True(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "explorer", "Taskbar"));

            // Legitimate work apps should NOT be ignored
            Assert.False(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "Revit", "Autodesk Revit 2025 - Project1.rvt"));
            Assert.False(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "acad", "AutoCAD 2025 - Drawing1.dwg"));
            Assert.False(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "chrome", "Google - Chrome"));
            Assert.False(WindowTrackerService.IsIgnoredWindow(IntPtr.Zero, "devenv", "IdleWork - Microsoft Visual Studio"));
        }

        [Fact]
        public async Task DatabaseService_PurgeIgnoredActivities_RemovesSnippingToolAndSelf()
        {
            // [v0.003: ExcludeSelfAndOverlays] Verify purge removes any previously recorded self/SnippingTool activities
            await _dbService.EnsureInitializedAsync();

            var now = DateTime.Now;
            var validSpan = new ActivityTimeSpan
            {
                StartTime = now.AddMinutes(-30),
                EndTime = now.AddMinutes(-10),
                DurationSeconds = 1200,
                ProcessName = "Revit",
                WindowTitle = "Autodesk Revit 2025 - Hospital.rvt",
                State = "Active"
            };
            var snippingSpan = new ActivityTimeSpan
            {
                StartTime = now.AddMinutes(-10),
                EndTime = now.AddMinutes(-9),
                DurationSeconds = 60,
                ProcessName = "SnippingTool",
                WindowTitle = "Snipping Tool Overlay",
                State = "Active"
            };
            var selfSpan = new ActivityTimeSpan
            {
                StartTime = now.AddMinutes(-9),
                EndTime = now.AddMinutes(-8),
                DurationSeconds = 60,
                ProcessName = "IdleWork",
                WindowTitle = "IdleWork - Time Tracker",
                State = "Active"
            };

            await _dbService.SaveActivityAsync(validSpan);
            await _dbService.SaveActivityAsync(snippingSpan);
            await _dbService.SaveActivityAsync(selfSpan);

            // Verify they are filtered from query
            var queryActivities = await _dbService.GetActivitiesForDateRangeAsync(now.Date, now.Date.AddDays(1).AddTicks(-1));
            Assert.DoesNotContain(queryActivities, a => a.ProcessName == "SnippingTool");
            Assert.DoesNotContain(queryActivities, a => a.ProcessName == "IdleWork");
            Assert.Contains(queryActivities, a => a.ProcessName == "Revit");

            // Purge from DB
            int purged = await _dbService.PurgeIgnoredActivitiesAsync();
            Assert.True(purged >= 2);
        }

        [Fact]
        public void TimelineViewModel_SelectedActivityScreenshots_PopulatesMultiCaptureGrid()
        {
            // [v0.003: VisualEvidenceGrid] Verify SelectedActivityScreenshots gathers main and interval screenshots
            var classifier = new RuleClassifierService(_dbService);
            var vm = new TimelineViewModel(_dbService, classifier);

            var tempFile1 = Path.GetTempFileName();
            var tempFile2 = Path.GetTempFileName();

            try
            {
                var span = new ActivityTimeSpan
                {
                    Id = 1,
                    ProcessName = "Revit",
                    WindowTitle = "Autodesk Revit 2025 - Hospital.rvt",
                    StartTime = DateTime.Now.AddMinutes(-30),
                    EndTime = DateTime.Now,
                    DurationSeconds = 1800,
                    ScreenshotPath = tempFile1,
                    Intervals = new List<ActivityInterval>
                    {
                        new ActivityInterval
                        {
                            ActivityId = 1,
                            StartTime = DateTime.Now.AddMinutes(-20),
                            EndTime = DateTime.Now.AddMinutes(-15),
                            DurationSeconds = 300,
                            SubProcessName = "chrome",
                            SubWindowTitle = "Building Specs - Google Chrome",
                            ScreenshotPath = tempFile2
                        }
                    }
                };

                vm.SelectedActivity = span;

                Assert.True(vm.HasAnyScreenshots);
                Assert.Equal(2, vm.SelectedActivityScreenshots.Count);
                Assert.Equal("2", vm.ScreenshotCountBadgeText);
                Assert.Equal("Main Window", vm.SelectedActivityScreenshots[0].Label);
                Assert.Equal("Sub: chrome", vm.SelectedActivityScreenshots[1].Label);
            }
            finally
            {
                try { File.Delete(tempFile1); } catch { }
                try { File.Delete(tempFile2); } catch { }
            }
        }

        [Fact]
        public async Task SettingsViewModel_SystemTray_DefaultsAndToggles_PersistToDatabase()
        {
            // [v0.003: SystemTray] Verify defaults and toggle persistence
            await _dbService.EnsureInitializedAsync();

            var classifier = new RuleClassifierService(_dbService);
            var winTracker = new WindowTrackerService();
            var audio = new AudioLevelService();
            var idle = new IdleDetectionService(audio);
            var aggregator = new ActivityAggregator(_dbService, classifier, winTracker, idle);

            var vm = new SettingsViewModel(_dbService, aggregator, idle, audio);

            // Defaults must be true
            Assert.True(vm.MinimizeToTray);
            Assert.True(vm.CloseToTray);
            Assert.True(SystemTrayService.Instance.MinimizeToTray);
            Assert.True(SystemTrayService.Instance.CloseToTray);

            // Toggle off
            vm.MinimizeToTray = false;
            vm.CloseToTray = false;
            Assert.False(vm.MinimizeToTray);
            Assert.False(vm.CloseToTray);
            Assert.False(SystemTrayService.Instance.MinimizeToTray);
            Assert.False(SystemTrayService.Instance.CloseToTray);

            // Wait for fire-and-forget database save to complete
            await Task.Delay(100);

            // Verify persisted in SQLite
            string? minVal = await _dbService.GetSettingAsync("MinimizeToTray");
            string? closeVal = await _dbService.GetSettingAsync("CloseToTray");
            Assert.Equal("False", minVal);
            Assert.Equal("False", closeVal);

            // Reset command restores defaults
            vm.ResetTraySettingsCommand.Execute(null);
            Assert.True(vm.MinimizeToTray);
            Assert.True(vm.CloseToTray);
            Assert.True(SystemTrayService.Instance.MinimizeToTray);
            Assert.True(SystemTrayService.Instance.CloseToTray);
        }

        [Fact]
        public void SystemTrayService_PropertiesAndTooltip_UpdatesState()
        {
            // [v0.003: SystemTray] Verify SystemTrayService state and disposal
            var service = SystemTrayService.Instance;
            service.MinimizeToTray = true;
            service.CloseToTray = true;

            Assert.True(service.MinimizeToTray);
            Assert.True(service.CloseToTray);

            service.UpdateTooltip("Testing Tooltip");
            // Tooltip and notification methods run gracefully without crashing even before HWND attachment
            service.ShowNotification("Test Title", "Test Text");
        }

        [Fact]
        public void TimesheetService_ExportWeeklyTimesheetToMarkdown_FormatsMarkdownTableCorrectly()
        {
            // [v0.004: MarkdownExport] Verify TimesheetService Markdown table formatting for Jira/Slack
            var service = new TimesheetService(_dbService);
            var data = new WeeklyTimesheetData
            {
                WeekStartDate = new DateTime(2026, 9, 21),
                WeekEndDate = new DateTime(2026, 9, 27),
                Rows = new List<WeeklyTimesheetRow>
                {
                    new WeeklyTimesheetRow
                    {
                        ProjectName = "Alpha Revamp",
                        DayHours = new double[] { 4.0, 5.0, 3.5, 0, 0, 0, 0 }
                    }
                },
                DailyActiveHours = new double[] { 4.0, 5.0, 3.5, 0, 0, 0, 0 },
                DailyMeetingHours = new double[] { 1.0, 0.5, 0, 0, 0, 0, 0 }
            };

            string markdown = service.ExportWeeklyTimesheetToMarkdown(data);

            Assert.Contains("Idle-Work Timesheet Summary", markdown);
            Assert.Contains("| Project / Category | Mon | Tue | Wed | Thu | Fri | Sat | Sun | Total |", markdown);
            Assert.Contains("**Alpha Revamp**", markdown);
            Assert.Contains("**Total Active Work**", markdown);
            Assert.Contains("**Total Meetings**", markdown);
            Assert.Contains("**Grand Total (Work+Meetings)**", markdown);
        }

        [Fact]
        public async Task DatabaseService_GetIntervalsForActivitiesAsync_BatchFetchesIntervalsCorrectly()
        {
            // [v0.004: BatchQuery] Verify batch fetching intervals across multiple activities in one query
            await _dbService.EnsureInitializedAsync();

            var act1 = new ActivityTimeSpan
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                DurationSeconds = 3600,
                ProcessName = "revit.exe",
                WindowTitle = "Project Model",
                State = "Active"
            };
            await _dbService.SaveActivityAsync(act1);

            var act2 = new ActivityTimeSpan
            {
                StartTime = DateTime.Today.AddHours(10),
                EndTime = DateTime.Today.AddHours(11),
                DurationSeconds = 3600,
                ProcessName = "acad.exe",
                WindowTitle = "Drawing 1",
                State = "Active"
            };
            await _dbService.SaveActivityAsync(act2);

            var int1 = new ActivityInterval
            {
                ActivityId = act1.Id,
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(9.5),
                DurationSeconds = 1800,
                SubProcessName = "chrome.exe"
            };
            var int2 = new ActivityInterval
            {
                ActivityId = act2.Id,
                StartTime = DateTime.Today.AddHours(10),
                EndTime = DateTime.Today.AddHours(10.5),
                DurationSeconds = 1800,
                SubProcessName = "slack.exe"
            };
            await _dbService.SaveIntervalAsync(int1);
            await _dbService.SaveIntervalAsync(int2);

            // Fetch in batch
            var intervals = await _dbService.GetIntervalsForActivitiesAsync(new[] { act1.Id, act2.Id });

            Assert.Equal(2, intervals.Count);
            Assert.Contains(intervals, i => i.ActivityId == act1.Id && i.SubProcessName == "chrome.exe");
            Assert.Contains(intervals, i => i.ActivityId == act2.Id && i.SubProcessName == "slack.exe");

            // Empty input returns empty list immediately without error
            var empty = await _dbService.GetIntervalsForActivitiesAsync(Array.Empty<int>());
            Assert.Empty(empty);
        }

        [Fact]
        public void ScreenshotService_RetentionAndStorageStats_CalculatesCorrectly()
        {
            // [v0.004: ScreenshotRetention] Verify retention setting and storage calculations
            var screenshotService = ScreenshotService.Instance;
            screenshotService.RetentionDays = 30;
            Assert.Equal(30, screenshotService.RetentionDays);

            var (fileCount, totalBytes) = screenshotService.GetStorageStats();
            Assert.True(fileCount >= 0);
            Assert.True(totalBytes >= 0);

            // Purge runs without error
            int deleted = screenshotService.PurgeOldScreenshots();
            Assert.True(deleted >= 0);
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
