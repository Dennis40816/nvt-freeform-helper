# CAD / REG Numbering Reference
Last updated: 2026-06-16

## Purpose
This document summarizes the meanings of the CAD / REG / Diff numbering currently visible to FreeformHelper users, to avoid confusing stable keys, UI display indices, and FW diff identities.

## Quick reference

| Type | Starting point / direction | Affected by `ScanOrder` | Main purpose | User visibility |
| --- | --- | --- | --- | --- |
| `REG id` / `RegularPadId` | Starts at the bottom left, proceeds right, then moves up to the next row | No | Stable REG identification key | Normally visible |
| Internal `row` | Bottom row = 0, increases upward | No | Internal grid coordinates | Usually hidden |
| UI display / input `Row` | Top row = 0, increases downward | No | Header rows range, Pad info display | Visible |
| `REG FW Diff idx` / `DiffIndex` | Determined by `ScanOrder` | Yes | Diff identity for FW / Notch / Simulation | Normally visible |
| `CAD id` | DXF read / expansion order, increasing from 0 | No | Stable CAD key, project/edit/export reference | De-emphasized display or visible in diagnostics |
| `CAD #display` | For UI display, top left to bottom right, 1-based | Uses a fixed display sort order | Visual inspection of CAD order | Normally visible |
| `CAD Output FW Diff idx` | Output diff connecting the CAD pad to the FW diff space | Indirectly affected by REG diff / Step1 match | CAD source diff for Step4 / Export / Simulation | Visible in diagnostics / handoff |

## REG id

When a normal generated grid creates REG pads, `RegularPadId` is `Index`, with a fixed calculation:

```text
RegularPadId = row * cols + col
```

Internal `row = 0` is the geometric bottom row, and `col = 0` is the left side. Therefore, `REG id` uses row-major numbering starting from the bottom left.

For example, with `cols = 4` and `rows = 3`:

| Visual position | REG id |
| --- | --- |
| Top row, left to right | 8, 9, 10, 11 |
| Middle row, left to right | 4, 5, 6, 7 |
| Bottom row, left to right | 0, 1, 2, 3 |

Related code entry points:

- `src/FreeformHelper.Application/Services/RegularGridBuilder.cs`
- `src/FreeformHelper.Domain/Pads/RegularPad.cs`

## UI Row

The row shown to users in the UI is top-origin:

```text
UI row 0 = AA 視覺上方第一排
```

UI rows and internal rows therefore convert as follows:

```text
displayRow = (totalRows - 1) - actualRow
actualRow = (totalRows - 1) - displayRow
```

Related code entry points:

- `src/FreeformHelper.UI/Services/ManualSizingService.Parsing.cs`

## REG FW Diff idx

`DiffIndex` / `RegularFwDiffIndex` is the diff identity used by FW / Notch / Simulation and is not equal to `REG id`.

After the grid is built, it is calculated from the current `ScanOrder` and IC column partitioning:

```text
DiffIndex = scanRow * icCols + scanColLocal
```

Currently supported scan orders:

| `ScanOrder` | Row direction | Col direction |
| --- | --- | --- |
| `LeftToRight_TopToBottom` | Top to bottom | Left to right |
| `RightToLeft_TopToBottom` | Top to bottom | Right to left |
| `LeftToRight_BottomToTop` | Bottom to top | Left to right |
| `RightToLeft_BottomToTop` | Bottom to top | Right to left |

Related code entry points:

- `src/FreeformHelper.Application/Services/AfeMapper.cs`
- `src/FreeformHelper.Application/Settings/ScanOrder.cs`

## CAD id

`CAD id` is the stable key assigned to a CAD pad during DXF import. It starts at `0` and increases in DXF entity read and block insert expansion order.

It is not a spatial ordering number and should not be used to indicate a CAD pad's position in the user's visual sequence.

Related code entry points:

- `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.cs`
- `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.PolylineCommit.cs`
- `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.InsertExpansion.cs`

## CAD #display

`CAD #display` is a UI display index that lets users find CAD pads in visual order. It always sorts from top left to bottom right and is 1-based.

The inspector currently prioritizes this display:

```text
CAD #123 (id 4823)
```

Here:

- `#123` is the display index, suitable for human inspection.
- `id 4823` is the stable key, suitable for project state, edit, diagnostics, and runtime query.

Related code entry points:

- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspectorSummary.cs`

## CAD Output FW Diff idx

`CAD Output FW Diff idx` is the output-side number that connects a CAD pad to the FW diff space. It is connected to `REG FW Diff idx`, but is neither the same field nor the CAD display index.

In the normal case:

```text
CAD pad
  -> Step1 geometry best-match REG
  -> matched REG 的 FW Diff idx
  -> CAD Output FW Diff idx
```

When entering Notch / Simulation, this can be understood as:

```text
source = CAD Output FW Diff idx
target = REG FW Diff idx
```

Cases that may change CAD Output FW Diff assignments:

| Case | Result |
| --- | --- |
| Normal geometric match | Uses the matched REG's `FW Diff idx` |
| `SeeRegular.csv` active mask / repair | May reconnect to the `FW Diff idx` of an active REG |
| Manual override | Uses the user-specified `CAD Output FW Diff idx` |
| `StrictUnique` auto mode | Duplicate diffs within the same IC remain unresolved, awaiting correction |

Related code entry points:

- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs`
- `src/FreeformHelper.Application/Services/CadBestMatchSeedService.cs`
- `src/FreeformHelper.Application/Services/CadOutputFwDiffIndexAssignmentService.cs`
- `src/FreeformHelper.Application/Settings/CadOutputFwDiffAutoMode.cs`

## UI display principles

Normal operations should prioritize displaying:

```text
CAD #display / REG id / FW Diff idx
```

Only diagnostics, export, runtime query, and project/edit traces should additionally display:

```text
CAD id / CAD Output FW Diff idx / internal row-col
```

Do not treat `CAD id` as spatial order or `REG id` as FW diff identity.
