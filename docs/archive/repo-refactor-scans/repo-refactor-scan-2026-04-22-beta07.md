# Repo Refactor Scan

Generated: 2026-04-22 22:00:37
Branch: beta0.6

## File counts
- tracked files: 789
- src: 542
- tests: 125
- docs: 63
- scripts: 24

## Line count hotspots
- src/FreeformHelper.UI/Styles/Controls.Core.axaml: 1231
- docs/archive/TODO-history-2026-03-05.md: 1131
- tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs: 1048
- docs/archive/TODO-history-2026-03-24.md: 971
- src/FreeformHelper.UI/ViewModels/IndexMappingReportViewModel.cs: 934
- tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs: 916
- tests/FreeformHelper.Tests/UI/ViewModels/IndexMappingReportViewModelTests.cs: 894
- tests/FreeformHelper.Tests/UI/ViewModels/NotchExportSelectionViewModelTests.cs: 874
- tests/FreeformHelper.Tests/UI/Services/RuntimeQueryUseCaseTests.cs: 801
- src/FreeformHelper.UI/Styles/Tokens.axaml: 719
- src/FreeformHelper.UI/ViewModels/ShellViewModel.cs: 702
- src/FreeformHelper.Application/Services/CoordinatePlannerComputationService.cs: 701
- src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml: 689
- src/FreeformHelper.UI/ViewModels/CoordinatePlannerWorkspaceViewModel.cs: 665
- tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectSaveLoad.cs: 636
- src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.cs: 626
- src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml: 609
- src/FreeformHelper.UI/Views/IndexMappingReportWindow.axaml: 607
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs: 599
- src/FreeformHelper.UI/Styles/Controls.Form.axaml: 596
- src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs: 579
- src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml: 570
- tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs: 568
- docs/reference/behavior-inventory.md: 557
- src/FreeformHelper.UI/Controls/PadCanvas.Input.Selection.cs: 550

## Direct state mutation hotspots
- scan scope: public mutable collection properties in src/**/*.cs
- hotspot count: 0
- no hotspots found by current heuristic.

## Build gate
- status: ok
- log: build\logs\repo-skill-build.log

## Lint gate
- status: ok
- log: build\logs\repo-skill-lint.log

## Beta 0.6 close-out snapshot
- Step5 export surface is now converging on three real outputs only:
  - `CSV`
  - `C v2.1 (.c)`
  - `C v2.2 (.c)`
- Repo-level dead path already identified and removed in beta0.6 implementation work:
  - legacy `txt` notch export command/service/exporter path
  - `txt` runtime query alias
  - old example-project persisted value `"Txt"`
- Example exports now have a clean target shape in `example/BOE36.35`:
  - `notch_export_v21_current.c`
  - `notch_export_v22_current.c`

## Beta 0.7 objective
Beta 0.7 should not chase math changes. Its primary purpose is structural reduction:
- remove features and contracts that are already dead,
- shrink documentation sprawl,
- split hotspot files so future changes become local and readable,
- quarantine compatibility-only mechanisms before deciding whether to keep or delete them.

## Safe reduction candidates

### B0.7-S1 Documentation pruning around notch
Evidence:
- canonical source already exists: `docs/reference/notch-system-reference.md`
- surrounding notch docs are fragmented across `docs/core` and `docs/code`
- `docs/README.md` still points readers at older notch docs first

Safe action:
- audit each notch doc for unique content
- delete files fully subsumed by canonical reference
- keep only one canonical reference plus a minimal set of deep-dive docs
- update `docs/README.md` and user manual links to the new reading order

Expected reduction:
- lower doc-entry ambiguity
- less duplicated terminology drift (`visible / output / best match / regular / cad`)

### B0.7-S2 Test hotspot splitting without assertion changes
Evidence:
- `FreeformHelperViewModelTests.CommandsAndUndo.cs`: 1048 lines
- `NotchTableGeneratorTests.cs`: 916 lines
- `IndexMappingReportViewModelTests.cs`: 894 lines
- `NotchExportSelectionViewModelTests.cs`: 874 lines
- `RuntimeQueryUseCaseTests.cs`: 801 lines
- `FreeformHelperViewModelTests.SettingsPersistence.ProjectSaveLoad.cs`: 636 lines

Safe action:
- split by feature slice only
- reuse helpers through shared test infrastructure instead of duplicate local builders
- remove tests that target deleted dead paths only

Expected reduction:
- smaller diff scope per feature
- clearer ownership of export/runtime-query/persistence expectations

### B0.7-S3 UI ViewModel hotspot slicing
Evidence:
- `IndexMappingReportViewModel.cs`: 934 lines
- `ShellViewModel.cs`: 702 lines
- `CoordinatePlannerWorkspaceViewModel.cs`: 665 lines

Safe action:
- split by responsibility, not by random size
- preserve single-entry command/workflow rules from `AGENTS.md`
- move pure formatting/filtering/report projection into dedicated services or partials

Expected reduction:
- lower coupling between selection/workflow/report logic
- faster naming convergence and easier behavior review

### B0.7-S4 Large XAML page/component decomposition
Evidence:
- `NotchExportSelectionWindow.axaml`: 689 lines
- `CoordinatePlannerWorkspaceView.axaml`: 609 lines
- `IndexMappingReportWindow.axaml`: 607 lines
- `SimulationWorkspaceView.axaml`: 570 lines

Safe action:
- extract repeated sections into dedicated controls/views
- keep bindings and commands unchanged
- keep tokens/styles centralized; no inline style reintroduction

Expected reduction:
- better readability of workflow screens
- lower risk when modifying one region of a page

### B0.7-S5 Compatibility quarantine audit before deletion
Evidence:
- `V31` still exists in domain/application/tests/docs
- UI settings code explicitly says `V31` is hidden and kept for compatibility
- example `boe_30.25.json` was identified as a V31-era legacy fixture and removed by S11.149.

Safe action:
- do not delete `V31` directly in the first beta0.7 slice
- first produce dependency proof:
  - examples depending on it
  - tests depending on it
  - user-visible entry points depending on it
- then choose one of two paths:
  - keep but isolate into a compatibility module
  - delete after fixtures/examples/tests are migrated

Expected reduction:
- prevents speculative deletes
- turns compatibility debt into an explicit decision instead of hidden residue

## Recommended execution order
1. `B0.7-S1` documentation pruning
2. `B0.7-S2` test hotspot splitting
3. `B0.7-S3` ViewModel hotspot slicing
4. `B0.7-S4` XAML decomposition
5. `B0.7-S5` compatibility quarantine audit

## Non-goals for beta0.7
- no notch math rewrite
- no simulation behavior redefinition
- no UI theme redesign
- no bulk performance tuning unless uncovered by a structural slice
