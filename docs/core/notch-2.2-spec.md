# Notch 2.2 Spec（implementation-calibrated）

> Canonical reference：[`docs/reference/notch-system-reference.md`](../reference/notch-system-reference.md)
> 本檔保留 v2.2 數學 / UI / 歷史設計背景；若描述目前實作行為，以 canonical reference 為準。

最後更新：2026-08-08

本文件整理 Notch 2.2 的目標、數學定義、UI/輸出要求、與現行 2.1 流程差異，作為後續開發基準。

## 1. 目標
- 以 `CadAllocation` 作為 Notch 2.2 主骨架（Post Algo 階段執行）。
- 修正 2.1 中 `To Regular / To Full` 的語義不一致問題。
- 將異形補償從「長度近似」改為可直接使用「面積比例」。
- 保留 2.1 相容路徑；目前 `NotchSettings` 預設同時啟用 V2.1 與 V2.2，設計真值仍以 V2.2-oriented canonical row 為主。

## 2. 與 Notch 2.1 差異（重點）
- 2.1 的 `[1]` 是 `cad area / 單一 anchor regular area`，`[2]` 是 `cad bbox area / 單一 anchor regular area`。
- 2.2 將補償拆成三段：
1. `Undo NF (To Regular)`
2. `To Full`
3. `X/Y Freeform 切割並產生側向參數`
- 2.2 的 To Full 實作更新為「polygon-overlap seed + 邊界 regular 擴展」：
  - seed 以 `CAD polygon ∩ regular` 的實際重疊作為起點（不是 bbox-only seed）。
  - 擴展仍受邊界/碰撞限制，不代表最終補償直接採 bbox 面積。

## 3. Notch 2.2 流程（Post Algo）
1. `CAD -> Regular` 分配（沿用 CadAllocation）
2. `Undo NF (To Regular)`：先還原 NF 對感應量的壓縮/放大效應
3. `To Full`：在不碰撞已完成 pad 的前提下，朝 regular 外邊界最大化延展
4. `X Freeform / Y Freeform` 依面積比例切割，產生原 4~9 欄位語義對應
5. 依 per-target coverage 組裝 7-field `NotchV22Node`；CAD-level ToRegular/ToFull/Combined 只保留診斷用途，不直接當 final payload。

## 4. Undo NF（To Regular）定義
### 4.1 物理意義
- NF 可能將不同尺寸 sensor 正規化到相近量級，導致實際幾何面積對 diff 的影響被扭曲。
- Undo NF 的目標是先把這種扭曲還原，使 diff 與幾何面積更接近正比。

### 4.2 數學定義（多 regular pad 正確做法）
- 設 CAD pad 為 `C`，重疊到多個 regular pad `R_i`。
- 定義 `A_i = Area(C ∩ R_i)`。
- 建議 Undo NF 比例：

`UndoNFRatio(C) = Σ_i (A_i / Area(R_i))`

- 這種「分區後加總」可成立，因為在 regular 不重疊的前提下，`C ∩ R_i` 是可加總分割。
- 不可使用 `Σ_i (Area(C)/Area(R_i))`，該式會重複計入同一 CAD 面積，數學上不正確。

### 4.3 與 2.1 To Regular 差異
- 2.1 只看單一 anchor regular。
- 2.2 使用 CAD 對多 regular 的交集分配，對跨格/跨不等尺寸 regular 的情境更穩定。

## 5. To Full 定義
### 5.1 物理意義
- 目標是讓邊界異形 pad 盡量延展到 regular 最外邊界，模擬邊界缺失 pad 的補償。
- 內部緊密排列 pad 理論上不應明顯擴張。

### 5.2 幾何/流程約束
- 延展目標只能是 regular pad 外邊界（outer boundary）。
- 延展過程不得與「已完成 To Full 的 pad」發生碰撞。
- 若被鄰近 pad 阻擋，允許部分延展或不延展（保留物理可行性）。
- **中間 CAD pad（未接觸 panel 外邊界 regular）不做 To Full 擴展**，理論上 `ToFullRatio = 1.0`。
- 僅對「有 overlap 的 boundary regular」啟用 To Full；未 overlap 的 regular 不參與。

