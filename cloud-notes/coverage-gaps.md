# Coverage gaps through real entry paths

Base: origin/main at 5e48bec. Analysis only. No production or test code was changed.

## How this was measured

I collected every command property that the UI binds or defines. That gave 138 names, taken from `Command=` bindings in the `.axaml` files and from the `IRelayCommand`, `IAsyncRelayCommand` and `ICommand` properties in the view models. I also listed every code-behind handler such as `*_Click`, `OnTopLevelKeyDown` and the pointer handlers, and every window and dialog class.

For each name I searched `tests/` for the command property and then for the underlying view-model method. A hit counts as "covered" only when a test calls the command or the public view-model method and asserts on a result. A hit that only appears in `UiLayoutGuardTests` counts as "not covered", because that test reads the `.axaml` text or checks layout and never runs the action.

Limits of this method, stated plainly:

- I did not build the solution or run any test. All results come from reading source and test files, so a test that reaches a command through reflection or a string name could be missed.
- "Covered" means the command or method is exercised. It does not mean every branch is tested.
- Tests that drive a command through a headless window and a real button click were looked for. Only a few exist (for example `SaveProjectShortcutTests`, `MainWindowCloseTests`, `UiEventFailureTests`). The helper `UiEventProbe.Click(view, "<handler name>")` in `TestInfrastructure/UiEventProbe.cs` already clicks a code-behind handler by name, and I searched for handler names as strings, so only the two handlers named below are exercised. Almost all other command tests call the view model directly.
- The Application-layer logic under these commands is mostly well covered (notch generation, exporter, project store, migration, golden tests). The gaps below are in the UI entry layer that calls that logic.

## Summary counts

- Commands and view-model methods bound in the UI: 138.
- Covered through the command or a public view-model method: about 90. Two more names (`FitCommand`, `ClearRegularVisibilityMaskCommand`) appear only in `UiLayoutGuardTests` and are counted as not covered.
- Not covered by any test: about 40. The raw search returned 45 zero-hit names, but a few were noise (a placeholder name and a lambda parameter) or are reached through a public method such as `ImportCsvAsync`. The main groups are listed in the inventory.
- Code-behind handlers with no test that clicks them: 20 of 22 checked. Only `EditCascadeDetailsButton_Click` and `CopyAll_Click` (both in `UiEventFailureTests`) are exercised, and only for the failure path.
- Windows and dialogs with no test that opens them: `ConfirmDialog`, `WarningDialog`, `CoordinateArtifactDetailWindow`, `DxfLayerImageExportWindow`, `NotchDetailWindow`, `NotchExportColumnFilterWindow`, `NotchExportFilterBuilderWindow`. `CascadeIcSettingsWindow` appears only in a layout guard.

## Inventory by area

Legend: covered, partly covered, not covered. File names are under `tests/FreeformHelper.Tests/`.

### Main shell and navigation

| Entry point | Status | Evidence |
| --- | --- | --- |
| Show Workspace, How To Use, Dev, Simulation, Coordinate tabs | covered | `UI/ViewModels/ShellViewModelNavigationTests.cs`, `ShellViewModelSimulationTests.cs`, `ShellViewModelCoordinateTests.cs` |
| `OnShellTabClick` code-behind handler | not covered | no test clicks the tab strip |
| Ctrl+S save shortcut | covered | `UI/Smoke/SaveProjectShortcutTests.cs` |
| Other keys in `OnTopLevelKeyDown` | not covered | only a text match in `UiLayoutGuardTests.cs` |
| Close with unsaved project (Save and exit, Exit without saving) | covered | `MainWindowCloseTests.cs` |
| Open Settings button in the workflow panels (6 handlers) | not covered | no test |
| Show or hide left and right panel buttons | not covered | no test |
| Restore Notch Export Selection window button | not covered | no test |

### Project and files

| Entry point | Status | Evidence |
| --- | --- | --- |
| Open DXF | covered | 14 files, for example `FreeformHelperViewModelTests.CommandsAndUndo.InputAndExport.cs` |
| Load Project | covered | 33 files, including `DiagnosticsAndFreeformResultBehaviorTests.cs` |
| Save Project | covered | `ProjectLoadingInputTests.cs`, `MainWindowCloseTests.cs`, `SaveProjectShortcutTests.cs`, `FreeformHelperViewModelTests.SettingsPersistence.ProjectSave.cs` |
| Embed DXF confirm dialog (`ConfirmEmbedDxfAsync`) | partly covered | the delegate is stubbed in tests, so the real `ConfirmDialog` wording and result mapping is never run |
| Import and Clear Regular Visibility Mask | partly covered | import is covered in `FreeformHelperViewModelTests.RegularVisibilityMask.cs`; the clear command appears only in `UiLayoutGuardTests.cs` |

