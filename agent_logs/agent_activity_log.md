# Agent Activity Log

## [2026-09-29 08:28:00]
- **Walkthrough**:
  - Cloned the repository `https://github.com/EduardLouis/Idle-Work-WPF-App` into the project root directory `c:\SDEV_ELO\GitHub\Idle_Work` using authentication token.
  - Verified repository file checkout.
- **Results**:
  - Successfully cloned repository files into the workspace.

## [2026-09-29 08:33:30]
- **Walkthrough**:
  - Queried all remote references (`git ls-remote`), branches (`git branch -a`), tags, and commit history across the repository.
- **Results**:
  - Confirmed the remote repository only contains one branch (`master`) with a single initial commit (`a2b05b8`), containing only `LICENSE` and `README.md`. No other branches, tags, or code files exist on the remote repository.

## [2026-09-29 08:41:00]
- **Walkthrough**:
  - Researched open-source activity tracking patterns (ActivityWatch, CoreAudio, Win32 hooks, smart debouncing).
  - Drafted a comprehensive implementation plan for Idle-Work WPF (.NET 8) targeting version `v0.1`.
  - Created implementation plan artifact covering multi-monitor window tracking, 3-input idle sensing (keyboard, mouse, microphone), smart data aggregation, SQLite persistence, and interactive timeline analytics.
- **Results**:
  - Implementation plan artifact generated at `implementation_plan.md` awaiting user review.

## [2026-09-29 08:54:40]
- **Walkthrough**:
  - Researched time-tracking features across ManicTime, ActivityWatch, and Timely (Auto-tagging rules, tag combinations, project categorization, weekly timesheet matrices).
  - Designed the persistent smart rules engine with 1-click rule learning and retroactive weekly batch classification.
  - Updated implementation plan artifact to integrate daily/weekly project timesheet matrix, multi-condition rule matching (App + Title keyword/regex), and CSV/Excel timesheet export.
- **Results**:
  - Updated `implementation_plan.md` artifact ready for user approval.

## [2026-09-29 09:07:45]
- **Walkthrough**:
  - Verified local and global toolchains; confirmed availability of .NET 8.0 SDK (8.0.415) and .NET 9.0 SDK with Windows Desktop (WPF) support.
  - Loaded previously drafted architecture and benchmarks from session plan `94bf6ba8-4743-4a1b-a6e7-50bdce82cb2d`.
  - Formatted and generated the official technical implementation plan artifact at `implementation_plan.md` targeting version milestone `v0.1`.
- **Results**:
  - Implementation plan artifact generated and registered with `request_feedback=true` awaiting user confirmation to proceed to build.

## [2026-09-29 09:10:30]
- **Walkthrough**:
  - Addressed user feedback regarding UI/UX and ease-of-use requirements.
  - Updated `implementation_plan.md` to explicitly incorporate modern Windows 11 Fluent design principles (e.g., using WPF-UI/MaterialDesign), Zero-Config startup, intuitive 1-click assigning, and a visual Gantt-style timeline for premium usability.
- **Results**:
  - Implementation plan enhanced with clear UI/UX targets. Awaiting final execution go-ahead.

## [2026-09-29 09:12:30]
- **Walkthrough**:
  - Conducted a web search comparing features of top time tracking competitors (Rize, Memtime, RescueTime, ManicTime) to benchmark their integrations and intelligent categorization capabilities.
  - Addressed user feedback to support external workflows by introducing a new "Extensible Plugin & Integration Architecture" section in the plan.
  - Outlined integration points for PowerAutomate (webhooks/APIs for pushing timesheets), Outlook (calendar sync for scheduled meetings), and Microsoft Teams (presence detection to enrich local tracking).
- **Results**:
  - Updated `implementation_plan.md` artifact to reflect the competitive integrations and plugin architecture. Awaiting final user approval to proceed with execution.

## [2026-09-29 09:15:30]
- **Walkthrough**:
  - Updated the integration architecture to remove Zapier references and prioritize a native C# Plugin SDK (`IIdleWorkPlugin`) for deeper developer-friendly extensibility.
  - Refined the Microsoft Teams integration design: shifted from simple presence detection to extracting precise conversation time spans and combining them with active window history for granular activity breakdowns during meetings.
- **Results**:
  - `implementation_plan.md` artifact updated with custom plugin SDK and refined Teams logic. Awaiting execution approval.

