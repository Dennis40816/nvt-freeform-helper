# FreeformHelper User Manual
Last updated: 2026-08-08

This document is for users taking over a project, validating a panel, or running the full Step1~Step5 for the first time.  
The goal is for readers to learn from the document alone:

- What this app does
- Where each page is
- What each step takes as input and produces
- Where to look during validation

## 1. What this app does

The main purposes of FreeformHelper are:

1. Load DXF CAD pads
2. Create or import a regular grid
3. Establish `CAD ↔ Regular` geometry mappings
4. Tag freeform
5. Preview and export notch tables
6. Validate results with diagnostics / simulation / export review

The main flow can be summarized as:

```text
Load DXF / Load Project
-> Step1 Geometry Match
-> Step2 Freeform
-> Step3 Notch Preview
-> Step4 Mapping Diagnostics
-> Step5 Export
```

## 2. Main pages

### 2.1 `Freeform Helper`
- Main workspace page.
- DXF / layer / geometry editing is on the left.
- The AA / CAD / regular canvas is in the center.
- The Step1~Step5 workflow and General settings are on the right.
- The terminal / log is at the bottom.

### 2.2 `How To Use`
- Quick instructions built into the UI.
- Suitable for quickly checking keyboard shortcuts and key terms.

### 2.3 `Simulation`
- Dedicated to before/after comparisons of regular pad values, CSV playback, and Before/After/Delta observations.
- Uses the same workspace regular grid without creating a second grid.

### 2.4 `Dev`
- Used to preview controls, styles, and debug behavior.
- General users usually do not need to visit this page.

## 3. Basic operations and canvas shortcuts

### 3.1 Canvas navigation
- Middle-button drag: pan
- `Space + left-button drag`: pan
- Mouse wheel: zoom
- `Ctrl + mouse wheel`: fast zoom
- `F`: Fit current content
- `Shift + arrow keys`: fine pan adjustment

### 3.2 Selection
- Left click: select a pad
- `Ctrl/Shift + left click`: add/remove selection
- `Alt + left click`: prefer regular when CAD / regular overlap
- Drag in a blank area: box selection
- Double click: open pad info
- Click a blank area: clear selection

## 4. Recommended workflow for first-time use

### 4.1 Loading
1. `File -> Open DXF`
2. Or `File -> Load Project`

When starting from DXF for the first time, the recommended order is:
1. Open DXF
2. Check layer visibility
3. Check grid / cascade / AA size
4. Then run Step1

### 4.2 Continuing an existing project
1. `Load Project`
2. Check that the workflow state on the right has replayed correctly
3. Check that the canvas, selection, and step summary match expectations

## 5. Left panel: DXF / editing

### 5.1 Layer visibility
- Show/hide CAD pads by layer.
- If the regular source is `From DXF layer`, that layer is hidden from the CAD display layers to avoid visual overlap.

### 5.2 DXF overlap checks
- `Check DXF overlaps` finds duplicate pads, overlapping pads, and abnormal geometry.
- `Details / Select / Clear` opens the review page.

### 5.3 DXF edit
Common actions:
- `Hide`: manually hide selected CAD pads
- `Duplicate`: exact duplicates detected automatically by the system, managed separately
- `Combine`: create a synthetic pad
- `Move`: move to the specified layer
- `Rotate`: rotate the selected group or specified layer as a rigid body around a shared center

Diagnostics and edit review use a review workspace similar to export:
- row click: select for the inspector
- `Locate/Focus`: explicit action

## 6. Central workspace: AA / CAD / Regular

### 6.1 Main displays
- `CAD layer`
- `Regular grid`
- `Color by area`
- `Highlight unmatched`
- `Freeform / Notch overlay`

### 6.2 The three most common checks here
1. Whether geometry overlap is reasonable
2. Whether regular / diff numbering is reasonable
3. Whether the Step3 Notch overlay matches expectations

## 7. Right panel: main workflow

## 7.1 General settings

This mainly controls:
- cascade / IC layout
- X/Y channels
- AA size
- grid padding
- scan order
- alignment mode

These changes affect the subsequent Step1~Step5.

## 7.2 Step1 - Geometry Match

Main purpose:
- Establish `CAD ↔ Regular` geometry overlap relationships

This step produces:
- `PadMatchResult`
- raw best-match candidates
- Step1 summary

### Step1 now has another important feature: `Regular Visibility Mask (SeeRegular.csv)`
- `Import Regular Visibility Mask (SeeRegular.csv)`
- `Use Regular Visibility Mask`
- `Clear mask`

This mask means:
- If a regular pad in `SeeRegular.csv` is `0` in every frame
- That regular pad has no actual signal surface
- Step4 then uses this mask to constrain diff assignment; Simulation also builds its active surface from `regular grid ∩ mask`, while compensation still executes the notch table generated through the shared path

Note:
- The mask is explicitly imported and is not automatically searched for in directories adjacent to the project; a saved project snapshot/embedded mask can still be restored during `Load Project`
- The mask does not rewrite Step1 geometry truth
- It acts as an assignment constraint after Step1 and explicitly limits the Simulation active surface

