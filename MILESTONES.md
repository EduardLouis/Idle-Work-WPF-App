# Milestones & Version History

## [v0.004] - Full-Codebase Performance Hardening, Async Safety, Native GDI Leak Prevention & Multi-Monitor Precision
- **Status**: Completed
- **Target**: v0.004

### Implemented & Enhanced
- **Async Void Elimination & Safe Fire-and-Forget Architecture**:
  * Created `TaskExtensions.SafeFireAndForget(string callerName)` with automatic exception logging via `LoggingService.Instance.LogError`.
  * Eliminated dangerous `async void` methods across `WeeklyTimesheetViewModel.cs`, `TimelineViewModel.cs`, `RulesManagerViewModel.cs`, and `LiveTrackerViewModel.cs`, converting them to robust, awaitable `async Task` methods.
  * Resolved all CS4014 unawaited task warnings in `MainViewModel.cs` and `WeeklyTimesheetViewModel.cs`.
- **COM Object Resource Management & Nullable Safety**:
  * Added `SafeReleaseCom(object? comObj)` pattern in `OutlookInteropService.cs` ensuring Outlook Explorer, Inspector, MailItem, and Selection COM objects are deterministically released from memory with `Marshal.ReleaseComObject`.
  * Fixed CS8602 dereference of possible null reference warnings.
- **Native GDI Leak Prevention & Automated Screenshot Retention Policy**:
  * Enforced zero-leak GDI resource lifecycle in `ScreenshotService.cs` using deterministic nested `try/finally` blocks for `hdcScreen`, `hdcMem`, `hBitmap`, and `hOld`.
  * Implemented automated screenshot retention management (`RetentionDays`: Forever, 7d, 14d [default], 30d, 60d, 90d) with background startup purging (`PurgeOldScreenshots()`), live storage usage metrics (`GetStorageStats()`), and manual storage purge (`ClearAllScreenshots()`).
  * Added retention controls, live storage badge, and cleanup actions to Preferences UI (`SettingsView.xaml` and `SettingsViewModel.cs`).
- **High-Performance Win32 Process Caching & Multithreading Protection**:
  * Implemented high-speed 30-second TTL process image cache in `Shell32.cs` using `QueryFullProcessImageName` with `PROCESS_QUERY_LIMITED_INFORMATION`, eliminating recurring process inspection overhead and GC pressure during `EnumWindows` sweeps.
  * Wrapped cache access with reader/writer synchronization for thread safety.
- **Negative Coordinate Multi-Monitor Geometry & Intersection Precision**:
  * Fixed multi-monitor window assignment in `WindowTrackerService.cs` for complex desktop layouts (e.g. secondary monitors positioned above or to the left of the primary monitor resulting in negative virtual coordinates).
  * Replaced center-point heuristic with 2D rectangular intersection area calculation, accurately assigning windows to the monitor containing the largest overlapping surface area.
- **Database Batch Querying & SQLite Performance Indexes**:
  * Added composite indexes on `ActivityTimeSpans(StartTime, EndTime)` and `ActivityIntervals(ActivityId)` in `DatabaseService.cs`.
  * Implemented `GetIntervalsForActivitiesAsync(IEnumerable<int> activityIds)` for single-query bulk retrieval of child intervals, eliminating N+1 database queries in `TimelineViewModel.cs`.
  * Upgraded retroactive rule application to atomic batch updates via `_db.UpdateAllAsync()`.
- **Thread Serialization Lock & OS Power Event (Sleep/Resume) Handling**:
  * Added `SemaphoreSlim _saveLock` concurrency control in `ActivityAggregator.cs`, serializing database saves and ensuring `ActivityCommitted` dispatches only after persistence is complete.
  * Integrated `Microsoft.Win32.SystemEvents.PowerModeChanged` to detect system sleep (`Suspend`) and wake (`Resume`), seamlessly recording sleep periods as offline spans and preventing phantom work hours on laptops.