### Match, freeform, notch preview

| Entry point | Status | Evidence |
| --- | --- | --- |
| Match | covered | 8 files, for example `FreeformHelperViewModelTests.NotchResolvedSnapshot.cs` |
| Auto Detect Freeforms | covered | 9 files |
| Clear Step 1 to 5 result | covered | `NotchExportBusyLifecycleBehaviorTests.cs`, `DiagnosticsAndFreeformResultBehaviorTests.cs` |
| Set freeform None, X, Y, XY on selection | covered | `ManualFreeformTaggingTests.cs` |
| Previous and Next notch preview stage | not covered | no test (`ShiftNotchPreviewStage` never called) |
| Notch validation: use selected regular, analyze, clear | covered | `NotchValidationDisplayBehaviorTests.cs`, `NotchValidationCountProjectionTests.cs` |
| Notch validation: focus an item from the list | not covered | `FocusNotchValidationItemCommand` has no test |
| Show Notch Detail | partly covered | one test in `FreeformHelperViewModelTests.NotchResolvedSnapshot.cs`; `NotchDetailWindow` is never opened |
| Fit, Clear Selection, Quick Focus | partly covered | Quick Focus has one test (`FreeformHelperViewModelTests.Basics.LocateAndMatchActions.cs`); no test executes the Fit or Clear Selection commands, and Fit appears only in a layout guard |

### Pad info popover

| Entry point | Status | Evidence |
| --- | --- | --- |
| Toggle geometry details, toggle low-frequency sections, locate match | covered | `UI/ViewModels/PadInfoViewModelTests.cs` |
| Regular pad: Apply Changes (pad width and height) and Discard Changes | not covered | `ApplyChanges` and `DiscardChanges` are never called. Sizing logic is covered below the popover in `ManualSizingServiceTests.cs`, `ManualSizingUseCaseTests.cs`, `ManualSizingInputPropertyTests.cs` |
| Regular pad: Reset Pad Size | not covered | no test |
| CAD pad: Apply and Clear output FW diff override | not covered | no test of the popover command; the report window's apply path is covered |
| CAD pad: Set and Clear DXF index anchor | not covered | tests mention anchors only through settings and display |
| CAD pad: select area bucket, highlight all notch owners | not covered | no test |
| `ApplyAndClose_Click`, `DiscardAndClose_Click` in `PadInfoPopover` | not covered | no test |

### DXF editing

| Entry point | Status | Evidence |
| --- | --- | --- |
| Delete, Restore last deleted, Restore all hidden | covered | `FreeformHelperViewModelTests.CommandsAndUndo.CadEditing.cs`, `DxfEditCountProjectionTests.cs` |
| Combine, Clear combined | covered | `...GeometryTransforms.cs`, `...CadEditing.cs` |
| Move to layer, Create layer and move | covered | `...GeometryTransforms.cs`, `DxfEditCountProjectionTests.cs` |
| Rotate | covered | `...GeometryTransforms.cs` |
| Undo | covered | 6 files |
| Reset all DXF edits | partly covered | command covered; the confirm dialog in `LeftDxfPanel.ResetAllDxfEditsButton_Click` is only text-matched in `UiLayoutGuardTests.cs` |
| Change list windows: Hidden, Duplicate, Combined | partly covered | opened in `ProjectLoadingSecondaryWindowTests.cs` and `DxfNestedCombineClearCharacterizationTests.cs` |
| Change list windows: Moved and Rotated | not covered | `OpenMovedDxfEditChangeListCommand` and `OpenRotatedDxfEditChangeListCommand` have no test |
| Change list window: kind tabs (SetHidden, SetDuplicate, SetCombined, SetMoved, SetRotated), Refresh, Focus, Close | not covered | no test |
| Change list window: apply, restore, rehide, select entry | covered | `DxfEditChangeListViewModelTests.cs` |
| Check DXF quality, select overlap pads, clear highlights | covered | `DxfOverlapResultBehaviorTests.cs`, `OperationProgressLifecycleBehaviorTests.cs` |
| Show DXF overlap details (opens report) | not covered | the report window is only exercised for Copy All failure in `UiEventFailureTests.cs` |

### Exports