## [2026-09-29 09:33:30]
- **Walkthrough**:
  - Scaffolding: Initialized solution `IdleWork.sln` with single-source versioning and dynamic build timestamping in `Directory.Build.props` (`<AppMajorMinor>0.1</AppMajorMinor>`).
  - Native & Core Engine: Implemented `User32.cs` (WinEventHook, GetLastInputInfo, EnumDisplayMonitors) and `Shell32.cs` for low-overhead foreground window tracking, process detection, and multi-monitor bounds resolution.
  - 3-Input Inactivity & Meeting Mode: Built `AudioLevelService.cs` (NAudio CoreAudio peak volume metering without recording) and `IdleDetectionService.cs` to accurately maintain Meeting Mode during voice/video discussions.
  - Smart Timespan Aggregation: Built `ActivityAggregator.cs` with dwell-time debouncing (< 2.5s) to prevent focus-switch clutter and continuously chunk work sessions into `ActivityTimeSpan` records.
  - Persistent Smart Rules: Implemented `DatabaseService.cs` (local SQLite) and `RuleClassifierService.cs` with 1-click "Assign & Remember" learning and retroactive weekly batch classification.
  - Reporting & Timesheets: Built `TimesheetService.cs` for daily timelines and weekly Project x Day (Mon-Sun) matrices with one-click CSV export.
  - Extensible C# Plugin SDK: Created `IIdleWorkPlugin`, `PluginManager`, `TeamsIntegrationPlugin`, `OutlookCalendarPlugin`, and `PowerAutomatePlugin`.
  - Modern Fluent UI Shell: Created `App.xaml`, `MainWindow.xaml`, `LiveTrackerView.xaml`, `TimelineView.xaml`, `WeeklyTimesheetView.xaml`, `RulesManagerView.xaml`, `SettingsView.xaml`, and `MilestonesWindow.xaml`.
  - Testing & Verification: Created test project `IdleWork.Tests` with 9 unit tests verifying rule classification, priority ordering, retroactive learning, timesheet matrix aggregation, CSV export, and document title parsing.
- **Results**:
  - All 9 unit tests passed successfully.
  - Code compiles cleanly with 0 warnings and 0 errors.
  - Milestone `v0.1` completed, logged in `MILESTONES.md`, and documented in `walkthrough.md`.

