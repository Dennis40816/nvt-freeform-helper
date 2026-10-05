# Settings Parameter Guide

Last updated: 2026-05-23

This document describes the purpose, scope of impact, and tuning recommendations for the parameters currently visible in `SettingsWindow`. Existing documents cover the settings entry matrix and some algorithm background, but do not list every Settings parameter in one place; this document fills that gap in the operating instructions.

## Usage principles

- Fix General grid / cascade / AA / source first, then adjust Step 1~5. After the geometry foundation changes, rerun the subsequent match, freeform, notch, mapping, and export steps.
- Change only one group of parameters at a time. Check Step summary, canvas overlay, and Simulation safety before deciding whether to continue tuning.
- Prefer the default safety guard values. Disabling a guard or lowering a cap is suitable only for diagnostic comparisons, not for direct FW handoff.
- Display parameters do not change calculation results, such as preview layer, labels, diff marker, font size, and highlight width.
- Before exporting, check at least the Step 5 safety summary, Simulation EMS results, export versions, and gate threshold.

## Cascade and IC X/Y

Settings currently supports different X/Y counts for each IC, through the edit button next to `Settings > General > Cascade`. `Total X channels` and `Total Y channels` on the main form are passively calculated fields and cannot be entered directly.

| Action | Behavior |
| --- | --- |
| Change `Cascade` | Adjust the number of IC rows in the General main form or the `Cascade IC details` modal; a new row inherits the X/Y of the last existing IC, or uses the current total X/Y as a fallback if no rows exist. |
| Open `Cascade IC details` | Set `IC1`, `IC2`, ... row by row in the modal using `X channels` and `Y channels`; the `Cascade` count can also be adjusted directly. |
| Change per-IC X | `Total X channels = sum(per-IC X)`. |
| Change per-IC Y | `Total Y channels = max(per-IC Y)`. |
| Click `Done` | Only closes the modal; changes remain in the Settings draft. |
| Click `Save Settings` | Applies the draft to project settings and writes `Grid.PerIcXChannels` / `Grid.PerIcYChannels`. |

Example:

| IC | X channels | Y channels |
| --- | ---: | ---: |
| IC1 | 10 | 20 |
| IC2 | 12 | 18 |
| IC3 | 14 | 22 |

The example above displays `Total X channels = 36` and `Total Y channels = 22`. If the per-IC X list length or sum is inconsistent with the total column count, IC column allocation in the application layer falls back to an even split to prevent invalid downstream IC indices.

Current runtime contract: the application layer uses per-IC X to resolve the IC that owns each column; per-IC Y currently calculates and stores the global `Total Y channels = max(per-IC Y)`, rather than acting as a per-IC row mask. In other words, the regular grid still uses a global row model.

Tuning recommendations: first enter the actual number of cascaded ICs in `Cascade`, then enter the actual X/Y for each IC. If only one IC has a longer Y, total Y takes the maximum; do not manually lower the longer IC's Y to align it with shorter ICs. If downstream processing needs to trim rows by IC Y length, application/export must be extended separately to use `Grid.PerIcYChannels`.

## General

