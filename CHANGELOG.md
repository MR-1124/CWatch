# Changelog

All notable changes to **C:Watch** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.1] - 2026-09-21

### Added
- **Splash screen**: branded window appears instantly at launch with staged status (services, settings, drive history) and fades into the main window. Startup initialization now runs behind it, so the app never shows a blank frame.

### Fixed
- **Restore defaults never persisted**: it swapped the settings service's live object reference and saved immediately; the service kept serializing the old instance. Reset now stages defaults like any edit and requires Save.
- **Drive change was silent**: picking a new target drive saved it, but the dashboard, labels, and monitoring kept the old drive until restart. Saving settings now re-targets the running app immediately.
- **Fire-and-forget saves** surfaced no failure. Settings now save through an explicit Save action with a pending-changes bar, working Save button, and discard option.

### Changed
- **Settings page reworked**: edits apply only on Save (clone-based staging with change detection), plain-language labels, theme options without emoji, and a two-column layout for scanning rows.
- **Performance**: exclusion globs are parsed once and cached instead of per path evaluation; the Cleanup page caches scan results for five minutes instead of rescanning the filesystem on every visit.
- **Removed dead settings**: `StartWithWindows`, `StartMinimized`, `TrayModeEnabled`, `RequireCleanupConfirmation`, and `ShowAdvancedCleanupProviders` were serialized but read by nothing; deleted from the model and Settings page.

### Added
- **Multi-Drive Support**: New Target Drive selector in Settings drives all scanning, snapshots, background monitoring, trends, and reports. Recycle Bin cleanup now enumerates every ready local volume instead of only C:.
- **Dependency Injection**: Application composition moved to `Microsoft.Extensions.DependencyInjection` (`AppComposition.BuildServices`) with a test-override callback; ViewModels and services are now resolvable and unit-testable. The test suite references the full UI project and asserts the composition graph.
- **Hardened Path Safety**: `PathSafetyValidator` now rejects descendants of protected subtrees (Program Files, Windows, ProgramData, personal libraries), 8.3 short-name aliases (C:\PROGRA~1), Win32 trailing dot/space evasion, UNC/device paths, and junction/symlink cleanup targets - with a sanctioned carve-out for `Windows\Temp`. 8.3 aliases of protected roots are pre-registered via GetShortPathName.

### Added
- **Excluded Paths Enforcement**: `AppSettings.ExcludedPaths` (glob patterns, `**` recursive) is now actually enforced — previously the setting was read but ignored. `FileSystemScanner` skips excluded directories and files in all three scan modes; `CleanupEngine` filters candidates out of recommendations and re-validates before every execution step (defense-in-depth against settings changing between scan and execute).
- **Cancellable Long Operations**: The Duplicate Finder and Cleanup Engine now accept `CancellationToken`s, and both pages have working Cancel buttons that stop the operation mid-run.
- **Storage Explorer O(1) Breadcrumbs**: `StorageItem` gained a `Parent` back-reference populated during scanning; `ExplorerViewModel` no longer walks the whole tree per breadcrumb update.

### Changed
- **Installer redesign**: `CWatch.Installer` now uses the same "pressure gauge" tokens, type scale, button styles, and sentence-case copy as the main app.
- **Automatic snapshot pruning**: `PruneOldSnapshotsAsync` now runs at startup and after every recorded snapshot, honoring `Settings.RetentionDays` (previously defined but never called).
- **SQLite schema versioning**: `DatabaseManager` tracks `PRAGMA user_version` with a migration chain. Future schema changes upgrade existing databases; databases from newer app versions are rejected instead of silently corrupted. Legacy pre-versioning databases (version 0) are adopted automatically.
- **Visual redesign - "pressure gauge" instrument system**: tuned ink/mist palette with a single pressure-signal orange (`#F2632B` family, light-theme variant deepened), Segoe UI Variable type scale with tabular figures, radius-as-hierarchy (cards 6 / controls 4 / chips 2), Segoe Fluent/MDL2 vector icons replacing emoji, sentence-case copy across all nine pages, segmented capacity bar with 90% red line and state-colored fill, theme-aware growth-delta chips, keyboard focus rings on all interactive controls, and safety colors now paired with a colored rule (never color alone). Removed the decorative "Audit: pass" pill and hard-coded hex colors in value converters (they broke the light theme).
- `CategoryClassifier` classifies volume-root anchors (Windows, Program Files, ProgramData, Users) on any drive letter, not just C:.
- WPF drive labels (sidebar pill, telemetry bar, dashboard identity) are bound to the live `DriveStatus` instead of hardcoded text.

