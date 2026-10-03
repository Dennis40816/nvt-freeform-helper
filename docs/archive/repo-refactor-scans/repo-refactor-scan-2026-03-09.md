# Repo Refactor Scan - 2026-03-09

## Summary
- No new P0 correctness bug was found beyond the already tracked `S11.10 unique-path regression guard`.
- The current rewrite candidates are concentrated in:
  - settings mutation flow,
  - DXF edit reset contract,
  - Export Notch Rows filter/projection flow,
  - release/scan gate stability,
  - stale behavior documents.

## Hotspots
| File | Lines | Why it matters |
| --- | ---: | --- |
| `src/FreeformHelper.UI/Styles/Controls.Core.axaml` | 918 | Style surface is still heavily centralized; future UI tweaks remain high blast-radius. |
| `src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml` | 695 | Export window layout is large and tightly coupled to one ViewModel contract. |
| `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs` | 585 | ViewModel owns filtering, counting, grouping, selection, preview, and export state. |
| `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.Selection.cs` | 483 | Rebuild path mixes filtering, sorting, grouping, counts, and preview retention. |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.PropertyCallbacks.cs` | 406 | Settings side effects still rely on many manual property callbacks. |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs` | 404 | Settings draft apply path duplicates owner/draft mapping and change classification. |
| `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs` | 403 | Settings draft state mirrors main ViewModel state almost field-for-field. |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs` | 416 | Still a major workflow entry surface; command wiring remains broad. |

## Findings

### 1. Settings mutation still has two parallel orchestration paths
Severity: P1

Evidence:
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.PropertyCallbacks.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs`
- `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs`

Current problem:
- Main settings edits flow through many `partial void On...Changed(...)` callbacks.
- Settings dialog edits are copied into a draft object, then copied back field-by-field in `ApplySettingsWindowDraft(...)`.
- Change classification (`layoutChanged`, `step2Changed`, `step3Changed`, `projectSettingsChanged`, etc.) is maintained separately from property-callback side effects.

Risk:
- New settings can be saved in one path but forget to trigger the same downstream effects in the other.
- The repo already has a documentation matrix for settings entry, which indicates this logic is important enough to keep explicit, but the code still duplicates the policy.

Rewrite direction:
- Introduce a single settings mutation orchestrator or descriptor table:
  - field mapping,
  - side effects,
  - persistence scope,
  - rebuild/step invalidation policy.
- Keep the dialog as a draft-only shell; commit through the same mutation path as direct settings edits.

### 2. DXF edit reset command contract is still misnamed
Severity: P1

Evidence:
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Commands.cs`
- `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Edits.cs`

Current problem:
- `RestoreAllDeletedCadPadsCommand` no longer only restores hidden pads.
- UI and behavior inventory already treat it as `Reset all DXF edits`, including:
  - hidden,
  - combined,
  - layer move.
- The command/property name still exposes the old contract.

Risk:
- Future code can wire this command under the assumption that it only touches hidden pads.
- Tests and view code will continue to drift semantically from command naming.

Rewrite direction:
- Rename to a truthful command contract such as `ResetAllDxfEditsCommand`.
- Keep hidden-only restore as its own explicit action if still needed.
- Update docs/tests/UI bindings to the new contract.

### 3. Export Notch Rows filter flow is still over-aggregated
Severity: P1

Evidence:
- `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs`
- `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.Selection.cs`
- `src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml`

Current problem:
- A single ViewModel currently owns:
  - raw rows,
  - visible rows,
  - grouping by IC,
  - workspace-linked rows,
  - search/filter state,
  - column filters,
  - selected-only behavior,
  - preview row retention,
  - summary chips/counts.
- `RebuildVisibleRows()` manually rebuilds rows/groups and manually raises a long list of dependent property changes.

Risk:
- Filter behavior changes can silently desync summary chips, counts, or workspace-link highlights.
- Any further feature on export filtering will keep inflating the same ViewModel.

Rewrite direction:
- Introduce a `NotchExportFilterSnapshot` + `NotchExportProjectionService`.
- Make one method return:
  - visible rows,
  - grouped rows,
  - chip counts,
  - selected/visible summaries,
  - workspace-linked subset.
- Let the ViewModel only hold UI state and commands.

### 4. Repo scan / lint / build gates are currently unstable
Severity: P1

Evidence:
- `pwsh .agents/skills/repo-optimizer-loop/scripts/repo_scan.ps1 -RepoRoot . -OutFile docs/guides/repo-refactor-scan-2026-03-09.md`
  - fails because `build/logs/repo-skill-lint.log` is not writable in the current environment.
- `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
  - fails at `dotnet format` restore.
- `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj --no-restore /p:UseAppHost=false`
  - fails in this environment with SDK workload resolver issues (`MSB4276`) under `dotnet sdk 9.0.301`.

Current problem:
- Repo-wide scan cannot be trusted as an automated gate because the script/log path and the SDK environment are brittle.

Risk:
- Release scan can report infra failure instead of code findings.
- Developers may skip the scan because it is noisy or non-deterministic.

Rewrite direction:
- Make the scan script write logs to a guaranteed writable repo path or temp path.
- Add clear environment preflight checks around workload SDK resolution.
- Separate "format/restore environment failed" from "code lint failed" in script output.

### 5. Docs are stale for recent DXF edit and load-overlay behavior
Severity: P2

Evidence:
- `docs/reference/behavior-inventory.md` mentions `Reset all DXF edits`, but does not yet cover:
  - `DxfEditChangeListWindow`,
  - `Hidden/Combined/Moved` badge modal entry points,
  - AA canvas loading overlay on `Open DXF` and `Load Project`.
- `docs/guides/settings-entry-matrix.md` does not mention the new CAD-load overlay workflow.

Risk:
- Repo scan can falsely conclude behavior/document contracts are aligned while newer workflows are undocumented.

Rewrite direction:
- Sync behavior inventory and settings entry matrix with the current DXF edit modal and AA loading overlay contracts.

## Recommended TODO Items
1. `S11.14` Rename and split DXF reset command contract.
2. `S11.15` Refactor settings mutation to a single orchestrated apply path.
3. `S11.16` Refactor Export Notch Rows filter/projection flow into a projection service.
4. `S11.17` Stabilize repo scan/build/lint gate under current SDK/environment.
5. `S11.18` Sync docs for DXF edit modal and AA loading overlay.

## Current blocker status
- Existing active item `S11.10 unique-path regression guard` remains valid and should stay ahead of the lower-priority refactors above.
