# src banned-symbols batch B4: file IO, environment and Application signatures

Status: plan only. No production code changed. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
Snapshot: `main` at `5e48bec` (2026-11-05). Source of truth for counts: `eng/code-health/baseline.json`, rule `RS0030`.

## 1. Scope and metric

B4 covers the `src/` findings of `RS0030` (BannedApiAnalyzers) that come from:

- `BannedSymbols.Files.txt`: `System.IO.File.Copy/Move/Read*/Write*`.
- `BannedSymbols.Common.txt`: `Environment.GetEnvironmentVariable`, `GetFolderPath`, `TickCount64`.
- `eng/code-health/{application,domain}/BannedSymbols.txt`: the whole `T:System.IO.File` type in the pure layers.
  This is the "Application signatures" part: an Application API that takes a path has to touch the file system.

Not in B4: `Stopwatch`, `DateTime(Offset).UtcNow`, `Task.Delay`, `Process.Start`, `Dispatcher.UIThread` (other batches).
`File.Exists`, `File.Open` and `File.GetLastWriteTimeUtc` are not banned outside the pure layers and stay.

Metric: `RS0030` findings in `baseline.json` for src projects. B4 can lower it by **18**
(Application 3, Infrastructure 1, UI 14). Today src has 70 `RS0030` findings
(UI 64, Application 4, Infrastructure 2).

## 2. Ledger constraints that shape the plan

Read from `eng/core-health/repo-health.ps1`:

1. A finding key is `rule | project | member | symbol | syntaxHash`. The hash is of the innermost
   non-block **statement** around the diagnostic (or the member declaration when there is no statement, as for fields).
2. Any key that is new or has a higher count fails Verify (`New fingerprint; deleting another finding cannot offset it`).
   So a banned call cannot be moved into a new helper, a new member or a renamed member. Each site must be
   **replaced by an API that is not banned**, or deleted.
3. Every fix must lower the ledger in the same change (`Fixes must lower/delete baseline debt in this change`).
   `LowerBaseline` needs `pwsh`, so every B4 PR must be finished on a machine with PowerShell 7.
4. Other findings in the same statement or field get a new hash when that statement changes.
   Known coupling in B4:
   - `StartupPerfTracker.EmitAsInfo` also carries `IDE1006`, keyed on the field declaration. Changing the
     initializer gives the `IDE1006` finding a new hash. Fix both together (rename to `_emitAsInfo`).
   - `DiffFrameCsvImporter.ImportLines(...)` carries a `methodLines: 166` entity. Do not rename it, move it
     or change its signature.
   - `AppGeneralSettingsStore.Save` has `File.WriteAllText` (line 135) and `DateTimeOffset.UtcNow` (line 133) in
     separate statements, so they can be fixed separately. Do not merge those two statements.

## 3. Existing abstractions to reuse

| Need | Use | Where it is already used |
| --- | --- | --- |
| Write a file | `Nvt.Core.IO.AtomicOutput.WriteBytesAsync(path, bytes, ct)` (sibling temp file, flush, publish) | `JsonProjectStore.Publisher` |
| Read text | `StreamReader` (detects BOM, UTF-8 default; same result as `File.ReadAllText`/`ReadAllLines`) | `RegularVisibilityMaskService.DecodeText`, `TestFiles.ReadContent` |
| Read bytes | `FileStream` + `CopyTo`/`ReadExactly` | `TestFiles.ReadBytes` |
| Elapsed ticks | `TimeProvider` (`GetTimestamp`, `GetElapsedTime`) | `JsonProjectStore`, `ConsoleLinkOpener` |
| Environment | none. Nvt.Core 0.9.5 has no reviewed environment reader | see section 5 |

New small abstractions proposed: one (`TextExportWriter`, section 4.2) and one Application entry point
(`RegularVisibilityMaskService.LoadText`, section 4.3). Nothing else.

## 4. Inventory and replacement, ordered by risk (lowest first)