### 5.3 UI 與可視化要求
- To Full 提供右側 Panel 開關。
- 原始 CAD 維持藍色表現。
- To Full 結果使用橘色外框疊加（僅外框，不與藍色填色競爭）。
- 建議在 Notch Inspector 顯示阻擋來源（哪個 pad 造成停止延展）。
- Step3 顯示開關分離為：
  - `Show To Regular labels`（僅控制 To Regular 標籤顯示）
  - `Show To Full overlay`（僅控制 To Full seed/candidate/final 顯示）
- Step3 可見性規則由單一 policy 計算（`ToRegularLabel / ToFullSeed / ToFullCandidate / ToFullFinal`）。
- Step3 計算 gate 路徑可切換：
  - `To Full rule engine EN`：使用 `NotchToFullRuleEngine`。
  - 關閉時改走 legacy inline gate（規則碼維持相容）。
- `To Full rule trace EN` 開啟時，保留 per-regular rule trace，並由 runtime query（`query notch` / `query multi-owner`）回傳 `ruleTrace` 明細。
- AA 區 staged preview（Step3）：
  1. `Seed`（CAD polygon overlap source）
  2. `Boundary candidates`（可擴展的 boundary regular）
  3. `Final To Full`（擴展後結果）
- Stage preview 顯示契約：
  - `Stage 1` 只顯示 `Seed`
  - `Stage 2` 只顯示 `Boundary candidates`
  - `Stage 3` 只顯示 `Final To Full`
  - 不採累積疊加顯示，避免把前一層誤認成 final 幾何
- Seed/Candidate/Final 色票與線寬由各自 token 控制，避免 cross-fallback 混用。

### 5.4 為何「有 To Full」但比例仍顯示 100%
- `ToFullRatio` 定義為：
  - `ToFullRatio = max(1.0, Stage3Area / CadArea)`。
  - 其中 `Stage3Area = Area(Union(CAD polygon, Stage2 applied-regular area, ToFull reachable area))`。
- 因此只要 Stage3 與原始 CAD 面積幾乎相同（或增量極小），最後就會是 `1.0x`（顯示 100%）。
- 常見情境：
1. 該 CAD 雖在邊界，但有效擴張方向被鄰近 CAD 阻擋（`BLOCKED_BY_NEIGHBOR`）。
2. 擴張增量小於顯示精度（例如 UI 只顯示 1 位或 2 位小數時會看起來是 100%）。
3. 幾何上已接近 full regular（可達面積與來源面積差極小）。
4. `To Full enabled` 應以「至少一格實際 apply（`IsToFullApplied=true`）」判定；僅 boundary candidate 不算生效。

### 5.5 邊界阻擋規則（以 CAD 4767 / 4775 為例）
- 規則目標（strict）：
1. To Full 的目標只能是 regular outer boundary。
2. 若某方向已有其他 CAD 存在，該方向不可擴展，不可穿越/覆蓋鄰 pad。
3. Stage2 應僅顯示「仍有可擴展方向」的 regular；若四向皆被阻擋，不應出現在 Stage2。
4. Stage3 必須至少完整包含原始 CAD 幾何，且再加上允許擴展區；不可把原始 CAD 面積裁掉。
- 對應案例要求：
1. `CAD 4775` 左側有 `CAD 4767`，因此 `REG 4793` 不應被視為可擴展候選。
2. `CAD 4767` 佔到 `REG 4793` 的部分同樣不可判定為可 To Full（受 `CAD 4775` 阻擋）。
3. Stage1 只標示「最終會被 To Full 使用的 regular」上的 CAD overlap 區域；不應把最終不會 apply 的 boundary overlap 一起框出。
4. Stage3 要以 `CAD 原始輪廓 ∪ Stage2 可擴展區` 做最終輪廓，且結果必須是閉合外輪廓。

## 6. 切割（X/Y Freeform）
- 依 X/Y 方向面積比例分配，生成原 4~9 欄位對應行為。
- `XY Freeform` 納入下一正式版 TODO（本版先不實作）。

