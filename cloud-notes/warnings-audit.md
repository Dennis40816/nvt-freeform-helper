# Warnings audit (November warning-elimination goal)

- Date: 2026-11-05 (cloud run, scheduled routine). Repo HEAD: `5e48bec` (detached). Nothing under `src/` or `tests/` was changed; this file is the only commit.
- Toolchain: .NET SDK 10.0.301 (installed with `dotnet-install.sh` to a scratch dir; the box had no `dotnet`), `python scripts/fetch_core_packages.py` run first (all 3 Nvt.Core packages downloaded and SHA-verified), `dotnet restore FreeformHelper.sln`.
- Commands: `dotnet build FreeformHelper.sln -c Debug|Release --no-restore --no-incremental -p:TreatWarningsAsErrors=false`. `TreatWarningsAsErrors` is `true` in `eng/core-health/Directory.Build.props`; I turned it off only so every warning is listed instead of stopping at the first. Both builds finished with **0 errors**.
- **Not run:** `eng/core-health/repo-health.ps1` (and the `scripts/*.ps1` helpers such as `prepare-ui-workspace.ps1`/`lint.ps1`), because `pwsh` is not available here. The ledger numbers below are read straight from the JSON files, not verified by the script. The full 25-minute `-Mode Verify` run still has to happen before any fix PR.
- **Not run:** tests (out of scope). Dependency graph `docs/generated/project-dependency-graph.md` was checked first; it is dated 2026-10-29 and lists the same 5 projects as `FreeformHelper.sln`, so I treated it as current (could not regenerate it without pwsh).

## 1. Headline numbers

| | Debug | Release | Union (distinct file/line/rule) |
|---|---:|---:|---:|
| Compiler (CS) warnings | 0 | 0 | 0 |
| Analyzer / style / build warnings | 350 | 351 | **351** |

The raw build log prints each warning about 4 times (project builds repeated by the solution), so the raw line count (700 / 702) is not the number to quote. Counts here are de-duplicated on file, line, column, rule and project.

- 350 warnings appear in both configurations. The 1 extra Release warning is `src/FreeformHelper.UI/Program.cs:83` (`RS0030`, `Environment.GetEnvironmentVariable`) inside the `#else` (non-DEBUG) branch.
- **Every one of the 351 warnings is already in the accepted-debt list.** Each rule ID emitted is listed for its project in `eng/code-health/projects.props` (`HealthBaselineWarningIds`, applied through `WarningsNotAsErrors`). So with the repo's real settings the build is green, and there are no new un-baselined warnings. The goal is therefore to shrink the baseline to zero, not to stop a regression.
- Only the Linux build was possible. Code under `#if WINDOWS`/Windows-only paths or Windows-only RIDs is not compiled here and could carry warnings that this audit cannot see.

## 2. Counts by rule ID

| Rule | Count | Share | Meaning |
|---|---:|---:|---|
| `RS0030` | 296 | 84.3% | Banned API (`BannedSymbols*.txt`): direct clock, delay, file, env, process, Dispatcher, reflection |
| `VSTHRD003` | 33 | 9.4% | Awaiting a Task not started in this context (Threading analyzer) |
| `IDE1006` | 12 | 3.4% | Naming: private static/const field needs `_` prefix |
| `VSTHRD002` | 4 | 1.1% | Synchronous wait on a Task |
| `VSTHRD200` | 2 | 0.6% | Missing `Async` suffix |
| `EnableGenerateDocumentationFile` | 1 | 0.3% | IDE0005 needs `GenerateDocumentationFile=true` (project-level, no source location) |
| `IDE0052` | 1 | 0.3% | Private member assigned but never read |
| `VSTHRD103` | 1 | 0.3% | Sync call where an async one exists |
| `IDE0330` | 1 | 0.3% | Use `System.Threading.Lock` |
| **Total** | **351** | 100% | |

### RS0030 by banned symbol

| Symbol | Count |
|---|---:|
| `BindingFlags.NonPublic` | 124 |
| `Stopwatch` | 36 |
| `Dispatcher.UIThread` | 32 |
| `Environment.GetEnvironmentVariable(string)` | 19 |
| `Stopwatch.GetTimestamp()` | 14 |
| `DateTime.UtcNow` | 10 |
| `Task.Delay(int)` | 10 |
| `Task.Delay(int, CancellationToken)` | 9 |
| `DateTimeOffset.UtcNow` | 8 |
| `File.WriteAllText(string, string?)` | 6 |
| `Environment.TickCount64` | 6 |
| `Task.Delay(TimeSpan)` | 3 |
| `Process.Start()` | 3 |
| `Environment.SetEnvironmentVariable(string, string?)` | 3 |
| `File` | 2 |
| `File.ReadAllText(string)` | 2 |
| `File.ReadAllBytes(string)` | 2 |
| `File.WriteAllTextAsync(string, string?, Encoding, CancellationToken)` | 2 |
| `File.ReadAllLines(string)` | 1 |
| `Environment.GetFolderPath(Environment.SpecialFolder)` | 1 |
| `Process.Start(ProcessStartInfo)` | 1 |
| `Thread.Sleep(int)` | 1 |
| `Task.Delay(TimeSpan, CancellationToken)` | 1 |