- **Timesheet Markdown Export & Dynamic Live Tray Tooltip**:
  * Added `ExportWeeklyTimesheetToMarkdown` in `TimesheetService.cs` and `CopyMarkdownCommand` in `WeeklyTimesheetViewModel.cs` for instant 1-click clipboard export to Jira, Slack, Teams, or daily standups.
  * Bound "📋 Copy Markdown" button in `WeeklyTimesheetView.xaml`.
  * Integrated dynamic tooltip updates in `SystemTrayService.cs` reflecting active process, elapsed duration, and state next to the Windows clock.
- **Comprehensive Quality Assurance**:
  * Verified 0 compiler errors and 0 compiler warnings.
  * All 49 unit tests passed successfully.

## [v0.003] - Multi-Check Rules Dropdown, Rich App Metadata & Visual Screenshot Engine
- **Status**: Completed
- **Target**: v0.003

### Implemented
- **Windows System Tray & Background Runtime (Near Clock)**:
  * Implemented native Win32 `Shell_NotifyIcon` integration (`SystemTrayService.cs`) placing an active tracking icon in the Windows taskbar notification area directly beside the system clock.
  * Generated a vector circular tracker badge icon with a dark navy background (`#0F172A`), vibrant cyan accent ring (`#38BDF8`), and green status dot with clock hands.
  * **Minimize to Tray**: Clicking the window minimize button (`_`) or pressing "Minimize to Tray Now" hides the application from the Windows taskbar (`ShowInTaskbar = false; Hide();`) and runs completely in the background without taskbar clutter while continuous time tracking, meeting detection, and screenshot capture remain active.
  * **Close to Tray**: Closing the window (`X`) minimizes Idle-Work to the system tray so ongoing tracking sessions are not accidentally terminated. To quit cleanly, users select "Exit Application" from the tray context menu.
  * **Interactive Left & Double Click**: Clicking or double-clicking the tray icon immediately restores and focuses the window (`WindowState.Normal`, `ShowInTaskbar = true`, `Activate()`, `Focus()`).
  * **Dark Fluent Context Menu**: Right-clicking the tray icon opens a custom dark-themed WPF ContextMenu at the cursor position with quick navigation (`Open Live Dashboard`, `Daily Timeline`, `Weekly Timesheet`, `Smart Rules`, `Preferences...`, and `Exit Application`).
  * **Preferences Card**: Added "SYSTEM TRAY & BACKGROUND RUNTIME (NEAR CLOCK)" card in Preferences (`SettingsView.xaml`) with toggles for `MinimizeToTray` and `CloseToTray`, a "Minimize to Tray Now" shortcut, and a "Test Tray Notification" trigger.
  * Added unit tests in `MultiMonitorAndConsolidationTests.cs` (all 46 tests pass).
- **Weekly Timesheet Matrix Column Alignment & Full-Width Stretch**:
  * Fixed an issue where all day columns (Mon-Sun, Total) collapsed into the first column on the far left due to `ListViewItem` defaulting to `HorizontalContentAlignment="Left"`.
  * Configured `ListView.ItemContainerStyle` in `WeeklyTimesheetView.xaml` with `<Setter Property="HorizontalContentAlignment" Value="Stretch" />` and Fluent `ContainerBorder` control template so rows fill 100% of the card width with smooth hover and selection highlights.
  * Synchronized row grid column definitions (`2.5*, *, *, *, *, *, *, *, 1.2*`) with header grid and disabled horizontal scrollbar (`ScrollViewer.HorizontalScrollBarVisibility="Disabled"`), ensuring every day column (Mon, Tue, Wed, Thu, Fri, Sat, Sun, Total) aligns cleanly under its respective header.
