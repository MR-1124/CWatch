# Changelog

All notable changes to **C:Watch** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