## [2026-09-29 09:54:40]
- **Walkthrough**:
  - Created `.vscode/tasks.json` and `.vscode/launch.json` configuring live F5 debugging for `.NET: Debug Idle-Work (WPF)` and attach configurations.
  - Launched live interactive application process [`IdleWork.App.exe`](file:///c:/SDEV_ELO/GitHub/Idle_Work/IdleWork.App/bin/Debug/net8.0-windows/IdleWork.App.exe).
- **Results**:
  - Live debugging environment configured and interactive app session launched.
## [2026-09-29 10:26:00]
- **Walkthrough**:
  - Investigated Visual Studio debug log showing rapid repeated `System.InvalidCastException in NAudio.Wasapi.dll` and process termination code `0xffffffff`.
  - Identified root cause in `AudioLevelService.cs`: NAudio CoreAudioApi endpoint acquisition and `IAudioMeterInformation` COM querying threw `InvalidCastException` due to MTA thread apartment mismatch and hardware/virtual audio devices lacking COM meter interfaces.
  - Hardened `AudioLevelService.cs`: switched background thread to `ApartmentState.STA`, wrapped COM endpoint and meter queries in targeted try-catch blocks, and implemented automatic backoff to 30s when `IAudioMeterInformation` is unsupported to prevent exception flooding.
  - Hardened WPF App shell: converted `DarkTheme.xaml` dictionary inclusion in `App.xaml` to standard WPF pack URI (`pack://application:,,,/IdleWork.App;component/Themes/DarkTheme.xaml`), eliminated silent shutdown risks by programmatically instantiating and activating `MainWindow` in `App.xaml.cs`.
  - Added physical startup diagnostic logging (`startup.log`) to record each lifecycle phase from SQLite initialization to window load.
  - Rebuilt `IdleWork.App` and verified that all unit tests in `IdleWork.Tests` continue to pass.
- **Results**:
  - `IdleWork.App` compiled cleanly (0 errors).
  - All 9 unit tests passed successfully.
  - `InvalidCastException` COM spam resolved with safe fallback.

## [2026-09-29 11:38:00]
- **Walkthrough**:
  - Diagnosed UI startup freeze: tracked down synchronous `.Wait()` and `.GetAwaiter().GetResult()` blocking the WPF UI thread during `MainWindow` / `MainViewModel` initialization.
  - Eliminated deadlock in `RuleClassifierService.cs`: removed synchronous `RefreshRulesAsync().Wait()` from constructor, replacing with non-blocking fire-and-forget `_ = RefreshRulesAsync()`.
  - Refactored `DatabaseService.cs` to non-blocking async initialization (`_initTask`), ensuring database operations await schema setup without freezing the UI thread constructor.
  - Clarified run vs build execution for user: verified that `dotnet build` compiles binaries while `F5` / `Ctrl+F5` / `dotnet run` executes the live UI process.
  - Recompiled and ran all 9 unit tests to ensure stability.
- **Results**:
  - `IdleWork.App` compiled with 0 errors.
  - All 9 unit tests passed successfully.
  - UI thread synchronization deadlock completely resolved.

## [2026-09-29 12:16:00]
- **Walkthrough**:
  - Analyzed user requirements for multi-monitor top window tracking, software priority hierarchy (main vs sub-activity), user-defined projects management, smart rules reordering and bulk actions, scroll wheel fix, and activity session consolidation inspired by Trackabi and ActivityWatch.
  - Investigated Win32 native APIs (`EnumWindows`, `IsIconic`, `DwmGetWindowAttribute` with `DWMWA_CLOAKED`, `EnumDisplayMonitors`) to reliably resolve the topmost visible application per display monitor.
  - Designed the data model extensions (`SoftwarePriority`, `ActivityInterval`, `SubProcessName`, `SubWindowTitle`, `TopWindowsJson`) and daily session consolidation algorithm.
  - Drafted and published the comprehensive technical implementation plan artifact at `implementation_plan.md` targeting version milestone `v0.2`.
- **Results**:
  - Created implementation plan artifact targeting milestone `v0.2` awaiting user review and approval.

## [2026-09-29 12:30:00]
- **Walkthrough**:
  - Implemented single-source version bump in `Directory.Build.props` from `<AppMajorMinor>0.1</AppMajorMinor>` to `<AppMajorMinor>0.2</AppMajorMinor>` with dynamic build timestamp propagation.
  - Multi-Monitor Top Window Engine: Added Win32 P/Invokes (`EnumWindows`, `IsIconic`, `GetWindowRect`, `DwmGetWindowAttribute` with `DWMWA_CLOAKED`) in `User32.cs`, and implemented `WindowTrackerService.GetTopWindowsForAllMonitors()` in Z-order with center-coordinate monitor matching and cloaked/shell window filtering.
  - Software Priority & Main/Sub-Activity Engine: Created `SoftwarePriority` entity, seeded defaults (Revit [Prio 0], AutoCAD [Prio 1], Visual Studio [Prio 2], etc.), and implemented `ActivityAggregator.ResolveHierarchy()`. When high-priority software remains visible on one monitor and focus shifts to another monitor, the primary activity remains unchanged while tagging the focused window as a `SubActivity`.
  - Consolidated Daily Aggregation & Intervals: Extended `ActivityTimeSpan` with `Intervals` and discrete `ActivityInterval` tracking (`Start`, `End`, `Duration`, `SubActivity`). Aggregator consolidates returning visits into a single primary record with cumulative duration and expandable drawer intervals.
  - Custom Projects Management: Implemented `ProjectsViewModel.cs` and `ProjectsView.xaml` with full CRUD, color picking, and dynamic project assignment. Added navigation button and template in `MainWindow.xaml` and `MainViewModel.cs`.
  - Rules & Priority Order UI: Upgraded `RulesManagerViewModel.cs` and `RulesManagerView.xaml` with Move Up, Move Down, Edit, Duplicate, Delete, and Batch actions. Added dedicated "Primary Software Priority" management tab.
  - Scroll & Fluent Styling: Added `ScrollHelper.BubbleMouseWheel` attached behavior to forward mouse wheel events from nested ListViews without swallowing. Styled modern Fluent dark-theme `ScrollBar` with functioning LineUp/LineDown repeat buttons.
  - Timeline Visual Bands: Upgraded `TimelineView.xaml` with 3-lane visual ribbon bands (Application, Document, Project) and interactive drawer for discrete activity intervals.
  - Automated Testing: Created 4 new unit tests in `MultiMonitorAndConsolidationTests.cs` and updated `TitleParserTests.cs` for v0.2 version verification. Executed test suite (`dotnet test`).
  - Updated `MILESTONES.md` and `MilestonesWindow.xaml` to document the completed `[v0.2]` release.
- **Results**:
  - All 13 unit tests passed (100% green).
  - Code compiles cleanly with 0 errors and 0 warnings.
  - Physical log updated, milestone v0.2 complete.

## [2026-09-29 12:57:00]
- **Walkthrough**:
  - Investigated ComboBox dropdown rendering: discovered default WPF Aero2 theme was causing dropdown popup lists (`PART_Popup`) to display system-default light gray/white backgrounds and gray item selections.
  - Designed and implemented complete Fluent dark theme templates in `Themes/DarkTheme.xaml`:
    * `ComboBoxItem`: Styled with transparent background, `#F8FAFC` foreground, `#283548` mouseover hover, `#334155` selected background with `#38BDF8` accent border, and rounded corners (`CornerRadius="4"`).
    * `ComboBoxToggleButton`: Sleek border with `#0F172A` background, `#334155` border, `#94A3B8` dropdown arrow changing to `#38BDF8` accent on hover/open.
    * `ComboBox`: Custom `ControlTemplate` with dark popup background (`#1E293B`), `#334155` border, and integrated dark `ScrollViewer` utilizing the custom scrollbar.
  - Verified compilation of `IdleWork.App` and executed full test suite (`dotnet test`).
- **Results**:
  - Dropdown lists now render with the dark theme palette.
  - Build succeeded with 0 errors; all 13 unit tests passed.

## [2026-09-29 13:03:00]
- **Walkthrough**:
  - Priority Badge Numbering Fix:
    * Identified root cause of duplicated `Prio 3` badges: `SoftwarePriority.cs` was a plain POCO without `INotifyPropertyChanged`, so WPF bindings never updated when items were promoted/demoted or renumbered.
    * Converted `SoftwarePriority` to inherit `ObservableObject` and raise `PropertyChanged` on `Priority`, `DisplayName`, and `ProcessFilter`.
    * Added automatic database priority normalization in `DatabaseService.GetSoftwarePrioritiesAsync()` to heal any existing duplicate ranks in SQLite into strict sequential order (0, 1, 2, 3...).
    * Updated `RulesManagerViewModel.cs` with `RenumberPrioritiesInCollection()` across `MovePrioUpAsync`, `MovePrioDownAsync`, `AddPriorityAsync`, `DeletePriorityAsync`, and `LoadPrioritiesAsync` to renumber UI collection items directly on the UI thread.
  - Real-Time Feed Main & Sub-Activity Display:
    * Enhanced `ActivityTimeSpan.cs` with `HasSubActivity` and `SubActivityDetailDisplay` helper properties.
    * Redesigned the activity item DataTemplate in `LiveTrackerView.xaml`:
      - **Column 1**: Prominent cyan `[MAIN: Process]` badge and amber `[SUB: Process]` badge when a secondary monitor activity is active.
      - **Column 2**: Primary window title & document, plus a dedicated `↳ Sub (2nd Mon): [Document/Title]` callout row.
- **Results**:
  - Software priority numbers now update dynamically in the UI and remain strictly sequential.
  - Real-Time Activity Feed clearly differentiates and highlights Main Activity and concurrent Sub-Activities.

## [2026-09-29 13:06:00]
- **Walkthrough**:
  - Designed and implemented dual assignment mechanism on Daily Timeline (`TimelineView.xaml` & `TimelineViewModel.cs`):
    * Created `AssignmentChoice.cs` model supporting dynamic representation of both classification rules and direct projects.
    * Added RadioButton mode toggle (`Classification Rule` vs `Direct Project`) in `TimelineView.xaml`.
    * Implemented application-aware rule filtering in `TimelineViewModel.PopulateAssignmentChoices()`: when `Classification Rule` is selected, only rules applicable to the selected activity's application (e.g. Revit rules for Revit, excluding AutoCAD or VS rules) appear in the dropdown.
    * When `Direct Project` is selected, the dropdown populates with user-defined projects, enabling "Remember this rule" and retroactive learning.
    * Added dynamic label (`Select Applicable Rule` vs `Select Target Project`) and a contextual alert when no rules match an application, guiding the user to use `Direct Project`.
    * Enhanced `AssignProjectAsync()` to assign rule targets and tags, with optional 7-day retroactive batch updates.
    * Added unit test `TimelineViewModel_DualAssignment_FiltersRulesByActivityApplication` in `MultiMonitorAndConsolidationTests.cs`.
- **Results**:
  - Users can seamlessly switch between assigning existing applicable classification rules and assigning direct projects.
  - Prevents invalid cross-application rule assignments (e.g. AutoCAD rules cannot be assigned to Revit activities).
  - Code compiles cleanly with zero syntax/type errors.

## [2026-09-29 13:13:00]
- **Walkthrough**:
  - Researched industry standard patterns for handling application shutdowns / offline gaps (ManicTime, ActivityWatch, RescueTime, Time Doctor):
    * Applications log untracked gaps as discrete "Offline" or "Away" blocks rendered with slate/muted colors on the timeline.
    * Users can see when the system was offline, distinguish app-closed time from active computer time, and retroactively classify or assign offline time (e.g. meetings, field visits, lunch).
  - Implemented automatic app-closed gap detection in `ActivityAggregator.cs`:
    * Added `GetMostRecentActivityAsync()` in `DatabaseService.cs` to query the latest recorded activity timestamp.
    * Added `GetLastInputTime()` in `IdleDetectionService.cs` using Win32 `User32.GetLastInputInfo` to identify the most recent user keyboard/mouse event.
    * Implemented `DetectAndRecordAppClosedGapAsync()` in `ActivityAggregator.cs`. On app startup, checks if `(now - lastEnd) >= 60 seconds`.
    * Added midnight-boundary split: if the gap crosses midnight (e.g., overnight or weekend), it partitions the gap across calendar days so that daily timelines remain strictly within 24h bounds.
    * Generates `ActivityTimeSpan` records with `State = "Offline"`, `ProcessName = "App Inactive"`, `WindowTitle = "App Closed / Untracked"`, and `Category = "Untracked"`.
  - Updated Timeline UI & Metrics (`TimelineView.xaml` & `TimelineViewModel.cs`):
    * Added `TotalOfflineText` property to `TimelineViewModel.cs` displaying cumulative offline time for the selected day.
    * Added a 4th metric summary card in `TimelineView.xaml`: `UNTRACKED / OFFLINE` styled with a subtle slate border.
    * Colored offline intervals in the 24-hour visual ribbon with dark slate (`#475569`) and detailed tooltip duration info.
  - Added unit test `ActivityAggregator_DetectAndRecordAppClosedGap_CreatesOfflineActivity` in `MultiMonitorAndConsolidationTests.cs`.
- **Results**:
  - App-closed periods are now automatically captured on startup and clearly visualized on the daily timeline.
  - Users can click on untracked intervals to view details and assign them if necessary.
  - Clean separation between active work, idle time, meeting mode, and offline/closed gaps.

## [2026-09-29 13:16:00]
- **Walkthrough**:
  - Implemented refined Idle Timeout threshold stepping and formatting in `SettingsViewModel.cs`:
    * Enforced values: `30 seconds`, `60 seconds` (`1 minute`), and strictly 1-minute multiples above 60s (e.g. 2 min, 3 min, 4 min... up to 15 min).
    * Suppressed seconds display above 60s: `IdleTimeoutDisplayText` formats durations above 60s exclusively as `"{N} minutes"`.
    * Snapped setter values in `SettingsViewModel.cs` so any slider movement or direct binding above 60 seconds automatically rounds to the nearest 60-second multiple.
  - Added Default Value Markers & Reset Buttons across all Preferences Sliders:
    * Marked defaults: `Default: 3 min` (Idle Timeout, 180s), `Default: 2.5s` (Dwell Debounce), `Default: 5%` (Mic Sensitivity, 0.05f).
    * Added visual default position indicators on the slider tracks in `SettingsView.xaml` with mathematically aligned column proportions and pin markers (`▲ Default`).
    * Added compact `↺ Reset` buttons beside each slider in `SettingsView.xaml` wired to `ResetIdleTimeoutCommand`, `ResetDwellDebounceCommand`, and `ResetMicSensitivityCommand`.
  - Added unit tests:
    * `SettingsViewModel_IdleTimeout_SnappingAndDisplayText_WorksCorrectly`: verified 30s, 1m, and 1-minute intervals and minute text formatting.
    * `SettingsViewModel_ResetCommands_RestoreDefaults`: verified reset commands return all slider values to their official defaults.
- **Results**:
  - Preferences UI provides clear, intuitive time formatting for idle timeout thresholds without awkward 30-second jumps.
  - All sliders visually communicate their default positions and allow instantaneous 1-click resets.

## [2026-09-29 13:22:00]
- **Walkthrough**:
  - Implemented in-dropdown Quick Create action rows for Daily Timeline (`TimelineView.xaml` & `TimelineViewModel.cs`):
    * Added `IsCreateAction` flag to `AssignmentChoice.cs`.
    * In Rule mode, dynamically appends `➕ Create New Rule...` to the dropdown options.
    * In Project mode, dynamically appends `➕ Create New Project...` to the dropdown options.
    * Added visual styling in `TimelineView.xaml` ComboBox DataTemplate to highlight create action rows in accent cyan with a distinct `NEW` badge.
    * Added a dedicated companion `➕ New` button beside the ComboBox for one-click access.
  - Created dedicated modal dialog windows:
    * `NewProjectDialog.xaml` / `NewProjectDialog.xaml.cs`: Allows inputting Project Name, Code, Color accent chips, and Description.
    * `NewRuleDialog.xaml` / `NewRuleDialog.xaml.cs`: Prefills Process Filter with the selected activity's `ProcessName`, Title Pattern with `DocumentName`/`WindowTitle`, and suggested Rule Name, allowing users to quickly assign a Target Project, Category, and Tags.
  - Wired event handling and seamless database persistence:
    * `TimelineViewModel.RequestCreateNew` event triggers modal dialogs via `TimelineView.xaml.cs`.
    * Upon creation, items are immediately saved via `DatabaseService`, lists are reloaded, and the newly created project or rule is automatically selected and ready for assignment.
    * If cancelled, selection reverts cleanly to the previously active choice.
  - Added unit test `TimelineViewModel_QuickCreate_TriggersEvent_AndAppendsNewProject` in `MultiMonitorAndConsolidationTests.cs`.
- **Results**:
  - Users can now create new projects and classification rules directly from the daily timeline without navigating away to other tabs.
  - Dialogs pre-populate relevant context from the selected activity for an effortless 1-click workflow.

## [2026-09-29 13:25:00]
- **Walkthrough**:
  - Streamlined Project Management Selection & Edit Workflow (`ProjectsViewModel.cs` & `ProjectsView.xaml`):
    * Updated `SelectedProject` setter: selecting any project in the list automatically transitions the form into edit mode, loads all properties, and changes the action button from `+ Create Project` to `💾 Update Project`.
    * Removed redundant `Edit Selected` button from the toolbar in `ProjectsView.xaml`, leaving clean `➕ New Project` and `🗑 Delete Selected` actions.
    * Calling `ClearForm()` or unselecting resets the form and transitions the button back to `+ Create Project`.
  - Fixed Live List Updating on Project Modification:
    * Refactored `Project.cs` entity to inherit `ObservableObject` and raise `PropertyChanged` for `Name`, `Code`, `ColorHex`, `Description`, and `IsActive`.
    * Modifications to project description and details now propagate instantaneously to the ListView without needing manual list reloads.
  - Integrated Classification Rule Correlation at Project Creation and Update:
    * Created `SelectableRule.cs` model for managing rule selections.
    * Added Associated Classification Rules section in `ProjectsView.xaml` with multi-select checklist of existing rules.
    * Added inline quick rule creator (`Process Filter` & `Title Pattern` with `➕ Add Rule` button).
    * When saving or updating a project, all selected existing rules and newly defined rules have their `TargetProject` set to the project and are saved to SQLite. Unselected rules previously pointing to the project are cleanly unlinked.
    * Updated `NewProjectDialog.xaml` & `NewProjectDialog.xaml.cs` to also support selecting existing rules or creating a new rule at project creation time.
  - Added unit test `ProjectsViewModel_SelectProject_SetsUpdateMode_AndUpdatesDescriptionAndRules` in `MultiMonitorAndConsolidationTests.cs`.
- **Results**:
  - Selecting a project immediately switches the UI to Update Project mode.
  - Description and property changes immediately reflect in the project list.
  - Rules can now be directly correlated with projects at creation time or during project edits.

## [2026-09-29 13:37:00]
- **Walkthrough**:
  - Implemented multi-row selection and batch execution across Smart Classification Rules (`RulesManagerView.xaml` & `RulesManagerViewModel.cs`):
    * Enabled `SelectionMode="Extended"` on `RulesListView` and `PrioritiesListView`.
    * Implemented custom `ListViewItem` container style with Fluent hover and accent border (`#38BDF8`) highlight so all selected rows are clearly visually distinguished.
    * Added command parameter binding `CommandParameter="{Binding SelectedItems, ElementName=RulesListView}"` for all toolbar buttons (`MoveUpCommand`, `MoveDownCommand`, `EditRuleCommand`, `DuplicateRuleCommand`, `ToggleEnableCommand`, `DeleteRuleCommand`).
    * Refactored `RulesManagerViewModel.cs` commands to accept `IList? selectedItems`, snapshotting items to prevent concurrent collection modification exceptions during removal.
  - Replaced separate Enable and Disable buttons with a Unified Toggle Enable Button:
    * Replaced `EnableSelectedCommand` (`☑ Enable`) and `DisableSelectedCommand` (`⬜ Disable`) in `RulesManagerView.xaml` with a single `🔘 Toggle Enable` button (`ToggleEnableCommand`).
    * The toggle operation intelligently determines target state: if all selected rules are enabled, it disables them; otherwise, it enables them all.
  - Converted `AutoTagRule` to inherit `ObservableObject`:
    * Refactored `AutoTagRule.cs` to implement `INotifyPropertyChanged` for `IsEnabled`, `RuleName`, `ProcessFilter`, `TitlePattern`, `TargetProject`, `Priority`, etc.
    * Hooked `rule.PropertyChanged` in `RulesManagerViewModel.cs` so directly clicking any row's checkbox immediately persists the change to SQLite and refreshes `RuleClassifierService` in real time.
  - Multi-select support for Software Priorities:
    * Enabled `SelectionMode="Extended"` on `PrioritiesListView` with batch delete and batch priority promotion/demotion.
  - Fixed headless test runner dispatcher execution:
    * Made `OnNewProjectCreatedAsync` and `OnNewRuleCreatedAsync` in `TimelineViewModel.cs` check for null `App.Current?.Dispatcher` before dispatching, avoiding dropped updates in xUnit tests.
  - Added unit test `RulesManagerViewModel_MultiSelect_DeleteAndToggleAndDuplicate_WorksOnAllSelected` in `MultiMonitorAndConsolidationTests.cs`.
- **Results**:
  - All Smart Rules toolbar buttons (`Delete`, `Duplicate`, `Move Up/Down`, `Toggle Enable`) now operate on single or multiple selected rows.
  - Enable and Disable actions are consolidated into a single intuitive Toggle Enable button.
  - Directly toggling rule checkboxes in list rows persists instantly and triggers immediate classification updates.
  - All 20 automated unit tests pass.

## [2026-09-29 13:40:00]
- **Walkthrough**:
  - Implemented single-row auto-edit mode on Smart Rules (`RulesManagerView.xaml`, `RulesManagerView.xaml.cs`, `RulesManagerViewModel.cs`):
    * Removed redundant `✏ Edit` button from the toolbar in `RulesManagerView.xaml`.
    * Added `SelectionChanged="RulesListView_SelectionChanged"` on `RulesListView`.
    * Implemented `OnRuleSelectionChanged(IList? selectedItems)` in `RulesManagerViewModel.cs`:
      - When exactly 1 row is selected: sets `_editingRuleId = rule.Id`, loads fields into the form, and updates the action button to `💾 Update Rule`.
      - When multiple rows are selected: switches to batch action mode and resets the action button to `+ Add Persistent Rule`.
      - When deselecting or clicking "Clear / New": resets `_editingRuleId = 0`, clears inputs, and resets button text to `+ Add Persistent Rule`.
    * Updated `SelectedRule` property setter to automatically load the rule and enter update mode.
  - Added unit test `RulesManagerViewModel_SingleRowSelection_SwitchesToUpdateMode_AndMultiSelectionResets` in `MultiMonitorAndConsolidationTests.cs`.
- **Results**:
  - Selecting any single classification rule seamlessly loads it into the editor and sets the button to `💾 Update Rule`.
  - The redundant `✏ Edit` button is eliminated.
  - All 21 automated unit tests pass.

## [2026-09-29 13:48:00]
- **Walkthrough**:
  - Added In-UI Descriptions and Tooltips for Window Title Pattern / Keyword:
    * `RulesManagerView.xaml`: Added subtext explanation beneath "Window Title Pattern / Keyword (Optional)" explaining case-insensitive substring matching against active window titles (e.g. 'Hospital', 'Floor Plan', 'Project-101') and blank/wildcard behavior, with matching tooltip.
    * `NewRuleDialog.xaml`: Added descriptive helper text and tooltip explaining title matching.
    * `ProjectsView.xaml`: Added descriptive helper text and enhanced tooltip for inline rule creation in the project details card.
  - Formalized Major (0-999) & Minor (0-999) Versioning Architecture:
    * Structured centralized properties in `Directory.Build.props`:
      - `<AppMajor>0</AppMajor>` (0-999 integer, representing major versions 0, 1, 2, 3...).
      - `<AppMinor>2</AppMinor>` (0-999 integer, representing minor versions yielding 0.1, 0.2, 1.2, 2.1, 3.1...).
      - `<AppMajorMinor>$(AppMajor).$(AppMinor)</AppMajorMinor>` composite version string.
      - Exposed `AppMajor` and `AppMinor` alongside `AppMajorMinor` and dynamic `AppBuildTimestamp` via `AssemblyMetadataAttribute`.
    * Updated `AppVersionHelper.cs`:
      - Added typed `Major` (0-999) and `Minor` (0-999) integer properties with bounds validation.
      - Automatically extracts and exposes Major and Minor components across the application.
  - Expanded automated test suite:
    * Added unit test `AppVersionHelper_MajorMinor_BoundedWithin0To999` in `MultiMonitorAndConsolidationTests.cs`.
    * Validated all 22 tests pass with 0 failures.
- **Results**:
  - Clear user guidance on how title pattern / keyword matching works across all rule creation and editing screens.
  - Single-source Major (0-999) and Minor (0-999) version architecture explicitly configured, tested, and documented.

## [2026-09-29 13:52:00]
- **Walkthrough**:
  - Identified cause of modal dialog popping up when clicking an activity row in the Daily Timeline grid:
    * When selecting an activity in `ListView`, `SelectedActivity` updates, calling `PopulateAssignmentChoices()`.
    * If the selected application had no existing classification rules, the fallback `?? AssignmentChoices[0]` selected the `➕ Create New Rule...` action row.
    * `SelectedChoice` property setter detected `value.IsCreateAction` and fired `RequestCreateNew`, which opened `NewRuleDialog.ShowDialog()`.
  - Implemented Safeguards in `TimelineViewModel.cs`:
    * Added boolean flag `_isPopulatingChoices` during `PopulateAssignmentChoices()` execution.
    * Guarded `SelectedChoice` setter so that `RequestCreateNew` is never invoked during programmatic choice population.
    * Removed fallback to `AssignmentChoices[0]` if it is a create action; if no regular project/rule exists, `SelectedChoice` cleanly remains `null`.
    * Modal creation dialogs now ONLY open when the user explicitly clicks `➕ Create New...` in the dropdown or clicks the `➕ New` button beside the dropdown.
  - Concurrency hardening in `ProjectsViewModel.cs`:
    * Added `_loadLock` (`SemaphoreSlim`) guarding `LoadProjectsAsync` and `LoadRulesAsync` to prevent thread-unsafe collection modification collisions.
  - Added unit test:
    * `TimelineViewModel_SelectingRowInGrid_DoesNotFireRequestCreateNew_EvenWithNoRules` in `MultiMonitorAndConsolidationTests.cs`.
    * All 23 tests pass.
- **Results**:
  - Clicking activity rows in the daily timeline grid opens the details drawer without prematurely triggering the modal creation dialog.
  - Modal creation dialog opens exclusively on intentional user action (clicking `➕ Create New...` in the dropdown or the `➕ New` button).
  - All 23 automated tests pass.
