# Repo Refactor Scan

Generated: 2026-04-18 00:34:54
Branch: beta0.4

## File counts
- tracked files: 774
- src: 536
- tests: 123
- docs: 57
- scripts: 24

## Line count hotspots
- src/FreeformHelper.UI/Styles/Controls.Core.axaml: 1231
- docs/archive/TODO-history-2026-03-05.md: 1131
- tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs: 1048
- docs/archive/TODO-history-2026-03-24.md: 971
- src/FreeformHelper.UI/ViewModels/IndexMappingReportViewModel.cs: 864
- tests/FreeformHelper.Tests/UI/ViewModels/IndexMappingReportViewModelTests.cs: 859
- src/FreeformHelper.Application/Services/DxfRegularMaskAuditService.cs: 808
- tests/FreeformHelper.Tests/UI/ViewModels/NotchExportSelectionViewModelTests.cs: 805
- tests/FreeformHelper.Tests/UI/Services/RuntimeQueryUseCaseTests.cs: 739
- src/FreeformHelper.UI/Styles/Tokens.axaml: 719
- tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs: 706
- src/FreeformHelper.Application/Services/CoordinatePlannerComputationService.cs: 701
- src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml: 689
- src/FreeformHelper.UI/ViewModels/CoordinatePlannerWorkspaceViewModel.cs: 665
- src/FreeformHelper.UI/ViewModels/ShellViewModel.cs: 653
- tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectSaveLoad.cs: 613
- src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml: 609
- src/FreeformHelper.UI/Views/IndexMappingReportWindow.axaml: 604
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs: 599
- src/FreeformHelper.UI/Styles/Controls.Form.axaml: 596
- src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs: 579
- tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs: 568
- src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml: 557
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
