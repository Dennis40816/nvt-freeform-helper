# Freeform Helper 主演算法說明（最重要）
最後更新：2026-02-23

## 0. 文件目的與範圍
這份文件描述 Freeform Helper 的主流程演算法，重點是：
- Step1~Step5 每一步「實際怎麼算」
- 每一步的輸入/輸出資料
- 主要公式與判斷條件
- 對應程式位置（類別/方法/檔案）

這是接手、除錯、做效能優化時的第一份文件。

## 1. 核心資料模型
主要資料結構：
- `CadPad`：DXF 匯入後的 CAD pad（多邊形）
  - `src/FreeformHelper.Domain/Pads/CadPad.cs`
- `RegularPad`：規則網格 pad（矩形 cell）
  - `src/FreeformHelper.Domain/Pads/RegularPad.cs`
- `RegularGrid`：含 `Rows/Cols`、`XEdges/YEdges`、`Pads`
  - `src/FreeformHelper.Domain/Pads/RegularGrid.cs`
- `PadMatchResult`：Step1 的 CAD↔Regular overlap links
  - `src/FreeformHelper.Application/Services/PadMatchResult.cs`
- `NotchTable` / `NotchTableRow`：Step5 匯出資料表
  - `src/FreeformHelper.Domain/Notch/NotchTable.cs`
  - `src/FreeformHelper.Domain/Notch/NotchTableRow.cs`

幾何基礎：
- 面積與重心：`Polygon2.Area()`、`Polygon2.Centroid()`
- 多邊形與矩形交集面積：`Polygon2.IntersectionAreaWithRect(...)`
- 對應檔案：`src/FreeformHelper.Domain/Geometry/Polygon2.cs`

## 2. 端到端流程總覽
UI 主流程為 5 步：
1. Step1 幾何匹配（CAD↔Regular overlap）
2. Step2 Freeform 標記（XWay/YWay/XYWay）
3. Step3 Notch 2.2 補償與預覽（ToRegular/ToFull + Stage1/2/3）
4. Step4 Mapping diagnostics（DXF idx ↔ regular one-to-one 建議與警示）
5. Step5 Notch export（產生 Notch table 並匯出）