### 4.1 Low risk: read-only replacements with the same bytes (3 findings)

| File:line | Call | Why banned | Replacement |
| --- | --- | --- | --- |
| `src/FreeformHelper.Infrastructure/Project/JsonProjectStore.cs:41` | `File.ReadAllText(path)` | Files list: whole-file read helper outside a reviewed seam | `using var reader = new StreamReader(path); var json = reader.ReadToEnd();` Same decoding (UTF-8, BOM detection). |
| `src/FreeformHelper.UI/Services/ProjectPersistenceUseCase.cs:79` | `File.ReadAllBytes(file.LastDxfPath)` (`TryEmbedDxf`) | same | One private static `ReadFileBytes(string path)` in the use case, with `FileStream` + `MemoryStream.CopyTo`. Both sites call it. |
| `src/FreeformHelper.UI/Services/ProjectPersistenceUseCase.cs:98` | `File.ReadAllBytes(sourcePath)` (`TryEmbedRegularVisibilityMask`) | same | same helper |

Tests that cover this today: `ProjectPersistenceUseCaseTests` round trips (save then load, embedded DXF and mask).
Real input: save and load one project from `example/` with an embedded DXF and a SeeRegular mask.

### 4.2 Low to medium risk: export writes (4 findings, 1 new helper)

| File:line | Call | Current bytes | Replacement |
| --- | --- | --- | --- |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:159` | `File.WriteAllText(path, text)` | UTF-8, no BOM | `await TextExportWriter.WriteAsync(path, text, includeBom: false, ct)` |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs:114` | `Task.Run(() => File.WriteAllText(path, payload))` | UTF-8, no BOM | same, `includeBom: false` |
| `src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml.cs:264` | `File.WriteAllTextAsync(..., Encoding.UTF8)` | UTF-8 **with** BOM | same, `includeBom: true` |
| `src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml.cs:195` | `File.WriteAllTextAsync(..., Encoding.UTF8)` | UTF-8 **with** BOM | same, `includeBom: true` |

Why banned: the Files list routes writes through one reviewed path; the two views also do IO in code-behind,
which `AGENTS.md` forbids for Views.

New helper `src/FreeformHelper.UI/Services/TextExportWriter.cs` (about 15 lines): encodes with `UTF8Encoding(includeBom)`,
prepends the preamble when asked, and calls `AtomicOutput.WriteBytesAsync`. It has no banned call, so it adds no finding.

- Keep the BOM difference exactly as it is. Do not unify it in this batch: that would change output bytes
  ("Changing a default changes the output").
- Behavior change to state in the PR: the write becomes atomic (temp file then publish). The bytes are the same.
  A failed export no longer leaves a half-written file, and the parent directory is created if missing.
- `AtomicOutput` checks the token before publish. The two views pass `CancellationToken.None` today; keep that.
- Tests: one behavior test per BOM mode (bytes start or do not start with `EF BB BF`), and one
  that an existing target is replaced. Use `TestFiles.NewTempFilePath`.
- Real input: export DXF (visible and all), one notch export, one Simulation and one Coordinate text export on an
  `example/` project, and compare with the bytes from `main`.

### 4.3 Medium risk: Application signatures (3 findings, public API change)

| File:line | Call | Why banned | Replacement |
| --- | --- | --- | --- |
| `src/FreeformHelper.Application/Services/DiffFrameCsvImporter.cs:12` (count 2) | `File.ReadAllLines(sourcePath)` in `Import(string sourcePath, ...)` | Application is a pure layer: `T:System.IO.File` is banned there | Delete `Import(string path)`. Callers read the text and call the existing `ImportContent(sourceName, content, options)`. `StringReader.ReadLine` splits lines like `ReadAllLines`. |
| `src/FreeformHelper.Application/Services/RegularVisibilityMaskService.cs:67` | `File.Exists(sourcePath)` in `Load(string? sourcePath, ...)` | same | Replace `Load(path)` by `LoadText(string sourceName, Func<TextReader> openText, grid, rowOrigin)`. The existence check moves to the UI caller. |