- **Outlook & Teams Deep Context Extraction Plugins**:
  * Researched Outlook & Teams integration methods: Classic Outlook COM Automation, New Outlook (`olk.exe`) web-architecture constraints & UI Automation/Window Title inspection, and Teams window/accessibility models.
  * Implemented `OutlookInteropService.cs`:
    - Classic Outlook (`outlook.exe`): Dynamic late-bound COM automation (`Marshal.GetActiveObject("Outlook.Application")`) extracting active open email (`ActiveInspector.CurrentItem`) and reading pane selected email (`ActiveExplorer.Selection`), retrieving Subject, Sender Name, and SMTP/Exchange email address.
    - New Outlook (`olk.exe`): Extracts email subject, sender name, and account/folder from window title patterns (e.g. `{Subject} - {Sender} - Outlook`, `Inbox - {Account} - Outlook`) with Windows UI Automation (`System.Windows.Automation`) fallback on the active WebView2 reading pane.
  * Implemented `TeamsInteropService.cs`:
    - Extracts conversation type (`Call`, `Chat`, `Meeting`, `Channel`), participant names, and meeting topics from active window titles.
    - Detects active meeting and call status via UI Automation call control inspection (`Mute`, `Leave`, `Call controls`).
  * Updated `OutlookCalendarPlugin.cs` and `TeamsIntegrationPlugin.cs`:
    - Automatically enriches `activity.DocumentName`, `activity.Category`, `activity.State`, and window titles with rich email subject/sender and Teams meeting/call context.
    - Added comprehensive unit tests in `OutlookAndTeamsPluginTests.cs` (all 44 tests pass).
- **No Horizontal Scrollbar & Multi-Row Grid Cell Text Wrapping in Daily Activities**:
  * Set `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` on the Consolidated Daily Activities `ListView` in `TimelineView.xaml` to permanently prevent horizontal scrollbars from rendering.
  * Added `ItemContainerStyle` with `<Setter Property="HorizontalContentAlignment" Value="Stretch" />` and Fluent `ContainerBorder` control template so each row stretches to fill 100% of the viewport and highlights smoothly on hover and selection (`#1E2D4A` + Accent border).
  * Converted Window Title & Document cell to a 2-row `Grid` with `TextWrapping="Wrap"` on both `WindowTitle` and `SubActivityDisplay` so long process and window titles (e.g. build hashes, long URLs, document paths) wrap cleanly across multiple lines without horizontal overflow.
  * Added `TextWrapping="Wrap"` to `TimeSpanRangeText`, `ProcessName`, `ProjectName`, and interval drawer sub-process items to guarantee zero horizontal overflow under any window width.
- **Expanded Dwell Debounce Filter (30s to 3 min Range)**:
  * Expanded dwell debounce range in `SettingsView.xaml`, `SettingsViewModel.cs`, and `AppSettings.cs` from `1.0s - 5.0s` to **30 seconds (`30s`) to 3 minutes (`180s`)** in strict **30-second steps** (`30s`, `60s / 1m`, `90s / 1.5m`, `120s / 2m`, `150s / 2.5m`, `180s / 3m`).
  * Default updated to **30.0s** across models, aggregator, and view model reset command.
  * Added `DwellDebounceDisplay` formatting with user-friendly strings (e.g. `30 seconds`, `1 minute`, `1m 30s`, `2 minutes`, `3 minutes`).
- **Multi-Monitor Sub-Activity Registration & Main Activity Preservation**:
  * Multi-monitor dwell behavior in `ActivityAggregator.cs`:
    - When a main application (e.g. Revit, AutoCAD) is active on one monitor and user switches programs on a secondary monitor, the aggregator preserves the primary activity as the main activity and registers the focused window as a `sub-activity`.
    - Main activity only changes when the user switches applications on the primary monitor or dwells beyond the debounce threshold (30s - 3 min).
    - Sub-activities are registered and saved into SQLite as discrete `ActivityInterval` entries with exact timestamps, durations, and visual evidence screenshots when changing programs on the secondary monitor.
