# Notch System Reference

> Last updated: 2026-10-03
> Status: The sole canonical reference for the current Notch pipeline / Simulation / Export

This document does not aim to replace all historical documents. It brings together "how the system currently works" in one place. Any future changes to the flow, naming, or C export contract should update this file first, then adjust the deep-dive or legacy example.

## 1. Scope

This file covers the following topics:

- The main Notch flow from Step1 to Step5
- The current semantics of the `Regular`, `CAD`, `Best Match FW Diff`, and `Output` diff identities
- Simulation data sources and limitations
- v2.1 / v2.2 export payload
- The direct-import contract for C export (including multiple ICs)

Content excluded from this file:

- Detailed mathematical derivations
- Historical drafts of a specific algorithm version
- Validation traces for individual cases

That content remains in `docs/core` / `example`, but should be treated as a supplement to this file, rather than another source of truth.

## 2. Documentation Organization Rules

Notch-related documents follow these rules from now on:

- `docs/reference`
  - Holds canonical references, external contracts, and CLI contracts
- `docs/core`
  - Holds algorithm deep-dives, flow breakdowns, and design discussions
- `example`
  - Holds `.c` examples and fixtures that can currently be compiled/imported directly

If a document describes both "current behavior" and "historical background", it must clearly identify the canonical and historical sections at the beginning.

## 3. Core Data Models

### 3.1 Regular Pad

- `RegularPad.DiffIndex`
  - Represents `FW Diff Idx`
  - This is the actual order of the regular grid in FW / memory
  - It should be unique within an IC and is treated as a fixed source of truth once the grid is built

### 3.2 CAD Pad

A CAD pad is not itself a FW memory node, but several diff identities are derived from it in the workflow:

- `Best-Match FW Diff Idx`
  - Comes from the regular seed selected by geometric matching
  - Used for geometric analysis, trace, and anchor seed
  - Is not the final output address

- `CAD Output FW Diff Idx`
  - Comes from the Step4 allocation result
  - Represents the FW diff / memory address to which this CAD pad will ultimately be output
  - An override should affect this value, rather than the best match

- `Visible CAD Diff`
  - This is the diff label used for UI projection / display
  - It can be affected by `See Regular` or visible projection rules
  - It cannot be used directly as the source of truth for the Step5 source diff

## 4. Current Main Flow

### 4.1 Step0 to Step4

1. Build `RegularGrid`
   - Fix the `(IcIndex, FW Diff Idx)` baseline
2. Perform geometric matching between CAD and regular
   - Produce overlap / best-match seeds
3. Determine `CAD Output FW Diff Idx`
   - Overrides take priority
   - Otherwise, follow the best-match / unique assignment rules
4. Freeze the workflow snapshot
   - Step5 / inspector / runtime query should all read data from the same snapshot

### 4.1.1 Active Regular Set and SeeRegular Mask

- `Match-scope active regular set`
  - Source: `_latestPadMatchResult.RegularToCad.Keys`
  - Meaning: Regular pads already assigned to a CAD pad in the current Step1 / matching result
  - Current uses:
    - v2.2 `ToFull` boundary seed
    - Active/inactive neighbor decisions for notch compensation

- `Regular Visibility Mask (SeeRegular.csv)`
  - Source: `SeeRegular.csv` loads successfully, and `UseRegularVisibilityMask` / `IsRegularVisibilityMaskEnabled` is enabled
  - Meaning: An additional set that restricts active regulars
  - Current uses:
    - Step4 geometry seed / visible assignment
    - Simulation active surface

Key points:

- The current `boundary` decision is based on the `match-scope active regular set`
- It is not based on the `Regular Visibility Mask (SeeRegular.csv)`
- The two can be identical, but the system should not assume they must be identical

### 4.2 Step5 Notch Table

- `CadAllocation` is the current main path
- `LegacyRegularAnchor` is the compatibility path

Under the `CadAllocation` path:

- The row source diff uses `CAD Output FW Diff Idx` first
- It falls back to the raw anchor regular diff only when the output mapping is currently missing
- v2.2 target legs come only from geometric aggregation of regular `(IC, FW Diff Idx)`
- Targets should no longer be rewritten by visible / CAD projection
- The `ToFull` boundary seed is currently defined as:
  - A regular on the outer edge of the grid, or
  - A regular with any up/down/left/right neighbor missing or outside the `match-scope active regular set`
- The boundary regular index set is precomputed once within the same generation pass, then filtered and reused in each candidate; this is a performance optimization that does not change results

### 4.3 Simulation

The correct role of Simulation is:

- Input: regular grid + notch table
- Behavior: Redistribute regular sensing amounts according to the notch table
- `CadPad` should no longer be treated as the main input source of truth for Simulation

In other words, Simulation can reference the notch table built from CAD, but simulation execution should center on regular / FW diff rather than depend on CAD pad projection again.

Simulation currently has two other fixed contracts:

- `Active surface`
  - Uses the entire regular grid by default
  - Uses `grid ∩ mask` if the `Regular Visibility Mask (SeeRegular.csv)` is enabled
- `Build path`
  - Shows the host page and build overlay first when Simulation opens
  - The notch table still shares `GenerateCurrentNotchTableAsync(...)`
  - If the fingerprint has not changed, directly reuses the cache / prewarm artifact without recomputing a second simulation-specific algorithm

### 4.4 1.3.x shared execution contract: target and current debt

Hard target:

```text
project/filter
  -> immutable version-neutral generation context/evidence
  -> one canonical resolved result
  -> final V2.1 projector -> fixed V2.1 C formatter
  -> final V2.2 projector -> fixed V2.2 C formatter
```

- The output version must not enter matching, compensation, allocation, or candidate/evidence construction before final projection; version-specific threshold admission may only reside in the final row projector. The version-neutral resolved audit and upstream cache key must not carry the output version either.
- V2.1-only, V2.2-only, and both for the same workflow revision must read the same canonical fingerprint/result; the first and only version branch may occur only in the final Firmware data projector/formatter.
- `V21_before == V21_after` and `V22_before == V22_after` are hard gates already in effect for 1.3.x; the final-only architecture is the exit target for R13.101～R13.103, and the two must not be conflated as completed.

After `R13.102a-2a` / `R13.102a-2b-2` / `R13.102b-2`, the current state still has three explicit boundaries:

