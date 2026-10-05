# Notch Validation Flow (Step 5)
Last updated: 2026-08-10

> Purpose: Explain the current data sources, caching, and behavior of "Validation quick trace" to prevent misinterpretation by future AI agents/developers.

## Scope
- UI：Step 5 `Validation quick trace`
- VM：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchValidation.cs`
- UseCase：`src/FreeformHelper.UI/Services/NotchValidationUseCase.cs`
- Trace mapper (shared source for UI/CLI): `src/FreeformHelper.UI/Services/NotchValidationTraceService.cs`

## Input Prerequisites
1. Validate current settings with `ProjectSettings.ValidateOrThrow()` first; an invalid `NullValue` fails fast before readiness lookup
2. The Grid must be built (otherwise return `Validation: build grid first.`)
3. The target `REG id` must exist
4. Step 5 Export must have run at least once, and `_lastGeneratedNotchTable` must be available

Direct VM/`RuntimeQueryUseCase` calls preserve validation exceptions; only at the outermost layer does the Named Pipe server convert unhandled command execution exceptions into a single-line `IPC_ERROR` failure envelope, preserving the original message. Normal grid/REG/table readiness failures still follow the existing return contract and must not be conflated with invalid-settings errors.

## Core Data Source (Single Source)
- Validation only reads `_lastGeneratedNotchTable`; it does not rerun notch generation.
- When `_lastGeneratedNotchTable` changes, rebuild the bucket; otherwise reuse the cache.

## Bucket Cache Behavior
- Cache keys:
  - `ReferenceEquals(_notchValidationBucketTable, table)`
  - `nullDiffValue`
- Cache hit: use `_notchValidationBucketCache` directly
- Rebuild after invalidation: `NotchValidationUseCase.BuildBucket(...)`
- Log on rebuild:
  - `Validation bucket rebuilt: tableRows=..., null=..., elapsed=...ms`

## Classification Logic (UseCase)
- `directRowsByRegularPadId`
  - Rows that fall directly on this REG (all versions)
- `incomingRowsByIcDiff`
  - Legs from other source rows that point to `(ic, diff)` (currently mainly from v2.2 row decoding)
- `outgoingRowsByRegularPadId`
  - Legs transferred out with this REG as the source

### v2.2 Leg Decoding Details
- Decoding is currently **typed-first**:
  - If `row.V22Node` exists, prefer typed fields (`TargetDiffIndex1/2`, `TargetRatioPercent1/2`)
  - Otherwise fall back to `row.Values` (slot1/slot2)
- Treat `targetDiff == nullDiffValue` as an empty leg and ignore it
- Clamp `ratioPercent` to `[-100, 100]`

## UI Display Model
- Both the VM and Runtime CLI first generate the same trace through `NotchValidationTraceService.BuildTrace(...)`.
- The trace outputs three groups of items (using the same mapper):
  - `DIRECT`
  - `IN`
  - `OUT`
- The right-side Step5 panel displays three group panels: `DIRECT / IN / OUT`; each group can be expanded independently and displays its count.
- The VM still retains a merged list (`NotchValidationItems`) for summaries and empty-state checks.
- Sort order: `DIRECT -> IN -> OUT`, then `IC / source diff / target diff` (the same order also applies within each group).
- v2.2 decoding of the row payload is also typed-first; the CLI no longer duplicates decoding itself.

## Focus Behavior
- Clicking a row attempts to focus:
  - source cad
  - source reg
  - target reg (if different)
- If the target pads are not visible, display `Validation focus: target pads are not visible.`

## Relationship to Export
- Validation results depend entirely on "the most recently generated notch table".
- Whenever Export runs again (even without writing files), the Validation view should be treated as a new data version.
- Runtime CLI `query notch-validation --regular-id ...` and the Step5 UI use traces from the same source (to prevent UI/CLI rule drift).

## Change Rules (Important)
- If any of the following changes, update this file and `docs/reference/runtime-cli-plan.md` (if the CLI is affected) together:
  - v2.2 leg field definitions (slot/order/null checks)
  - Bucket cache keys
  - DIRECT/IN/OUT classification conditions
  - Validation row sorting rules
