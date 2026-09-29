# Milestones & Version History

## [v0.2] - Multi-Monitor Hierarchy, Projects & Consolidated Intervals
- **Status**: Completed
- **Target**: v0.2

### Implemented
- **Multi-Monitor Top Window Engine**:
  * Win32 Z-order enumeration via `EnumWindows`, `IsIconic`, and `DwmGetWindowAttribute(DWMWA_CLOAKED)`.
  * Topmost visible window detection across all active monitors simultaneously.
  * Real-time multi-monitor topology snapshot reporting in UI and database.
- **Software Priority Hierarchy & Sub-Activity Engine**:
  * Configurable priority ranking table (`SoftwarePriorities`): e.g., Revit (Prio 0), AutoCAD (Prio 1), Visual Studio (Prio 2).
  * Main vs. Sub-Activity tracking: When high-priority software remains visible on one monitor and user focuses secondary software (e.g. PDF reference or AutoCAD) on another monitor, the primary activity remains unchanged while tracking the secondary focused window as a distinct Sub-Activity.
  * Database schema migrations and automatic seeding of default software priorities.
- **Dedicated Custom Projects & Clients Manager**:
  * Full CRUD UI (`ProjectsView.xaml` & `ProjectsViewModel.cs`) for custom projects, project codes, client names, and visual hex color codes.
  * Replaces static hardcoded categories with dynamic user-configured projects.
- **Enhanced Rules & Priority Order Management**:
  * Added Move Up / Move Down priority order reordering with persistent database synchronization.
  * Added Edit, Duplicate, Delete, and Batch Enable/Disable/Delete capabilities in `RulesManagerView.xaml`.
  * Added dedicated Software Priority tab in Rules Manager.
- **Fluent Scroll & Nested Scroll Fix**:
  * Fixed mouse-wheel swallowing inside nested ListViews with `ScrollHelper.BubbleMouseWheel` attached behavior.
  * Added Fluent dark-theme `ScrollBar` styling with functional RepeatButtons (`LineUpCommand`, `LineDownCommand`) and customized thumbs.
- **Trackabi/ActivityWatch-Style Consolidated Daily Aggregation & Timeline Bands**:
  * Grouping and consolidation of daily sessions returning to the same application/document/project into single primary records with cumulative total duration.
  * Discrete `ActivityInterval` tracking (`Start`, `End`, `Duration`, `SubActivity`) expandable via interactive UI drawer (`ToggleExpandCommand`).
  * 3-lane visual ribbon timeline bands in `TimelineView.xaml`: Application, Document, and Project lanes.
- **Offline / App-Closed Inactivity Gap Tracking**:
  * Industry benchmark alignment (ManicTime, ActivityWatch, RescueTime): Automatic gap detection when IdleWork is closed, system is rebooted, or the app restarts.
  * Startup gap detection in `ActivityAggregator.DetectAndRecordAppClosedGapAsync()` querying `DatabaseService.GetMostRecentActivityAsync()` and Win32 `User32.GetLastInputInfo`.
  * Automatic midnight-crossing split ensuring multi-day/overnight offline intervals are properly partitioned across calendar days.
  * Timeline visualization: 4th Daily Metric summary card (`UNTRACKED / OFFLINE`) and distinct slate-colored (`#475569`) timeline segments with full tooltips and retroactive project reassignment support.
- **Dual Assignment & Quick-Create Dialogs (Rules vs. Direct Project)**:
  * Dynamic toggle between assigning applicable classification rules or direct user projects in the timeline drawer.
  * Application-aware filter ensuring only rules relevant to the selected process (e.g. Revit rules for Revit activities) are exposed.
  * In-dropdown action rows (`➕ Create New Rule...` and `➕ Create New Project...`) plus a companion `➕ New` button.
  * Built dedicated modal dialogs (`NewRuleDialog.xaml` and `NewProjectDialog.xaml`) prefilled with active context (process filter, title pattern, suggested names), saving directly to SQLite and instantly auto-selecting the created item.
