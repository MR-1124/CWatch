# Contributing to C:Watch

Thank you for your interest in contributing to **C:Watch**! We welcome contributions from the community to help make disk storage intelligence and safe cleanup on Windows faster, safer, and more insightful.

---

## 🚀 Getting Started

### Prerequisites
- **Operating System**: Windows 10 (Build 19041+) or Windows 11
- **.NET SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- **IDE**: Visual Studio 2022 (with .NET desktop development workload), JetBrains Rider, or VS Code with C# Dev Kit.

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/MR-1124/CWatch.git
cd CWatch

# Restore dependencies and build
dotnet build CWatch.sln -c Debug

# Run test suite
dotnet test tests/CWatch.Tests/CWatch.Tests.csproj
```

### Running Locally
```powershell
# Launch the desktop UI
dotnet run --project src/CWatch.UI/CWatch.UI.csproj
```

---

## 🏗️ Solution Architecture

The solution follows a clean, modular architecture:

| Project | Purpose |
| :--- | :--- |
| **`CWatch.Core`** | Domain models, enums (`SafetyLevel`, `StorageCategoryType`), contracts, and byte formatting utilities. |
| **`CWatch.Storage`** | SQLite snapshot storage, repository implementations, and historical migration management. |
| **`CWatch.Analysis`** | Differential delta engine, growth analyzer, trend forecasting, classifier rules, and report generation. |
| **`CWatch.Cleanup`** | Safe cleanup providers (System Temp, Node/npm, Pip, Gradle, Docker, Windows Caches), dry-run engine, and execution safeguards. |
| **`CWatch.Infrastructure`** | Win32 API interop (File Explorer integration, disk telemetry, process locking detection, file properties). |
| **`CWatch.Monitoring`** | Background storage timer and threshold alarm service. |
| **`CWatch.UI`** | WPF MVVM desktop user interface, Nordic Terminal design tokens, viewmodels, views, and value converters. |
| **`CWatch.Tests`** | Unit and integration test suite (27 passing tests covering core, analysis, cleanup, and storage). |

---

## 🛠️ Development Workflow

1. **Create a Feature Branch**:
   ```powershell
   git checkout -b feature/your-feature-name
   ```
2. **Coding Standards**:
   - Write clean, idiomatic C# with nullable reference types enabled.
   - Maintain separation of concerns: UI views strictly bind to ViewModels; business logic resides in `Analysis` or `Cleanup`.
   - Never delete user files without explicit safety checks and user confirmation.
3. **Commit Conventions**:
   Follow [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat: add docker buildx cache cleanup provider`
   - `fix: resolve file locking issue in explorer view`
   - `docs: update setup guide in readme`
   - `test: add unit test for linear trend analyzer`
4. **Pull Requests**:
   - Open a PR against the `main` branch.
   - Fill out the PR template with a clear description and testing steps.
   - Ensure all automated tests pass (`dotnet test`).

---

## 🐛 Reporting Bugs & Requesting Features

- **Bug Reports**: Please include your Windows version, steps to reproduce, and any relevant log snippets from `%LOCALAPPDATA%\CWatch\Logs\`.
- **Feature Requests**: Describe the problem you are trying to solve and your proposed solution.

---

## 📜 License

By contributing to C:Watch, you agree that your contributions will be licensed under the project's [MIT License](LICENSE).