| Entry point | Status | Evidence |
| --- | --- | --- |
| Export Notch | covered | `NotchExportBusyLifecycleBehaviorTests.cs`, `Cad3635EndToEndBenchmarkTests.cs`, `FreeformHelperViewModelTests.NotchGenerationStaleCompletion.cs`, `FreeformHelperViewModelTests.NotchExportCache.cs` |
| Notch export selection window: Export, Cancel, Copy payload, filter builder, column filter | partly covered | the view model is tested in `NotchExportSelectionViewModelTests.*`; the window has one layout test (`NotchExportSelectionWindowSmokeTests.cs`) and none of its buttons are clicked |
| Notch export selection: Select all and none per group, Toggle selected only | partly covered | select none is tested; select all and toggle selected only are not |
| Export DXF (visible) | partly covered | one test checks the file for the regular-layer case; no test checks hidden, combined or moved pads in the output |
| Export DXF (all) | partly covered | only the "no DXF loaded" status is tested |
| Export DXF layer image | partly covered | missing-DXF warning and initial request defaults are tested; the settings window and the image writer call are not (only `BmpWriterTests.cs` covers the bitmap encoder) |
| Coordinate planner: export CSV and JSON | covered | `CoordinatePlannerWorkspaceViewModelTests.cs` |
| Coordinate planner: export preview PNG | not covered | the view model only forwards to a host delegate that tests never set |
| Coordinate planner: copy selected, copy all, sort, add point, add path, clear custom | covered | `CoordinatePlannerWorkspaceViewModelTests.cs` |
| Coordinate planner: sync from workspace, fit canvas | not covered | no test |
| Coordinate planner: use active area safe corners, show artifact detail command | covered | `CoordinatePlannerWorkspaceViewModelTests.cs` |
| Coordinate artifact detail window (real window) | not covered | `CoordinateArtifactDetailWindow` is never opened |
| Copper path replay: copy, CSV, JSON | covered | `SimulationWorkspaceViewModelTests.cs` |

### Simulation workspace

| Entry point | Status | Evidence |
| --- | --- | --- |
| Open Simulation host | partly covered | the host view model is tested in `SimulationHostViewModelTests.cs`; `OpenSimulationCommand` itself is not |
| Import CSV | covered | `SimulationWorkspaceViewModelTests.cs` (through the public method) |
| Clear CSV | not covered | no test |
| Manual presets, randomize, apply selected override | covered | `SimulationWorkspaceViewModelTests.cs` |
| Clear selected override, Clear all manual overrides | not covered | no test |
| Play and pause (`TogglePlayback`) | not covered | the playback test steps frames only; the timer path is not run |
| Previous and next frame | covered | `SimulationWorkspaceViewModelTests.cs` |
| Replay copper path | covered | `SimulationWorkspaceViewModelTests.cs` |
| Use current copper as path start and end | not covered | no test |
| Select copper path replay row | not covered | no test |
| Fit canvas | not covered | no test |

### Index mapping report window

| Entry point | Status | Evidence |
| --- | --- | --- |
| Analyze index mapping | covered | `DiagnosticsAndFreeformResultBehaviorTests.cs` and 6 others |
| Apply diff override, apply segment overrides, apply output FW diff overrides, clear override, select decision, select verification step, locate issue | covered | `IndexMappingReportViewModelTests.Actions.cs`, `...Verification.cs`, `ProjectLoadingSecondaryWindowTests.Reports.cs` |
| Decision filter mode buttons (8 bindings) | not covered | `SetDecisionFilterModeCommand` has no test |

### Settings

| Entry point | Status | Evidence |
| --- | --- | --- |
| Save and Cancel | covered | `SettingsLoadingRegionBehaviorTests.cs`, `FreeformHelperViewModelTests.SettingsPersistence*.cs` |
| Reset all settings (view-model call) | covered | `SettingsLoadingRegionBehaviorTests.cs` |
| Reset all settings button with confirm dialog | not covered | `ResetAllSettingsButton_Click` and its `ConfirmDialog` have no test |
| Enable and Disable per category | not covered | no test |
| Cascade IC details window | partly covered | the Edit button's failure path is clicked in `UiEventFailureTests.cs`; the success path, the Done button and edited values are not tested |

### Console panel

| Entry point | Status | Evidence |
| --- | --- | --- |
| Copy visible, save log | covered | `ConsoleSessionTests.cs`, `ConsoleZoomTests.cs` |
| Copy selection | not covered | no test |

## Ranked gaps

Ranking rule: data loss or wrong export first, then crashes, then cosmetic. Within a class, the more often a user is likely to hit the path ranks higher.

### Data loss or wrong output

