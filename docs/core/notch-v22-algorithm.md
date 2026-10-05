# Notch V22 Algorithm Details

> Canonical reference：[`docs/reference/notch-system-reference.md`](../reference/notch-system-reference.md)
> This file retains the v2.2 algorithm deep-dive; the canonical reference governs flow truth and export contracts.

Last updated: 2026-08-08

`V22` has two related implementations at different levels in the repo:

1. Main path:
- `NotchV22CompensationService`
- `NotchV22TargetAllocationService`
- `NotchTableGenerator.Generation.V22`
- This is the actual `v2.2` main path for Step3 / Step5

2. Compatibility:
- `V22LegacyRowStrategy`
- This is a legacy 9-column compatibility row, not the Step5 main output

This document covers the main path first, then the legacy compatibility branch.

## 1. Main Path Entry Points

- `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
- `src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs`
- `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs`
- `src/FreeformHelper.Domain/Notch/NotchV22Node.cs`

## 2. Three Core Ratios in the Main Path

### 2.1 `ToRegular`
- Source: the area relationship between the CAD and all overlapped regulars
- Ratio: `sum(overlapArea / regularArea)`
- Note: this is a diagnostic CAD-level total area coverage and should no longer be treated as a source-wide gain multiplied into all target shares.
- Actual v2.2 rows use per-target regular coverage:
  - No Gain：`overlapAreaOnTarget / targetRegularArea`
  - Gain：`stage3EffectiveAreaOnTarget / targetRegularArea`

### 2.2 `ToFull`
- Source: final area after Stage3 compensation / original CAD area
- Expansion occurs only after passing the rule gate

### 2.3 `Combine`
- Main path formula after beta0.9:

```text
CombinePercent = retainedCoveragePercent + Σ(targetLegCoveragePercent)
```

- `Current (Gain)`: coverage comes from `stage3EffectiveAreaOnTarget / targetRegularArea`
- `Conservative (No Gain)`: coverage comes from `overlapAreaOnTarget / targetRegularArea`
- `R` / `F` remain in diagnostics/comment, but are no longer combined into a source-wide gain and then redistributed.

Example: in 3635, `CAD4818` covers `REG384/FW1472` by approximately `98.6%` and `REG385/FW1473` by approximately `58.8%`, giving diagnostic `R≈157.4%`. The v2.2 row does not multiply `R` back into target shares; the No Gain row should output `diff1472 leg≈99%`, `diff1473 retained≈59%`, and `C≈158%`. The corresponding row in the current `example/BOE36.35/notch_export_v22_current.c` is `{ 1473, 158, 1472, 99, ... }`, with `NODE KEEP=59% MOVE=99%`.

## 3. `ToFull` Gates

Each overlapped regular is evaluated for:
- boundary candidate？
- Sufficient source area?
- multi-owner？
- blocker？
- really has expansion？

Common gate codes:
- `GATE_TOFULL_DISABLED`
- `GATE_NOT_BOUNDARY`
- `GATE_SOURCE_EMPTY`
- `GATE_MULTI_OWNER`
- `NO_EXPANSION_NEEDED`
- `EXPAND_CLEAR_PATH`

Rule engine:
- `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs`

## 4. Stage1 / Stage2 / Stage3

### 4.1 Stage1
- seed overlap
- The area where the CAD actually overlaps the regular

### 4.2 Stage2
- candidate regular boundary
- A candidate regular for possible expansion

### 4.3 Stage3
- final to-full result
- The union / outline after actual application

UI, RuntimeQuery, and Inspector must all read the same resolved result and must not derive it independently.

## 5. Target allocation

The `V22` main output considers not only the anchor itself but also calculates target legs.

Target allocation rules:
- Bucket by `(IC, Diff)`
- Exclude the anchor diff
- Pass the strict threshold
- Form `target diff + ratio`
- `Current (Gain)`: target legs directly use Stage3 target coverage, no longer `CombinePercent * share`.
- `Conservative (No Gain)`: target legs directly use source-overlap target coverage; ToFull serves only as support/cap/allowance.
- If a single target coverage exceeds `100%`, split it into multiple <=100% chunks when necessary.

Main service:
- `src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs`

## 6. `NotchV22Node` 7 Columns

Main output payload:

1. `AnchorDiffIndex`
2. `CombinePercent`
3. `TargetDiffIndex1`
4. `TargetRatioPercent1`
5. `TargetDiffIndex2`
6. `TargetRatioPercent2`
7. `Flags`

### 6.1 continuation row

If an anchor has more than two target legs:
- The first row retains the actual `CombinePercent`
- Continuation rows use a fixed `CombinePercent = 100`
- Set the continuation bit in `Flags`

## 7. `V22LegacyRowStrategy` Compatibility Branch

File:
- `src/FreeformHelper.Application/Services/NotchAlgorithms/V22LegacyRowStrategy.cs`

Its purpose is to:
- Provide a legacy 9-column compatibility representation
- It is not the Step5 main export payload

In the normal production flow, in public `Generate`, only `LegacyRegularAnchor` calls its `Build`, so normal rows
use the legacy 9-column geometry estimate; the `CadAllocation` canonical path directly uses the shared compensation/allocation pipeline.
However, after mode dispatch, `Generate` synchronously calls the caller-supplied `IProgress.Report`; the callback can modify the same
mutable settings, allowing `Build` to read CadAllocation again. R13.004f therefore retains the compensation branch, comment,
and `allCadPads`, rather than mistaking "unused in the normal flow" for unreachable under the public API.

This strategy's `CanHandle` is still shared by CadAllocation eligibility queries, but eligibility does not call `Build`;
this cross-mode seam and generation input immutability are left for R13.101～R13.103 to converge and should not be mistaken for canonical
V2.2 ownership。

## 8. Contracts to Preserve When Making Changes

1. `Combine` always comes from the per-target regular coverage sum
- UI / export / simulation must not calculate it independently.
- `ToRegular * ToFull` remains only a CAD-level diagnostic concept and must not be used to derive the row payload.

2. Stage overlays are only display projections
- They must not feed back as business truth.

3. `NotchV22Node` is the single source of the Step5 main output
- The legacy 9-column format is only for compatibility, not the main path.

More detailed Stage / UI contracts:
- `docs/core/notch-2.2-spec.md`