1. Normal `CadAllocation` has separated phases 1～3 output-neutral resolution from phase 4 final projection; the Step5 export cache holds an Application-owned opaque batch that remains reusable even when projection yields zero rows. Concurrent Export/Simulation with the same CAD/grid/Step3/computation identity and generation epoch create only one resolution task; joiners do not replay phases 1～3 progress, and each caller executes phase 4 using the enabled versions, V21/V22 threshold, `NullValue`, and target coverage guard/cap frozen at its own entry point. The resolved-batch settings fingerprint excludes final-only fields, but the completed cache and in-flight join still perform full computation-settings equality to guard against 32-bit collisions. A faulted task is removed for the next retry; final-only invalidation retains the task/batch and rejects publication of the old projection, while full invalidation advances the epoch, detaches the old task, and rejects its store/publish. Each generation request also freezes the Step5 final-projection revision and Simulation source revision; projected row count, last table, progress/flow, and caller continuation may be committed only while all three remain current. Warm projection executes/reports only phase 4. This is narrow progress for the full-table consumer task owner, not a repository-wide canonical result.
2. Step 3 preview, the 200 ms deferred CAD Inspector, Notch Detail, and Runtime Query `query multi-owner` for the same single-CAD selection/revision all project a revisioned sparse `NotchV22ResolvedResult`; the VM no longer holds a parallel compensation dictionary. R13.102a-2b-2 connects the request for the currently selected single CAD to the existing full batch session: a matching warm result is consumed by the candidate as the exact same instance, while a cold batch carries at most one resolved result identified by CAD ID + anchor IC/diff, which is promoted back to the same per-CAD owner only after completion passes the currentness check. Reuse identity locks the exact CAD, immutable grid/CAD-pool signatures, active mask, all computation settings, and anchor; a change to the same mutable grid reference must not cause an old result to be reused incorrectly. The full batch still deliberately stores only compact candidate evidence, without retaining polygons/debug results for all CAD pads; this is therefore a bounded sparse/full bridge and does not claim that all readers use one giant batch shape.
3. `LegacyRegularAnchor` still performs fixed V21/V22 compatibility dispatch according to the enabled version/threshold and retains a request-specific `NotchTable` cache. Top-level dispatch creates an owned `LegacyNotchGenerationRequest` before the first synchronous progress callback, fixing the ordered enabled versions, effective V22/V21 thresholds, `NullValue`, and `LenScale` at once; threshold admission, eligibility, and both versions of row builders read only this request and the same explicit version switch. Callbacks still synchronously mutate caller settings, but affect only the next generation. R13.103d-2 removed the strategy interface/dictionary/injection seam with no consumers; the V22 builder no longer retains the CadAllocation hybrid compensation branch or all-CAD input either.

Owner: R13.101e has converged normal generator/UI compensation on a single compute path through the complete, output-version-neutral `NotchV22CompensationContext`; the old multi-parameter API serves only as a compatibility adapter, and explicitly empty allocation evidence no longer falls back to recomputation. R13.101 still continues through R13.101c-2 to complete the repository-wide shared-result/final-output cache split; R13.102a-2b-2 has enabled concurrent full-table consumers to share a revisioned resolution task and completed the bounded selected sparse bridge. R13.102/R13.102a/R13.102a-2 still require audits of all consumers and Legacy convergence against their respective complete exit criteria; R13.103 completes typed final projectors/formatters and the legacy boundary. All of these parents remain open. R13.102b-2 completes only a single Runtime Query reader; R13.103d-1 freezes the Legacy request and removes the hybrid branch, and R13.103d-2 further deletes the registry/injection seam, but neither merges the request-specific table cache nor the entire Legacy boundary. R13.104a-1 to a-10 sequentially converge the EMS predicate, resolved display decisions, audit/replay/export status, active Notch cap guidance, and target-cap help. `LegacyRegularAnchor` convergence, other hard-coded caps, and the remaining Simulation/replay/display text are still unfinished; `LegacyRegularAnchor` serves only as an explicit compatibility path and does not enter the normal operator flow.

## 5. Diff Identity Comparison

| Name | Layer | Main source | Main use | Can be used directly as Step5 source |
| --- | --- | --- | --- | --- |
| `FW Diff Idx` | Regular / FW | `RegularPad.DiffIndex` | Actual memory / channel order | Yes |
| `Best-Match FW Diff Idx` | CAD geometry | Best regular seed for CAD | Geometric analysis / trace | No |
| `CAD Output FW Diff Idx` | CAD workflow | Step4 assignment / override | The FW diff to which CAD is ultimately output | Yes; the CadAllocation main path uses it first |
| `Visible CAD Diff` | UI display | display projection / See Regular | On-screen labels and inspection | No |

## 6. Export Formats

### 6.1 CSV / C

- CSV review: A human-readable trace / diff review artifact, not the FW direct-import contract
  - Fixed ordering: `ic_index -> fw_diff_idx -> version_code -> regular_pad_id -> cad_pad_id`
  - Fixed fields: `row_number, version, version_code, ic_index, ic_number, fw_diff_idx, regular_pad_id, cad_pad_id, payload_width, payload_01..payload_09, comment`
  - `payload_01..payload_09` corresponds to the currently supported `v2.1` 9-int row or `v2.2` 7-int node; unused fields remain empty
- `C v2.1`: The output retains only the `stV21` row section; `stV22` is an empty section
- `C v2.2`: The output retains only the `stV22` row section; `stV21` is an empty section

### 6.2 v2.2 Main Payload

`NotchV22Node` has 7 fields:

1. `AnchorDiffIndex`
2. `CombinePercent`
3. `TargetDiffIndex1`
4. `TargetRatioPercent1`
5. `TargetDiffIndex2`
6. `TargetRatioPercent2`
7. `Flags`

Key rules:

- `ToRegularRatio = Σ(overlapArea / targetRegularArea)` remains a diagnostic value; it describes the equivalent regular area covered by a CAD pad in the regular view.
- Since beta0.9, actual `Current (Gain)` / `Conservative (No Gain)` rows no longer multiply all target shares by the source-wide `R` of the entire CAD pad first.
- Per-target group coverage: Compute effective area / regular area for each regular first, then group and sum by `(IC, target diff)`:
  - `Current (Gain)`: Only `IsToFullApplied` regulars use `max(sourceArea, stage3EffectiveArea) / targetRegularArea`; the rest still use source coverage.
  - `Conservative (No Gain)`: Uses `source overlap area / targetRegularArea`; ToFull serves only as support/cap/allowance and does not directly increase the amount.
  - `Disabled`：`100`
