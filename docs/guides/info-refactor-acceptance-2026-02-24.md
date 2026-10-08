# Info Refactor Acceptance Guide (2026-02-24)

## 1. Acceptance Goals
This acceptance focuses on tonight's `T1~T4`:

1. The Inspector shows everything at once, using fixed block sections (not collapsible). Content does not overflow the container.
2. Canvas ratio labels avoid collisions (`Match` and Notch ratio do not overlap).
3. Step5 `Select notch rows` window information is reorganized (fixed IC groups, row field breakdown, complete preview fields).
4. Selection interaction is unblocked (dragging a selection does not stutter due to inspector or deferred context creation).

## 2. Preparation Before Acceptance
1. Branch: `refactor-non-notch-architecture`
2. Build:
```powershell
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release
```
3. Tests (run at least once):
```powershell
scripts/tests/run-tests.ps1 -Group ui-core -Configuration Release
scripts/tests/run-tests.ps1 -Group ui-snapshots -Configuration Release
scripts/tests/run-tests.ps1 -Group smoke -Configuration Release
```
4. Test data:
`example/BOE36.35/project_3635.json`
`example/BOE36.35/cz_36d2_6480x848_Lucid_panel_touch_block_CAD_20250729_NVTint_regular.dxf`

## 3. Manual Acceptance Cases

### Case A: Inspector Readability and Overflow Prevention
1. On the right side, switch to `Inspector`.
2. Select one CAD, then select one REG.
3. Check the `Inspector snapshot` content.

Expected:
1. The content has three fixed sections: `summary`, `Snapshot details`, and `Rule trace`, with a single-line separator between sections.
2. There is no collapsible dropdown.
3. Long strings wrap (`WrapWithOverflow`) and do not overflow the outer container.

### Case B: Ratio Label Overlap
1. Make a selection in an area that has both Match and Notch ratio (3635 dense area recommended).
2. Repeatedly zoom (in/out) and switch selections.

Expected:
1. The `Match` label keeps priority.
2. Notch ratio labels avoid already-occupied areas and do not directly overlap `Match`.

### Case C: Step5 Row Window
1. Go to Step5 export and open `Select notch rows`.
2. Check the left-side IC section and the row card information.
3. Click different rows and observe the right-side preview.

Expected:
1. The left-side IC groups are fixed blocks (not collapsible).
2. Each group shows statistics: `v2.2 / legacy / CAD linked`.
3. Row cards contain at least: `row/version/IC/diff`, `REG->CAD`, `column breakdown`, `values/comment`.
4. The right-side preview contains: `pad relation`, `payload`, `column breakdown`, `flags`, `analysis`.

### Case D: Selection Unblocking (Key Focus)
1. Load 3635 and use `Fit to content`.
2. In a dense multi-pad area, hold the left mouse button and drag a selection box directly, 10 times in a row.
3. At the same time, observe whether the right-side inspector blocks during multi-selection.

Expected:
1. Starting a box selection should not be noticeably stuck.
2. During multi-selection, the inspector uses a lightweight mode (no real-time calculation for a single pad).
3. After release, the correct selection result is still obtained, and subsequent operations remain available.

## 4. Log Verification (Performance)
Check the selection debug entries in `build/logs/app.log`:

1. Keyword: `Selection updated:`
2. Fields should include: `summary=...ms, inspector=...ms, notchPreview=...ms, total=...ms`
3. Compare before and after the change. `inspector` and `total` should not stay high for long periods during common box-selection workflows.

## 5. Supplementary Notes
1. This round only covers `H8/E1 Phase A`: reduce latency through guard and deferred position adjustments. No major algorithm changes.
2. If extreme scenarios still stutter, the next round moves to `Phase B` (selection event debounce + background snapshot pipeline).