Why `Func<TextReader>` and not a string: today a read error (`IOException`, access denied) is inside
`ReadAndProject`'s one best-effort boundary and becomes `RegularVisibilityMaskResult.Failed`. Opening the reader
inside that boundary keeps this. The UI supplies `() => new StreamReader(path)`, so Application itself never names a path.

Do not reuse `LoadEmbedded(bytes)` for the file path: it returns `NotFound` for an empty file, while `Load(path)`
returns `Failed` for an existing empty file. That would change the result.

Readers to update (searched `src`, `tests`, `.axaml`, scripts; no reflection strings found):

- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.RegularVisibilityMask.cs:78` → small UI method:
  `NotFound` when the path is blank or `File.Exists` is false, else `LoadText(path, () => new StreamReader(path), ...)`.
- `src/FreeformHelper.UI/Services/NotchApplySimulationReviewUseCase.cs:35` → read with `StreamReader`, then `ImportContent(sourcePath, content)`.
- `RegularVisibilityMaskService.cs:73` (the internal call in `Load`) goes away with `Load`.
- Tests: `Workflow/DiffFrameCsvImporterTests.cs:25,70,87`, `Workflow/DiffFrameCsvFixtureTests.cs:19,47,140`,
  `Application/Simulation/RegularVisibilityMaskServiceTests.cs:29,63`,
  `Application/Simulation/RegularVisibilityMaskServiceFailureTests.cs:63` → read through `TestFiles.ReadContent`, or call the UI loader.
  Keep the `InvalidDataException` and "existing non-CSV file" cases as behavior tests.

Leave `ImportLines` untouched (entity ceiling, section 2).
Real input: load the `example/` SeeRegular CSV as a mask and as a simulation source; the frame counts and
active/inactive pad IDs must match `main`.

### 4.4 Medium to high risk: app settings store file IO (2 findings, protected mechanism)

| File:line | Call | Replacement |
| --- | --- | --- |
| `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:66` (`TryLoad`) | `File.ReadAllText(_settingsPath)` | `StreamReader` + `ReadToEnd` |
| `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:135` (`Save`) | `File.WriteAllText(_settingsPath, json)` | synchronous `StreamWriter(path, append: false)` (UTF-8, no BOM, same bytes) |

Why higher risk: `AGENTS.md` lists app settings persistence (deferred flush after `Load Project`) as a runtime
mechanism that must not change without an explicit task. Keep `Save` synchronous: `AtomicOutput` is async only,
and waiting on it would add a `blockingWait` finding. Making `Save` atomic is a separate, owner-approved change.
Do not touch line 133 (`DateTimeOffset.UtcNow`, clock batch) or lines 140/146 (section 5).
The existing `readTextOverride`/`saveOverride` test seam does not cover the real file path, so add one round-trip
test through `TestAppSettingsIsolation`.

### 4.5 Low risk, but clock-like: `Environment.TickCount64` (2 findings)

| File:line | Replacement |
| --- | --- |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:159` | `TimeProvider.System.GetTimestamp()`, and compare with `TimeProvider.System.GetElapsedTime(lastTimestamp).TotalMilliseconds` |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:210` | same |

Both throttle progress reports to `minUpdateIntervalMs`. Same behavior; the final report is never throttled.
If the clock batch gives `FreeformHelperViewModel` an injected `TimeProvider`, use that instead of `TimeProvider.System`,
and these two sites can move to the clock batch.

## 5. Blocked: environment reads need a decision (4 findings)

| File:line | Call | Readers outside src |
| --- | --- | --- |
| `src/FreeformHelper.UI/Program.cs:83` (`BuildAvaloniaApp`, release only) | `GetEnvironmentVariable("FREEFORM_ENABLE_AVALONIA_TRACE")` | none found (docs, scripts, tests) |
| `src/FreeformHelper.UI/Services/StartupPerfTracker.cs:17` (field, also `IDE1006`) | `GetEnvironmentVariable("FREEFORM_PERF_MARKERS_INFO")` | `scripts/perf/measure-startup-load.ps1`, `analyze-startup-markers.ps1`, `docs/performance/perf-baseline-howto.md` |
| `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:140` | `GetEnvironmentVariable("FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH")` | `TestAppSettingsIsolation`, `scripts/perf/run-3635-regression-baseline.ps1`, perf docs |
| `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:146` | `GetFolderPath(LocalApplicationData)` | default settings location |

Why banned: process environment is ambient global state; Core wants it read in one reviewed place.
Why blocked: section 2 rule 2. Moving these reads into one `ProcessEnvironment` owner creates a new fingerprint,
and there is no unbanned API that reads the environment honestly. `Environment.ExpandEnvironmentVariables("%X%")`
would pass the analyzer but is a workaround of the ban; do not use it.

Options for the owner (recommendation first):

1. **Core adds a reviewed environment seam** (like the `*.TestSupport` exemption, or a host-level seam owner listed in
   the layer `BannedSymbols.txt`, as its header comment foresees). NFH then reads all four values once in
   `Program.Main` into an immutable `AppHostEnvironment` record and passes it to `StartupPerfTracker` and
   `AppGeneralSettingsStore`. This also gives "find tools one way" a single entry point.
2. Use `Microsoft.Extensions.Configuration.EnvironmentVariables` in the composition root. No banned call,
   but it adds a package (high-risk `Directory.Packages.props` change) and `GetFolderPath` stays.
3. Delete `FREEFORM_ENABLE_AVALONIA_TRACE` under "delete a setting that has no user". It is a release-only
   diagnostic switch; deleting it changes release behavior, so the owner decides. Lowers the count by 1 with no new code.

The settings path override is part of the test-isolation mechanism in `AGENTS.md`. Whatever option is chosen,
it must keep `TestAppSettingsIsolation` working and needs its own PR.

## 6. Suggested split

The coding rules say one batch is one PR with several commits, and `baseline.json` goes in the last PR.
Verify also requires each fix to lower the ledger in the same change. So:

- **PR B4a (ready now)**, one PR, branch `refactor/code-health/src-b4-file-io`, one commit each:
  1. 4.1 read replacements (Infrastructure + `ProjectPersistenceUseCase`), −3.
  2. 4.5 `TickCount64` → `TimeProvider`, −2 (drop if the clock batch takes it).
  3. 4.2 `TextExportWriter` and the four export writes, −4. States the atomic-write behavior change.
  4. 4.3 Application signatures and the test updates, −3.
  5. 4.4 app settings store read and write, −2. Can be held back if the owner wants it apart.
  6. Last commit: `LowerBaseline` for `baseline.json` (and `test-debt.json` if the test rewrites lower it),
     plus a `ROADMAP.md` entry. Expected total −14 (−12 without 4.4).
- **PR B4b (after the decision in section 5)**, from `main` after B4a merges, branch
  `refactor/code-health/src-b4-environment`: the environment seam, the `StartupPerfTracker` `IDE1006` rename,
  and the script/doc updates if a name changes. Expected −4 (−3 if option 3 is used only for the trace switch).

Before each PR: `prepare-ui-workspace.ps1`, `dotnet build`, the affected tests, `lint.ps1 -UseNoAppHost`,
and once `repo-health.ps1 -Mode Verify` (needs `pwsh`, about 25 minutes).

## 7. Open questions for the owner

1. Section 5: which option for the environment reads? Can `FREEFORM_ENABLE_AVALONIA_TRACE` be deleted?
2. 4.2: is the atomic export write (same bytes, temp-file publish) acceptable as part of a code-health PR?
3. 4.4: may the app settings store change in B4a, or should it wait for its own PR?
4. Should the views' UTF-8 BOM and the view models' no-BOM exports stay different? (Plan: keep them as they are.)