- Each target group first applies `Math.Round(ratio * 100)`; absolute values greater than 100 are split into `<=100` chunks. Final legs include only emitted groups on the anchor IC, excluding the source/anchor diff, with `RatioPercentRounded != 0`, that pass the strict threshold or have ToFull applied coverage; not all overlap targets necessarily enter C.
- `CombinePercent` is the sum of the retained anchor group and the positive target legs actually emitted, with per-group rounding before summation; it must not be replaced by CAD-level `CombinedRatio`.
- `NotchV22TargetCoverageProjection` is the Application owner of the emitted target list, pre-guard raw combined percent, and `>255%` risk described above; the generator and revisioned resolved result both read it. For normal `CadAllocation` with both anchor/source present, the effective count, compact target line, and card role in the target-regular coverage display consume only `(IcIndex, DiffIndex)` membership in `EmittedTargets`; Inspector, Pad Info, Notch Detail, and Runtime Query must not sum targets again or independently reconstruct the `strict || ToFull` gate.
- On 2026-10-03, the Owner decided that for normal `CadAllocation` with both anchor/source present and coverage set to `Disabled`, the display follows the generator: even when `RawCombinedPercent=null`, the target summary/line/role still uses emitted membership and retains the existing strict gate filtering. A target that passes the strict gate but rounds to `0%` does not count as effective, has the `Below gate` role, and is excluded from effective details; full target diagnostics and the `0%` display are still retained. This new rule applies only to normal `CadAllocation`, not to `LegacyRegularAnchor`.
- `LegacyRegularAnchor` preserves the display from before `c8456b76`: it uses emitted membership only when `RawCombinedPercent` has a value, otherwise retaining `strict && !anchor`. The same case with both anchor/source present, `Disabled`, a passed strict gate, and rounding to `0%` still displays `Targets: 1 effective / 2 total` / `Target` / `0%` and retains the effective detail `IC1/diff1  A 0.45 mm²  R 0%`; V21 and Legacy calculations/outputs remain unchanged. SDISPLAY-3's `Build_LegacyRegularAnchor_DisabledRoundedZeroTargetPreservesPreviousDisplay` verifies this contract through an Inspector snapshot of a Legacy project; before the fix it was `0 pass / 1 fail` (Expected=`Targets: 1 effective / 2 total`, Actual=`Targets: 0 effective / 2 total`), and rerunning the same filter after the fix and rebuild yielded `1 pass / 0 fail / 0 skipped`, completing RED → GREEN. Output is saved in ignored `build/sdisplay3-red-test.txt` and `build/sdisplay3-green-test.txt`.
- When anchor or source is missing, the projection has `HasEmittedTargetMembership=false`; empty `EmittedTargets` means there is no emission context yet and must not be treated as a computed zero effective target count. The target summary/line/role retains the existing `strict && !anchor` fallback; a strict-passing `100%` target still displays `1 effective` / `Target` / `100%` and effective details. When anchor/source is missing or the mode is not target-regular coverage, `RawCombinedPercent` remains null, and the display ratio retains the existing all-target positive sum through the same Application policy (falling back to the CAD-level combined diagnostic when there are no targets). All target diagnostics, ordering, and strict/ToFull evidence remain unchanged.
- Since beta0.9, `Current (Gain)` has two additional guards that can be toggled:
  - `Boundary virtual-area cap`: Before the ToFull virtual extension area enters Stage3 gain/allocation, limit it to `inside overlap × cap ratio`.
  - `Target coverage guard`: After canonical rows are selected, sum retained/incoming coverage for each target FW diff; if it exceeds the cap (default `120%`), proportionally reduce that target's retained/leg percentages to prevent uniform 400 from exceeding EMS 480.
- More than two target legs are split into continuation rows
- The first row retains the actual `CombinePercent`
- Continuation rows fix `CombinePercent = 100`
- `NotchV22FirmwareProjector` is the sole normalization entry point from typed/legacy V22 table rows to final Firmware nodes: typed `V22Node` takes priority, `Values.Length >= 7` retains raw compatibility decode, and `< 7` preserves the existing legacy combine fallback and exact overflow exception.
- The projection provides both `SourceRows`, which preserves the original row/action ordinal, and `NodesByIc`, which preserves the existing C per-IC ordering; the two views share the same projected row. The exporter only formats projected nodes; simulation executes the same batch of nodes before aggregating Actions/diagnostics.
- Release no-op filtering belongs only to the C emission policy; it does not change the row set seen by the projector or simulation. Release must still exclude no-ops first, then order the remaining nodes according to the existing unstable sorting contract to preserve frozen C bytes.

### 6.3 v2.1 legacy payload

- Retains the legacy `9-int row`
- Primarily intended for compatibility with old formats or comparison with historical data
- Should no longer be treated as the source of a new primary contract
- The leg ratio is a `UINT8` Q7 magnitude of `0..255`, with `128=100%` and `255≈199%`; the sign is carried only by the `ADD/SUB` type.
- Encoding uses AwayFromZero; the final Firmware projector applies UINT8 saturation to the magnitude.
- Apply directly executes `(INT16 source * magnitudeQ7) >> 7`; it must not decode to percent/double first.
- `ThresholdQ7` is a separate `0..128` admission gate and must not be merged with the payload range.
- `NotchV21FirmwareProjector` is the sole projection from CadAllocation/LegacyRegularAnchor to final v2.1 ABI nodes; the exporter only formats, and simulation executes the same nodes.
- CadAllocation terms are grouped by `(IC, destination diff)`, then the final projector establishes deterministic order by source diff; each ABI node has at most two terms, and the third term starts the next destination node, without truncation or insertion into non-ABI fields.
- Both the evaluator and generated C execute modular narrowing using the `INT16` firmware carrier. After the characterization fixture splits three `32767 × Q7 128` terms into two nodes, the destination's observable final value is fixed at `32765`; this must not become saturation or unbounded floating-point/integer summation.
- Missing destination, missing source, and missing anchor are three distinct diagnostic contracts; IC/diff identity and the stable evaluator→source-row action order must be preserved.

## 7. C Export Contract

The C file currently produced by `NotchTableExporter.ExportAsCInitializer(...)` follows the `func_notch.c` style of codebase `v2.0.0`.

### 7.1 C File Guarantees

- 1.3.x frozen Lucid 3635 Release baseline: V2.1 has 692 nodes / 130,979 bytes / SHA-256 `8961B8155B0571B193C7C87D8EEA75077B4EF8822828506C4661B50BA2E57488`; V2.2 has 548 nodes / 84,023 bytes / SHA-256 `5208068BBD8D82FC0A628693EF6035B31288EC674A25724C0C962CD58757BB47`. See `docs/performance/regression-baseline-3635.md` for complete input provenance.
- 1.3.x permits only `V21_before == V21_after` and `V22_before == V22_after`; neither Q7 correctness nor simulation parity is an exception for updating the C golden.
- Revalidation must run V21→V22 with `run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget` and V22→V21 with `-ReverseCExportOrder`; Windows/.NET/MSBuild/GCC, script hash, complete layers/filter/settings, and artifact paths are maintained centrally in `docs/performance/regression-baseline-3635.md` to avoid creating a second calibration owner in this file.
- The output file begins with `#include "notch.h"` and is wrapped in `#if (USER_SWITCH_NOTCH_COMPENSATION == FUNC_ENABLE)`.
- It does not directly `#include <stdint.h>`; the output file assumes the FW platform already provides `UINT8`, `UINT16`, `INT8`, and `INT16`.
- It retains the public `FUNC_NHC_DiffCompensation(void)` entry point.
- Multiple ICs add only a minimal helper: `FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)`.
- `FUNC_NHC_DiffCompensation(void)` calls IC1 by default, preserving the old single-IC calling convention.
- `castNHC_TABLE` remains a legacy alias pointing to `castNHC_TABLE_IC1`.
- The Release profile does not output the FW simulation base mask; only the Debug profile additionally outputs `FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)`, allowing FW bring-up to recreate the same active regular baseline as FreeformHelper Simulation.
- It does not output a new root ABI such as `ST_PRI_NHC_TABLE_EXPORT` / `PriNhc_GetExport(...)`.
- It can be compiled with `gcc -std=c11 -Wall -Wextra -Werror` in a FW environment that provides `notch.h` / platform typedef / `S_2D_DIFFAFTER`.

### 7.2 Multi-IC Representation

- Each IC outputs an independent table, for example:
  - `castNHC_TABLE_IC1`
  - `castNHC_TABLE_IC2`
  - `castNHC_TABLE_IC3`
- `castNHC_TABLE_BY_IC[USER_NHC_IC_NUM]` stores the table pointer and row count for each IC.
- If an IC has no rows, its dispatch entry has a null pointer + `0` count; no invalid empty array is output.

### 7.3 Main Types

