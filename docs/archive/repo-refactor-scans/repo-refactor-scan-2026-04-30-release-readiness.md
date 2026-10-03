# Repo Refactor Scan

Generated: 2026-04-30 00:09:55
Branch: codex/beta0.10-focus-highlight-search

## File counts
- tracked files: 855
- src: 586
- tests: 147
- docs: 63
- scripts: 24

## Line count hotspots
- src/FreeformHelper.UI/Styles/Controls.Core.axaml: 1247
- src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs: 1174
- docs/archive/TODO-history-2026-03-05.md: 1131
- tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs: 1089
- docs/archive/TODO-history-2026-03-24.md: 971
- tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs: 948
- src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.cs: 781
- src/FreeformHelper.UI/Styles/Tokens.axaml: 777
- src/FreeformHelper.Application/Services/CoordinatePlannerComputationService.cs: 701
- src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs: 686
- src/FreeformHelper.UI/ViewModels/SimulationWorkspaceViewModel.cs: 651
- docs/reference/notch-system-reference.md: 633
- src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs: 630
- tests/FreeformHelper.Tests/UI/ViewModels/SimulationWorkspaceViewModelTests.cs: 627
- src/FreeformHelper.UI/Styles/Controls.Form.axaml: 604
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs: 599
- src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml: 587
- src/FreeformHelper.UI/Controls/PadCanvas.Properties.cs: 581
- src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs: 579
- tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs: 574
- docs/reference/behavior-inventory.md: 558
- tests/FreeformHelper.Tests/Application/Notch/NotchV22CompensationServiceTests.cs: 555
- src/FreeformHelper.UI/Controls/PadCanvas.Input.Selection.cs: 550
- src/FreeformHelper.Application/Services/DxfRegularMaskAuditService.cs: 548
- src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs: 548
- tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs: 543
- src/FreeformHelper.UI/Views/DevSections/DevInspectorPrototypeSectionView.axaml: 541
- docs/core/notch-v21-v22-flow.md: 539
- src/FreeformHelper.UI/Styles/Controls.Panel.axaml: 529
- src/FreeformHelper.UI/Services/NotchDisplayProjector.cs: 519

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