## 3. Counts by project

| Project | Layer | Total | `RS0030` | `VSTHRD003` | `IDE1006` | `VSTHRD002` | `VSTHRD200` | `EnableGenerateDocumentationFile` | `IDE0052` | `VSTHRD103` | `IDE0330` |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| FreeformHelper.Domain | domain | 0 | · | · | · | · | · | · | · | · | · |
| FreeformHelper.Application | application | 8 | 8 | · | · | · | · | · | · | · | · |
| FreeformHelper.Infrastructure | infrastructure | 2 | 2 | · | · | · | · | · | · | · | · |
| FreeformHelper.UI | presentation | 77 | 65 | 3 | 3 | 3 | 2 | · | 1 | · | · |
| FreeformHelper.Tests | tests | 264 | 221 | 30 | 9 | 1 | · | 1 | · | 1 | 1 |
| **Total** | | **351** | 296 | 33 | 12 | 4 | 2 | 1 | 1 | 1 | 1 |

Domain is clean. The Tests project holds 264 of 351 (75%); the five production projects together hold 87.

## 4. Top 20 files

| # | File | Warnings | Rules |
|---:|---|---:|---|
| 1 | `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs` | 21 | RS0030×19, IDE1006×2 |
| 2 | `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs` | 15 | RS0030×15 |
| 3 | `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs` | 12 | RS0030×6, VSTHRD002×3, IDE1006×1, VSTHRD200×1, VSTHRD003×1 |
| 4 | `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs` | 12 | RS0030×12 |
| 5 | `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs` | 12 | RS0030×12 |
| 6 | `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs` | 11 | RS0030×11 |
| 7 | `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs` | 11 | VSTHRD003×11 |
| 8 | `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs` | 11 | RS0030×11 |
| 9 | `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs` | 10 | RS0030×10 |
| 10 | `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs` | 9 | RS0030×9 |
| 11 | `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs` | 8 | RS0030×8 |
| 12 | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs` | 7 | RS0030×7 |
| 13 | `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs` | 7 | RS0030×7 |
| 14 | `tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs` | 6 | RS0030×6 |
| 15 | `tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs` | 6 | RS0030×6 |
| 16 | `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs` | 6 | RS0030×5, IDE1006×1 |
| 17 | `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingInputTests.cs` | 6 | RS0030×4, VSTHRD003×2 |
| 18 | `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs` | 6 | RS0030×3, IDE1006×2, IDE0330×1 |
| 19 | `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs` | 6 | RS0030×6 |
| 20 | `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelSimulationTests.cs` | 6 | RS0030×5, VSTHRD003×1 |

(There are 103 distinct files with at least one warning. Ties at the 20th place (5 warnings) are broken alphabetically; the project-level `EnableGenerateDocumentationFile` has no file.) The top 20 files hold 188 warnings (54%).

## 5. Debt currently accepted by the ledgers

Read directly from JSON (not verified with `repo-health.ps1`, see the top).

### 5.1 `eng/code-health/baseline.json`

- Schema 1, measurement `roslyn-physical-v1`, snapshot commit `32fb1abb` (not present in this clone; the repo is shallow, so I could not diff it against HEAD). Limits: fileLines=800, partialFiles=8, axamlCodeBehindLines=150, methodLines=80, viewTypeLines=800, stateMembers=30.
- **427 finding entries** (sum of `count` = 463), **135 entities** over the size ceilings. All owned by `NFH`; every entity points at issue `health-refactor/2026-10-31`.

| Finding rule | Entries | Sum of count | Live warning in this build |
|---|---:|---:|---|
| `RS0030` | 280 | 296 | 296 |
| `VSTHRD003` | 33 | 33 | 33 |
| `suppressions` | 32 | 32 | n/a (ledger-only metric) |
| `blockingWait` | 28 | 48 | n/a (ledger-only metric) |
| `generationFields` | 23 | 23 | n/a (ledger-only metric) |
| `IDE1006` | 12 | 12 | 12 |
| `suppressionScopes` | 7 | 7 | n/a (ledger-only metric) |
| `VSTHRD002` | 4 | 4 | 4 |
| `VSTHRD200` | 2 | 2 | 2 |
| `fakeClockDuplicates` | 1 | 1 | n/a (ledger-only metric) |
| `asyncVoid` | 1 | 1 | n/a (ledger-only metric) |
| `EnableGenerateDocumentationFile` | 1 | 1 | 1 |
| `IDE0052` | 1 | 1 | 1 |
| `IDE0330` | 1 | 1 | 1 |
| `VSTHRD103` | 1 | 1 | 1 |

The live warning counts match the ledger for every analyzer rule except `RS0030` (ledger 280 entries / count 296, live 296 distinct sites; same total) and are one-to-one for the rest. The ledger rules with no compiler warning (`blockingWait` 48, `generationFields` 23, `suppressions` 32, `suppressionScopes` 7, `fakeClockDuplicates`, `asyncVoid`) are debt that exists even when the build prints nothing.

By project (entries): FreeformHelper.Tests 277, FreeformHelper.UI 132, FreeformHelper.Application 9, FreeformHelper.Infrastructure 6, FreeformHelper.Domain 3.

Suppressions inside the ledger (all explicitly forbidden to grow by AGENTS.md): 15 `NoWarn` (1572/1573/1591 in 5 projects), 15 `WarningsNotAsErrors` (the baselined IDs per project), 2 `SuppressMessage` (CA1707 in `ScanOrder.cs`, CA1822 in `ManualSizingService.cs`), 7 attribute-scope CA1822 entries.

Size-ceiling entities: member 113, file 10, type 12; by project FreeformHelper.Application 30, FreeformHelper.Infrastructure 1, FreeformHelper.UI 69, FreeformHelper.Tests 35; ceiling type: 113 `methodLines`, the rest file/partial/state/view-size.

### 5.2 `tests/FreeformHelper.Tests/Architecture/debt-baseline.json`

Ratchet metrics (counted offenders; the architecture tests fail if one rises):

| Metric | Baseline |
|---|---:|
| `asyncVoid` | 0 |
| `asyncVoidLambdas` | 0 |
| `suppressMessage` | 2 |
| `pragmaWarningDisable` | 0 |
| `suppressUndoAssignments` | 0 |
| `fileWriteAllText` | 5 |
| `processStart` | 1 |
| `directClockReads` | 15 |
| `bareCatch` | 0 |
| `catchException` | 1 |
| `suppressFlagFields` | 4 |
| `freeformViewModelStateMembers` | 95 |
| `shellNavigationBooleans` | 0 |
| `noWarnIds` | 3 |

Layering exceptions: `domainAndApplicationAvalonia` 0, `domainAndApplicationPlatformIo` 2, `viewModelsAvalonia` 10, `controlsViewModels` 5, `viewModelsControls` 3, `servicesViewModels` 28, `viewModelsServices` 60. Compiled-binding exceptions: 51 `.axaml` files.

### 5.3 `eng/code-health/test-debt.json` (also requested by the ledger rules)

57 findings, all in `FreeformHelper.Tests`: `elapsedAssertionsInTests` 29, `timeWaitsInTests` 15, `childProcessInTests` 6, `tempPathInTests` 5, `headlessSessionSetups` 2. Snapshot `fd5916dd`.

## 6. Mechanical vs. design decision

**Mechanical** (safe batch fixes: the right answer is the same at every site, no new type or API shape):

| Rule | Sites | Why mechanical |
|---|---:|---|
| `IDE1006` | 12 | Rename `private static readonly`/`private static` field to `_camelCase` (e.g. `JsonOptions`, `PrivateInstance`, `Gate` in tests; `App.axaml.cs:17`, `StartupPerfTracker.cs:17`, `CadLoadSpinnerProcessHost.cs:32` in UI). IDE can do it; file-local. |
| `IDE0330` | 1 | `object` lock to `System.Threading.Lock` in `HeadlessSessionGuardAttribute.cs:36` (same line also has IDE1006; do both together). |
| `IDE0052` | 1 | `CadLoadSpinnerWindow._ipcLoopTask` is written but never read. Follow the "check readers first" rule (search `.axaml`/reflection/scripts) then drop the field or use it for observed completion. |
| `VSTHRD103` | 1 | `UiEventRunnerTests.cs:30` use the awaitable `RunAsync`. |
| `VSTHRD200` (rename half) | 2 | Add `Async` suffix. Public/internal rename: search readers first; rename moves the ledger fingerprint, so do it in the same change that removes the finding. |
| RS0030 test clocks/delays *where an injected `TimeProvider` already exists* | up to ~30 | Replace `DateTime.UtcNow`/`Stopwatch` with the project's existing clock seam. Only mechanical once the seam is confirmed per project (see below). |

**Needs a design decision** (one root-cause rule must be chosen first; then apply it everywhere):

| Rule / group | Sites | The decision |
|---|---:|---|
| `RS0030` `BindingFlags.NonPublic` (tests) | 124 | 124 sites reflect into private members of ViewModels/Canvas/generators. Options: `InternalsVisibleTo` + `internal` test seams, a small test-accessor type per SUT, or moving the logic to a public Application service. This changes production visibility, so it is an owner call. AGENTS.md also says tests check behavior, not private names, so some tests may be rewritten rather than re-plumbed. |
| `RS0030` `Dispatcher.UIThread` | 32 (18 UI, 14 tests) | Needs a single UI-dispatch abstraction (inject an `IUiDispatcher`/Avalonia `Dispatcher` instance). Touches threading in `PadCanvas`, view models and headless-test setup. |
| `RS0030` clocks (`Stopwatch`, `Stopwatch.GetTimestamp`, `DateTime(Offset).UtcNow`, `TickCount64`) | 74 | Choose one clock (`TimeProvider` is already used by `JsonProjectStore`, `ConsoleLinkOpener`) and how perf timers (`Stopwatch.GetTimestamp` on hot paths in `PadCanvas` and `NotchV22CompensationService.Stages.cs`) get one without allocation or behavior change. Test benchmarks need a real clock on purpose. |
| `RS0030` `Task.Delay`/`Thread.Sleep` | 24 | Delays in production (spinner host, debounce) and tests. Needs `TimeProvider.Delay`/fake timer; test timing assertions are in `test-debt.json` (`timeWaitsInTests` 15). Flakiness risk. |
| `RS0030` `Environment.*` | 23 | Product code must "find tools one way: project settings, then environment variables" (AGENTS.md). Needs one environment-settings accessor, then replace sites; tests need an isolation helper (like `TestAppSettingsIsolation`). `Program.cs:83` is Release-only. |
| `RS0030` `File.*` | 15 | Needs an approved file-IO seam; `DiffFrameCsvImporter` and `RegularVisibilityMaskService` are also listed as layering exceptions (platform IO in Application), so the fix is moving IO out of Application. |
| `RS0030` `Process.Start` | 4 | Spinner process host and test helpers (`childProcessInTests` 6 in `test-debt.json`). Needs a process-launch abstraction and security review. |
| `VSTHRD003` | 33 (30 tests, 3 UI) | Mostly awaiting a `TaskCompletionSource`/test-started task. The real fix is `JoinableTaskFactory` or a wrapper; for each site decide if the task is truly foreign. Cannot be done by a regex. |
| `VSTHRD002` + `VSTHRD200` (spinner host) + `CadLoadSpinnerProcessHost` | 4 + 2 | Sync-over-async in the spinner process host (lines 505, 629, 735). Restructuring async lifetimes; risk of deadlock/hang at app shutdown. |
| `EnableGenerateDocumentationFile` | 1 | `Directory.Build.props` deliberately sets `GenerateDocumentationFile=false` for the tests layer. Either allow it for tests or stop IDE0005 asking. A policy choice, not an edit. |

The 5 non-RS0030 "blockingWait" / "generationFields" / "suppression" ledger-only debts (see 5.1) need their own design work and are out of scope of the 351 compiler-visible warnings.

## 7. Batch plan

Rules: each batch has at most 20 entries (an entry = one warning site, `file:line`). One batch is one PR (AGENTS.md: *one batch, one PR*). Batches are ordered by risk, lowest first, so the baseline can go down early. After each batch lower the ledger with `repo-health.ps1 -Mode LowerBaseline`, never by hand, and remove a rule ID from `projects.props` only when its last site in that project is gone. Batches B-03 onward should wait for the owner's choice listed in section 6; the batches only fix the *grouping*.

| Batch | Group | Scope | Entries | Risk | Files |
|---|---|---|---:|---|---:|
| B-01 | IDE1006/IDE0330/IDE0052/EnableGenerateDocumentationFile | prod | 4 | low | 4 |
| B-02 | IDE1006/IDE0330/IDE0052/EnableGenerateDocumentationFile | tests | 11 | low | 6 |
| B-03 | VSTHRD002/103/200 blocking wait, Async suffix | prod | 5 | high | 2 |
| B-04 | VSTHRD002/103/200 blocking wait, Async suffix | tests | 2 | medium | 2 |
| B-05 | VSTHRD003 awaiting foreign Task | prod | 3 | high | 3 |
| B-06 | VSTHRD003 awaiting foreign Task | tests | 19 | medium | 4 |
| B-07 | VSTHRD003 awaiting foreign Task | tests | 11 | medium | 9 |
| B-08 | RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64) | prod | 20 | medium | 4 |
| B-09 | RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64) | prod | 11 | medium | 7 |
| B-10 | RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64) | tests | 15 | medium | 1 |
| B-11 | RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64) | tests | 19 | medium | 3 |
| B-12 | RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64) | tests | 9 | medium | 5 |
| B-13 | RS0030 Task.Delay / Thread.Sleep | prod | 9 | medium | 6 |
| B-14 | RS0030 Task.Delay / Thread.Sleep | tests | 15 | medium | 10 |
| B-15 | RS0030 Environment get/set/GetFolderPath | prod | 4 | medium | 3 |
| B-16 | RS0030 Environment get/set/GetFolderPath | tests | 19 | medium | 10 |
| B-17 | RS0030 System.IO.File.* | prod | 12 | medium | 9 |
| B-18 | RS0030 System.IO.File.* | tests | 3 | medium | 2 |
| B-19 | RS0030 Process.Start | prod | 1 | high | 1 |
| B-20 | RS0030 Process.Start | tests | 3 | high | 1 |
| B-21 | RS0030 Dispatcher.UIThread | prod | 18 | high | 14 |
| B-22 | RS0030 Dispatcher.UIThread | tests | 14 | high | 3 |
| B-23 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 12 | high | 1 |
| B-24 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 12 | high | 1 |
| B-25 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 11 | high | 1 |
| B-26 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 19 | high | 2 |
| B-27 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 18 | high | 3 |
| B-28 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 19 | high | 5 |
| B-29 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 20 | high | 10 |
| B-30 | RS0030 BindingFlags.NonPublic (tests reflection) | tests | 13 | high | 13 |

30 batches, 351 entries (= 351; none lost, none counted twice).

Risk key: **low** = file-local, compiler/IDE-verifiable, no behavior change. **medium** = needs the group's design decision but stays within test code or a single service; a regression shows up in tests. **high** = production threading/process/visibility or a public rename; can hang the UI, break shutdown, or change what tests exercise; needs owner sign-off, headless-UI smoke tests, and the full `repo-health.ps1 -Mode Verify`.

Notes on risk assignment: production `VSTHRD002/003/200` are marked high (spinner process host and view-model task flow). Test-only clock/delay/env/file batches are medium because replacing real time in tests can create flaky or slower tests (`timeWaitsInTests` is itself ledger debt). The 124-site reflection group is high not because of per-site complexity but because it forces a decision on production visibility.

### Entries per batch

**B-01** (IDE1006/IDE0330/IDE0052/EnableGenerateDocumentationFile, prod, low, 4 entries)

- `src/FreeformHelper.UI/App.axaml.cs:17` IDE1006
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:32` IDE1006
- `src/FreeformHelper.UI/Services/StartupPerfTracker.cs:17` IDE1006
- `src/FreeformHelper.UI/Views/CadLoadSpinnerWindow.axaml.cs:20` IDE0052

