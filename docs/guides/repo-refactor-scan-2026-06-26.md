# Repo Refactor Scan

Generated: 2026-06-26 15:11:26
Branch: codex/code-size-cutdown

## File counts
- tracked files: 906
- src: 597
- tests: 153
- docs: 95
- scripts: 26

## Line count hotspots
- docs/archive/TODO-history-2026-05-05.md: 1377
- src/FreeformHelper.UI/Styles/Controls.Core.axaml: 1303
- src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs: 1227
- tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs: 1159
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
- tests/FreeformHelper.Tests/UI/ViewModels/SimulationWorkspaceViewModelTests.cs: 683
- docs/reference/notch-system-reference.md: 673
- src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml.cs: 649
- src/FreeformHelper.UI/Styles/Controls.Form.axaml: 617
- src/FreeformHelper.UI/ViewModels/CoordinatePlannerWorkspaceViewModel.cs: 603
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs: 599
- src/FreeformHelper.UI/Controls/PadCanvas.Properties.cs: 594
- src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml: 587
- src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs: 579

## Direct state mutation hotspots
- scan scope: public mutable collection properties in src/**/*.cs
- hotspot count: 1
- category summary:
  - PublicMutableCollectionProperty: 1

### Top hotspots (with convergence route)
- src/FreeformHelper.UI/Controls/BalancedWrapPanel.cs:149 [PublicMutableCollectionProperty]
  - snippet: `public List<Control> Children { get; } = new();`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.

## Triage notes
- `BalancedWrapPanel.Row.Children` is inside a private nested row model and is not an externally exposed mutable API; not selected for this code-size cut-down slice.
- First executable cut-down slice: remove unused Dev prototype style selectors from `src/FreeformHelper.UI/Styles/Controls.Form.axaml`, confirmed by `rg "devDropdownCandidate|devRightTabPreview|devInspector" src tests docs -n`.
- Second executable cut-down slice: remove unused legacy workspace style aliases from `src/FreeformHelper.UI/Styles/Controls.Core.axaml` and `src/FreeformHelper.UI/Styles/Controls.Scroll.axaml`; `rg "notchExport[A-Za-z0-9_]*|notchApply[A-Za-z0-9_]*" src/FreeformHelper.UI/Views src/FreeformHelper.UI/Controls tests -n` shows only `notchExportRestoreHint*` remains active in Views.
- Third executable cut-down slice: remove unused DXF edit legacy selectors `dxfEditAdvancedCard` and `dxfEditIconOnlyAction`, while preserving active `dxfEditCompactIconAction`.
- Fourth executable cut-down slice: remove unused workspace container/header selectors (`workspaceSummaryChip`, `workspaceDataCard`, `workspaceInfoFlyout`, `validationGroupTitle`, `verificationTableHeader*`) and update current density docs to point summary badges at `chipStatus` / `actionChip chipAction`.

## Build gate
- status: ok
- log: build\logs\repo-skill-build.log

## Lint gate
- status: ok
- log: build\logs\repo-skill-lint.log