- **Project Management & Rule Correlation Enhancements**:
  * Auto-Edit Mode on Selection: Selecting a project in the active list automatically loads it into the form and transitions the action button to `💾 Update Project`, eliminating the redundant `Edit Selected` button.
  * Live-Updating List Items: Converted `Project` entity to inherit `ObservableObject`, ensuring changes to project description, name, code, or color instantly reflect in the UI list without requiring manual reloads.
  * Direct Classification Rule Correlation: Added multi-select rule checklist and quick inline rule creator in the Project Details card and `NewProjectDialog.xaml`, allowing users to link existing or newly created rules with a project upon creation and saving.
- **Preferences & Diagnostics Slider Enhancements**:
  * Configurable Idle Timeout with 30s, 60s (1 min), and strictly 1-minute intervals above 60s (eliminating awkward 30s increments like 90s or 150s).
  * Enforced minute formatting above 60s (e.g. "2 minutes", "3 minutes", never "120 seconds").
  * Marked default positions (`Default: 3 min`, `Default: 2.5s`, `Default: 5%`) visually on all slider tracks and in header badges.
  * Added compact `↺ Reset` buttons for all preferences sliders restoring individual settings to their defaults with a single click.
- **Smart Rules Multi-Select & Unified Toggle Engine**:
  * Multi-Row Selection: Configured `SelectionMode="Extended"` and unified Fluent `ItemContainerStyle` highlights on both classification rules and software priority list views.
  * Auto-Edit Single Row Mode: Selecting exactly 1 rule in the list automatically loads it into the editor and transitions the action button to `💾 Update Rule`, eliminating the redundant `✏ Edit` toolbar button. Selecting multiple rows or clearing resets to `+ Add Persistent Rule` batch mode.
  * Multi-Row Action Support: Updated Delete (`DeleteRuleCommand`), Duplicate (`DuplicateRuleCommand`), Move Up/Down (`MoveUpCommand`, `MoveDownCommand`), and Priority commands to execute seamlessly on all selected rows as a batch.
  * Unified Toggle Enable Button: Replaced separate `Enable` and `Disable` buttons with a single `🔘 Toggle Enable` button (`ToggleEnableCommand`) that intelligently flips the enabled state across all selected rules.
  * Dynamic Row CheckBox Synchronization: Inherited `ObservableObject` on `AutoTagRule` with live database synchronization and classifier refresh whenever row checkboxes are clicked directly.
- **Title Pattern / Keyword In-UI Description & Guidance**:
  * Added clear in-form descriptions and contextual tooltips across `RulesManagerView.xaml`, `NewRuleDialog.xaml`, and `ProjectsView.xaml`.
  * Informs users how title matching works: case-insensitive substring matching against active window titles (e.g. "Hospital", "Floor Plan", "Project-101"), with empty/wildcard matching any window of the chosen process.
- **Major (0-999) & Minor (0-999) Versioning System Formalization**:
  * Structured explicit single-source properties `<AppMajor>` (0-999) and `<AppMinor>` (0-999) in `Directory.Build.props` generating composite `<AppMajorMinor>` versions (`0.1`, `0.2`, `1.2`, `2.1`, `3.1`, etc.) and assembly metadata.
  * Enhanced `AppVersionHelper` with typed `Major` and `Minor` properties with 0-999 bounds validation and dynamic timestamp propagation.
- **Timeline Row Selection & Quick-Create Dialog Isolation**:
  * Fixed unexpected modal dialog popup when clicking activity rows in the daily timeline grid.
  * Added `_isPopulatingChoices` guard and removed fallback to create action in `TimelineViewModel.cs` so modal creation dialogs ONLY open on explicit user actions (clicking `➕ Create New...` in the dropdown or the `➕ New` button).
  * Added thread-safe synchronization (`SemaphoreSlim`) in `ProjectsViewModel` collection loaders.