**B-02** (IDE1006/IDE0330/IDE0052/EnableGenerateDocumentationFile, tests, low, 11 entries)

- `tests/FreeformHelper.Tests/Application/Dxf/DxfSinglePassImportTests.cs:122` IDE1006
- `tests/FreeformHelper.Tests/Application/Dxf/DxfSinglePassImportTests.cs:130` IDE1006
- `tests/FreeformHelper.Tests/Application/Dxf/DxfSinglePassImportTests.cs:157` IDE1006
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs:36` IDE0330
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs:36` IDE1006
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs:38` IDE1006
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:14` IDE1006
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:15` IDE1006
- `(project-level) FreeformHelper.Tests` EnableGenerateDocumentationFile
- `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:19` IDE1006
- `tests/FreeformHelper.Tests/Architecture/ArchitectureSource.cs:286` IDE1006

**B-03** (VSTHRD002/103/200 blocking wait, Async suffix, prod, high, 5 entries)

- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:505` VSTHRD002
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:629` VSTHRD002
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:665` VSTHRD200
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:735` VSTHRD002
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs:16` VSTHRD200

**B-04** (VSTHRD002/103/200 blocking wait, Async suffix, tests, medium, 2 entries)

- `tests/FreeformHelper.Tests/UI/Services/UiEventRunnerTests.cs:30` VSTHRD103
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.LoadOverlay.cs:53` VSTHRD002

**B-05** (VSTHRD003 awaiting foreign Task, prod, high, 3 entries)

- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:665` VSTHRD003
- `src/FreeformHelper.UI/Services/GridRebuildUseCase.cs:48` VSTHRD003
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs:150` VSTHRD003

**B-06** (VSTHRD003 awaiting foreign Task, tests, medium, 19 entries)

- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:194` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:260` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:273` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:308` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:322` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:350` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:354` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:369` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:408` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:416` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs:428` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/MainWindowCloseTests.cs:16` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/MainWindowCloseTests.cs:39` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/MainWindowCloseTests.cs:119` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/MainWindowCloseTests.cs:151` VSTHRD003
- `tests/FreeformHelper.Tests/Application/Project/ProjectPersistenceUseCaseTests.cs:442` VSTHRD003
- `tests/FreeformHelper.Tests/Application/Project/ProjectPersistenceUseCaseTests.cs:460` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingInputTests.cs:25` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingInputTests.cs:96` VSTHRD003

