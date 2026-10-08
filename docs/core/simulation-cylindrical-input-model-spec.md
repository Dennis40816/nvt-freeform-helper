# Simulation Cylinder/Free-Slide Input Model Spec (S11.66)
Last updated: 2026-04-06
Status: Draft v1 (ready for prototype)

## 1. Goal
- Add a "continuous slide input" model without breaking the existing single path of `frame slider + playback + CSV`.
- Users can treat input as "continuously rolling on the cylinder surface," not only jumping between discrete frames.
- All input still converges to the same projection path: `SimulationWorkspaceUseCase -> snapshot`.

## 2. Non-Goals
- Do not change the notch v2.1/v2.2 algorithm.
- Do not add a second simulation numeric calculator.
- Do not recalculate delta / histogram in the UI layer.

## 3. Core Concepts
- `virtual timeline`: continuous timeline (double), supports fractional positions.
- `frame anchor`: integer node aligned with the existing discrete frames.
- `interpolation window`: when a continuous position falls between two anchors, interpolate using the same rule.
- `cylindrical wrap`: optional cycling (last frame connects to first frame) or boundary clamping.

## 4. Data Contract
- Add `SimulationInputMode`
  - `DiscreteFrame` (current behavior)
  - `ContinuousCylinder`
- Add `SimulationContinuousInputState`
  - `position` (double)
  - `velocity` (double)
  - `isWrappingEnabled` (bool)
  - `interpolationMode` (`Linear`/`Hold`)
  - `sensitivity` (double)
- Add `SimulationInputSample`
  - `source` (mouse/wheel/touch/keyboard)
  - `delta`
  - `timestampUtc`

## 5. UI Behavior
- Keep the existing frame slider. In `ContinuousCylinder` mode:
  - The slider shows the continuous position (can be fractional).
  - The right side shows `anchor low/high` and `blend ratio`.
- Add input area:
  - `Input mode` toggle
  - `Wrap EN` toggle
  - `Interpolation` toggle
  - `Sensitivity` numeric value
- All changes only update input state, then call the same snapshot refresh path.

## 6. Projection and Performance Contract
- The main thread only does:
  - Read input state
  - Schedule snapshot update
  - Update visible UI state
- Interpolation and caching:
  - If position falls in the same interval and the source frames have not changed, reuse the previous interpolation cache first.
  - Cache key: `frameLow/frameHigh/ratio/viewMode/colorMode/areaFilter/revision`.
- Not allowed:
  - Adding a second AA projector because of continuous sliding.
  - Fully rebuilding all non-visible detail panels on every pointer move.

## 7. Prototype Scope (Beta 0.3)
- Only do the interaction prototype and state contract:
  - Mode switching
  - Position updates
  - wrap/hold/linear behavior
  - Snapshot still goes through the existing path
- Not included:
  - Physical inertia model
  - Multi-touch gesture library
  - New animation system

## 8. Validation Criteria
- With `ContinuousCylinder` on, AA values change continuously and do not jump to the wrong frame.
- After turning it off, the view returns to discrete frame mode, and values match the current behavior.
- With `Wrap EN` on, out-of-bounds input wraps around; when off, it clamps.
- Histogram / hotspot / inspector stay in sync with the same snapshot.
- There must be no second simulation truth source.