- **Test Suite Expansion**:
  * Unit tests covering multi-monitor priority resolution, sub-activity assignment, priority reordering, session interval consolidation, dual-assignment rule filtering, offline gap detection, idle timeout snapping/display, slider reset commands, quick-create dropdown actions, grid row selection dialog isolation, project auto-edit mode with rule correlation, smart rules multi-select batch operations (toggle, duplicate, delete), smart rules single-row auto-edit mode, and AppVersionHelper Major/Minor bounds validation (23 passing tests).

## [v0.1] - Initial Release & Tracking Engine
- **Status**: Completed
- **Target**: v0.1

### Implemented
- **Solution & Build Automation**: Single source of truth versioning in `Directory.Build.props` with dynamic compilation timestamping (`$(AppMajorMinor)-$(AppBuildTimestamp)`).
- **Core Native Win32 Engine**:
  * Event hook (`SetWinEventHook`) for `EVENT_SYSTEM_FOREGROUND` transitions.
  * Shell32 process name and title extraction.
  * Multi-monitor topology enumeration (`EnumDisplayMonitors`, `GetMonitorInfo`).
- **3-Input Hybrid Inactivity Sensing**:
  * Keyboard & Mouse idle detection via `User32.GetLastInputInfo`.
  * Hardware microphone peak audio level metering via `NAudio.CoreAudioApi` (zero recording, 100% privacy preserving).
  * Automatic `Meeting Mode` detection to keep status active during voice calls/meetings.
- **Smart Dwell-Time Filter & Aggregator**:
  * Dwell-time debouncer filtering rapid window focus hops (< 2.5s).
  * Continuous timespan chunker merging activity into discrete `ActivityTimeSpan` records.
- **Persistent Smart Classification Rules**:
  * Rule engine matching application processes and title regex/keywords.
  * Priority ordering for rule matching.
  * 1-Click "Assign & Remember" learning with retroactive application over historical records.
- **Daily Timeline & Weekly Timesheet Matrix**:
  * Visual daily timeline with category & project tagging.
  * Weekly Project x Day (Monday-Sunday) matrix with daily totals.
  * One-click CSV timesheet export.
- **Extensible C# Plugin SDK**:
  * `IIdleWorkPlugin` interface for 3rd-party and custom data enrichment.
  * Native plugins for Microsoft Teams meeting context, Outlook calendar sync, and PowerAutomate webhooks.
- **Modern Fluent UI Shell**:
  * Windows 11-inspired Dark Theme palette with clean typography and rounded cards.
  * Real-time dashboard with microphone volume level and recent activity feed.
  * In-app `MilestonesWindow.xaml` modal inspector.
- **Automated Test Suite**:
  * 9 unit tests covering rule classification, priority ordering, retroactive learning, timesheet matrix aggregation, CSV export, and document title parsing.

### Fixed
- **NAudio CoreAudio `InvalidCastException` Spam**: Hardened `AudioLevelService` by switching the worker thread to `ApartmentState.STA` and wrapping endpoint/meter queries in graceful try-catches. When an audio device or virtual driver does not implement the `IAudioMeterInformation` COM interface, the service falls back gracefully with a 30s backoff and does not spam exceptions.
- **WPF UI Launch Resilience**: Updated `App.xaml` to reference `DarkTheme.xaml` via explicit WPF pack URI (`pack://application:,,,/IdleWork.App;component/Themes/DarkTheme.xaml`) and switched `App.xaml.cs` to explicit programmatic `MainWindow` instantiation and activation with diagnostic logging (`startup.log`).
- **UI Thread Synchronization Deadlock**: Replaced synchronous `.Wait()` in `RuleClassifierService` with non-blocking async cache refresh `_ = RefreshRulesAsync()`, and refactored `DatabaseService` to non-blocking async initialization (`_initTask`), eliminating UI thread constructor freezing.