## 7.3 Step2 - Freeform

Main purpose:
- Tag regular pads as `None / XWay / YWay / XYWay`

There are two sources:
1. auto detect
2. manual override

Common validation points:
- If a CAD pad physically corresponds to only one regular pad, it should not be misclassified as freeform because of a tail
- `freeformAxisThreshold` strongly affects auto detect; check its configured value

## 7.4 Step3 - Notch Preview

Main purpose:
- Preview `ToRegular / ToFull / Combined`
- View Stage1 / Stage2 / Stage3 overlays

Key points:
- `ToRegular`: area relationship between CAD and regular
- `ToFull`: whether safe expansion is possible under boundary / owner / gate rules
- `Combined`: combined ratio

Common places to inspect:
- Stage overlay on the AA canvas
- Notch section in the pad inspector
- `query notch` / `query notch-stage`

## 7.5 Step4 - Mapping Diagnostics

Main purpose:
- Check whether `CAD Output FW Diff` is reasonable
- View `raw best / masked best / suggested diff`
- Find low-confidence, ambiguous, unmapped, and mask-changed cases

This page is currently decision-first:
- Summary chips at the top
- Decision list on the left
- Inspector on the right
- Row color / focus behavior matches export review

Common fields:
- `Current`
- `Raw best`
- `Masked best`
- `Suggested`
- `Reason`

Common validation questions:
- Why does this CAD pad not use the diff of its best match?
- Did the `mask` change it, or was the current assignment already different?

## 7.6 Step5 - Export

Main purpose:
- Generate a notch table
- Preview rows
- Manually include/exclude
- Export CSV / `C v2.1` / `C v2.2`

Key points:
- Supports `V21 + V22` by default
- `V22` is the mainline
- `V21` retains legacy rows and geometry comparisons
- CSV review is mainly for trace / diff review, not a contract for direct FW import
- For direct FW import, export `C v2.1` or `C v2.2`
- CSV review uses the fixed sort order `IC -> FW Diff -> Version -> REG -> CAD`, with fixed fields through `payload_01..payload_09`

The `Select notch rows` window is the current reference implementation for review UX:
- row color
- inspector
- group strip
- Explicit `Locate/Preview`

## 8. Simulation page

Simulation is a numeric work page that shares the workspace with `Freeform Helper`.

Main uses:
- Observe Before / After / Delta using regular pad values
- Import CSV frames for playback
- Check value distributions before and after applying a table

### 8.1 Display modes
- `Before`
- `After`
- `Delta`
- `Changed only`

### 8.2 Coloring modes
- `AUTO`
- `TH`

Coloring is only an aid; the core information is:
- The value at the center of each regular pad

### 8.3 Input sources
- Manual
- Global baseline
- CSV import

### 8.4 CSV playback
- Supports frame-by-frame playback
- Adjustable FPS
- Changes only projected values without rebuilding the grid

## 9. Where to look during export / inspection / validation

### 9.1 To validate geometry
- Check Step1
- Check pad info
- Check `query pad`

### 9.2 To validate freeform
- Check Step2 summary
- Check the regular / CAD inspector
- Check whether freeform overrides exist

### 9.3 To validate notch
- Check Step3 overlay
- Check `query notch`
- Check `query notch-stage`
- Check Step5 export row preview

### 9.4 To validate diff numbering
- Check Step4 diagnostics
- Compare:
  - current
  - raw best
  - masked best
  - suggested

### 9.5 To validate measured panel signals
- Import `SeeRegular.csv` into the Step1 mask
- Check how it changes Step4, the final notch table, and the Simulation active surface

## 10. Save / Load Project

### 10.1 Save Project
- Saves:
  - grid
  - freeform override
  - DXF edit
  - notch settings
  - workflow state

### 10.2 Load Project
- Attempts to replay the workflow after loading
- App-level settings follow the deferred flush contract

## 11. Runtime CLI

When the UI is running, query from another terminal:

- `query status`
- `query pad`
- `query notch`
- `query notch-stage`
- `query notch-validation`
- `query simulation`

Full commands:
- `docs/reference/runtime-cli-plan.md`

## 12. Recommended checklist for first-time validation

1. Load a project or DXF
2. Confirm that grid / AA / cascade are correct
3. Run Step1 and check that overlap is reasonable
4. If `SeeRegular.csv` is available, import it manually and decide whether to enable the mask
5. Run Step2 and check that freeform is reasonable
6. Check Step3 overlay and inspector
7. Open Step4 diagnostics and check changed / removed / ambiguous
8. Open Step5 row preview and confirm output rows and target versions
9. To inspect value distributions, switch to Simulation

## 13. Related documents

- Main algorithms: `docs/core/freeform-helper-algorithms.md`
- Notch canonical reference: `docs/reference/notch-system-reference.md`
- Notch flow deep-dive: `docs/core/notch-v21-v22-flow.md`
- Notch V22 algorithm: `docs/core/notch-v22-algorithm.md`
- Workflow pipeline: `docs/core/workflow-pipeline.md`
- Runtime CLI: `docs/reference/runtime-cli-plan.md`