**B-07** (VSTHRD003 awaiting foreign Task, tests, medium, 11 entries)

- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.Reports.cs:58` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.Reports.cs:117` VSTHRD003
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelCoordinateTests.cs:45` VSTHRD003
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelCoordinateTests.cs:105` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryCoreBehaviorTests.cs:68` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Services/UiEventRunnerTests.cs:32` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.cs:66` VSTHRD003
- `tests/FreeformHelper.Tests/UI/Smoke/SaveProjectShortcutTests.cs:55` VSTHRD003
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.NotchGenerationStaleCompletion.cs:337` VSTHRD003
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelSimulationTests.cs:38` VSTHRD003
- `tests/FreeformHelper.Tests/UI/ViewModels/SimulationHostViewModelTests.cs:96` VSTHRD003

**B-08** (RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64), prod, medium, 20 entries)

- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:29` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:33` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:192` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:199` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:240` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:258` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:267` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:271` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs:307` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs:97` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs:108` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs:121` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs:144` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs:160` RS0030 `Stopwatch.GetTimestamp()`
- `src/FreeformHelper.UI/Controls/PadCanvas.ViewRefresh.cs:58` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/Controls/PadCanvas.ViewRefresh.cs:97` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/Controls/PadCanvas.ViewRefresh.cs:110` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:133` RS0030 `DateTimeOffset.UtcNow`
- `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:194` RS0030 `DateTimeOffset.UtcNow`
- `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:207` RS0030 `DateTimeOffset.UtcNow`

