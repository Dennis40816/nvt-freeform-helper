# User guide gaps (zh-TW draft)

Source: `main` at the time of drafting `docs-draft/user-guide-zh-TW.md`.
Each item is a behaviour that the code does not make clear enough to document with confidence.
Paths are relative to the repo root.

## Project open / save

1. **Load Project prefers the DXF on disk over the embedded DXF.**
   `src/FreeformHelper.UI/Services/CadLoadUseCase.cs:59-80` loads `LastDxfPath` first and only falls back to `EmbeddedDxf` when the path is missing or fails.
   A user who chose `Embed` may expect the embedded copy to win. If the DXF on disk was edited after the save, the project silently loads the edited file.
   Needed: an owner decision on whether this is intended, plus a status or log line that says which source was used.

2. **`Save Project` / `Ctrl+S` always opens the save dialog.**
   `src/FreeformHelper.UI/Services/ProjectPersistenceUseCase.cs:28` always calls `PickSaveProjectPathAsync`. `src/FreeformHelper.UI/Views/FreeformHelperView.Pickers.cs:228` always suggests `freeform_helper_project.json`, even though `_lastSavedPath` is tracked (`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs:76`).
   It is unclear whether the dialog opens in the last folder or whether a "save to the same file" path was planned. The guide currently says "every save opens a dialog".

3. **What `Open DXF` resets.**
   `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.CadLoad.cs:19-20` clears DXF edit runtime state, and `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs:247` clears the last saved path.
   It is unclear whether freeform overrides, the regular visibility mask, and Step 1–5 results are also reset when a second DXF is opened in the same session.

## Layer list and DXF

4. **The layer bulk toggle shows `All off` when only some layers are off.**
   `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.LayerSelection.cs:36-40`.
   `docs/guides/settings-entry-matrix.md` (S10.10) describes `Off` as "all layers are disabled", which does not match the code. The guide describes the code behaviour.

5. **The effect of layer visibility on Step 1 / export is not obvious.**
   `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs:47-52` uses `BuildFilteredCadPadSet` for notch export, while DXF image export uses `BuildActiveCadPadSet` (only edit-hidden pads are removed, `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.cs:99-114`).
   The guide only says that layer visibility "affects display and some workflows". Someone needs to confirm exactly which steps respect layer visibility.

## Layer image export

6. **The layer image export ignores layer visibility, but the message says "visible".**
   `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:46` builds the layer list from `BuildActiveCadPadSet()`, so layers that are toggled off still appear. Line 81 reports `selected layer has no visible pad`.
   Needed: confirm the intended behaviour and wording.

7. **Where the remembered image export parameters are stored.**
   `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfLayerImageExportSettings.cs:40-51` calls both `MarkUnsaved()` (project) and `SchedulePersistAppGeneralSettings()` (app settings).
   It is unclear which copy wins after `Load Project`, especially with `Apply visual preferences on load` on or off.

## Settings / Advanced mapping

8. **"Panel alignment mode" wording.**
   The tooltips in `src/FreeformHelper.UI/Views/SettingsSections/SettingsGeneralSectionView.axaml:192` and `:203` say "used only in panel alignment mode". The UI has no option with that name. The code maps it to `Use panel AA` (`src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs:242`).
   The guide says `Use panel AA`. Suggest aligning the tooltip text with the option label.

9. **Precedence of `Coordinate pixel X/Y` between project and app settings.**
   `docs/guides/settings-entry-matrix.md` ("Persistence hierarchy") states that both stores hold the value and that app-general can override the project when the visual preference overlay is on, and that the final rule is pending R13.301/R13.305.
   Users on the 3635 project may see a different value after `Load Project` on another machine.

10. **The right panel General fields are documented as editable, but the UI shows a summary.**
    `docs/guides/settings-entry-matrix.md` (M11 Phase 2, item 1) says the right panel keeps editable General fields. `src/FreeformHelper.UI/Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml:12` shows a read-only `Project / grid` summary with the tooltip "Edit values in Settings > General".
    One of the two is stale.

## Workflow / export

11. **Step 5 does not require Step 2.**
    `src/FreeformHelper.UI/Services/WorkflowStepGateService.cs:80-93` only checks Step 1. A user can export a notch table without running `Detect freeform`.
    Needed: confirm whether 3635 handoff requires Step 2 and, if so, whether the gate or the guide should say so.

12. **Simulation safety gate when Simulation was never opened.**
    `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs:193` reads `GetCurrentSimulationSafetyAudit?.Invoke()`. The right panel tooltip says to "Refresh Simulation after geometry or notch changes".
    It is unclear whether export is blocked, allowed, or checked against a stale audit when the Simulation page has not been refreshed.

13. **Picking one version in `Export Notch Rows` changes project settings.**
    `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:80-109` turns the other version off (`EnableV21` / `EnableV22`). The log says "persist-on-save".
    The guide documents this as a common mistake. Confirm whether this is intended.

## Shortcuts

14. **`Ctrl+Z` scope.**
    `PushUndo` is called from DXF edits, layer selection, pad edits, and diff shift/indexing (`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Undo.cs:23`, plus `DxfEditing.*`, `Operations.LayerSelection*`, `PadEdits`, `DxfIndexing*`). No undo entry was found for Step 2 manual freeform override (`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Commands.cs:258-260`).
    `How To Use` says "Ctrl + Z undoes settings and pad edits". The exact list needs confirmation.

15. **The `How To Use` page is stale.**
    `src/FreeformHelper.UI/Views/HowToUseView.axaml:35` lists a `Match` menu, but the menu bar only has `File`, `Export`, and `Settings` (`src/FreeformHelper.UI/Views/FreeformHelperView.axaml:37-51`).
    `src/FreeformHelper.UI/Views/HowToUseView.axaml:28` lists the top tabs as `Freeform Helper, How To Use, Dev`, but `src/FreeformHelper.UI/MainWindow.axaml:47-67` also has `Simulation` and `Coordinate`.

## Console / logs

16. **The Console header and toolbar come from the `Nvt.Core.Avalonia` package.**
    `src/FreeformHelper.UI/Views/ConsolePanel.axaml:8` and `:40-53` use `ConsoleHeader`, `ConsoleToolbar`, and `ConsoleListView` from `Nvt.Core.Avalonia.LogConsole`. Their button labels, filters, and search are not in this repo.
    The guide describes them only in general terms. Screenshots are needed to name the controls.

17. **The log file location depends on the executable path.**
    `src/FreeformHelper.UI/NLog.config:7` and `src/FreeformHelper.UI/Logging/LoggingBootstrapper.cs:95-98` put logs at `<exe dir>/../../../../logs`.
    For a Debug build (`build/bin/FreeformHelper.UI/Debug/net10.0/`) this resolves to `build/logs/`. For the single-file publish (`build/publish/win-x64/single-file/`) it resolves to the repo root `logs/`. For an EXE copied to another folder, it resolves to a folder four levels up, which may not be writable.
    `README.zh-TW.md` section 7 says `logs/app.log`. Needed: a stated location for the published EXE handed to engineers.
