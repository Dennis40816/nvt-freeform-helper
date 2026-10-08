# Test Category Guide
Last updated: 2026-07-20

`tests/FreeformHelper.Tests` is organized by responsibility layer, so a single directory does not fill up with test files.

## Directory Categories
- `Application/Cad`: CAD import, bucketing, and merge logic
- `Application/Dxf`: DXF import, mapping, overlap, and layer grid builder
- `Application/Freeform`: Freeform detection and statistics
- `Application/Notch`: Notch calculation and export core
- `Application/Pad`: Pad match, override, and grid creation
- `Application/Project`: Project file migration, save, and load store
- `Application/Sizing`: Manual sizing and scope behavior
- `Application/Workflow`: Workflow pipeline dependencies and invalidation
- `Infrastructure/Console`: console link parser
- `Infrastructure/Logging`: log format and NLog configuration
- `UI/TestHost`: Avalonia headless test app entry
- `UI/Smoke`: UI startup smoke tests
- `UI/ViewModels`: ViewModel command and state tests
- `UI/Canvas`: Pad canvas hit-test, cache, and invalidation
- `UI/Snapshots`: UI layout guard and rendered/hash snapshot
- `Snapshots`: baseline json (snapshot baselines)

## Common Test Commands
```powershell
# lint (by default, only checks current changes)
./scripts/tests/lint.ps1

# lint full scan (includes historical files)
./scripts/tests/lint.ps1 -AllFiles

# lint full scan (if FreeformHelper.UI.exe file lock occurs)
./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost

# lint full solution analyzer (for CI/before release)
./scripts/tests/lint.ps1 -AllFiles -AnalyzerScope Solution

# full run
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj

# Notch related (class name filter)
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~Notch"

# Notch core data path (15 classes; includes golden, generator, export, simulation, projection, settings, Runtime Query)
./scripts/tests/run-tests.ps1 -Group notch-core -UseNoAppHost

# Notch checked-in golden (3635 V21/V22 C, 3635/TM8.1 snapshot, TM8.1 acceptance matrix)
./scripts/tests/run-tests.ps1 -Group notch-golden -UseNoAppHost

# UI snapshot/guard related
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~Ui"

# UI baseline dry-run (preview changes, do not write files)
./scripts/tests/update-ui-baseline.ps1 -Mode DryRun

# UI baseline apply (update baseline json directly)
./scripts/tests/update-ui-baseline.ps1 -Mode Apply
```

## Fixed Test Order for Refactoring (Minimum Set)
Each time you do a refactor, run the following minimum regression order:

1. `notch-golden`
2. `application`
3. `ui-core`
4. `smoke`

You can use the one-click gate script directly:

```powershell
# includes lint + build + notch-golden/application/ui-core/smoke
./scripts/tests/run-refactor-gate.ps1

# same as above, but uses UseAppHost=false consistently (avoids apphost/exe locks)
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost

# Core Notch/Application/Domain/Settings/RuntimeQuery/VM slice: replace notch-golden stage with notch-core
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -IncludeNotchCore

# lint runs full solution analyzer instead
./scripts/tests/run-refactor-gate.ps1 -LintAllFiles -LintAnalyzerScope Solution

# additionally run startup budget gate (workspace.initial-grid-built <= 1000ms)
./scripts/tests/run-refactor-gate.ps1 -IncludeStartupBudget -InitialGridBudgetMs 1000

# run startup budget gate alone
./scripts/tests/check-startup-budget.ps1 -SkipBuild -InitialGridBudgetMs 1000

# Production source code-size baseline contract (line/path semantics + signed reference)
./scripts/tests/check-code-size-baseline.ps1

# if you only want to rerun the test sequence (skip lint/build)
./scripts/tests/run-refactor-gate.ps1 -SkipLint -SkipBuild
```

Output:
- `build/test-gate/refactor-gate-summary.json`

