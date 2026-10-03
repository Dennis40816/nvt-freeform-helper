# Repo Refactor Scan

Generated: 2026-04-06 15:46:39
Branch: beta0.3

## File counts
- tracked files: 734
- src: 511
- tests: 116
- docs: 51
- scripts: 22

## Line count hotspots
- src/FreeformHelper.UI/Styles/Controls.Core.axaml: 1193
- docs/archive/TODO-history-2026-03-05.md: 1131
- src/FreeformHelper.Application/Services/DxfRegularMaskAuditService.cs: 1129
- tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.cs: 1048
- docs/archive/TODO-history-2026-03-24.md: 971
- tests/FreeformHelper.Tests/UI/ViewModels/NotchExportSelectionViewModelTests.cs: 788
- tests/FreeformHelper.Tests/UI/Services/RuntimeQueryUseCaseTests.cs: 735
- src/FreeformHelper.UI/Styles/Tokens.axaml: 716
- tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs: 706
- src/FreeformHelper.Application/Services/CoordinatePlannerComputationService.cs: 701
- src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml: 689
- src/FreeformHelper.UI/ViewModels/CoordinatePlannerWorkspaceViewModel.cs: 665
- src/FreeformHelper.UI/ViewModels/ShellViewModel.cs: 636
- tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectSaveLoad.cs: 611
- tests/FreeformHelper.Tests/UI/ViewModels/IndexMappingReportViewModelTests.cs: 609
- src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml: 609
- src/FreeformHelper.UI/Styles/Controls.Form.axaml: 596
- src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.PropertyCallbacks.cs: 587
- src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs: 582
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs: 581
- src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs: 579
- tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs: 568
- src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Edits.cs: 560
- src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml: 557
- docs/reference/behavior-inventory.md: 556

## Direct state mutation hotspots
- scan scope: public mutable collection properties in src/**/*.cs
- hotspot count: 31
- category summary:
  - PublicSettableMutableCollectionProperty: 25
  - PublicMutableCollectionProperty: 6

### Top hotspots (with convergence route)
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs:544 [PublicMutableCollectionProperty]
  - snippet: `public List<int> SourceRowNumbers { get; } = new();`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.
- src/FreeformHelper.Application/Services/NotchApplySimulationService.cs:545 [PublicMutableCollectionProperty]
  - snippet: `public List<PendingLeg> Legs { get; } = new();`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.
- src/FreeformHelper.Application/Services/NotchV22CompensationService.SpatialIndex.cs:213 [PublicMutableCollectionProperty]
  - snippet: `public HashSet<int> EffectiveBoundaryRegularIndices { get; }`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.
- src/FreeformHelper.Application/Settings/GridSettings.cs:63 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<int> PerIcXChannels { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Application/Settings/GridSettings.cs:70 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<int> PerIcYChannels { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Application/Settings/GridSettings.cs:113 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<double> ColumnWidths { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Application/Settings/GridSettings.cs:119 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<bool> ColumnOverrides { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Application/Settings/GridSettings.cs:127 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<double> RowHeights { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Application/Settings/GridSettings.cs:133 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<bool> RowOverrides { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Application/Settings/NotchSettings.cs:23 [PublicSettableMutableCollectionProperty]
  - snippet: `public HashSet<NotchAlgorithmVersion> EnabledVersions { get; set; } = new()`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.cs:101 [PublicMutableCollectionProperty]
  - snippet: `public List<ParsedPolyline> Polylines { get; } = new();`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.
- src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.cs:103 [PublicMutableCollectionProperty]
  - snippet: `public List<ParsedInsert> Inserts { get; } = new();`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.
- src/FreeformHelper.Infrastructure/Project/ProjectDxfEditState.cs:10 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<int> SourceCadIds { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectDxfEditState.cs:12 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<ProjectCadPadSnapshot> OutputPads { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectDxfEditState.cs:23 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<Point2> Vertices { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectDxfEditState.cs:46 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<Point2> Vertices { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:62 [PublicSettableMutableCollectionProperty]
  - snippet: `public Dictionary<int, FreeformType> FreeformOverrides { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:69 [PublicSettableMutableCollectionProperty]
  - snippet: `public Dictionary<int, double> CadPadCustomValues { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:75 [PublicSettableMutableCollectionProperty]
  - snippet: `public HashSet<int> HiddenCadPadIds { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:81 [PublicSettableMutableCollectionProperty]
  - snippet: `public HashSet<int> VisibleDuplicateCadPadIds { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:87 [PublicSettableMutableCollectionProperty]
  - snippet: `public Dictionary<int, string> DxfCadLayerOverrides { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:93 [PublicSettableMutableCollectionProperty]
  - snippet: `public Dictionary<int, ProjectCadGeometrySnapshot> DxfCadGeometryOverrides { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:99 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<ProjectDxfCombinedCadGroup> DxfCombinedCadGroups { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:105 [PublicSettableMutableCollectionProperty]
  - snippet: `public Dictionary<int, int> DxfRegularMappingOverrides { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:111 [PublicSettableMutableCollectionProperty]
  - snippet: `public Dictionary<int, int> DxfVisibleIndexOverrides { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:123 [PublicSettableMutableCollectionProperty]
  - snippet: `public Dictionary<int, int> DxfVisibleIndexAnchorCadPadByIc { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:45 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<int> IcX { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:49 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<int> IcY { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:203 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<LayerSelectionSnapshot> LayerSelections { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:305 [PublicSettableMutableCollectionProperty]
  - snippet: `public List<string> EnabledVersions { get; set; } = new();`
  - convergence route: Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case.
- src/FreeformHelper.UI/ViewModels/NotchExportColumnFilterDialogViewModel.cs:60 [PublicMutableCollectionProperty]
  - snippet: `public ObservableCollection<NotchExportColumnFilterOptionViewModel> Options { get; }`
  - convergence route: Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade.

## Build gate
- status: ok
- log: build\logs\repo-skill-build.log

## Lint gate
- status: ok
- log: build\logs\repo-skill-lint.log