- `ST_PRI_NHC_TABLE_NODE_INFO`
  - Both the v2.1 / v2.2 C files use this name, following codebase v2.0.0 naming conventions.
  - v2.1 fields retain the legacy `NHC_IDX / NHC_REGULAR / NHC_REGU_TO_FULL / NHC_1ST_* / NHC_2ND_*`.
  - v2.2 fields change to a source-oriented payload: `NHC_IDX / NHC_COMBINE / NHC_1ST_IDX / NHC_1ST_RATIO / NHC_2ND_IDX / NHC_2ND_RATIO / NHC_FLAGS`.
- `ST_PRI_NHC_IC_TABLE_INFO`
  - Per-IC table dispatch metadata in codebase v2.0.0 style.
- `ST_PRI_NHC_FW_BASE_MASK_INFO`
  - FW simulation-specific mask dispatch metadata, output only by the Debug profile.
  - `pu16ActiveDiffs` points to the active regular FW diff list for that IC.
  - `u16ActiveDiffNum` is the active diff count.
  - `u16DiffSpan` is the diff span for that IC that must first be cleared to 0.

### 7.4 v2.2 Node Comment Semantics

- `R=...%` represents the To Regular diagnostic multiplier: `Σ(overlapArea / targetRegularArea)`. It is no longer multiplied into all target shares as a source-wide gain.
- `F=...%` represents the To Full diagnostic multiplier; in `Conservative (No Gain)`, To Full mainly serves as boundary support/cap/allowance, so `F=100%` is common without boundary expansion. In `Current (Gain)`, the Stage3 effective area after To Full affects only the coverage of the corresponding target.
- `C=...%` represents the node's actual coverage sum, namely the total self retained + target legs of this source row.
- `NODE KEEP=...% MOVE=...%` is the actual node flow additionally derived by C export; for example, `C=100%` with a target leg of `44` means the source retains `56%` and moves `44%`.

The current model rules for `C` are:

- `Current (Gain)`: `C = Σ(stage3EffectiveAreaOnTarget / targetRegularArea)`, not the source-wide gain of `round(R * F / 100)`.
- `Conservative (No Gain)`: `C = Σ(overlapAreaOnTarget / targetRegularArea)`; ToFull does not amplify source combine.
- `Disabled`：`C = 100`。

The target allocation rules for each model are:

- `Current (Gain)`: Target legs use per-target Stage3 coverage; a single leg exceeding `100%` is split into multiple continuation/chunk legs.
- `Conservative (No Gain)`: Target legs use per-target source overlap coverage; ToFull still serves only as support/cap/allowance.
- `Disabled`: Retains baseline source-area projection.

### 7.4.1 Complete No Gain / Gain Flow and CAD4818 Example

Shared preprocessing:

1. For each CAD pad, compute its intersection area with all overlapped regular pads.
2. Record the following for each target regular:
   - `regularArea`
   - `overlapArea`
   - `target FW Diff`
   - Whether it is affected by `ToFull` boundary expansion
   - `stage3EffectiveArea`, the effective area of that target after ToFull
3. Compute the CAD-level diagnostic:

```text
ToRegularRatio / R = Σ(overlapAreaOnTarget / targetRegularArea)
```

Key limitation: `R` describes only the equivalent regular area covered by this CAD pad in the regular view; it is no longer written directly into the row payload or multiplied into each target share as a source-wide gain.

`Conservative (No Gain)` flow:

```text
targetCoverage = overlapAreaOnTarget / targetRegularArea
CombinePercent = self retained coverage + Σ(target leg coverage)
```

- `ToRegular` remains enabled to restore area differences flattened by NF.
- `ToFull` still runs the rule engine, but determines only support / cap / boundary allowance.
- Actual target legs use only the original overlap coverage.
- No Gain therefore means "no ToFull amount gain", not no compensation at all.

`Current (Gain)` flow:

```text
targetCoverage = stage3EffectiveAreaOnTarget / targetRegularArea
CombinePercent = self retained coverage + Σ(target leg coverage)
```

- The Stage3 effective area after `ToFull` enters the allocation amount.
- The gain is per-target: only targets affected by ToFull are amplified.
- Combining the entire CAD pad into `R * F` first and then allocating again according to the CAD source share is not allowed.

Historical explanatory example: `CAD 4818`

The following `98.6%/58.8%` values are approximate intermediate values from early geometric analysis; the current signed artifact does not preserve their derivation command, so they cannot serve as a standalone acceptance gate. Executable current evidence consists of the checked-in V2.2 C row and its whole-file signed hash; signing off the original coverage again requires separately generating and preserving a hashable diagnostic artifact with `-CadId 4818`.

This case is on the right edge of 3635. The source of `CAD 4818` is `CAD Output FW Diff 1473`, and it geometrically covers two target regulars:

| target regular | target FW diff | CAD area share | target regular coverage |
| --- | ---: | ---: | ---: |
| `REG384` | `1472` | Approximately `47.0%` | Approximately `98.6%` |
| `REG385` | `1473` | Approximately `53.0%` | Approximately `58.8%` |

The diagnostic is therefore:

```text
R = 98.6% + 58.8% = 157.4%
```

The new No Gain row should be close to:

```text
source diff 1473
target leg to diff1472 = 99%
retained on diff1473   = 59%
CombinePercent         = 99% + 59% = 158%
```

The currently re-exported v2.2 C example already matches these semantics:

```c
{ 1473, 158, 1472, 99, NHC_DIFF_NONE, 0, NHC_FLAG_NONE }, // CAD=4818 ... NODE KEEP=59% MOVE=99%
```

The old algorithm incorrectly treated `R=157.4%` as a source-wide gain and then allocated by CAD area share:

```text
old retained on diff1473 ≈ CAD share 53% * R 157.4% ≈ 83%
```

However, the actual target coverage of `REG385` is only `58.8%`. If the adjacent CAD contributes approximately `41%`, the old algorithm yields:

```text
old diff1473 total ≈ 83% + 41% = 124%
400 * 124% ≈ 496
```

This is the root cause of the previous abnormal values around 500 at both side edges. The new algorithm lets `CAD4818` retain only approximately `59%` on `diff1473`; approximately `41%` incoming from the adjacent pad brings it back close to `100%`.

### 7.4.2 Consistency Between C Runtime and FreeformHelper Simulation

`NotchFirmwareCExporter` and `NotchApplySimulationService` both consume final nodes produced by `NotchV22FirmwareProjector`; simulation no longer decodes raw rows itself or merges continuation legs before recomputing physics.

1. First quantize the active baseline to FW `INT16`: truncate finite fractional values toward zero, saturate out-of-range values, and map NaN/Infinity to 0.
2. Before applying any node, back up the original value of each source diff; main/continuation rows for the same source all read the same original source.
3. For non-continuation rows, compute:

```text
retainedPercent = max(0, CombinePercent - TargetRatioPercent1 - TargetRatioPercent2)
sourceDelta     = retainedPercent - 100
```

4. Source and target scaling both use `(INT16)((source * percent) / 100)`; integer division truncates toward zero, and accumulation retains firmware modular narrowing.
5. Continuation rows add only their own target legs, without adjusting source retained again.
6. Aggregate Actions by `SourceRows` only after physics completes; action grouping must not affect firmware calculations in reverse.

Test contracts:

- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22ContinuationRows_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22Int16Truncation_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccAndCSharpSimulationMatchV21Q7BoundaryContract_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21LegacyRegularAnchor_WhenGccIsAvailable`

These tests compile and execute the exporter-produced C code with `gcc`, then compare it point by point with C# `NotchApplySimulationService` for exact equality; the previous `±1` tolerance is no longer accepted. The boundary fixture explicitly covers `0/1/127/128/129/255 × ADD/SUB`, and the Legacy fixture additionally locks ABI saturation. The C export algorithm is therefore protected by runtime parity tests, not only static format checks.

R13.103c-1 has two intentional correction fixtures:

- The V22 source in the MAIN/CONT continuation fixture is corrected from the incorrect `30` to `40`, with the complete result `[40,60,20,10]`; the frozen V21 ABI remains `[30,60,20,10]`, without requiring cross-version equality.
- `50%` target scaling for source `1` is corrected from floating-point `0.5` to the FW `INT16` result `0`.

Both cases correct only C# simulation; generated V22 C nodes, ordering, raw bytes, and signed golden remain unchanged. Valid `< 7` legacy rows are also extended from exporter-only compatibility to shared exporter/simulation compatibility; export bytes remain unchanged, and simulation no longer rejects such rows.

After R13.103c-2, the executable Firmware domain of `NullValue` is fixed at `0..UINT16.MaxValue`. `NotchSettings.ValidateNullValueOrThrow` is the Application owner; `ProjectSettings.ValidateOrThrow` and the V21/V22 final projectors share this rule, and Step 5/Runtime Query notch validation must also project it after validation through the same settings entry. A single C export must snapshot the sentinel before reading caller-owned collections, and the projector, macro, and formatter use the same value throughout; subsequent settings mutations affect only the next export. An out-of-range sentinel fails fast with `InvalidOperationException("NullValue must be in [0,65535].")` before node formatting/evaluation or validation report creation, without silent clamping or migration; the direct UseCase preserves this exception, while the Named Pipe server converts it at the outermost layer to the existing `IPC_ERROR` failure envelope and preserves the exact message, preventing a disconnected connection from degrading into `EMPTY_RESPONSE`. The empty C table, unsupported simulation, public `int`/persistence shape/UI range remain unchanged. With the V21 custom-adjacent sentinel `65534`, the real ref diff `65535` is corrected from the old incorrect `NHC_DIFF_NONE` literal to numeric `65535`; the default sentinel and Lucid signed C bytes remain unchanged.

The input domain for V21/V22 simulation is FW INT16: finite fractional values truncate toward zero, values beyond the INT16 range saturate, and NaN/Infinity quantize to 0. `Cells.BeforeValue`, Actions, and safety audit all use this quantized baseline to avoid misrepresenting input quantization as notch compensation. V21 CadAllocation additionally provides source-oriented Actions; V21 LegacyRegularAnchor rows are destination-oriented, so they provide only exact Cells/EMS, without producing source-flow Actions in the wrong direction.

If a parity gap is found in 1.3.x, only simulation/decoder/compatibility adapter adjustments that do not change C bytes are allowed; a correctness change requiring encoder, row ordering, or C emission changes must be filed as a separate post-1.3.x product issue.

### 7.4.3 C export generation metadata

C export outputs a comment-only metadata region without adding C structures or a root ABI. Metadata comments record:

- `ComputationMode`
- `CompensationModel`
- `SourceCombineRule`
- `TargetAllocationRule`
- effective `ToRegular / ToFull`
- `ToFullRuleEngine / Trace`
- `BoundaryVirtualAreaCap / BoundaryVirtualAreaCapRatio`
- `TargetCoverageGuard / TargetCoverageCapPercent`
- export profile
- Whether v2.1 / v2.2 actually has rows
- The active diff count and span to clear to 0 for the FW simulation base mask (Debug profile only)

### 7.5 FW simulation base mask

The Debug profile of C export additionally outputs a FW simulation base mask so FW can create a baseline consistent with FreeformHelper Simulation without hand-writing another `SeeRegular`-like mask.

The Release profile omits the entire simulation base mask section and does not output the related defines, structs, `cau16NHC_FW_BASE_MASK_ICx[]`, dispatch table, or `FUNC_NHC_SimulationLoadFwBaseMask*` helpers, so the production `.c` file does not carry extra constant data.

- Mask source:
  - Uses the active regular set if one is available during export.
  - Otherwise, falls back to all pads in the current regular grid.
- Mask contents:
  - One `cau16NHC_FW_BASE_MASK_ICx[]` per IC containing the FW diff indices corresponding to active regulars.
  - One dispatch entry per IC: `castNHC_FW_BASE_MASK_BY_IC[u8Ic]`.
- runtime helper：
  - `FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)`
    - First sets the entire `0..u16DiffSpan-1` range of that IC to `0`.
    - Then sets the active FW diffs in the mask to `s16BaseValue`.
  - `FUNC_NHC_SimulationLoadFwBaseMask(void)`
    - Legacy single-IC helper; loads IC1 by default with base value `USER_NHC_SIM_BASE_VALUE`, currently defaulting to `400`.
- Notes:
  - This is a simulation / bring-up helper and does not change the production compensation flow of `FUNC_NHC_DiffCompensationByIc(...)`.
  - Production FW compensation does not need this data; switch to the Debug profile for export only when recreating the FreeformHelper Simulation baseline inside FW is needed.
  - Before a multi-IC FW call, the FW side still needs to switch to the corresponding IC's `S_2D_DIFFAFTER` buffer/context.

### 7.6 Public Symbols

- `castNHC_TABLE_ICx`
  - The actual table for each IC.
- `castNHC_TABLE_BY_IC`
  - Multi-IC dispatch table.
- `castNHC_TABLE`
  - Legacy single-IC alias, equivalent to `castNHC_TABLE_IC1`.
- `castNHC_FW_BASE_MASK_BY_IC`
  - Output only by the Debug profile; the per-IC FW simulation base mask dispatch table.
- `FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)`
  - Output only by the Debug profile; the FW simulation baseline loading helper.
- `FUNC_NHC_SimulationLoadFwBaseMask(void)`
  - Output only by the Debug profile; the legacy single-IC baseline loading helper.
- `FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)`
  - Multi-IC entry point; before calling, the FW side should switch to the corresponding IC's `S_2D_DIFFAFTER` buffer/context.
- `FUNC_NHC_DiffCompensation(void)`
  - Old codebase entry point; applies IC1 by default.

### 7.7 v2.1 / v2.2 Differences

- `C v2.1`
  - Still uses the legacy compensation formula from codebase v2.0.0: first multiply by `NHC_REGU_TO_FULL`, then build the offset using `NHC_TYPE_ADD/SUB + Q7 ratio`, and finally add it back to `NHC_IDX`.
  - Under the `CadAllocation` canonical path, the shared final projector first converts the source-oriented v2.1 payload into destination-oriented rows that the legacy FW function can execute correctly; both exporter and simulation consume only this result.
  - This allows the old v2.1 runtime formula to support the current v2.2 canonical semantics.
- `C v2.2`
  - The file structure and entry points still follow codebase v2.0.0 style.
  - The table payload becomes source-oriented: source diffs are backed up first, then redistributed to targets according to `combine / legs / continuation`; legs are `INT8 -100..100` signed percentages and do not pass through the V21 Q7 codec.
  - v2.2 is no longer imported through a unified export root.

Both v2.1 and v2.2 are therefore currently "based on codebase v2.0.0, replacing only the table contents and compensation algorithm".

## 8. Common Points of Confusion Between Current Behavior and Documentation

### 8.1 `Visible` Does Not Equal `Output`

- `Visible CAD Diff` is the on-screen projection result
- `CAD Output FW Diff Idx` is the workflow source of truth

### 8.2 `Best Match FW Diff` Does Not Equal `Final Output`

- Best match is a geometric seed
- Final output is the Step4 assignment result

### 8.3 Simulation Should Not Return to CAD as the Source of Truth

- CAD participates in notch table generation
- Once Simulation begins, it should center on regular + FW diff + notch table

## 9. Domain Golden Rules (Sensor / FW / Notch Physical Assumptions)

This section records the domain golden rules that current Freeform / Notch design should follow. If future algorithms conflict with these rules, this section should first be used as the review baseline before deciding whether to correct the algorithm or explicitly revise the documentation.

### 9.1 Staggered Geometry Assumptions

- The sensor team necessarily introduces locally staggered arrangements when arranging irregular-shaped panels.
- The golden rule is that severe staggering occurs along only one main axis at a time: severe staggering should primarily follow either `X` or `Y`, without large offsets on both axes simultaneously.
- `CAD pad area / Regular pad area` should generally fall within `75% ~ 125%` to prevent excessive pad area differences from causing abnormal Open Test or actual sensing amounts.

### 9.2 FW / Memory Perspective

- The tool and IC memory see only sensing amounts arranged in a rectangle.
- FW ultimately consumes the regular view / rectangular memory view and does not directly see the staggered geometry on the sensor.
- `(IC, FW Diff Idx)` therefore represents the final memory / channel source of truth, rather than the CAD geometry source of truth.

### 9.3 Fundamental Purpose of the Notch Table

- The actual sensing amount distribution in freeform / staggered regions differs from the distribution FW assumes when computing coordinates with a rectangular grid.
- The notch table redistributes "the sensing amounts actually received by the sensor" back to the regular view, allowing FW to compute positions close to the actual touch points in the regular / rectangular coordinate system.

### 9.4 NF (Normal Force) Assumptions

- Current sensor pads all have `NF`; a grounded copper pillar is swept across the entire surface to bring sensing amounts across the surface to roughly `400`.
- The original purpose of NF is to compensate for:
  - Near-end/far-end differences
  - Positional differences
  - Differences inside / outside the surface (edges)
- However, NF also flattens some sensing amount differences caused by different pad areas:
  - Sensing amounts of larger pads are partially reduced
  - Sensing amount differences of smaller pads are partially smoothed out

### 9.5 Physical Meaning of To Regular

- `ToRegular` is not intended simply to apply gain; it first restores the area differences flattened by NF.
- In other words, `ToRegular` should be viewed as "bringing sensing amounts back closer to proportionality with area" to offset NF's partial compensation for area differences.
- If this assumption holds, `ToRegular` is physically meaningful and closer to the actual sensor area distribution than simply retaining the post-NF values.

### 9.6 Physical Meaning of To Full

- `ToFull` is intended to support boundary edge extrapolation, rather than arbitrarily amplify sensing amounts.
- The reason is that the Boundary algorithm estimates positions from a line of sensing amounts outside the edge; if the freeform region has no valid sensing output before notch correction, the edge cannot be extrapolated outward correctly.
- Reasonable uses of `ToFull` should therefore be:
  - Allowing edge regulars to obtain usable boundary sensing support
  - Helping edge touch reporting, rather than rewriting the main allocation rules across the interior

### 9.7 Canonical Recommendations for To Full

- Whether `ToFull` should directly use the expanded full regular area as the target allocation weight should not currently be considered settled.
- The recommendation currently more consistent with physical intuition is:
  - `exact overlap area` should be the main allocation weight
  - `ToFull` should serve as a `support mask / reachability cap / boundary allowance`
  - Use `ToFull` only at the level of "whether allocation can reach there" and "whether boundary compensation is allowed"
  - Tiny overlap should not become the dominant target ratio directly through full-area expansion
- Exception: `Current (Gain)` is a research/calibration model. Since beta0.9, it no longer treats CAD-level `R*F` as a source-wide gain; instead, it directly uses per-target Stage3 coverage to generate retained / target legs, avoiding coverage overflow from small edge regulars.

### 9.8 Role of Simulation

- Simulation aims to locally simulate FW behavior after applying the notch table.
- It is not a second source of truth; it serves to:
  - Verify whether Step5 / notch table is reasonable
  - Use uniform input such as `global 400` as diagnostic input to expose local net-flow imbalance or unreasonable accumulation
  - Check risks that the shared EMS after-cap predicate evaluates as true and that may trigger EMS protection
- An `After` close to 400 is not sufficient for physical correctness; the main constraint remains whether redistribution of geometric area ratios into the FW memory view is internally consistent.

### 9.8.1 Simulation safety / physical audit gate

- `SimulationSafetyAuditService.Analyze(NotchApplySimulationResult)` is the formal audit entry point before Simulation / export.
- `SimulationSafetyAuditService.IsEmsAfterCapViolation(afterValue, afterCap)` is the Application owner of the EMS after-cap decision; the default cap remains `DefaultEmsAfterCap = 480`. The sole decision contract is `afterValue > afterCap + 1e-9`: `cap` and `cap + 0.5e-9` are safe, while `cap + 2e-9` is a violation.
- `SimulationSafetyTextProjector.BuildStatusText(SimulationSafetyAuditResult?)` is the UI owner of workspace status and overview base availability/severity text: `null` or `HasCells == false` yields `Simulation not run`; with cells present, priority is `EMS risk > audit warning > EMS OK`, and physical warnings directly read the audit's existing global-flow/net-flow/target-coverage facts. Overview `HasRisk` still represents only EMS danger, while `NeedsAttention` covers physical risk and stale state; non-stale summaries append `BuildPhysicalAuditSummaryText` evidence after the existing EMS sentence. The stale suffix and build-failure `Simulation unavailable` are still decorated by the overview boundary; cell/high-risk text and the EMS cap/predicate are outside this mapping. Copper replay severity is separately projected by `BuildReplayStatusText` in the next section.
- `SimulationSafetyTextProjector.FormatEmsAfterCap` is the UI owner of overview/export no-cells cap text: a supplied audit retains its `AfterCap` even when `HasCells == false`; only a null audit uses `DefaultEmsAfterCap = 480`. This fallback does not change `HasAudit`, availability status, summary, or the EMS violation predicate.
- `SimulationSafetyTextProjector` also owns the Notch `EMS cap ...` short text, Simulation safety policy, export handoff policy, and target-cap help templates. The main `FreeformHelperViewModel` supplies only the current `SimulationSafetyOverviewEmsCapText` and current target cap to this owner, notifying the corresponding dependent properties when the cap changes; Settings preserves the documented context using the same owner, current draft target cap, and default EMS cap. The existing target-cap tooltips in active Step 3 and Settings Step 3 bind to `NotchTargetCoverageCapHelpText`: for example, target cap `128%` projects `After 512` for uniform `400`, with the active side comparing the current EMS cap and Settings comparing the default cap. This text projection does not independently compute risk or change the target coverage guard, EMS after-cap predicate, or its inputs.
- The Notch export review badge reads the shared `BuildStatusText` severity for a supported audit; EMS cases retain the existing violation count, while physical-only cases show `audit warning · Max After ...` without blocking export. The physical-only export summary retains the existing safe EMS-cap/location sentence, then appends the same `BuildPhysicalAuditSummaryText` evidence; when both EMS/physical risks are present, the full existing block dialog text remains unchanged. The VM supplies only the physical-only visibility fact, and XAML reuses the existing `chipStatus warning`/token; status strings must not be parsed and global-flow/net-flow/coverage must not be recomputed. Null/no-cells still means not run, and only EMS violations can trigger the existing block dialog.
- The audit uses the same simulation result, without re-deriving it from partial UI state:
  - `Cells` provides `Before / After / Delta`, Max After, and EMS cap violations.
  - `Actions` provides source retained, target legs, and per-diff net-flow.
- Default hard gates:
  - EMS after-cap: The default cap is `480`; a violation occurs only when `After > 480 + 1e-9`
  - Target coverage cap: Retained + incoming coverage of the same `(IC, FW Diff)` must not exceed `120%` by default
- The audit outputs:
  - global action-flow residual：`Result.DeltaTotal - Σ(action source/target delta)`
  - per-diff net-flow residual：`cell.Delta - (sourceDelta + targetDelta)`
  - Target coverage risk: Locates `(IC, FW Diff)` whose retained + incoming coverage exceeds the cap
  - High-risk diff: Ordered by `After`, retaining the cap margin and worst REG / IC / FW Diff
- Audit violation/count, net-flow/target-coverage `EmsRisk` classification, Runtime Query `regular.isEmsSafetyRisk`, cell `EMS OK` / `EMS risk`, and workspace/overview high-risk status all project the same predicate. `MarginToCap = afterCap - afterValue` remains raw numeric evidence and must not be reevaluated solely by its sign; a tiny negative margin within tolerance is still safe and is presented as a zero margin.
- Cause classifications:
  - `GeometryExpected`: Meets the geometry coverage cap, and the deviation can be explained by area/boundary semantics
  - `NetFlowSuspicious`: Cell delta is inconsistent with action net-flow
  - `EmsRisk`: The shared EMS after-cap predicate evaluates to true for that cell/max After
- `global 400` remains diagnostic input only. The audit does not treat "closer to 400" as a correctness goal; the actual gates are EMS, net-flow consistency, and the geometry coverage cap.

### 9.8.2 Copper path replay

- Copper path replay executes through `SimulationWorkspaceUseCase.ReplayCopperPath(...)`.
- Every path point follows the same production path:
  - `BuildCopperDataset`
  - `BuildSnapshot`
  - `SimulationSafetyAuditService.Analyze(...)`
- Replay steps record:
  - copper center
  - Max After
  - EMS violation count
  - worst REG / IC / FW Diff
  - net-flow residual count
  - target coverage risk count
- Replay steps preserve simulation support state and global action-flow residual through non-positional, `[JsonIgnore]` facts without changing the existing positional constructor; artifact rows copy the same JSON-ignored global-flow fact so existing structured physical-risk and status stay consistent, without adding artifact serialized fields.
- `CopperPillarPathReplayStep.HasPhysicalAuditRisks` covers global-flow residual, net-flow residual count, and target-coverage risk count; `CopperPillarPathReplayResult` then projects `HasUnsupportedSteps` and `HasPhysicalAuditRisks` from the steps, and consumers must not parse `StatusText` to reconstruct state.
- `SimulationSafetyTextProjector.BuildReplayStatusText(isSupported, hasEmsViolations, hasPhysicalAuditRisks)` is the sole owner of replay severity wording, with fixed priority `unsupported`, `EMS risk`, `audit warning`, `EMS OK`. UseCase step status and VM aggregate summary both read this owner; the aggregate retains the existing total violation count suffix only for EMS cases.
- Existing artifact CSV/JSON/clipboard fields, headers, ordering, public positional constructors, and replay physics/sampling remain unchanged. Public headless characterization verifies only the result/ViewModel seam and does not mean the normal operator workflow necessarily builds the same replay.
- The purpose is to automate sweep validation at edges and notch regions, avoiding reliance on manual observation to assess release risk, rather than to replace interactive mouse preview.

### 9.9 Practical Application Assessment

Given the current practical sensor / FW / NF scenarios, `ToRegular` and `ToFull` should be treated as processing steps with two different purposes:

- `ToRegular`
  - Conclusion: Reasonable and should be retained.
  - Reason: NF flattens sensing amount differences caused by different pad areas; the reasonable meaning of `ToRegular` is to first restore sensing amounts in staggered regions closer to proportionality with area.
  - Usage limit: It should be understood as `Undo NF area flattening`, rather than arbitrary gain.
- `ToFull`
  - Conclusion: Reasonable, but expanded/full area should not directly determine the target ratio.
  - Reason: The actual purpose of `ToFull` is to provide sufficient edge support for boundary / edge extrapolation algorithms, rather than let tiny overlap become the main allocation leg through full expansion.
  - Recommended meaning: `ToFull` determines reachability, support eligibility, caps, or boundary allowance; the actual allocation ratio should still primarily be determined by `exact/source overlap`.
- `with gain`
  - Conclusion: Unsuitable as the default main path.
  - Reason: It has been corrected to per-target Stage3 coverage, but gain may still amplify local net-flow imbalance until measurements establish uniform-field stability for target allocation.
- `without gain`
  - Conclusion: More suitable as the default main path.
  - Reason: It preserves the area restoration of `ToRegular` without treating `ToFull` as a source-side amplification factor, making it less likely to create EMS safety risks or extra local accumulation.

## 10. Tradeoffs and Recommendations for the Current Four Combinations

### 10.1 Direct Conclusion

- If the goal is "the closest match to the physical assumptions above while retaining acceptable resource costs", the currently most recommended production design source of truth is:
  - `v2.2 + without gain (ConservativeNoGain)`
- If FW or an external importer still accepts only `v2.1`:
  - The design source of truth should still be `v2.2 canonical`
  - `v2.1` serves only as a compatibility projection / export format
- `with gain` should not currently be the default main path; although target legs now use Stage3 effective allocation, measurements are still needed to confirm whether ToFull gain matches physical measurements.

### 10.2 Comparison of the Four Combinations

| Combination | Physical fidelity | Tool / Simulation cost | Export / FW cost | Conclusion |
| --- | --- | --- | --- | --- |
| `v2.2 + without gain` | Highest (among current shipping options) | Medium | Medium to low | Most recommended; serves as canonical truth |
| `v2.2 + with gain` | Medium | Medium | Medium to low | May be retained for calibration/research, but unsuitable as the default |
| `v2.1 + without gain` | Medium-low | Low to medium | Low | Suitable only for compatibility export, not as the design source of truth |
| `v2.1 + with gain` | Lowest | Low to medium | Low | Least recommended; both format limitations and gain amplification issues apply |

Additional notes:

- The current normal `CadAllocation` export cache holds a compact output-request-neutral candidate batch; concurrent full-table consumers with the same identity/epoch share a phases 1～3 resolution task, then each performs phase 4 final projection for its enabled versions, threshold, `NullValue`, and target guard/cap. Generation completion rejects stale store/publish using the cache epoch, final-projection revision, and source revision frozen at entry; final-only invalidation retains the task/batch but keeps the row count at `0`, while full invalidation advances the epoch, detaches the task, and prevents old completions from storing afterward. R13.102a-2b-2 additionally lets the batch carry and reuse/promote at most one identity-bound sparse resolved result for the currently selected single CAD; it does not store the full polygons/debug evidence of other candidates. R13.101e has made the normal generator/UI first build a complete compensation context, so callers no longer control stage order through nullable combinations of allocation/boundary/active/strict/query/policy evidence; the context still contains no final-output state. R13.103c-1 lets the V22 C exporter and simulation share the same final Firmware projector, while R13.103c-2 fixes the `UINT16` sentinel fail-fast shared by V21/V22 final projection. Although `LegacyRegularAnchor` has removed the registry and switched to fixed, explicit V21/V22 dispatch, it still retains a request-specific table cache and compatibility boundary; the R13.101c-2/R13.102/R13.102a-2/R13.103 parents remain open against their respective exit criteria.
- Looking only at row payload, a `v2.2 node` is `7-int`, while a `v2.1 legacy row` is `9-int`; under the current contract, `v2.2` therefore does not necessarily use more memory than `v2.1`.
- The actual cost difference lies mainly in semantic capabilities and downstream compatibility, rather than "v2.2 is always heavier".

### 10.3 Why `without gain` Is More Suitable as the Default

- Since beta0.9, `with gain` no longer multiplies `ToRegularPercent * ToFullPercent` back into each target share.
- The v2.2 row for `with gain` uses per-target Stage3 coverage: `stage3EffectiveAreaOnTarget / targetRegularArea`.
- The v2.2 row for `without gain` uses per-target source overlap coverage: `overlapAreaOnTarget / targetRegularArea`; ToFull serves only as support/cap/allowance.
- `ToRegularRatio = Σ(overlapArea / targetRegularArea)` remains an important diagnostic value, but no longer serves as a source-wide gain for the entire CAD pad.
- `Target coverage guard` remains an EMS safety net. It does not force all values back to 400; after row selection, it limits the total coverage of a single target FW diff, reducing the chance that multiple sources accumulating at the boundary are classified as a risk by the shared EMS after-cap predicate.
- Historical non-signed diagnostics recorded `max=424 / EMS violations=0`; the current 3635 signed gate did not preserve that uniform-400 metric, so these numbers must not be treated as current acceptance facts. Reuse requires generating a simulation artifact containing input/command/environment/hash.
- The design judgment remains to avoid treating CAD-level `R` as a source-wide gain and then allocating again by source share; however, EMS conclusions should come from current `SimulationSafetyAuditResult` evidence, not be inferred from these bare historical numbers.
- When using `global 400` as diagnostic input, you should first check:
  - Whether risks identified by the shared EMS after-cap predicate exist
  - Whether large deviations at a diff can be explained by geometric area ratios, boundary support, or net-flow residual
  - Enable `with gain` only after the allocation matrix has been shown to be reasonable and physical measurements additionally support gain calibration

### 10.4 Why `v2.2` Is More Suitable Than `v2.1` as the Mainline

- `v2.2` can explicitly express:
  - `source diff`
  - `combine percent`
  - `target diff legs`
  - continuation rows
- `v2.1` is already a compatibility payload in the current system and should no longer carry new primary semantic design.
- `v2.2` has clearly more suitable expressive capabilities for freeform / boundary / SeeRegular / per-diff redistribution.

### 10.5 Post-1.3.x Algorithm Research Directions (Outside the Zero-Diff Refactor)

1. `ToRegular` should be retained
   - It matches the physical assumption of "first restoring the area differences flattened by NF".
2. `ToFull` should become support/cap rather than directly serving as target weight
   - The beta0.7 baseline v2.2 allocation takes `ReachableArea` as `EffectiveArea` directly into the target ratio when `ToFull` applies.
   - The beta0.8 prototype already switched to `SourceArea` as the main weight so `ToFull` no longer directly dominates the ratio.
   - Since beta0.9, `Current (Gain)` exceptionally returns to Stage3 effective allocation because gain mode produces abnormal edge retained signals if allocation does not use the post-ToFull area.
   - beta0.9 adds the first cap/guard implementation: ToFull virtual extension area can be limited by the inside overlap ratio, and CurrentGain can use the target coverage guard to prevent boundary net-flow accumulation from exceeding EMS.
   - This still requires AutoCAD measurements: if target coverage >120% comes from actual CAD geometry coverage, the guard can serve only as a safety policy; only if it comes from algorithmic accumulation should the guard be the CurrentGain default.
3. Target allocation should add area-preserving + EMS-guarded constraints
   - The main goal is to avoid unreasonable accumulation while preserving source/target area ratios, rather than force every `After` close to `400`.
   - `global 400` is a diagnostic case: frequent deviations of `100+` at interior diffs must be explainable as reasonable geometry / boundary effects; otherwise, classify them as net-flow suspicious.
   - A true shared EMS after-cap predicate is treated as an EMS safety violation that requires a warning or block before Simulation / export; the target coverage guard is the first automatic suppression strategy.
   - Since 2026-04-30, the formal audit checks global action-flow residual, per-diff net-flow residual, and target coverage cap together, allowing high-deviation diffs to be classified as `GeometryExpected`, `NetFlowSuspicious`, or `EmsRisk`.
4. `single-axis` and `CAD/Regular area ratio` remain domain assumptions for now
   - They describe the reasonable range of sensor design inputs.
   - beta0.8 keeps them outside the main development scope because the tool currently cannot control the sensor team's output.

The future state that best matches your description should therefore be:

- `v2.2 canonical`
- `without gain` as the default
- Retain `ToRegular`
- Change `ToFull` to `support / cap / boundary allowance`
- Add `area-preserving + EMS-guarded` safety correction and audit to target allocation; beta0.9's `Boundary virtual-area cap` / `Target coverage guard` is the current first implementation

## 11. Related Deep-Dives / Examples

- [Notch v2.1 / v2.2 Flow Definitions and Flowcharts](../core/notch-v21-v22-flow.md)
  - Breakdown of Step5 flow, buckets, continuation, and export paths
- [Notch / Simulation Mermaid Diagram Collection](../diagrams/notch-simulation/zh-TW/README.md)
  - Bilingual overview, Notch table module map, Simulation module map, and detailed subdiagrams for each module
- [Notch V21 Algorithm Details](../core/notch-v21-algorithm.md)
  - Geometric semantics and Q7 contract of the v2.1 legacy 9-field row
- [Notch V22 Algorithm Details](../core/notch-v22-algorithm.md)
  - Algorithm breakdown of the v2.2 mainline and legacy compatibility branch
- [Current v2.1 C export example](../../example/BOE36.35/notch_export_v21_current.c)
  - v2.1 `.c` example produced by the current exporter
- [Current v2.2 C export example](../../example/BOE36.35/notch_export_v22_current.c)
  - v2.2 `.c` example produced by the current exporter

## 12. Maintenance Rules

Any future change to the following must update this file first:

- Diff identity naming
- Step5 source / target definitions
- Simulation input source of truth
- C export ABI / symbol / multi-IC format

If a deep-dive document conflicts with this file, this file takes precedence; the deep-dive document must add a note explaining the difference or be retired.