- **Exclusion of Self Program & Screen Capture Overlays**:
  * Implemented centralized `IsIgnoredWindow` filtering in `WindowTrackerService.cs` and `ActivityAggregator.cs`:
    - Automatically excludes the time tracker app itself (`IdleWork`, `IdleWork.App`, and current process PID) so user time in IdleWork is never logged as an activity.
    - Excludes transient Windows screen snip/capture overlays (`SnippingTool`, `ScreenClippingHost`, `Snipping Tool Overlay`) so manual or system screenshots never interrupt active work sessions or become long main activity spans.
    - When an ignored or overlay window becomes focused, the tracker seamlessly preserves the user's active work session window without interrupting the active time span.
    - Added `PurgeIgnoredActivitiesAsync()` in `DatabaseService.cs` to automatically purge previously recorded self or SnippingTool activities from the SQLite database (`ActivityTimeSpans` and `ActivityIntervals`) upon startup and filter them from queries.
- **Multi-Capture Screenshot Evidence Grid & Fit-to-Window Preview**:
  * **Multi-Row & Multi-Column Table List Gallery**:
    - Replaced the single screenshot view in the right-hand Activity Details panel (`TimelineView.xaml`) with a multi-row, 2-column table list gallery (`UniformGrid Columns="2"` in a `ScrollViewer`).
    - Aggregates and displays all visual screenshots captured for the active consolidated activity: including the primary activity window screenshot and all discrete multi-monitor sub-activity intervals.
    - Each thumbnail card features an exact time badge (e.g. `13:29:44`), context label (e.g. `Main Window` or `Sub: chrome`), high-definition thumbnail preview, and 1-click enlarge trigger.
    - Displays dynamic capture count badge (e.g. `3 captures`) in the header.
  * **Window-Fitted Screenshot Preview Modal**:
    - Upgraded `ScreenshotPreviewDialog.xaml` so screenshots are automatically resized to fit within the dialog window (`Stretch="Uniform"` with no overflow).
    - Added interactive "🔍 1:1 Actual Size" vs "📐 Fit to Window" view toggle button.
    - Added title bar drag move, double-click to maximize, maximize/restore button (`🗖`), and resize grip support (`ResizeMode="CanResizeWithGrip"`).
- **Multi-Check Rules Dropdown & Shared Modal Creation**:
  * Replaced the static checkbox list and inline rule creation textboxes with a modern Fluent multi-check dropdown list (`ToggleButton` + `Popup`) in both `ProjectsView.xaml` and `NewProjectDialog.xaml`.
  * Added quick selection actions: `All` (select all rules) and `None` (clear selection) with dynamic summary text (e.g. `2 rules linked: Revit Modeling, AutoCAD Drafting`).
  * Replaced inline textboxes with a `➕ New Rule` button that launches the shared `NewRuleDialog` modal dialog, pre-linking the rule directly to the active project.
- **Rich Application & Window Metadata Engine**:
  * Win32 executable inspection via `FileVersionInfo` and `Shell32.GetRichProcessInfo`:
    - File description / Product name (e.g. `Autodesk Revit 2025`, `Visual Studio 2022`, `Google Chrome`)
    - Company name (e.g. `Autodesk, Inc.`, `Microsoft Corporation`)
    - Product / File version (e.g. `25.0.0.0`)
    - Full executable path (e.g. `C:\Program Files\Autodesk\Revit 2025\Revit.exe`)
    - Win32 Window Class name (e.g. `Afx:00400000:...`, `Chrome_WidgetWin_1`)
  * Persisted across `ActivityTimeSpan` and `ActivityInterval` in local SQLite database with dynamic fallback to process name when metadata is unavailable.
- **Native GDI Screenshot Engine & Visual Evidence System**:
  * Native GDI `BitBlt` capture via `User32` and `Gdi32` interop with lightweight JPEG compression (`ScreenshotService.cs`).
  * Monitor-aware capture: automatically detects monitor bounds for the active window handle.
  * Saved to `%LocalAppData%\IdleWork\Screenshots\{yyyy-MM-dd}\act_{key}_{timestamp}.jpg`.
  * Event-driven capture on active window switch (when dwell debounce passes).
  * Periodic interval capture while user is actively working (configurable interval, default: 5 minutes).
