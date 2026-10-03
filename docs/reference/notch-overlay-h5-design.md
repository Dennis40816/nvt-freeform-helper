# H5 Overlay 清晰化整併設計
最後更新：2026-02-22  
狀態：Phase 0~4 已完成（policy + UI 開關 + token 收斂 + 測試/文件同步）

## 1. 背景
- `H3` 已完成：`ToRegular` 標籤與 `ToFull` overlay 可分離顯示。
- `H4 Phase 1` 已完成：Step3 參數變更時可即時重算 AA preview。
- 下一階段 `H5` 目標是把 Step3 overlay 的「語意、可見性、顏色規則」收斂成單一模型，避免使用者與實作都混淆。

## 2. 目前痛點（待整併）
1. 同時存在「計算開關」與「顯示開關」，但命名/文案不完全對齊。  
   `EnableToFull`（計算） vs `ShowNotchCanvasPreview`（顯示）易被誤解為同一件事。
2. AA quick toolbar 只有 `To Full` 顯示開關，缺少 `To Regular label` 顯示開關。
3. Step3 legend 為靜態說明，沒有清楚反映當下實際啟用的 overlay layer。
4. seed/candidate/final 的顏色與 alpha 規則散在 rendering code，部分仍靠 fallback 色票，不夠可控。
5. `H6`（分步可視化 + autoplay）需要建立在清楚的 layer model 之上，否則行為會繼續疊加複雜度。

## 3. H5 目標
1. 以單一 overlay 模型定義 Step3 所有視覺層。
2. 將「計算可用性」與「顯示可見性」分層，並提供清楚 UI 文案。
3. 讓 stage 顯示規則、顏色 token、繪製順序都可預測。
4. 保持專案快照相容，不破壞既有存檔欄位。

## 4. 非目標
- 不在 H5 變更 To Full 幾何演算法。
- 不在 H5 變更 Step5 export 公式與欄位。
- 不在 H5 完成 autoplay 行為重設計（放在 `H6`）。

## 5. Overlay 模型（統一語意）
以 layer 為單位管理顯示，固定四層：

| Layer | 語意 | 資料來源 | 受 Stage 影響 |
| --- | --- | --- | --- |
| `ToRegularLabel` | 每顆 CAD 的 `To Regular ratio` 文字 | `NotchCanvasPreviewItem.ToRegularRatio` | 否 |
| `ToFullSeed` | Stage1 seed overlap | `ToFullSeedPolygons` | 是（=1） |
| `ToFullCandidate` | Stage2 candidate regular | `ToFullCandidatePolygons` | 是（=2） |
| `ToFullFinal` | Stage3 final outline/fill | `ToFullFinalOutlinePolygons` / `ToFullPolygons` | 是（=3） |

## 6. 可見性規則（單一入口）
輸入狀態：
- `HasPreviewData`：`NotchCanvasPreviewItems.Count > 0`
- `EnableToFull`：Step3 計算開關
- `ShowToFullOverlay`：顯示開關（沿用 `ShowNotchCanvasPreview`）
- `ShowToRegularLabels`：新增顯示開關（預設 `true`）
- `PreviewStage`：1~3

規則：
1. `ToRegularLabel` 顯示條件：`HasPreviewData && ShowToRegularLabels`
2. `ToFull*` 顯示總條件：`HasPreviewData && EnableToFull && ShowToFullOverlay`
3. `ToFullSeed`：總條件且 `PreviewStage == 1`
4. `ToFullCandidate`：總條件且 `PreviewStage == 2`
5. `ToFullFinal`：總條件且 `PreviewStage == 3`

建議實作：
- 新增 policy（例如 `NotchOverlayVisibilityPolicy`）集中計算上述結果。
- ViewModel 與 PadCanvas 不直接分散 `if` 判斷，統一透過 policy 輸出。

## 7. UI/文案設計
1. Step3（Right panel + SettingsWindow）明確區分：
   - 計算：`To Regular EN (2.2)`、`To Full EN (2.2)`
   - 顯示：`Show To Regular labels`、`Show To Full overlay`
2. Canvas quick toolbar 顯示兩個顯示開關：
   - `To Regular`
   - `To Full`
3. Stage legend 改為「Layer legend」，保持 stage 語意但增加當下 active/inactive 狀態提示。
4. tooltip 固定句型：  
   `Compute switch affects ratio/result.` / `Display switch affects AA overlay only.`

## 8. Token 收斂方向
保留現有 token 命名，補齊缺口，不用 inline 色值：
- 新增（或明確分離）`ColorNotchToFullSeedFill`
- 新增 overlay alpha/line-width token（seed/candidate/final 各自可調）
- `ToRegular label` 背景/邊框/字色沿用既有 `ColorCanvasNotchRatio*`，避免再分裂新命名

## 9. Phase 切分（H5）
### Phase 0（設計盤點）`[Done]`
- 輸出本文件，凍結 layer 模型與可見性規則。

### Phase 1（可見性 policy 收斂）`[Done]`
- 新增 overlay visibility policy + ViewModel 單一入口。
- `PadCanvas` 改以 policy 結果驅動四層顯示。

### Phase 2（UI 顯示開關整併）`[Done]`
- 加入 `Show To Regular labels` 設定（SettingsWindow + quick toolbar）。
- Step3 文案與 tooltip 同步調整，清楚分離計算 vs 顯示。

### Phase 3（色彩與繪製規則收斂）`[Done]`
- Seed/Candidate/Final 色票與 alpha 全改由 token 驅動。
- 確認 draw order：`seed -> candidate -> labels -> final(top)`。

### Phase 4（測試與文件）`[Done]`
- 測試：
  - visibility policy unit tests（完整 state matrix）
  - ViewModel 行為測試（設定切換即時生效）
  - UI guard（Step3/SettingsWindow 必要欄位存在）
- 文件：
  - `docs/core/notch-2.2-spec.md`（overlay 章節）
  - `docs/reference/behavior-inventory.md`（Step3 顯示開關 side-effect）
  - `TODO.md` 進度同步

## 10. 驗收標準
1. 使用者可明確控制 `To Regular` 與 `To Full` 顯示，不互相影響。
2. `EnableToFull=false` 時，`ToRegular` 仍可獨立顯示。
3. Stage 切換只影響 To Full 三層，不影響 To Regular label。
4. 不新增 inline 顏色/尺寸；所有變更使用 token。
5. `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release` 通過。

## 11. 風險與回退
- 風險：命名調整可能影響現有綁定與快照欄位。
- 控制：先保留 `ShowNotchCanvasPreview` 作為相容欄位，新增欄位採向後相容預設值。
- 回退：若 UI 文案變更引發混淆，可先保留舊文案 tooltip，再於 H6 做最終命名收斂。