1. Export DXF (visible and all) with edited pads. Only one case of the visible export is checked. The "all" export has no content test at all. An edit that is dropped or duplicated in the file would reach a user's CAD tool silently.
2. Notch export selection window, Export button, then the written file. The window is never clicked, so the step that turns the user's chosen rows into the exported table is covered only through a stubbed delegate.
3. Pad popover Apply Changes and Discard Changes for regular pads. This is the only UI path that sends a manual pad size into the sizing use case, and a wrong width or height changes the notch result.
4. CAD pad popover FW diff override and DXF index anchor commands. These change the exported index values, and the popover command layer has no test.
5. Coordinate planner PNG export and artifact detail window. A user gets a file or a clipboard value that no test has ever produced.
6. DXF layer image export. The writer is tested, but the settings window, layer choice and result path are not.
7. Reset all DXF edits and Reset all settings confirm dialogs. Both are destructive. Tests cover the command, not the guard that stops an accidental reset when the user presses Cancel.

### Crashes or stuck state

8. Simulation: Clear CSV, Clear selected override, Clear all manual overrides, play and pause. These change state used by later frames, and a stale frame list or a running timer after a clear can throw.
9. DXF edit change list window: kind switching (Hidden, Duplicate, Combined, Moved, Rotated), Refresh, Focus, and opening the Moved and Rotated lists.
10. Notch preview previous and next stage, plus notch validation item focus. These move a stage index that other views read.
11. Dialog classes `ConfirmDialog` and `WarningDialog` never opened in a test, and the export dialogs' Cancel paths.

### Cosmetic or convenience

12. Decision filter buttons in the index mapping report.
13. Panel show and hide buttons, shell tab clicks, Open Settings buttons.
14. Console copy selection, fit canvas buttons, select-all in groups.

## Test plans for the top 10 gaps

Each plan uses the existing headless test host and the helpers in `tests/FreeformHelper.Tests/TestInfrastructure` (`HeadlessAppBootstrap`, `TestFiles`, `ExampleProjectFixture`, and `UiEventProbe.Click` for code-behind handlers). The file names and assertions below are suggestions and have not been compiled.

**1. Export DXF visible and all, with edits.** Entry point: `FreeformHelperViewModel.ExportDxfVisibleCommand` and `ExportDxfAllCommand`, with `PickSaveExportPathAsync` returning a temp path. Input: a view model with a small imported DXF of four pads on two layers, then hide one pad, combine two pads, move one pad to a new layer, and rotate one pad. Assertion: read the written DXF text and count the `LWPOLYLINE` entities and the layer names. The visible export must omit the hidden pad and show the combined pad once; the "all" export must include the hidden pad on its original layer. Run both exports to the same path to check that the second write replaces the first without leftovers.

**2. Notch export selection window, Export button, written file.** Entry point: `NotchExportSelectionWindow` opened through `FreeformHelperView.ShowNotchExportSelectionWindowAsync` in an `AvaloniaFact`, then a real click on the Export button, then the file produced by `ExportNotchCommand`. Input: a notch table with at least six rows where two rows are unchecked in the dialog. Assertion: the dialog closes with a result, the exported file contains only the checked rows in table order, and a blocked selection (the case handled by `TryGetExportBlockMessage`) shows `WarningDialog` and leaves the window open. Add a Cancel click that must leave no file on disk.

**3. Regular pad popover Apply and Discard.** Entry point: `RegularPadInfoViewModel.ApplyChangesCommand` and `DiscardChangesCommand`, built through the same factory the popover uses, with a callback that records `(rows, cols, width, height)`. Input: edit only the width, apply; then edit only the height, discard. Assertion: the first apply sends `width` and a null `height`, the dirty flags reset, a second apply sends nothing; discard restores the original value and sends nothing. Add an end-to-end case with a real `FreeformHelperViewModel` and assert that the pad size override appears in the regular grid afterwards.

**4. CAD pad popover FW diff override and index anchor.** Entry point: `CadPadInfoViewModel.ApplyCadOutputFwDiffOverrideCommand`, `ClearCadOutputFwDiffOverrideCommand`, `SetDxfIndexAnchorCommand` and `ClearDxfIndexAnchorCommand`. Input: a view model for a single CAD pad with recording callbacks; set the override to 7.6 and to 2 000 000, then clear. Assertion: the callback receives 8 and then 1 000 000 (the rounding and clamp in `ApplyCadOutputFwDiffOverride`), `HasCadOutputFwDiffOverride` flips, and `DxfIndexText` follows. With a real main view model, assert that the notch export index for that pad changes and returns after Clear.