## 7. 參數與輸出格式（2.2）
### 7.1 App/診斷層（可分開觀察）
- `ToRegularRatio`（Undo NF）
- `ToFullRatio`
- `CombinedRatio = ToRegularRatio * ToFullRatio`（CAD-level diagnostic only）
- 上述三值可用於 UI、trace 與相容報告；不能把 source-wide `CombinedRatio` 平均分配或直接寫入每個 target 的 Firmware payload。

### 7.2 Per-target coverage（目前 canonical 計算）

對每個 `(IC, target diff)`，先以 regular 為單位計算 effective coverage，再分組相加：

```text
coverage(ic,diff) = Σ_regular effectiveArea(regular) / regularArea(regular)
```

- `ConservativeNoGain`：`effectiveArea = SourceArea`，即每格 CAD overlap coverage。
- `CurrentGain`：只有 `IsToFullApplied=true` 的 regular 使用 `max(SourceArea, Stage3EffectiveArea)`；其餘仍使用 `SourceArea`。
- `Disabled`／legacy baseline：維持 source-area-dominant allocation，不宣稱 target-regular coverage。
- eligibility：final legs 只包含 anchor IC、非 anchor/source diff，且通過 strict threshold 或有 ToFull applied coverage 的 target group；不是所有 overlap target 都必然進 C。
- rounding：每個 target group 先 `Math.Round(ratio * 100)`；絕對值超過 100 時拆成多個 `<=100` chunks，再組成 continuation rows。
- `CombinePercent`：retained anchor group 加上實際 emitted 的正向 target legs；是 final row assembly 值，不等於 CAD-level `ToRegularRatio * ToFullRatio`。

### 7.3 2.2 final Notch Table（7 fields）

Typed payload 為 `NotchV22Node`：

1. `AnchorDiffIndex`
2. `CombinePercent`（`0..255`，`100` = 不縮放）
3. `TargetDiffIndex1`（null sentinel = none）
4. `TargetRatioPercent1`（`-100..100` signed percent）
5. `TargetDiffIndex2`
6. `TargetRatioPercent2`
7. `Flags`（含 continuation）

- continuation 規則：
  - 若單列超過 2 個 target，拆為多列 continuation。
  - 首列套用真實 `CombinePercent`，續列固定 `100`（只做轉移腿）。
- Export gate：
  - final raw `CombinePercent > 255` 視為非法，Export 必須中止並在 UI 明示錯誤；不可先 clamp 來掩蓋 overflow。

## 8. 2.1 相容規則
- `LenScale` 視為 2.1 legacy 參數。
- 2.1 leg ratio 改為直接輸出 `UINT8 0..255` Q7 magnitude（`128=100%`、`255≈199%`），sign 只由 ADD/SUB type 承載，不再以 `LenScale` 對外量化。
- encode 使用 AwayFromZero；final projector 在 ABI 邊界飽和，firmware apply 使用 `(INT16 source * magnitudeQ7) >> 7`。
- 2.1 `ThresholdQ7` 是獨立的 `0..128` admission gate，不與 payload range 合併。
- UI 不再顯示 `LenScale`。
- 2.2 主流程可直接用面積比例，不依賴 `LenScale`。

## 9. 目前系統的 match / overlap 演算法（實作說明）
### 9.1 CAD↔Regular match（`PadMatcher`）
檔案：`src/FreeformHelper.Application/Services/PadMatcher.cs`
- 以 CAD bbox 對 grid edge 做候選 row/col 範圍裁切（降低比對數）。
- 對候選 cell 計算 `Polygon ∩ Rect` 交集面積。
- 生成雙向多對多關聯：
  - `CadToRegular`
  - `RegularToCad`
- 同時保留每個 regular 的 best match（相容既有流程）。

### 9.2 DXF overlap 偵測（`DxfOverlapAnalyzer`）
檔案：`src/FreeformHelper.Application/Services/DxfOverlapAnalyzer.cs`
- 先用 polygon signature 做 duplicate 分群（同層/跨層分開統計）。
- 再做 overlap 候選配對：
  - 小資料量：全配對
  - 大資料量：spatial buckets 加速
