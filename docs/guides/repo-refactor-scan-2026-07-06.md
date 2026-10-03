# Repo Refactor Scan

Generated: 2026-07-06 20:53:47
Branch: codex/code-size-cutdown

## File counts
- tracked files: 914
- src: 599
- tests: 158
- docs: 96
- scripts: 26

## Line count hotspots
- docs/archive/TODO-history-2026-05-05.md: 1377
- src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs: 1227
- src/FreeformHelper.UI/Styles/Controls.Core.axaml: 1148
- tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs: 1147
- docs/archive/TODO-history-2026-03-05.md: 1131
- tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs: 1089
- src/FreeformHelper.UI/Styles/Tokens.axaml: 973
- docs/archive/TODO-history-2026-03-24.md: 971
- tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs: 970
- src/FreeformHelper.Application/Services/CoordinatePlannerComputationService.cs: 925
- src/FreeformHelper.UI/Views/DevView.axaml: 868
- src/FreeformHelper.UI/Styles/Controls.Action.axaml: 800
- src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.cs: 781
- src/FreeformHelper.UI/ViewModels/SimulationWorkspaceViewModel.cs: 740
- src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs: 687
- src/FreeformHelper.UI/ViewModels/CoordinatePlannerWorkspaceViewModel.Operations.cs: 684
- tests/FreeformHelper.Tests/UI/ViewModels/SimulationWorkspaceViewModelTests.cs: 676
- docs/reference/notch-system-reference.md: 673
- src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml.cs: 649
- src/FreeformHelper.UI/ViewModels/CoordinatePlannerWorkspaceViewModel.cs: 603
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs: 599
- src/FreeformHelper.UI/Controls/PadCanvas.Properties.cs: 594
- src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml: 587
- src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs: 579
- src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs: 574

## UI no-visual-change refactor review

Goal for the next UI refactor wave: reduce code size and style/layout duplication while preserving the current visible UI, action roles, density, and workflow behavior.

### Evidence
- UI hotspots in the repo-wide line-count list:
  - `src/FreeformHelper.UI/Styles/Controls.Core.axaml`: 1148 lines; still contains mixed core/workspace/DXF/validation responsibilities.
  - `src/FreeformHelper.UI/Views/DevView.axaml`: 868 lines; action role laboratory is valuable but large enough to extract into stable preview sections.
  - `src/FreeformHelper.UI/Styles/Controls.Action.axaml`: 800 lines; role system is centralized, but preview/guard coverage must remain intact if split.
  - `src/FreeformHelper.UI/ViewModels/SimulationWorkspaceViewModel.cs`: 740 lines; large UI orchestration surface.
  - `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs`: 687 lines; dense draft/apply property surface.
  - `src/FreeformHelper.UI/ViewModels/CoordinatePlannerWorkspaceViewModel.Operations.cs`: 684 lines; workflow operation hotspot.
- XAML class usage scan found 1834 `Classes="..."` assignments across `Views` and `Controls`, so role/style consistency should be guarded by existing `check-xaml-action-roles.ps1` before broad edits.
- Inline value scan in `Views`/`Controls` found limited structural values such as `MinWidth="0"`, `MinHeight="0"`, spinner `Width/Height="30"`, grid star widths, and a few `BorderThickness="0"` entries. This does not look like a broad inline color problem, but any new extraction must avoid adding inline colors/sizes.
- Stale-doc risk: `docs/reference/behavior-inventory.md` and `docs/guides/settings-entry-matrix.md` still describe old branch/release baselines in their headers. Refresh them before the visual-preserving UI refactor so reviewers know the current single-entry and settings-entry contracts.
- Direct mutable state scan found one UI control hotspot: `BalancedWrapPanel.Children` exposes a public mutable `List<Control>`.
- Lint gate initially failed on whitespace in five UI ViewModel partials. The whitespace was corrected during this scan turn and the rerun build/lint gates are now green.

### Recommended order
1. `S14.013`: refresh no-visual-change baseline/docs and guard expectations.
2. `S14.009`: split `Controls.Core.axaml` workspace/DXF responsibilities only after preserving selector precedence.
3. `S14.014`: close the `BalancedWrapPanel.Children` direct mutation hotspot.
4. `S14.015`: extract Dev page/action preview sections without changing the role laboratory result.
5. `S14.016`: extract shared Simulation/Coordinate workbench shell patterns.

### Multiple derivation path audit
- Acceptable multi-entry/single-path designs remain documented for Save, runtime query, settings draft/apply, and Step3 preview refresh in `behavior-inventory.md`.
- Current scan did not identify a new unacceptable UI/export/runtime re-derivation comparable to prior Notch display/allocation issues.
- The UI refactor TODOs therefore focus on style/layout responsibility split, direct mutable state, and stale guard documentation rather than changing computation paths.

## Direct state mutation hotspots
- scan scope: public mutable collection properties in src/**/*.cs
- hotspot count: 1
- category summary:
  - PublicMutableCollectionProperty: 1

### Top hotspots (with convergence route)
- src/FreeformHelper.UI/Controls/BalancedWrapPanel.cs:149 [PublicMutableCollectionProperty]
  - snippet: `public List<Control> Children { get; } = new();`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.

## Build gate
- status: ok
- log: build\logs\repo-skill-build.log

## Lint gate
- status: ok
- log: build\logs\repo-skill-lint.log