流程編排策略：
- 下一步自動導向：Step1->2->3->4->5
- 上游變更時，下游結果失效清除
- 對應檔案：`src/FreeformHelper.UI/Services/WorkflowPipelineService.cs`
- ViewModel 入口：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.WorkflowSteps.cs`

## 3. Step1 幾何匹配（Geometry Match）
主要入口：
- UI：`MatchAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.MatchWorkflow.cs`
- Application：`PadMatcher.Match(...)`
  - `src/FreeformHelper.Application/Services/PadMatcher.cs`

### 3.1 核心想法
不是全域 O(N*M) 暴力比對，而是先用 `XEdges/YEdges` 做 candidate 範圍裁切，再做幾何交集。

### 3.2 候選範圍（空間裁切）
對每個 CAD pad：
- 用 `GetCandidateRange(bounds, grid)` 找 row/col 範圍
- `FindCellIndex(...)` 以 binary search 找邊界落在哪個 cell
- 只掃該矩形區域內的 regular cells

對應方法：
- `GetCandidateRange(...)`
- `FindCellIndex(...)`
- `src/FreeformHelper.Application/Services/PadMatcher.cs`

### 3.3 overlap 計算與過濾
對每個 candidate regular：
- 先過 AABB：`cad.Bounds.Intersects(regular.Bounds)`
- 再算真交集面積：
  - `overlapArea = Polygon2.IntersectionAreaWithRect(cad.Polygon, regular.Bounds)`
- 過濾噪音：
  - `overlapFloor = max(1e-6, regular.Area * 0.00001)`
  - 只有 `overlapArea > overlapFloor` 才保留 link

### 3.4 Link 指標
每個 link 儲存：
- `OverlapArea`
- `RegularCoverage = overlapArea / regularArea`
- `CadCoverage = overlapArea / cadArea`

排序規則（高到低）：
1. `RegularCoverage`
2. `OverlapArea`
3. `CadCoverage`

每個 regular 最終仍維持單一最佳匹配（相容舊流程）：
- `regular.MatchedCadPadId = best.CadPadId`
- `regular.MatchScore = best.RegularCoverage`

### 3.5 Step1 輸出
- `CadToRegular`：一顆 CAD 對應多個 regular links
- `RegularToCad`：一顆 regular 對應多個 CAD links
- `Telemetry`：
  - candidate cell visits
  - bounds intersections
  - polygon intersections
  - avg / p95 candidate per CAD

這些 telemetry 會在 UI log 成 `PERF PADMATCH`。

## 4. Step2 Freeform 標記（Freeform Tagging）
主要入口：
- UI：`AutoDetectFreeformsAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.MatchWorkflow.cs`
- UseCase：`FreeformTaggingUseCase.AutoDetect(...)`
  - `src/FreeformHelper.UI/Services/FreeformTaggingUseCase.cs`
- Application：`FreeformDetector.AutoTagFreeforms(...)`
  - `src/FreeformHelper.Application/Services/FreeformDetector.cs`

### 4.1 overlap-link 版本（主路徑）
若 Step1 有 `PadMatchResult`，會用 coverage 分佈判斷 freeform 方向。

對每顆 CAD：
- 蒐集其 links（`CadCoverage > 1e-6`）
- 若 links < 2，跳過
- 計算
  - `totalCoverage = sum(link.CadCoverage)`
  - `rowCoverage[row] = sum(row links)`
  - `colCoverage[col] = sum(col links)`
  - `rowDominance = max(rowCoverage) / totalCoverage`
  - `colDominance = max(colCoverage) / totalCoverage`
  - `xSpread = 1 - colDominance`
  - `ySpread = 1 - rowDominance`

決策：
- `max(xSpread, ySpread) < threshold` -> `None`
- `enableXY && xSpread>=threshold && ySpread>=threshold` -> `XYWay`
- 否則 `xSpread>=ySpread ? XWay : YWay`

### 4.2 邊界特化（edge specialization）
若主判斷是 `None` 且開啟 `EnableFreeformEdgeSpecialization`：
- 找 coverage 次要且在 boundary 的 weak cells
- 避免「左右同時 + 上下同時」的微位移誤判
- 只保留單軸/雙軸明確偏移，給 `XWay/YWay/XYWay`

### 4.3 legacy fallback
若沒有 `PadMatchResult`，退回舊邏輯：
- 依 matched regular 的 row/col span 推 `xSpread/ySpread`
- 同樣用 threshold 判斷方向

## 5. Step3 Notch 2.2 補償與預覽
主要入口：
- 預覽刷新：`RefreshNotchCanvasPreview(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`
- 補償核心：`NotchV22CompensationService.Compute(...)`
  - `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
- Rule engine：`NotchToFullRuleEngine`
  - `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs`
- 最終外框：`NotchV22FinalOutlineService.Build(...)`
  - `src/FreeformHelper.Application/Services/NotchV22FinalOutlineService.cs`

### 5.1 To Regular 比率
先找所有與 CAD 真正重疊的 regular：
- 條件：AABB 相交 + `IntersectionAreaWithRect > epsilon`

定義：
- `rawToRegularRatio = sum(overlapArea / regularArea)`
- 若 `EnableToRegular = false`，輸出固定 `ToRegularRatio = 1.0`
- `rawToRegularRatio` 是 CAD-level 診斷值，表示該 CAD 在 regular view 中等價覆蓋多少 regular 面積。
- beta0.9 後，v2.2 row 不再把這個 CAD-level `R` 當 source-wide gain 乘到每個 target share；實際輸出改用 per-target regular coverage。

### 5.2 To Full 的 gate 資訊
每顆 overlapped regular 計算：
- `strictThreshold = max(eps, regularArea * strictOverlapRatio)`
- `hasSourceArea = overlapArea > strictThreshold`
- `ownerCadPads`：在同 regular 上、且 overlap 超過 strict threshold 的所有 CAD
- `hasDirectionalBlocker`：owner 中存在非自己 CAD
- `isBoundaryRegular`：有效邊界 regular（外框邊界或鄰居 inactive）
- `hasEffectiveExpansion = (regularArea - overlapArea) > (regularArea * 1e-9)`

### 5.3 reachability 與 rule engine
Step3 目前路徑重點是「規則 gate」：
- 若 boundary + 有 source + 有 expansion + 無 blocker，會走 `ExpandToRegularBounds(...)`
- `NotchToFullRuleEngine.Evaluate(...)` 產生規則碼與是否 apply

規則碼：
- `GATE_TOFULL_DISABLED`
- `GATE_NOT_BOUNDARY`
- `GATE_SOURCE_EMPTY`
- `GATE_MULTI_OWNER`
- `NO_EXPANSION_NEEDED`
- `EXPAND_CLEAR_PATH`

rule trace（可選）會記錄每條 gate 是否通過。

### 5.4 Stage1/2/3 視覺語意
當某 regular `IsToFullApplied = true`：
- Stage1（seed）：CAD 與該 regular 的真 overlap polygon
- Stage2（candidate）：整顆 regular 矩形 polygon
- Stage3（final）：`CAD + stage2 + toFullPolygons` 的 union 外框

UI 對應：
- Stage layer 切換與 auto-play
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`

### 5.5 To Full 比率與 Combined
`RegularDebugInfos` 產生後，Notch 2.2 的最終數值結果固定走單一路徑：
- `Stage3Area = cadArea + Σ(max(0, ReachableArea - OverlapArea))`
  - 僅統計 `IsToFullApplied = true` 的 regular
  - `cadArea` 已包含原始 overlap，因此只額外累加 expansion delta
- `ToFullRatio = max(1.0, Stage3Area / cadArea)`

CAD-level diagnostic：
- `CombinedRatio = ToRegularRatio * ToFullRatio`

v2.2 row payload：
- `Current (Gain)`：`CombinePercent = Σ(stage3EffectiveAreaOnTarget / targetRegularArea)`。
- `Conservative (No Gain)`：`CombinePercent = Σ(overlapAreaOnTarget / targetRegularArea)`。
- target leg percent 也直接使用相同的 per-target coverage。
- 這個設計避免小邊緣 regular pad 被其他 target 的 `R` 一起放大；例如同一 CAD 覆蓋兩個 target `98.6% + 58.8%` 時，兩條 target coverage 應分別維持 `98.6%` 與 `58.8%`。

注意：
- Stage overlay / final outline 是顯示投影，不可再反向成為 `ToFullRatio` 或 `Stage3Area` 的 business truth。
- UI / RuntimeQuery / Inspector / export 皆應讀同一份 compensation/resolved result，不可各自二次推導。

### 5.6 Step3 快取（關鍵）
為避免重複重算：
- cache key 包含：
  - cadId
  - revision
  - cadCount
  - activeRegularHash
  - `EnableToRegular/EnableToFull/RuleEngine/RuleTrace`
  - strict overlap ratio
- 對應：
  - `BuildCadV22CompensationCached(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`

## 6. Step4 Mapping Diagnostics（DXF idx ↔ Regular）
主要入口：
- UI：`AnalyzeIndexMappingAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.IndexMapping.cs`
- UseCase：`DxfRegularMappingUseCase.Analyze(...)`
  - `src/FreeformHelper.UI/Services/DxfRegularMappingUseCase.cs`
- Analyzer：`DxfRegularMappingAnalyzer.Analyze(...)`
  - `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs`

### 6.1 候選與分數
每顆 CAD 對候選 regular 計分：
- 候選範圍：`GetCandidateRange(...)`（含 `CandidatePaddingCells`）
- 分數：
  - `iou = inter / union`
  - `distScore = 1 - clamp(distance(cadCentroid, regCentroid)/diag(reg), 0, 1)`
  - `areaRatio = min(cadArea, regArea) / max(cadArea, regArea)`
  - `score = (wIou*iou + wDist*distScore + wArea*areaRatio) / (wIou+wDist+wArea)`
  - 最後 clamp 到 `[0,1]`

### 6.2 one-to-one 指派
策略是 greedy matching：
- 所有 `(cad, reg, score)` pair 依 score 由大到小排序
- 若 cad/reg 任一已被占用，跳過
- 否則配對

這是可解釋、穩定且快的策略，不是 Hungarian 全域最適化。

### 6.3 問題分類
會產生報告與 issue：
- `CountMismatch`
- `UnmappedCad`
- `UnmappedRegular`
- `LowConfidence`（`assignedScore < LowConfidenceThreshold`）
- `Ambiguous`（`best-second < AmbiguousMargin`）

並保留 top-K 候選方便人工覆核。

### 6.4 手動 override
`manualOverrides[cadId] = regularPadId` 會先鎖定對應，再做 greedy。

## 7. Step5 Notch Export
主要入口：
- UI export 流程：`ExportNotchAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs`
- 生成器：`NotchTableGenerator.Generate(...)`
  - `src/FreeformHelper.Application/Services/NotchTableGenerator.cs`
- facade：`NotchExportService`
  - `src/FreeformHelper.UI/Services/NotchExportService.cs`

### 7.1 兩種計算模式
由 `NotchSettings.ComputationMode` 決定：
- `LegacyRegularAnchor`
- `CadAllocation`（預設）
  - `src/FreeformHelper.Application/Settings/NotchSettings.cs`

### 7.2 CadAllocation 模式（主線）
流程：
1. 每顆 CAD 算 allocations（overlap/cadArea）
2. 選 anchor regular（優先 freeform 且 matchedCadId 一致）
3. 依 version gate + strategy `CanHandle` 決定是否出 row
4. v2.2 走 diff-centric candidate 聚合

### 7.3 gate threshold
- v2.1：`q7 = clamp(roundAwayFromZero(ratio*128), 0, 128)`，需 `q7 >= ThresholdQ7`。這是 admission gate，不是 `0..255` 的 V21 firmware leg payload。
- v2.2：
  - 若 `LinkVersionThresholds = true`：用 `ThresholdQ7 * 100 / 128`
  - 否則用 `ThresholdPercentV22`

### 7.4 v2.2 diff-centric row（目前主輸出）
先算每 CAD 的補償：
- `compensation = NotchV22CompensationService.Compute(...)`
- `toRegularPercent = round(ToRegularRatio*100)`
- `toFullPercent = round(ToFullRatio*100)`（無 boundary candidate 時固定 100）
- `toRegularPercent` / `toFullPercent` 保留在 comment/diagnostics 中
- `combinePercent` 由 per-target regular coverage sum 得出

再做 target allocation：
- `NotchV22TargetAllocationService.Build(...)`
- 以 `(IC, Diff)` 分組、過 strict threshold、排除 anchor diff
- 形成 legs（target diff + coverage percent）

輸出 row payload 為 `NotchV22Node`（7 ints）：
1. AnchorDiffIndex
2. CombinePercent
3. TargetDiffIndex1
4. TargetRatioPercent1
5. TargetDiffIndex2
6. TargetRatioPercent2
7. Flags（續行 row 會標 continuation）

型別：
- `src/FreeformHelper.Domain/Notch/NotchV22Node.cs`

### 7.5 舊版策略（相容）
- `V21NotchAlgorithm`：傳統 9 欄
  - `src/FreeformHelper.Application/Services/NotchAlgorithms/V21NotchAlgorithm.cs`
- `V22LegacyRowStrategy`：legacy 路徑下的 9 欄 v2.2 相容 row；不是 canonical V2.2 owner
  - `src/FreeformHelper.Application/Services/NotchAlgorithms/V22LegacyRowStrategy.cs`

已移除：
- legacy `v3.1 / V31` 半支援路徑已在 beta0.7 回收。Step5 正式輸出只保留 `v2.1 / v2.2`。

## 8. Workflow Invalidation 與一致性
核心規則：
- Step1 改變 -> 清 Step2~5
- Step2 改變 -> 清 Step3~5
- Step3 改變 -> 清 Step4~5
- Step4 改變 -> 清 Step5

對應：
- `WorkflowPipelineService.GetDownstreamStepsToInvalidate(...)`
- `FreeformHelperViewModel.WorkflowSteps.cs`

這個機制保證「上游變了，下游不會沿用過時結果」。

## 9. 為什麼有時候 Selection 很慢（重點說明）
`Selection updated ... inspector=xxxxms` 的主要成本通常是 Inspector 快照，不是畫布本身。

目前策略：
1. 先做 fast snapshot（不做冷啟動昂貴 Notch 細節）
2. 200ms 後啟動 deferred refresh，在背景算完整 Notch 細節
3. 背景完成後再回 UI thread 套用

對應：
- `UpdateInspectorSnapshotFromSelection(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection.cs`
- `QueueDeferredCadInspectorSnapshotRefresh(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs`
- `BuildCadPadInspectorSnapshot(... includeExpensiveNotchDetails)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.cs`

## 10. 主要類別對照表（快速索引）
| 流程 | 核心類別/方法 | 檔案 |
|---|---|---|
| Step1 Match | `PadMatcher.Match` / `MatchOverlap` | `src/FreeformHelper.Application/Services/PadMatcher.cs` |
| Step1 結果 | `PadMatchResult`, `PadMatchLink` | `src/FreeformHelper.Application/Services/PadMatchResult.cs` |
| Step2 Freeform | `FreeformDetector.AutoTagFreeforms` | `src/FreeformHelper.Application/Services/FreeformDetector.cs` |
| Step2 UseCase | `FreeformTaggingUseCase.AutoDetect` | `src/FreeformHelper.UI/Services/FreeformTaggingUseCase.cs` |
| Step3 補償 | `NotchV22CompensationService.Compute` | `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs` |
| Step3 Gate | `NotchToFullRuleEngine.Evaluate` | `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs` |
| Step3 Final Outline | `NotchV22FinalOutlineService.Build` | `src/FreeformHelper.Application/Services/NotchV22FinalOutlineService.cs` |
| Step3 UI | `RefreshNotchCanvasPreview` | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs` |
| Step4 Diagnostics | `DxfRegularMappingAnalyzer.Analyze` | `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs` |
| Step4 UI | `AnalyzeIndexMappingAsync` | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.IndexMapping.cs` |
| Step5 生成 | `NotchTableGenerator.Generate` | `src/FreeformHelper.Application/Services/NotchTableGenerator.cs` |
| Step5 匯出 | `NotchExportService` | `src/FreeformHelper.UI/Services/NotchExportService.cs` |
| Workflow 編排 | `WorkflowPipelineService` | `src/FreeformHelper.UI/Services/WorkflowPipelineService.cs` |

## 11. 建議閱讀順序（程式碼）
1. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Commands.cs`
2. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.MatchWorkflow.cs`
3. `src/FreeformHelper.Application/Services/PadMatcher.cs`
4. `src/FreeformHelper.Application/Services/FreeformDetector.cs`
5. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`
6. `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
7. `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs`
8. `src/FreeformHelper.Application/Services/NotchTableGenerator.cs`
9. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs`

