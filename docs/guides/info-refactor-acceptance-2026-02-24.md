# Info Refactor 驗收指南（2026-02-24）

## 1. 驗收目標
本次驗收聚焦今晚 `T1~T4`：

1. Inspector 一次全顯示，改為固定 block 分段（非收合式），且內容不超出容器。
2. Canvas 比例標籤避撞（`Match` 與 Notch ratio 不重疊）。
3. Step5 `Select notch rows` 視窗資訊重整（IC 固定分組、row 欄位拆解、preview 完整欄位）。
4. Selection 互動去阻塞（框選期間不因 inspector/deferred context 建立而卡頓）。

## 2. 驗收前準備
1. 分支：`refactor-non-notch-architecture`
2. Build：
```powershell
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release
```
3. 測試（至少跑一次）：
```powershell
scripts/tests/run-tests.ps1 -Group ui-core -Configuration Release
scripts/tests/run-tests.ps1 -Group ui-snapshots -Configuration Release
scripts/tests/run-tests.ps1 -Group smoke -Configuration Release
```
4. 測試資料：
`example/BOE36.35/project_3635.json`
`example/BOE36.35/cz_36d2_6480x848_Lucid_panel_touch_block_CAD_20250729_NVTint_regular.dxf`

## 3. 手動驗收案例

### Case A：Inspector 可讀性與防超框
1. 右側切到 `Inspector`。
2. 選一顆 CAD，再選一顆 REG。
3. 檢查 `Inspector snapshot` 內容。

預期：
1. 內容為固定三段：`summary`、`Snapshot details`、`Rule trace`，中間有單線分隔。
2. 無收合 dropdown。
3. 長字串會換行（`WrapWithOverflow`），不應超出外層 container。

### Case B：比例標籤重疊
1. 在有 Match 與 Notch ratio 的區域進行選取（建議 3635 密集區）。
2. 反覆縮放（in/out）並切換 selection。

預期：
1. `Match` 標籤優先保留。
2. Notch ratio 標籤會避讓已佔位區域，不與 `Match` 直接重疊。

### Case C：Step5 row 視窗
1. 進入 Step5 匯出並打開 `Select notch rows`。
2. 檢查左側 IC 區塊、row 卡片資訊。
3. 點不同 row，觀察右側 preview。

預期：
1. 左側 IC 分組為固定 block（非收合）。
2. 每組顯示統計：`v2.2 / legacy / CAD linked`。
3. row 卡片至少含：`row/version/IC/diff`、`REG->CAD`、`column breakdown`、`values/comment`。
4. 右側 preview 含：`pad relation`、`payload`、`column breakdown`、`flags`、`analysis`。

### Case D：Selection 去阻塞（重點）
1. 載入 3635，`Fit to content`。
2. 在多 pad 密集區「按住左鍵直接拉框」連續 10 次。
3. 同時觀察右側 inspector 是否在多選時阻塞。

預期：
1. 框選起手不應被明顯卡住。
2. 多選期間 inspector 以輕量模式處理（不做單 pad 即時計算）。
3. 放開後仍能得到正確選取結果與後續可用操作。

## 4. Log 驗證（效能）
檢查 `build/logs/app.log` 的 selection debug：

1. 關鍵字：`Selection updated:`
2. 欄位應包含：`summary=...ms, inspector=...ms, notchPreview=...ms, total=...ms`
3. 對比改版前後，`inspector` 與 `total` 不應在常見框選流程中長時間卡高。

## 5. 補充說明
1. 本輪只做 `H8/E1 Phase A`：以 guard 與 deferred 位置調整降延遲，不做演算法大改。
2. 若仍有極端場景卡頓，下一輪進 `Phase B`（selection event debounce + background snapshot pipeline）。
