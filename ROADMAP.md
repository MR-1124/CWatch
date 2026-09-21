# C:Watch Roadmap

Where the project is going, and where help is wanted. Anything not marked
as claimed is open — comment on an issue or open one before starting big work.

## Good first issues

Small, well-scoped, and a good way to learn the codebase. The test suite
(99 tests and growing) makes these safe to attempt.

- **Add a dark/light/system preview to the Settings theme picker** —
  `SettingsViewModel` already tracks the value; show three small swatches.
  Files: `Views/SettingsView.xaml`.
- **Show scan progress percent** — `ScanProgressInfo.EstimatedPercent` exists
  but nothing binds to it. Wire it to the top-bar progress display.
  Files: `Views/` + `MainViewModel`.
- **Sort the cleanup candidate list by clicking column headers** — the
  collection is already filterable; add sort state.
  Files: `Views/CleanupView.xaml` + `CleanupViewModel`.
- **Add a "copy path" item to Explorer context menus** — one command, one
  menu item. Files: `ExplorerViewModel`, `Views/ExplorerView.xaml`.
- **Localize the number formatter** — `ByteSizeFormatter` assumes en-US;
  make unit strings culture-aware. Files: `CWatch.Core`.

## Feature candidates

Designed enough to start, open enough to shape.

- **Treemap storage visualization** — a real treemap control for the Explorer
  as an alternative to the tree list. Squarified treemap layout is a fun
  geometry problem.
- **Per-folder growth alerts** — extend `RecurringGrowthDetector` to watch
  user-pinned folders, not just detected patterns.
- **WizTree/Everything-style MFT fast scan mode** — read the NTFS Master File
  Table directly for a 100x faster full-drive scan. Requires raw volume
  access and careful admin-elevation UX. High effort, high impact.
- **Scheduled automatic cleanups** — let users run the Safe cleanup set
  weekly. Needs the CleanupEngine dry-run loop plus a scheduler.
- **Command-line interface** — `cwatch scan --json` for scripting and CI
  integration. Most services are already headless-safe behind interfaces.
- **Cloud-drive awareness (OneDrive/Google Drive)** — detect placeholder
  files and cloud-sync folders so cleanup never breaks sync state.

## Under the hood

- **Raise `FileSystemScanner` test coverage** — size aggregation, junction
  skipping, cancellation, progress reporting all need fixture-based tests.
- **Destructive-path coverage for cleanup providers** — temp-dir fixtures
  proving each provider deletes exactly what it claims.
- **ViewModel test layer** — ViewModels are DI-resolvable now; test their
  filter/sort/delete logic without the UI.
- **MSIX / Microsoft Store packaging** — an alternative distribution channel
  with auto-updates.

## Non-goals

- Telemetry, accounts, or any network feature — C:Watch is offline by design.
- Cross-platform (macOS/Linux) — the deep Windows integration is the product.
- Real-time filesystem watching of the whole drive — noisy and expensive;
  targeted monitoring via the detector is the chosen approach.
