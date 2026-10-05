# Notch V21 Algorithm Details
Last updated: 2026-03-23

This document describes the current implementation contract for `V21` in the repo. It explains "how the code calculates now", not historical external answer files.

Main implementations:
- `src/FreeformHelper.Application/Services/NotchAlgorithms/V21NotchAlgorithm.cs`
- `src/FreeformHelper.Application/Services/NotchAlgorithms/NotchAlgorithmHelpers.cs`

## 1. Applicability

`V21` only handles:
- `reg.Freeform != None`

That is:
- `XWay`
- `YWay`
- `XYWay` still passes `CanHandle`, but the legacy v2.1 axis helper treats any non-`YWay` type as the X axis

Decision entry point:
- `V21NotchAlgorithm.CanHandle(...)`

## 2. Output Format

`V21` outputs a legacy row with 9 columns:

1. `IDX`
2. `REGULAR_PERCENT`
3. `REGU_TO_FULL_PERCENT`
4. `FIRST_DIFF`
5. `FIRST_TYPE`
6. `FIRST_RATIO_Q7`
7. `SECOND_DIFF`
8. `SECOND_TYPE`
9. `SECOND_RATIO_Q7`

## 3. How the First Three Columns Are Calculated

### 3.1 `IDX`
- Take `reg.DiffIndex` directly

### 3.2 `REGULAR_PERCENT`
- `cad.Area / reg.Area * 100`
- Round to the nearest integer

### 3.3 `REGU_TO_FULL_PERCENT`
- `cad.Bounds.Width * cad.Bounds.Height / reg.Area * 100`
- Round to the nearest integer

Notes:
- These two columns are geometric ratios from two different perspectives.
- They are not multiplied together when generating a `V21` row.

## 4. Axis Determination

`V21` first reduces the freeform type to an axis:
- `XWay -> X`
- `YWay -> Y`
- `XYWay -> X` (legacy fallback; the formal Step5 main path still follows the v2.2 canonical flow)

Helper entry point:
- `NotchAlgorithmHelpers.ResolveAxisKind(...)`

## 5. Neighbors and Geometry Context

### 5.1 X Axis
- Left neighbor: `(row, col - 1)`
- Right neighbor: `(row, col + 1)`

### 5.2 Y Axis
- Upper neighbor: `(row + 1, col)`
- Lower neighbor: `(row - 1, col)`

This uses the internal grid row contract, not the top-down row labels shown in the UI.

Creation entry points:
- `CreateAxisGeometryContext(...)`
- `CreateAxisNeighborContext(...)`

## 6. `type` Definitions

`V21` has only three leg types:

- `0 = none`
- `1 = add`
- `2 = sub`

In plain terms:
- `add`: add the amount to the neighbor diff
- `sub`: subtract the amount back within the current diff

## 7. What the `Q7` Ratio Means

`V21` columns 6 / 9 are firmware `UINT8` Q7 magnitudes:

- `1.0 = 128`
- `0.5 = 64`
- `0.0 = 0`
- The valid payload range is `0..255`; `255` is approximately `199%`
- The sign is not stored in the magnitude; it is carried only by `NHC_TYPE_ADD / NHC_TYPE_SUB`

That is:

```text
ratioQ7 = roundAwayFromZero(fraction * 128)
```

A legacy geometry row can temporarily hold unsaturated values above `255` to preserve historical row/CSV semantics; the single final firmware projector saturates them to `0..255` at the ABI boundary. Firmware apply directly executes `(INT16 source * magnitudeQ7) >> 7`; it must not convert to integer percent or double before applying.

The fraction comes from:
- Geometric extension length / current cell length
- Or geometric extension length / neighbor cell length

Corresponding entry points:
- legacy row：`NotchV21Q7Codec.EncodeLegacyRowFractionRaw(...)`
- final payload／display／apply：`NotchV21Q7Codec`

`ThresholdQ7` is a separate `0..128` admission contract managed by `NotchThresholdQ7Contract`; it is not a leg payload and must not be combined with the `0..255` magnitude limit.

## 8. How `add` / `sub` Are Determined

The same rules apply to both the X / Y axes:

1. First calculate the leading / trailing geometric deltas
2. Determine whether the CAD actually extends beyond the cell on that side
3. If it extends beyond:
- `type = add`
- `diff = neighbor diff`
4. If it does not extend beyond but retracts:
- `type = sub`
- `diff = current reg diff`

Implementation entry points:
- `geometryContext.ExpandsLeading(...)`
- `geometryContext.ExpandsTrailing(...)`
- `geometryContext.GetLeadingDelta(...)`
- `geometryContext.GetTrailingDelta(...)`

## 9. What `V21` Actually Expresses

A `V21` row can be described in plain terms as:

- Roughly how much of its own regular this CAD occupies
- How much it would occupy based on the bounding box
- Which diff has `add/sub` in the first direction
- Which diff has `add/sub` in the second direction
- How large the amount is in each direction (using `Q7`)

It is a geometrically derived legacy row, not a firmware truth table.

## 10. Contracts to Preserve When Making Changes

1. `Q7` is currently the only V21 leg ratio contract
- Do not bring the UI-adjustable `LenScale` back to the user-facing layer.
- V22 legs remain `INT8 -100..100` signed percent and do not use the V21 Q7 codec.

2. `V21` `[1]/[2]` are geometric ratios
- They should not be reinterpreted as other units by UI / exporter / simulation.

3. `V21` `type / diff / ratio` must be considered together
- Do not look only at the ratio and ignore `add/sub` semantics.
- The exporter and simulation must consume the same `NotchV21FirmwareProjector` final node; the formatter no longer repeats clamping or projection.