**B-09** (RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64), prod, medium, 11 entries)

- `src/FreeformHelper.UI/Controls/PadCanvas.Input.PointerMove.cs:27` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/Controls/PadCanvas.Input.PointerMove.cs:55` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:396` RS0030 `DateTimeOffset.UtcNow`
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:635` RS0030 `DateTimeOffset.UtcNow`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs:99` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs:212` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:159` RS0030 `Environment.TickCount64`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:210` RS0030 `Environment.TickCount64`
- `src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:24` RS0030 `DateTimeOffset.UtcNow`
- `src/FreeformHelper.UI/Controls/PadCanvas.RenderFrame.cs:18` RS0030 `DateTime.UtcNow`
- `src/FreeformHelper.UI/Services/CadLoadSpinnerDebugState.cs:163` RS0030 `DateTimeOffset.UtcNow`

**B-10** (RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64), tests, medium, 15 entries)

- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:72` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:74` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:76` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:79` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:82` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:84` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:87` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:89` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:92` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:94` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:99` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:100` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:101` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:102` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:103` RS0030 `Stopwatch`

**B-11** (RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64), tests, medium, 19 entries)

- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:102` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:119` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:127` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:148` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:150` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:152` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:154` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:156` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:162` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:165` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:166` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:167` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Services/LoadingScopeCoordinatorTests.cs:34` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Services/LoadingScopeCoordinatorTests.cs:40` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Services/LoadingScopeCoordinatorTests.cs:44` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/Services/LoadingScopeCoordinatorTests.cs:45` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs:54` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs:58` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs:60` RS0030 `Stopwatch`

**B-12** (RS0030 direct clock reads (Stopwatch/UtcNow/TickCount64), tests, medium, 9 entries)

- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasViewRefreshTests.cs:16` RS0030 `DateTime.UtcNow`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasViewRefreshTests.cs:40` RS0030 `DateTime.UtcNow`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs:227` RS0030 `Environment.TickCount64`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs:230` RS0030 `Environment.TickCount64`
- `tests/FreeformHelper.Tests/UI/ViewModels/IndexMappingReportViewModelTests.cs:10` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/ViewModels/IndexMappingReportViewModelTests.cs:11` RS0030 `Stopwatch`
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelSimulationTests.cs:354` RS0030 `Environment.TickCount64`
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelSimulationTests.cs:357` RS0030 `Environment.TickCount64`
- `tests/FreeformHelper.Tests/UI/Services/AppGeneralSettingsStoreTests.cs:101` RS0030 `DateTimeOffset.UtcNow`