| Parameter | Function | Tuning recommendations |
| --- | --- | --- |
| `Cascade` | Number of cascaded ICs; determines the per-IC row count. | Set according to the actual hardware IC count; check the per-IC modal after changing it. |
| `Total X channels` | Panel-wide X column count, passively calculated as the sum of per-IC X. | Do not enter it directly; if the number is wrong, return to the modal and correct each IC X. |
| `Total Y channels` | Panel-wide Y row count, passively determined by the maximum per-IC Y. | Set according to the longest IC Y; this is currently not a per-IC row mask. |
| `AA size X/Y (mm)` | Panel active area dimensions, used for generated regular grid placement. | Use measured or specification values; rerun Step 1 after AA changes to update matching. |
| `Scan order` | Diff-index numbering direction of the generated regular grid. | Match the FW / CAD index definition; do not use it to fix geometry mismatch. |
| `Regular source` | Source of regular pads: generated grid or DXF layer. | Use a layer source when a reliable regular DXF layer is available; otherwise use the generated grid. |
| `Regular layer` | Layer used when Regular source is a DXF layer. | Enabled only for DXF layer source; choosing the wrong layer causes Step 1 mapping to deviate. |
| `Grid padding (%)` | Padding of the generated regular grid within AA. | Increase slightly when CAD bounds are too close to the edge; confirm the AA dimensions before making a large adjustment. |
| `Coordinate pixel X/Y` | Coordinate resolution for FW coordinate planning / export diagnostics. | Affects only coordinate diagnostics and output planning, not regular geometry or notch allocation. |
| `Panel bias X/Y` | AA alignment offset in panel alignment mode. | Effective only when `Grid alignment mode = FromPanelAa`; use it to correct a fixed offset, not a scaling error. |
| `Only closed polylines` | Accepts only closed polylines during DXF import. | Usually keep enabled to avoid mistaking open lines for pads. |
| `Import block polylines` | Also scans polylines in BLOCK during DXF import. | Enable when CAD places pads in block definitions; check this first if duplicate geometry appears. |
| `Auto bounds from visible layers` | Recalculates bounds from visible layers after layer visibility changes. | Enable when adjusting the layer filter to isolate geometry; temporarily disable when fixed bounds are needed for comparison. |
| `Apply visual preferences on load` | Preserves project geometry after Load Project while applying app-level display preferences. | Enable to share display preferences across projects; disable when verifying a project snapshot. |
| `Grid alignment mode` | Regular grid alignment source: panel AA or CAD bounds. | Use CAD bounds when CAD positions are reliable; use panel AA when alignment must follow the panel spec. |
| `Log level` | Minimum output level for the console / log file. | Normally use Info; switch to Debug/Trace to investigate issues, then switch back to avoid log noise. |
| `Global font size (%)` | UI text scaling. | Changes only the visuals, not data; the recommended adjustment range is 90~120. |
| `Highlight line width adjust` | Additional line width for selected/highlight pads on the canvas. | Increase slightly for large drawings or high DPI; avoid values large enough to obscure pad boundaries. |
| `Sizing scope` | Local or Global scope for manual sizing. | Use Local for fine adjustments to a single row/column; use Global for overall equal division or redistribution. |
| `Layer categories` | Batch On/Off by parsed DXF layer category. | Quickly isolates layers such as CAD output, regular, and annotation; this is a visibility operation and does not change project geometry. |

## Step 1

| Parameter | Function | Tuning recommendations |
| --- | --- | --- |
| `Unmatched TH (overlap / regular area)` | Diagnostic threshold for unmatched regular pads; does not rewrite Step 1 links. | A lower threshold flags fewer regular pads as suspicious; a higher threshold flags more suspicious unmatched pads. This is for diagnostics; do not expect it to change existing links. |

## Step 2

| Parameter | Function | Tuning recommendations |
| --- | --- | --- |
| `Freeform TH (dominant axis ratio)` | Dominant axis ratio threshold used by Step 2 to classify XWay / YWay / XYWay. | A lower value classifies directions more aggressively; a higher value is more conservative. Adjust this first if there are too many or too few XYWay pads. |
| `XY WAY EN` | Allows Step 2 to automatically detect X/Y freeform direction. | Normally keep enabled; disable only when verifying with manual overrides. |
| `Edge spill EN` | Enables edge-specific freeform rules for boundary spill cases. | Keep enabled when edge pads are frequently misclassified; temporarily disable for A/B comparison with legacy behavior. |
| `Load auto-run Step 2` | Automatically replays Step 2 tagging after Load Project. | Enable when projects often need the latest tagging checked immediately on opening; disable for bulk loading to view only the original snapshots. |

## Step 3

| Parameter | Function | Tuning recommendations |
| --- | --- | --- |
| `Compensation model` | Step 3 compensation model; controls To Regular / To Full semantics through a single source. | Follow the current product line specification; recheck Step 3 summary and Simulation after switching. |
| `To Regular EN (derived)` | Derived from the compensation model; indicates whether to restore the area-proportional signal first. | Read-only state; change `Compensation model` to change behavior. |
| `To Full EN (derived)` | Derived from the compensation model; indicates whether boundary target support / cap is enabled. | Read-only state; change `Compensation model` to change behavior. |
| `Multi-owner strict overlap (%)` | Required regular-area overlap ratio before CAD can own or block a boundary target. | Increase to reject ambiguous boundary targets earlier; decrease to retain more boundary candidates, but check with Simulation. |
| `Use boundary area cap` | Limits To Full virtual expansion area to prevent uncontrolled boundary EMS. | Keep enabled as recommended; disable only for diagnostic comparisons. |
| `Virtual cap (%)` | Maximum ratio of virtual To Full area to original overlap area. | 100% means the outer virtual area does not exceed the inner overlap. Check EMS margin before relaxing the limit. |
| `Use target coverage guard` | Limits total coverage for each target FW diff after CurrentGain rows are selected. | Keep enabled as recommended; this is the main guard against uniform-field EMS overflow. |
| `Target cap (%)` | Target-side coverage limit; 120% corresponds to uniform 400 reaching the EMS 480 cap. | Usually keep at 120; increasing it raises EMS risk, while decreasing it may cause undercompensation. |
| `Use To Full rule engine` | Uses the centralized To Full boundary gate engine; when disabled, uses the legacy inline gate path. | Keep enabled as recommended unless comparing with the legacy path. |
| `Keep To Full trace` | Stores the To Full rule decision for each regular pad for diagnostics / runtime query. | Enable when inspecting rule decisions; normally disable to reduce diagnostic data volume. |
| `Stage auto play` | Automatically cycles Step 3 preview layers on the AA canvas. | Display only; does not affect notch rows. |
| `Interval (ms)` | Interval between auto play layers. | Adjusts only viewing speed; does not affect calculations. |
| `Show To Regular labels` | Displays To Regular ratio labels on the Step 3 preview canvas. | Display only; enable when checking individual ratios. |
| `Show To Full overlay` | Displays To Full support stages on the Step 3 preview canvas. | Display only; enable when checking boundary support. |
| `To Full layer stage` | Selects the To Full preview layer: 1 seed, 2 candidate, 3 final. | Locates the layer where a rule gate excludes a candidate; does not change calculation results. |

