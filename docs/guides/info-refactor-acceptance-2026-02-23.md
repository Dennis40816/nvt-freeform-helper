# Info Refactor Acceptance Guide (2026-02-23)

## 1. Scope and Purpose
This refactor focuses on "information readability" and "Step5 row selection experience":

1. The Inspector is changed to a layered presentation (summary / details / trace) to reduce how much information is shown at once.
2. A dot indicator visual language is introduced so that primary information, secondary information, warnings, and background calculation status have clear layers.
3. `Select notch rows to export` changes from a flat list to IC groups plus a right-side preview panel.
4. Row preview is linked to main-screen selection: when a preview item is selected in the row window, the main screen selects the matching CAD/REG.
5. Selection start is optimized: the large hit-test on mouse-down for box selection is removed, which improves left-click lag in large scenes.

## 2. Preparation Before Acceptance
1. Switch to branch: `refactor/non-notch-architecture`
2. Build:
```powershell
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release
```
3. Start the program (IDE or the existing startup method is fine).

## 3. Acceptance Cases

### Case A: Selection Start Delay (3635 Project)
Goal: Confirm that "pressing the left button to start box selection" no longer stalls noticeably.

1. Load the file:  
`example\BOE36.35\cz_36d2_6480x848_Lucid_panel_touch_block_CAD_20250729_NVTint_regular.dxf`
2. Zoom in to a dense area with many pads.
3. Hold the left button and start dragging a selection box directly (repeat 5 to 10 times).

Expected:
1. Box selection should start immediately after the left button is pressed. There should be no long pause.
2. The selection is applied only after the mouse is released (the selection result updates).

### Case B: Inspector Layered Readability
Goal: Confirm that the right-side Inspector has changed from "everything flat" to "summary first, low-frequency content collapsed."

1. On the right side, switch to the `Inspector` tab.
2. Click one CAD (then click one REG to test each).
3. Observe the `Inspector snapshot` section.
4. Expand/collapse `Snapshot details`.
5. Expand/collapse `Rule trace`.

Expected:
1. Four summary lines appear first (primary key, source, match, compensation), each with a dot indicator in front.
2. `Snapshot details` is collapsed by default. Expanding it shows the low-frequency detail fields.
3. `Rule trace` is collapsed by default, and its header shows the trace count.
4. If background calculation is in progress, a separate status line shows a notice.

### Case C: Step5 Row Selection Window Redesign
Goal: Confirm that row selection changed from a flat list to IC groups plus preview.

1. First, run to a state where notch rows can be generated (Step5 has output).
2. Enter the export flow and open the `Select notch rows` window.
3. Check whether the left side is grouped by `IC` (each group has `All/None`).
4. Click different rows (do not just tick the checkbox; click the row card directly).
5. Observe the content of the right-side `Row preview`.

Expected:
1. The left side shows IC groups, not a single long table.
2. Each row card shows core summary (version, diff, REG, CAD, payload summary).
3. The right-side preview shows: pad relation, payload, flags, values/comment, and quick analysis.

### Case D: Row and Drawing Linkage
Goal: Confirm that when a preview is selected in the row window, the main-screen selection syncs.

1. Keep the row selection window open.
2. On the left, click 2 to 3 different rows (especially rows from different ICs).
3. Look back at the main screen selection state (Selection summary / canvas highlight).

Expected:
1. When different rows are clicked, the main-screen selection switches to the CAD/REG corresponding to that row.
2. If a row has no CAD, at least the REG selection updates.

## 4. Regression Checks
1. `Select all / Select none / IC group All / IC group None` all update the counts correctly.
2. `Export selected` cannot be clicked when nothing is checked; it can be clicked when something is checked.
3. Export content is still output according to the checked rows and is not affected by preview switching.

## 5. Test Records for This Round (Development Side)
Executed:

```powershell
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Release --filter "FullyQualifiedName~NotchExportSelectionViewModelTests|FullyQualifiedName~UiLayoutGuardTests|FullyQualifiedName~WorkspaceInteractionStateTests"
```

Result: the targeted tests above passed.

Note: `dotnet test FreeformHelper.sln -c Release --no-build` still has existing failures on the current branch (logging policy / log formatting / UI snapshot baseline / Step3 existing tests). These are not new issues introduced by this feature change and need to be handled separately later.