**B-13** (RS0030 Task.Delay / Thread.Sleep, prod, medium, 9 entries)

- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:367` RS0030 `Task.Delay(TimeSpan)`
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:400` RS0030 `Task.Delay(TimeSpan)`
- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:488` RS0030 `Task.Delay(int)`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs:178` RS0030 `Task.Delay(int, CancellationToken)`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs:288` RS0030 `Task.Delay(int, CancellationToken)`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.AutoPlay.cs:98` RS0030 `Task.Delay(int, CancellationToken)`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection.NotchPreview.cs:27` RS0030 `Task.Delay(int, CancellationToken)`
- `src/FreeformHelper.UI/ViewModels/ShellViewModel.Workspaces.cs:186` RS0030 `Task.Delay(int)`
- `src/FreeformHelper.UI/Views/DevView.axaml.cs:50` RS0030 `Task.Delay(TimeSpan)`

**B-14** (RS0030 Task.Delay / Thread.Sleep, tests, medium, 15 entries)

- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryIpcTests.cs:31` RS0030 `Task.Delay(int, CancellationToken)`
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryIpcTests.cs:59` RS0030 `Task.Delay(int, CancellationToken)`
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryIpcTests.cs:86` RS0030 `Task.Delay(int)`
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelSimulationTests.cs:266` RS0030 `Task.Delay(int)`
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelSimulationTests.cs:275` RS0030 `Task.Delay(int, CancellationToken)`
- `tests/FreeformHelper.Tests/UI/ViewModels/ShellViewModelSimulationTests.cs:362` RS0030 `Task.Delay(int)`
- `tests/FreeformHelper.Tests/UI/Smoke/HeadlessUiSmokeTests.cs:303` RS0030 `Task.Delay(int)`
- `tests/FreeformHelper.Tests/UI/Smoke/HeadlessUiSmokeTests.cs:439` RS0030 `Task.Delay(int)`
- `tests/FreeformHelper.Tests/Application/Dxf/DxfLayerImageExportServiceTests.cs:212` RS0030 `Thread.Sleep(int)`
- `tests/FreeformHelper.Tests/UI/Services/CadLoadSpinnerProcessHostTests.cs:85` RS0030 `Task.Delay(TimeSpan, CancellationToken)`
- `tests/FreeformHelper.Tests/UI/Services/LoadingScopeCoordinatorTests.cs:20` RS0030 `Task.Delay(int)`
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryCoreBehaviorTests.cs:185` RS0030 `Task.Delay(int, CancellationToken)`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs:235` RS0030 `Task.Delay(int)`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.AppGeneral.cs:175` RS0030 `Task.Delay(int, CancellationToken)`
- `tests/FreeformHelper.Tests/UI/ViewModels/IndexMappingReportViewModelTests.cs:18` RS0030 `Task.Delay(int)`

**B-15** (RS0030 Environment get/set/GetFolderPath, prod, medium, 4 entries)

- `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:140` RS0030 `Environment.GetEnvironmentVariable(string)`
- `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:146` RS0030 `Environment.GetFolderPath(Environment.SpecialFolder)`
- `src/FreeformHelper.UI/Program.cs:83` RS0030 `Environment.GetEnvironmentVariable(string)`
- `src/FreeformHelper.UI/Services/StartupPerfTracker.cs:17` RS0030 `Environment.GetEnvironmentVariable(string)`

**B-16** (RS0030 Environment get/set/GetFolderPath, tests, medium, 19 entries)

- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:25` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:37` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:176` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs:22` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs:29` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs:145` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:28` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:38` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:224` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/UI/Services/AppGeneralSettingsStoreTests.cs:14` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/UI/Services/AppGeneralSettingsStoreTests.cs:19` RS0030 `Environment.SetEnvironmentVariable(string, string?)`
- `tests/FreeformHelper.Tests/UI/Services/AppGeneralSettingsStoreTests.cs:27` RS0030 `Environment.SetEnvironmentVariable(string, string?)`
- `tests/FreeformHelper.Tests/UI/Smoke/FontAdoptionTests.cs:103` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/UI/Smoke/FontAdoptionTests.cs:124` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs:37` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:33` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/TestInfrastructure/ExampleData.cs:28` RS0030 `Environment.GetEnvironmentVariable(string)`
- `tests/FreeformHelper.Tests/TestInfrastructure/TestAppSettingsIsolation.cs:20` RS0030 `Environment.SetEnvironmentVariable(string, string?)`
- `tests/FreeformHelper.Tests/UI/Snapshots/UiBaselineUpdateMode.cs:16` RS0030 `Environment.GetEnvironmentVariable(string)`