**5. Coordinate planner PNG export and detail window.** Entry point: `CoordinatePlannerWorkspaceViewModel.ExportPreviewPngCommand` in two tests. First, with `RequestExportPreviewPngAsync` set to return true then false, assert the status text "Coordinate preview exported." and "Coordinate preview export cancelled.", and with the delegate null assert the "unavailable" message. Second, in an `AvaloniaFact`, host `CoordinatePlannerWorkspaceView`, stub the save picker to a temp path, run the command, and assert that a non-empty PNG file with the PNG signature bytes exists. Also open `CoordinateArtifactDetailWindow` through `ShowArtifactDetailCommand` and click Close.

**6. DXF layer image export.** Entry point: `ExportDxfLayerImageCommand` on a view model with a loaded DXF, plus `DxfLayerImageExportWindow` in an `AvaloniaFact`. Input: choose the second layer, a non-default size, and a temp output path. Assertion: Export writes a file whose header matches the chosen format and whose dimensions match the request; Cancel writes nothing and the status text says so; the chosen layer is remembered for the next request (this extends `DxfLayerImageExportSettingsBehaviorTests`).

**7. Destructive confirm dialogs.** Entry point: `LeftDxfPanel.ResetAllDxfEditsButton_Click` and `SettingsWindow.ResetAllSettingsButton_Click` in an `AvaloniaFact`, with a window host. Input: a view model that has edits (a hidden pad, a moved pad) or changed settings. Assertion: click the button, find the open `ConfirmDialog`, click Cancel, and assert that edits and settings are unchanged; repeat with Reset and assert they return to defaults. A timeout guard is needed so a missing dialog fails fast instead of hanging.

**8. Simulation clear commands and playback.** Entry point: `SimulationWorkspaceViewModel.ClearCsvCommand`, `ClearSelectedOverrideCommand`, `ClearAllManualOverridesCommand`, `TogglePlaybackCommand`, `FitCanvasCommand`. Input: import the two-frame CSV used in `PlaybackCommands_WhenCsvLoaded_AdvanceAndClampFrames`, apply two manual overrides, select one. Assertion: after Clear selected override only one override remains and `CanExecute` of the command turns false; after Clear all the overrides are zero and the frame summary is consistent; after Clear CSV `HasCsvFiles` is false, `CanStepFrames` is false and the frame text resets. For playback, use `HeadlessUiPump` to let the `DispatcherTimer` created in `SimulationWorkspaceViewModel.Playback.cs` fire and assert the frame index moves and that a second toggle stops it; assert `FitCanvasRequested` fires once.

**9. DXF edit change list window.** Entry point: `FreeformHelperViewModel.OpenMovedDxfEditChangeListCommand`, `OpenRotatedDxfEditChangeListCommand`, then the window's `SetHiddenKindCommand` through `SetRotatedKindCommand`, `RefreshCommand`, `FocusSelectedEntryCommand`, `CloseCommand`. Input: a project with one moved pad and one rotated pad (reuse the fixtures in `FreeformHelperViewModelTests.CommandsAndUndo.GeometryTransforms.cs`). Assertion: the Moved list shows exactly the moved pad, the Rotated list shows the rotated pad, switching kind changes the rows without throwing, Refresh after an Undo removes the row, Focus raises the focus request for the right pad id, and Close raises `CloseRequested` once.

**10. Notch preview stage navigation and validation focus.** Entry point: `FreeformHelperViewModel.PreviousNotchPreviewStageCommand`, `NextNotchPreviewStageCommand` and `FocusNotchValidationItemCommand`. Input: a view model with a generated notch preview that has at least three stages, and a validation result with two items. Assertion: Next from the last stage and Previous from the first stage stay in range, the displayed stage text and canvas overlay change with the stage, and focusing a validation item selects the matching regular pad and moves the canvas selection (use the same selection probe as `NotchValidationDisplayBehaviorTests`).

## Notes for whoever adopts this

- Plans 1, 2, 4 and 7 are the ones most likely to find a real defect, because each crosses a boundary that tests currently stub out.
- Per `AGENTS.md`, new tests must follow the Core testing rules and must not pin private names. Prefer the public command or the window's real button over reflection helpers like `FreeformHelperViewModelPrivateAccess`.
- If a plan needs a new fixture, put the sample under the existing `ExampleProjectFixture` pattern and do not copy anything from the private `example/` submodule into the repository.
- Re-run the name search before starting work. Tests may have been added after 5e48bec.