## Step 4

| Parameter | Function | Tuning recommendations |
| --- | --- | --- |
| `Weight IoU` | Weight of geometric overlap quality in the mapping diagnostic score. | Increase when CAD/regular overlap quality is reliable; decrease when shapes differ greatly. |
| `Weight distance` | Weight of CAD/regular centroid distance. | Increase when geometric position is more reliable than area; avoid high values when CAD has an offset. |
| `Weight area` | Weight of CAD/regular area similarity. | Increase when pad area is stable; decrease when edge / notch causes large area differences. |
| `Candidate number` | Number of candidates retained by Step 4 mapping diagnostics. | Increase when correct candidates are cut off; decrease when too many candidates cause report noise. |
| `Low confidence threshold` | Mappings below this confidence are marked for review. | Increase to catch more risks; decrease to reduce manual review volume. |
| `Ambiguous margin (best - 2nd)` | Marks a mapping as ambiguous when the gap between the best score and second score is below this value. | Increase to flag ambiguity more sensitively; decrease to trust the best candidate more. |
| `CAD Output FW Diff strategy` | Strategy for assigning CAD Output FW Diff indices based on geometry seed / override. | Usually select according to project convention and avoid frequent switching; recheck the overlay/report after switching. |
| `CAD Output FW Diff start` | Starting index for automatic CAD Output FW Diff numbering. | Align with the FW numbering range; changes affect output diagnostic indices. |
| `Future validation path` | Reserved field for a future CSV-confirmed geometry seed. | Currently not adjustable. |
| `Show CAD Output FW Diff markers` | Displays CAD output diff markers on the canvas. | Display only; checks numbering without changing mapping results. |

## Step 5

| Parameter | Function | Tuning recommendations |
| --- | --- | --- |
| `Safety before FW handoff` | Displays a summary of notch export / EMS safety policy. | Always check before exporting; if it shows unsafe, return to Step 3 / Simulation to correct it first. |
| `Export versions` | Selects the notch table version to export, such as v2.1 / v2.2. | Select according to FW requirements; enabling both produces a comparison, but confirm which version downstream uses. |
| `Export type` | Selects the output format, such as C source or CSV diagnostics. | Use a C profile for FW handoff; use CSV diagnostics for analysis and diff review. |
| `C export profile` | Selects the C output profile expected by downstream FW integration. | Match the target codebase version; mismatched profiles cause integration friction. |
| `Null value` | Placeholder for empty entries in the notch payload. | Usually use the FW-agreed value, such as 65535; confirm the FW parser before changing it. |
| `Notch gate TH v2.1 (Q7/128)` | v2.1 gate threshold in Q7 units, where 128 means 100%. | Adjust when v2.1 compatibility is needed; `Link TH` synchronizes it when linked to v2.2. |
| `Notch gate TH v2.2 (%)` | v2.2 gate threshold, expressed directly as a percentage. | Percentages are more intuitive; keep linkage enabled as recommended if FW consumes both v2.1/v2.2. |
| `Link TH (v2.1 Q7 <-> v2.2 %)` | Synchronizes v2.1 Q7 and v2.2 percent thresholds during editing. | Keep enabled as recommended unless deliberately comparing thresholds between versions. |

## Related code entry points

- `src/FreeformHelper.UI/Views/SettingsSections/SettingsGeneralSectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep1SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep2SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep3SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep4SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep5SectionView.axaml`
- `src/FreeformHelper.UI/Views/CascadeIcSettingsWindow.axaml`
- `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.Cascade.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs`
- `src/FreeformHelper.UI/Services/CascadeIcLayoutService.cs`
- `src/FreeformHelper.Application/Services/GridIcChannelAllocationService.cs`