---

## [1.0.1] - 2026-08-30

### Fixed & Refined
- **Fixed XAML StaticResource Exception**: Resolved missing `ListViewItem` resource lookup that caused cascading error dialogs upon navigating to Storage Explorer and Largest Files locator.
- **Enhanced UI Exception Handling**: Added debouncing in `DispatcherUnhandledException` to prevent modal popup cascading loops during unexpected runtime rendering glitches.
- **Added Global `InverseBooleanConverter`**: Replaced visibility binding on `Button.IsEnabled` with type-safe boolean conversion.
- **Enhanced Storage Explorer**: Added live category filters (`ALL`, `DEV / BUILD`, `APP DATA`, `SYSTEM`, `MEDIA`), interactive sorting (`SIZE ↓`, `NAME A-Z`, `ITEMS ↓`), instant search with clear button, directory telemetry statistics, and empty state handling.
- **Enhanced Largest Files Locator**: Added minimum size threshold filters (`ALL`, `>10 GB`, `>1 GB`, `>500 MB`), instant Recycle Bin deletion with in-memory list updates, and empty state guidance.
- **Enhanced Duplicate Files Analyzer**: Added scan target selector (`User Profile`, `Downloads`, `Documents`), per-group wasted space metrics, and live deletion feedback.
- **Enhanced Storage Timeline**: Added point-in-time snapshot capture button (`📸 RECORD SNAPSHOT`), growth delta filters (`GROWTH ONLY`, `FREED ONLY`), and capacity exhaustion forecasting.
- **Enhanced Recurring Growth Detector**: Added 1-click mitigation command copy for npm, pip, docker, nuget, cargo, and gradle caches, along with daily regrowth velocities.
- **Enhanced Safe Cleanup Engine**: Added safety level filter chips (`ALL`, `100% SAFE ONLY`, `CAUTION REQUIRED`, `DEV CACHES`), pre-cleanup dry-run confirmation dialog, and post-cleanup space reclaimed celebration banners.
- **Enhanced Diagnostic Reports**: Added Storage Health Scorecard (0–100 score + status rating) and live export feedback.

---

## [1.0.0] - 2026-08-29

### Initial Production Release

#### Added
- **Nordic Precision Cockpit Design System**: Modern high-density layout with tabular typography, customizable themes (`Dark`, `Light`, `System`), and dynamic `{DynamicResource}` token styling.
- **Storage Telemetry Dashboard**: Live capacity meters, burn rate calculations, trend analysis, and storage category heatmaps.
- **Storage Explorer**: Hierarchical directory inspection with breadcrumb path navigation, real-time search filtering, and explorer integration.
- **Largest Files Locator**: Rank and identify large space-consuming files across your drive with color-coded size tiers and category filter tabs.
- **Duplicate Files Finder**: Multi-phase SHA-256 byte hash detection with smart selection presets (`Keep Newest`, `Keep Oldest`, `Select All`) and safe deletion workflow.
- **Storage Timeline & Differential Growth**: Differential delta inspection between snapshots to pinpoint which specific directories grew or shrank over time.
- **Recurring Growth Detector**: Identifies regenerating caches (npm, pip, Gradle, Docker, build artifacts) with calculated regrowth velocities.
- **Recommended Safe Cleanup Engine**: Transparent safety ratings (`SAFE`, `LOW RISK`, `REVIEW`, `DO NOT DELETE`) with plain-English human explanations, dry-run modal, and live progress reporting.
- **Storage Intelligence Reports**: Generates executive diagnostics with KPI metrics and one-click export to standalone styled HTML reports.
- **Preferences & Background Monitoring**: Configurable background monitoring sampling intervals, low disk warnings, and snapshot archive retention limits.
- **Security & Reliability Hardening**: Reparse point symlink traversal guards, strict path safety validators, undoable `SHFileOperation` Recycle Bin deletion, Restart Manager session cleanup, and SQLite concurrency locks.
- **100% Offline & Local**: Zero network telemetry and zero external telemetry tracking.