- **Interactive Screenshot Preview & Timeline Intelligence Drawer**:
  * Expanded timeline activity details drawer to showcase rich application metadata badges, company, version, executable path, and window class.
  * Added visual evidence thumbnail card with zoom hover overlay. Clicking the thumbnail opens `ScreenshotPreviewDialog.xaml` modal dialog with high-resolution rendering, path display, "Open in Photos", and "Open Folder" actions.
  * Added `📸 Preview` button for discrete session intervals in the expandable drawer.
- **Screenshot & Visual Context Preferences**:
  * Added "📸 SCREENSHOTS & VISUAL CONTEXT" card in `SettingsView.xaml`.
  * Master switch for visual activity screenshots.
  * Toggle for automatic capture on window switch.
  * Periodic capture interval slider with snapping to valid intervals (`Off`, `1 min`, `2 min`, `5 min` [Default], `10 min`, `15 min`, `30 min`).
  * Compact `↺ Reset` button restoring screenshot defaults with one click.
  * `📸 Open Screenshots Storage Folder` button launching the folder directly in Windows Explorer.
- **On-the-Fly Project Creation in Classification Rule Dialog**:
  * Added `➕ New Project` button in `NewRuleDialog.xaml` header and `➕ New` action button beside `ProjectsComboBox`.
  * Added `+ Add New Project...` placeholder at the bottom of the project dropdown list. Selecting this option or clicking either button opens `NewProjectDialog`.
  * Persists newly created project and correlated rules directly to SQLite database, selects the new project immediately, and synchronizes the project collection across `TimelineViewModel` and `ProjectsViewModel`.
- **Real-Time Activity Feed Deduplication**:
  * Resolved duplicate offline gap entries in `LiveTrackerViewModel.RecentActivities` caused by concurrent initialization between `DetectAndRecordAppClosedGapAsync()` and `LoadDataAsync()`.
  * Added re-entrancy guard in `DetectAndRecordAppClosedGapAsync()` and duplicate verification by activity ID and timestamps.
- **Enterprise Structured Logging & Cloud Telemetry Engine**:
  * Implemented `LoggingService.cs` providing a dual-sink logging architecture:
    - Daily rotating local file logs in `%LocalAppData%\IdleWork\Logs\idlework_YYYYMMDD.log`.
    - Asynchronous background cloud dispatch (`POST /api/v1/telemetry/events`) with in-memory retry queue and silent network failure handling.
  * Added `AppConfigEntry` table in SQLite for key-value configuration persistence (`DatabaseService.cs`).
  * Added "ENTERPRISE LOGGING & CLOUD TELEMETRY" card in `SettingsView.xaml` with `📂 Open AppData Logs Folder` button and configurable `CloudEndpointUrl`.
  * Authored comprehensive `docs/cloud_telemetry_api_spec.md` with REST API endpoints, JSON request schemas, relational & BigQuery database schemas, and complete reference implementations for ASP.NET Core and Node.js.
- **Consolidated Daily Activities Smooth Mouse Wheel Scrolling**:
  * Resolved mouse wheel scroll lock on the Consolidated Daily Activities list view in `TimelineView.xaml`.
  * Removed `ScrollHelper.BubbleMouseWheel="True"` which was intercepting preview mouse wheel events and swallowing them when no parent ScrollViewer was present.
  * Configured `ScrollViewer.CanContentScroll="False"` on `ListView` to provide smooth, pixel-based scrolling when rows have varying heights (e.g. when discrete session drawers are expanded).
  * Upgraded `ScrollHelper.cs` with smart ancestor and boundary detection, ensuring it never suppresses mouse wheel events if no outer ScrollViewer exists or while an inner control can still scroll in the requested direction.
