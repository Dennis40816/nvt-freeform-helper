# TM 8.1 Notch Acceptance Matrix (v2.1 / v2.2)

## Purpose
- Establish a fixed project-level acceptance panel for `TM 8.1`, to avoid behavior drift that is not visible from row count or sample rows alone.
- Pin all later changes to `S11.76` (diff assignment / local repair / passive compensation) to regression against the same matrix.

## Fixed Sources
- Snapshot: `example/golden-snapshots/tm81-notch-acceptance-matrix.json` (private data repository)
- Test: `tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs`
- Project input: `example/TM 8.1/TM8.1.json`
- Mask input (diff repair summary): `example/TM 8.1/SeeRegular.csv`

## Current Baseline (2026-03-26)

### Row / Version Distribution
- `rowCount`: `631`
- `V21`: `416`
- `V22`: `215`

### Export Distribution
- `transfer`: `601`
- `warning`: `30`
- `noCad`: `0`
- `legacy`: `416`
- `linked`: `631`

### V22 No-Op Ratio
- `v22RowCount`: `215`
- `noOpRowCount`: `30`
- `noOpRatio`: `0.13953488372093023` (about `13.95%`)

### Warning Types (Current)
- `warningTypeCounts`
  - `NO_OP_OTHER`: `30`
- `warningCommentCounts`
  - `CAD=# R=#% F=#% C=#% NT`: `30`

### Diff Repair Summary (With Regular Visibility Mask (SeeRegular.csv) Enabled)
- `maskAuditRowCount`: `10`
- `repairSuggestionRowCount`: `2`
- `passiveCompensationRowCount`: `8`
- `segmentOffsetDetectedRowCount`: `0`
- `repairReasonCodeCounts`
  - `duplicate-conflict`: `2`
- `repairModeCounts`
  - `csv-constrained`: `2`
- Samples (current first two rows):
  - `CAD222`: `current=15`, `suggested=15`, `reason=duplicate-conflict`
  - `CAD332`: `current=34`, `suggested=34`, `reason=duplicate-conflict`

### Fixed Case Contract (Inventory)
- `CAD113`
  - `isToFullEnabled=false`
  - `ruleCode=NO_EXPANSION_NEEDED`
  - `stage1/2/3 count = 0/0/0`
- `CAD364 / REG291`
  - `ruleCode=EXPAND_CLEAR_PATH`
  - `isBoundary=false`
  - `stage2Coverage=25.98552576000293`
  - `stage3Coverage=25.98552231092617`
  - `reg291Area=25.98552576000293`
- `CAD402 / REG624`
  - `ownerCadPadIds=[402]`
  - `matchedCadPadIds=[402]`
- `CAD490 / CAD491 / REG643`
  - `ruleCode=EXPAND_SHARED_REACHABLE`
  - `stage3OverlapArea=0.0004078079998396788`
- freeform tail-link
  - `REG387=None`
  - `REG388=XWay (Override)`
  - `REG389=XWay (Override)`

## Suspicious Cases (Require Manual Confirmation)
- `CAD364 / REG291`: A small gap exists between `stage3Coverage` and `reg291Area` (about `3.45e-06`).
- `CAD490 / CAD491 / REG643`: `stage3OverlapArea` is currently `0.0004078079998396788`, not 0.
- For `TM 8.1`, the simulation fixture alone still has old unstable tests (some `NotNull` assertions in `DiffFrameCsvFixtureTests`) that need to be resolved separately later.

## Regression Commands
- Verify (compare against snapshot):
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --nologo /p:UseAppHost=false --filter "FullyQualifiedName~Tm81NotchAcceptanceMatrixTests"`
- Update snapshot (only after contract changes):
  - PowerShell:
    - `$env:FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX='1'`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --nologo /p:UseAppHost=false --filter "FullyQualifiedName~Tm81NotchAcceptanceMatrixTests"`
    - `Remove-Item Env:FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX`