- 窄相位以 polygon overlap 判斷「真正重疊」；邊貼邊/點接觸不算重疊。

## 10. 預設行為（目前決議）
- 預設 enabled outputs：`v2.1 + v2.2`；V2.2-oriented canonical row 是設計真值，V2.1 由 final compatibility projector 產生。
- 預設計算模式：`CadAllocation`（Notch 2.2 baseline）。
- `v2.1` 為 legacy fallback。

## 11. 效能優化空間（開發備忘）
- 交集面積快取：match/UndoNF/切割共用，同筆資料避免重算。
- To Full 的碰撞檢查導入空間索引（grid bucket / spatial hash）。
- 增量重算：只重算受設定變更影響的 CAD 集合。
- TH 變更時避免全流程重跑（優先做輸出層過濾）。
- Inspector/可視化改局部 AOI 渲染，降低大場景重繪成本。

## 12. 待確認
- To Full 的 pad 處理順序（diff idx / 邊界距離 / 面積優先）最終規則。
- 2.2 後續若增加欄位或調整 ABI，必須另立產品行為變更；現行 leg 契約固定為 `INT8 -100..100` signed percent。
- `XY Freeform` 進入正式版的時程與輸出欄位定義。

## 13. Diff idx 編號機制（Step 3）
- 目的：處理實際量測檔中 `Diff idx` 可能跳號、起始不固定、`Diff 0` 不一定存在的情境。
- 關鍵原則：`Diff idx` 以 **每顆 IC 獨立編號**（IC local），不可跨 IC 混編。
- 自動編號來源：`CAD scan order`。
- 可調參數：
1. `CAD Output FW Diff start`：起始編號值（例如從 1 開始）。
2. `CAD Output FW Diff anchor CAD id`：自動編號起點位置（-1 代表 scan-order 第一顆）。
3. 每顆 CAD 的 `CAD Output FW Diff override`：手動指定單點編號（可跳號）。
- 優先序（固定）：`override > auto(anchor + scan order)`，且衝突判定僅在同一 IC 內生效。
- 後續續編規則：
  - override 先佔用編號；
  - 其餘 CAD 從 `start` 起算，自動跳過已佔用值後遞增。
- 可視化建議（已實作方向）：
  - Overlay 可開關；
  - anchor 與 override 使用不同外框顏色；
  - Pad Info 明確顯示來源（Auto / Auto(anchor) / Manual override）。

## 14. 階段進度（2026-02-22）
- Phase-1 已落地：
  - `Undo NF (To Regular)` 公式已接入 2.2 路徑。
  - `To Full` 已提供右側開關與 ratio 欄位。
  - To Full 核心規則已更新：僅 boundary regular 可擴展，center regular 不擴展。
  - Step3 已支援 AA staged preview（seed / candidate / final）。
  - Notch detail 可分開顯示 `ToRegular / ToFull / Combined`。
  - Step 間失效鏈已落地（前步驟變更可清除後步驟結果），且每步驟提供 clear action。
  - Step3 staged preview 已支援 autoplay 與 stage legend。
  - To Full final overlay 已改為最上層視覺，並支援半透明填色避免被底圖遮蔽。
  - `H5` Overlay 清晰化整併已完成（policy + UI display 開關 + token 收斂）。
  - `H6` 分步可視化已補齊：Step3 顯示 current stage summary、layer active/hidden 狀態與 autoplay summary。
  - `H7` Right Panel 已分頁化：`Settings / Inspector` 分離顯示，降低同欄位重複密度。
  - `R4` Step5 驗證面板已收斂為 `DIRECT / IN / OUT` 分組顯示（沿用 `NotchValidationTraceService`）。
  - `Show To Regular labels` 已接入 quick toolbar / SettingsWindow / project snapshot / app-general white-list。
  - Runtime CLI 已可查單顆 CAD 的 `query notch`，且加入 revision-based cache（`step3Revision + cadId`）避免重複重算。
  - Runtime CLI 新增 `query notch-stage --cad-id`，可直接取 stage1/2/3 overlay polygon 供畫面對照。
  - Work Area 上方 quick-view 的 `Why` 改為線性連結展開（popup），避免主畫面被長文資訊擠壓。