- **Test Suite Expansion & Thread Safety**:
  * Added thread-safe `AssignmentChoicesLock` and `GetAssignmentChoicesSnapshot()` in `TimelineViewModel.cs` to eliminate collection modification concurrency during background queries.
  * 31 passing unit tests across tracking, screenshot generation, versioning, and rule correlation.

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
- **ComboBox Selection Display Fix & Editable Template**:
  * Resolved WPF bug where editable ComboBoxes rendered selected items blank due to missing `PART_EditableTextBox` in custom `ControlTemplate`.
  * Added `ComboBoxEditableTextBox` style and wired `PART_EditableTextBox` with automatic visibility toggle when `IsEditable="True"` in `DarkTheme.xaml`.
  * Fixed `NewRuleDialog` project selector so chosen project appears immediately upon selection.
- **Dedicated Categories & Tags Management Subpages**:
  * Added Fluent `TabControl` to `ProjectsView.xaml` organizing project administration into 3 subpages: `📁 Projects & Accounts`, `🏷️ Categories`, and `🔖 Tags`.
  * Created `WorkCategory` and `WorkTag` entity models with full SQLite persistence and auto-seeding of standard defaults (`BIM`, `Development`, `Design`, `Admin`, `Meeting`, `QA/QC`, `Revit`, `AutoCAD`, `3D`, `2D`, `Billable`, `Internal`, etc.).
  * Built complete CRUD cards for Categories (with visual color presets/chips) and Tags (with formatted badge chips) including auto-edit mode on row selection.
- **Integrated Category & Tag Selectors Across Rule Dialogs & Smart Rules Manager**:
  * Upgraded `NewRuleDialog.xaml` and `RulesManagerView.xaml` to feature editable ComboBox dropdowns for Target Category and Target Tags.
  * Allows users to select directly from predefined categories/tags or type custom values on the fly.
  * Cached and synced category and tag lists in `TimelineViewModel` and `RulesManagerViewModel`.
- **Categories & Tags Sub-Tabs in Smart Rules**:
  * Extended `RulesManagerView.xaml` with 2 additional sub-tabs (`🏷️ Categories` and `🔖 Tags`), making work categories and activity tags directly accessible and manageable from both the Projects view and the Smart Rules view.
  * Added full category and tag management backing in `RulesManagerViewModel.cs` with color presets, selection auto-edit mode, and SQLite CRUD operations.
- **Consolidated Daily Activities Sorting Engine (Percentage, Last Active, Old Activity)**:
  * Added sort mode dropdown in `TimelineView.xaml` header for Consolidated Daily Activities with 3 modes: `📊 Percentage (% / Duration)` (highest duration first), `🕒 Last Active (Recent First)` (latest EndTime first), and `⏮️ Old Activity (Earliest First)` (earliest StartTime first).
  * Added `ActivitySortMode` enum (`Percentage = 0`, `LastActive = 1`, `OldActivity = 2`), `SelectedSortMode`, `SelectedSortIndex`, `SetSortModeCommand`, and `ApplyActivitySorting()` in `TimelineViewModel.cs` with active selection preservation.
  * Added structured column headers above the activity list view (`Time Range`, `Application`, `Window Title / Document`, `Project`, `Share`, `Duration`) aligned with item cards.
  * Thread-safe query cancellation using `CancellationTokenSource` and lock in `LoadTimelineAsync()` preventing concurrent async execution or stale date query race conditions during date navigation or refresh.
- **Test Suite Expansion**:
  * Unit tests covering multi-monitor priority resolution, sub-activity assignment, priority reordering, session interval consolidation, dual-assignment rule filtering, offline gap detection, idle timeout snapping/display, slider reset commands, quick-create dropdown actions, grid row selection dialog isolation, project auto-edit mode with rule correlation, smart rules multi-select batch operations (toggle, duplicate, delete), smart rules single-row auto-edit mode, AppVersionHelper Major/Minor bounds validation, Category/Tag CRUD in DatabaseService, ProjectsViewModel subpage operations, RulesManagerViewModel Category/Tag subpage operations, and TimelineViewModel activity sorting modes (27 passing tests).

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

