# TM 8.1 Notch 驗收矩陣（v2.1 / v2.2）

## 目的
- 建立 `TM 8.1` 的專案級固定驗收面板，避免只看 row count / sample row 而看不到行為漂移。
- 將 `S11.76`（diff assignment / local repair / passive compensation）後續變更，固定回歸到同一份矩陣。

## 固定來源
- Snapshot：`example/golden-snapshots/tm81-notch-acceptance-matrix.json`（private data repository）
- Test：`tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs`
- 專案輸入：`example/TM 8.1/TM8.1.json`
- mask 輸入（diff repair 摘要）：`example/TM 8.1/SeeRegular.csv`

## 當前基準（2026-03-26）

### Row / 版本分布
- `rowCount`: `631`
- `V21`: `416`
- `V22`: `215`

### Export 分布
- `transfer`: `601`
- `warning`: `30`
- `noCad`: `0`
- `legacy`: `416`
- `linked`: `631`

### V22 no-op 比例
- `v22RowCount`: `215`
- `noOpRowCount`: `30`
- `noOpRatio`: `0.13953488372093023`（約 `13.95%`）

### Warning 類型（目前）
- `warningTypeCounts`
  - `NO_OP_OTHER`: `30`
- `warningCommentCounts`
  - `CAD=# R=#% F=#% C=#% NT`: `30`

### Diff repair 摘要（Regular Visibility Mask (SeeRegular.csv) 啟用後）
- `maskAuditRowCount`: `10`
- `repairSuggestionRowCount`: `2`
- `passiveCompensationRowCount`: `8`
- `segmentOffsetDetectedRowCount`: `0`
- `repairReasonCodeCounts`
  - `duplicate-conflict`: `2`
- `repairModeCounts`
  - `csv-constrained`: `2`
- sample（目前前兩筆）：
  - `CAD222`: `current=15`, `suggested=15`, `reason=duplicate-conflict`
  - `CAD332`: `current=34`, `suggested=34`, `reason=duplicate-conflict`

### 固定案例契約（盤點）
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

## 可疑案例（需人工確認）
- `CAD364 / REG291`：`stage3Coverage` 與 `reg291Area` 存在小量差距（約 `3.45e-06`）。
- `CAD490 / CAD491 / REG643`：`stage3OverlapArea` 目前為 `0.0004078079998396788`，非 0。
- `TM 8.1` 單指 simulation fixture 仍有舊測試不穩定項（`DiffFrameCsvFixtureTests` 某些 `NotNull` 斷言），需後續獨立收斂。

## 回歸命令
- 驗證（比對 snapshot）：
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --nologo /p:UseAppHost=false --filter "FullyQualifiedName~Tm81NotchAcceptanceMatrixTests"`
- 更新 snapshot（僅在契約變更後）：
  - PowerShell:
    - `$env:FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX='1'`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --nologo /p:UseAppHost=false --filter "FullyQualifiedName~Tm81NotchAcceptanceMatrixTests"`
    - `Remove-Item Env:FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX`