- Phase-2 待做：
  - To Full 幾何擴張 + 已完成 pad 碰撞約束 + outer boundary 連通限制。
  - 以 Rule pipeline 管理 Notch/Freeform 規則依賴與自動重算（DAG + dirty propagation）。
  - Pad 檢視的 presentation snapshot 已共用於 Popover、Runtime CLI 與右側 Inspector；deferred Inspector 仍會另算 compensation，完整 computation single-result 由 R13.102/102a 收斂。
  - UI 呈現分層：已完成第一階段（Top Strip 摘要 + Why、右側 Inspector 同源明細）；後續僅剩資訊密度/版位微調。
  - To Full 邊界阻擋嚴格化第一版已落地（boundary 連通 + multi-owner 禁止擴展 + Stage3 實際區域計算）；後續保留案例細修與幾何視覺微調。
  - `To Full enabled` 已以「至少一格 `IsToFullApplied=true`」判斷，避免 enabled/100% 的語意混淆。

## 15. 本輪確認目標（開始實作前凍結）
### 15.1 Rule Pipeline（自動化依賴重算）
- 導入 step/rule pipeline，支援：
1. 上游參數變更時，下游 step 自動標記 dirty。
2. dirty step 自動重算（可取消、可節流），避免「開關已改、結果仍舊」。
3. 明確 step 依賴圖：`Match -> Freeform -> Diff idx -> ToRegular/ToFull -> Export`。

### 15.2 Pad Inspector 共用 presentation snapshot
- `PadInspectorSnapshot` 是下列 presentation readers 的共用投影：
1. 左鍵 Popover
2. 右側明細區（資訊視圖）
3. Runtime CLI (`query pad`)
- 這只證明顯示資料收斂，不代表 repository-wide compensation computation 已是唯一入口。R13.102a-1／R13.102b-1 已讓 Step3 preview、deferred Inspector與normal Notch Detail command共用 revisioned per-CAD resolved result；export generator與其餘reader的per-CAD/per-IC batch仍是 R13.102/102a debt。
- 狀態：
  - 已完成：Popover（單選）與 `query pad` 改讀同一份 snapshot；`RuleTrace` 同步顯示於 Popover 與 CLI。
  - 已完成：ViewModel 新增 `CurrentPadInspectorSnapshot`（供右側/上方資訊層接入）。
  - 已完成：右側明細區直接綁定 snapshot、取代分散的 presentation 欄位來源。

### 15.3 UI 資訊分層（避免重複與混亂）
- 右側 Panel 以「設定」為主，不承載重複資訊。
- 在 Work Area 上方新增資訊列（Top Strip）顯示：
1. `ICx/diffY`
2. Diff 來源（Strict/Override/Probable）
3. Confidence
4. `ToRegular/ToFull`
5. `Why`（展開 rule trace）
- Popover 僅保留最小必要資訊與跳轉操作。
- 狀態：
  - 已完成第一階段：Top Strip 可顯示目前選中 pad 的主鍵摘要、來源/Match、補償摘要，並可展開 Why(rule trace)。
  - 已完成：右側明細視圖改綁同一份 snapshot（移除重複資訊來源）。

### 15.4 Diff idx 顯示模式（僅 UI 顯示層）
- `Strict`：只顯示 override + strict unique match；無法決定者顯示 `-`。
- `Hybrid`：先 strict；缺值再以 most-likely 補齊，並標記 `P`。
- `MostLikely`：全部以最佳候選顯示，衝突/低信心以標記提示。
- `Export` 仍以 `Strict` 為準，不受 UI 顯示模式影響。

## 16. 相關規格與狀態同步
- Runtime CLI Phase-A 已落地（`query status/selection/terminal/pad`）：
  - 詳見：`docs/reference/runtime-cli-plan.md`
- Notch/Freeform 後續規則化與同源化，以本文件第 15 節為準。