**B-17** (RS0030 System.IO.File.*, prod, medium, 12 entries)

- `src/FreeformHelper.Application/Services/DiffFrameCsvImporter.cs:12` RS0030 `File`
- `src/FreeformHelper.Application/Services/DiffFrameCsvImporter.cs:12` RS0030 `File.ReadAllLines(string)`
- `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:66` RS0030 `File.ReadAllText(string)`
- `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:135` RS0030 `File.WriteAllText(string, string?)`
- `src/FreeformHelper.UI/Services/ProjectPersistenceUseCase.cs:79` RS0030 `File.ReadAllBytes(string)`
- `src/FreeformHelper.UI/Services/ProjectPersistenceUseCase.cs:98` RS0030 `File.ReadAllBytes(string)`
- `src/FreeformHelper.Application/Services/RegularVisibilityMaskService.cs:67` RS0030 `File`
- `src/FreeformHelper.Infrastructure/Project/JsonProjectStore.cs:41` RS0030 `File.ReadAllText(string)`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:159` RS0030 `File.WriteAllText(string, string?)`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs:114` RS0030 `File.WriteAllText(string, string?)`
- `src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml.cs:195` RS0030 `File.WriteAllTextAsync(string, string?, Encoding, CancellationToken)`
- `src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml.cs:264` RS0030 `File.WriteAllTextAsync(string, string?, Encoding, CancellationToken)`

**B-18** (RS0030 System.IO.File.*, tests, medium, 3 entries)

- `tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs:39` RS0030 `File.WriteAllText(string, string?)`
- `tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs:49` RS0030 `File.WriteAllText(string, string?)`
- `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:37` RS0030 `File.WriteAllText(string, string?)`

**B-19** (RS0030 Process.Start, prod, high, 1 entries)

- `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:453` RS0030 `Process.Start(ProcessStartInfo)`

**B-20** (RS0030 Process.Start, tests, high, 3 entries)

- `tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs:1359` RS0030 `Process.Start()`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs:1432` RS0030 `Process.Start()`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs:1475` RS0030 `Process.Start()`

**B-21** (RS0030 Dispatcher.UIThread, prod, high, 18 entries)

- `src/FreeformHelper.UI/App.axaml.cs:100` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/App.axaml.cs:119` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/App.axaml.cs:142` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/SettingsWindow.axaml.cs:89` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/SettingsWindow.axaml.cs:110` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml.cs:328` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml.cs:400` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Controls/PadCanvas.View.cs:30` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Controls/PadCanvas.ViewRefresh.cs:75` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml.cs:271` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Program.cs:77` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Services/CanvasViewportAdapter.cs:101` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/CadLoadSpinnerWindow.axaml.cs:51` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml.cs:91` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/FreeformHelperView.Canvas.Layout.cs:19` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/FreeformHelperView.Canvas.cs:259` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/FreeformHelperView.Lifecycle.cs:178` RS0030 `Dispatcher.UIThread`
- `src/FreeformHelper.UI/Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml.cs:98` RS0030 `Dispatcher.UIThread`

**B-22** (RS0030 Dispatcher.UIThread, tests, high, 14 entries)

- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:43` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:45` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:77` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:138` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:149` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:167` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:177` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:179` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:190` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardTests.cs:192` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs:75` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs:103` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs:211` RS0030 `Dispatcher.UIThread`
- `tests/FreeformHelper.Tests/UI/TestHost/HeadlessDispatcherSetup.cs:23` RS0030 `Dispatcher.UIThread`

**B-23** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 12 entries)

- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:59` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:63` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:163` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:164` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:238` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:247` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:256` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:263` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:283` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:284` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:292` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasHitTestTests.cs:293` RS0030 `BindingFlags.NonPublic`

**B-24** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 12 entries)

- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:24` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:25` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:26` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:27` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:76` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:77` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:121` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:170` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:171` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:172` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:228` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.Duplicates.cs:229` RS0030 `BindingFlags.NonPublic`

**B-25** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 11 entries)

- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:100` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:199` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:224` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:1309` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:1311` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:1328` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:1330` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:1598` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:1599` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:1680` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs:2406` RS0030 `BindingFlags.NonPublic`

**B-26** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 19 entries)

- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:360` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:394` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:395` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:396` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:397` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:484` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:485` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:546` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:547` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:548` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs:549` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:41` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:42` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:43` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:55` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:72` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:73` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:74` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs:100` RS0030 `BindingFlags.NonPublic`

**B-27** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 18 entries)

- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs:128` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs:222` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs:223` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs:224` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs:225` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs:258` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs:259` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs:39` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs:40` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs:151` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs:152` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs:196` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs:197` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.CadEditing.cs:231` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.CadEditing.cs:232` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.CadEditing.cs:284` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.CadEditing.cs:285` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.CadEditing.cs:286` RS0030 `BindingFlags.NonPublic`

**B-28** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 19 entries)

- `tests/FreeformHelper.Tests/Application/Notch/NotchToFullCoverageSnapshotBuilder.cs:90` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchToFullCoverageSnapshotBuilder.cs:105` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchToFullCoverageSnapshotBuilder.cs:106` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchToFullCoverageSnapshotBuilder.cs:120` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingInputTests.cs:91` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingInputTests.cs:109` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingInputTests.cs:112` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingInputTests.cs:143` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.PadInspectorCache.cs:120` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.PadInspectorCache.cs:185` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.PadInspectorCache.cs:186` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.PadInspectorCache.cs:215` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Workflow/DiffFrameCsvFixtureTests.cs:39` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Workflow/DiffFrameCsvFixtureTests.cs:45` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Workflow/DiffFrameCsvFixtureTests.cs:76` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Workflow/DiffFrameCsvFixtureTests.cs:110` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs:69` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs:70` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs:71` RS0030 `BindingFlags.NonPublic`

**B-29** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 20 entries)

- `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:260` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:261` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:262` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.Reports.cs:26` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.Reports.cs:123` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.Reports.cs:155` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.cs:112` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/ProjectLoadingSecondaryWindowTests.cs:119` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.LocateAndMatchActions.cs:48` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.LocateAndMatchActions.cs:49` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs:42` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs:43` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.NotchResolvedSnapshot.cs:277` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.NotchResolvedSnapshot.cs:280` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.WorkflowSnapshot.cs:32` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.WorkflowSnapshot.cs:41` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/MaskCurrentBehaviorTests.cs:186` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/MaskCurrentBehaviorTests.cs:193` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Cad/Cad3635LoadBenchmarkTests.cs:15` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/Application/Notch/NotchExampleCExportDriftTests.cs:135` RS0030 `BindingFlags.NonPublic`

**B-30** (RS0030 BindingFlags.NonPublic (tests reflection), tests, high, 13 entries)

- `tests/FreeformHelper.Tests/Application/Notch/NotchV22CompensationServiceTests.cs:39` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/TestInfrastructure/UiEventProbe.cs:34` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Canvas/PadCanvasCacheInvalidationTests.cs:436` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Services/CadLoadSpinnerProcessHostTests.cs:148` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Services/NotchDisplayProjectorTests.cs:140` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryCharacterizationSubject.cs:27` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryCoreBehaviorTests.cs:175` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryIpcTests.cs:106` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Services/RuntimeQueryUseCaseTests.cs:195` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/Smoke/SaveProjectShortcutTests.cs:96` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.GridPitch.cs:36` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.InputAndExport.cs:94` RS0030 `BindingFlags.NonPublic`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.cs:262` RS0030 `BindingFlags.NonPublic`

## 8. Suggested order and next step

1. B-01 and B-02 (low, 15 sites in 2 PRs or 1 PR by owner choice): drop IDE1006/IDE0330/IDE0052/VSTHRD103 to zero, which removes IDE1006, IDE0330, IDE0052, VSTHRD103 from the UI and Tests `HealthBaselineWarningIds` lists (EnableGenerateDocumentationFile stays until its policy decision).
2. Ask the owner for the decisions in section 6 (reflection access, UI dispatcher, clock, delay, env accessor, file IO seam, process launch, Task wait policy, test doc-file policy). Everything past B-02 is blocked on them.
3. Then run RS0030 clocks/delays/env/file in tests (medium) before production, so seams are proven in tests first.
4. Last: reflection (124) and production threading batches.
5. Before the first fix PR run `pwsh eng/core-health/repo-health.ps1 -Mode Verify ...` on a machine that has pwsh (not possible in this cloud run).
