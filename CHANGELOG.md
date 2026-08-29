# Changelog

All notable changes to **C:Watch** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-08-29

### Initial Production Release

#### Added
- **Storage Telemetry Dashboard**: Live capacity meters, burn rate calculations, trend analysis, and storage category heatmaps.
- **Storage Explorer**: Hierarchical directory inspection with breadcrumb path navigation, real-time search filtering, and explorer integration.
- **Largest Files Locator**: Rank and identify large space-consuming files across your drive with color-coded size tiers and category filter tabs.
- **Duplicate Files Finder**: Multi-phase SHA-256 byte hash detection with smart selection presets (`Keep Newest`, `Keep Oldest`, `Select All`) and safe deletion workflow.
- **Storage Timeline & Differential Growth**: Differential delta inspection between snapshots to pinpoint which specific directories grew or shrank over time.
- **Recurring Growth Detector**: Identifies regenerating caches (npm, pip, Gradle, Docker, build artifacts) with calculated regrowth velocities (e.g. `+850 MB / week`).
- **Recommended Safe Cleanup Engine**: Transparent safety ratings (`SAFE`, `LOW RISK`, `REVIEW`, `DO NOT DELETE`) with plain-English human explanations, dry-run modal, and live progress reporting.
- **Storage Intelligence Reports**: Generates executive diagnostics with KPI metrics and one-click export to standalone styled HTML reports.
- **Preferences & Background Monitoring**: Configurable background monitoring sampling intervals, low disk warnings, and snapshot archive retention limits.
- **100% Offline & Local**: Zero network telemetry and zero external telemetry tracking.
