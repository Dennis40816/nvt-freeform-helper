# Notch 驗證資訊架構與欄位契約 Spec（Step 3/4/5/6 + Simulation + Export）
最後更新：2026-04-06
狀態：Draft（可進入 UI implementation）

## 1. 背景與問題
目前驗證資訊分散在多個 surface：
- Right panel Step 3 / Step 4 / Step 5 / Step 6
- `Analyze mapping` 獨立視窗
- `Export notch rows` 獨立視窗
- `Simulation` workspace

核心痛點不是「資料不足」，而是「同一批資料在不同 surface 的投影結構不同」：
- 同一個 CAD/row 的 decision、repair、notch 影響被拆到不同視圖，使用者需要切換畫面才能拼出完整脈絡。
- 低頻文字敘述直接顯示在主流程，造成資訊密度過高。
- 可編輯欄位與唯讀欄位混在一起，使用者不易判斷「這欄能不能改」「改了會影響哪裡」。

## 2. 目標與非目標
目標：
- 建立一份單一「欄位契約」，明確定義每個欄位的來源、可編輯性、改動 action 與下游影響。
- 驗證 UI 採「table + 右側 review panel」主模式；主畫面只保留高頻訊息，長描述移到 info icon/tooltip。
- 保持現有 single-path 計算契約，不新增平行重算路徑。

非目標：
- 本 spec 不修改 notch 核心演算法（v2.1/v2.2 math）。
- 本 spec 不重寫 Step 3 幾何預覽繪製引擎，只定義驗證資訊的投影方式。

## 3. Single Source of Truth 分層
- L0 幾何與 mask truth
  - `Step1` match candidates + `RegularVisibilityMaskService`。
- L1 diff assignment / decision truth
  - `DxfRegularMappingUseCase.Analyze(...)` + `DxfRegularMaskAuditService`。
- L2 notch table truth
  - `GenerateCurrentNotchTableAsync(...)` 產出的 `NotchTable`（`_lastGeneratedNotchTable` 為最近一次快照）。
- L3 validation trace truth
  - `NotchValidationUseCase.BuildBucket(...)` + `NotchValidationTraceService.BuildTrace(...)`（僅讀 L2，不重算 notch）。
- L4 simulation projection truth
  - `SimulationWorkspaceSession(Grid, Table, NullDiff, ActiveRegularPadIds, CadOutputFwDiffAssignmentDecisions)`。

## 4. 提議資訊架構（IA）
## 4.1 主結構
- 左側：`Verification table`（可排序/篩選/搜尋）
- 右側：`Review panel`（單筆詳情 + actions + before/after）
- 頂部：`Summary chips`（統計 + 狀態）
- 長文說明：只放在 `info icon`（hover 顯示）

## 4.2 視圖整併原則
- Step 4（decision/repair）與 Step 5/6（export/validation trace）用同一套 row identity：`IC + Diff + Row + CAD/REG`。
- Simulation 保留獨立 workspace，但 inspector 欄位名稱與 Verification table 一致。
- 不把演算法狀態直接塞成大段字串；改成結構化欄位 + reason badge。

## 4.3 右側 Step rail（節點 + 連線）契約
- 右側 panel 固定分兩層：
  - 上層：`Verification summary`（固定在最上方，sticky）
  - 下層：`Step rail`（垂直節點 + 連線，每個節點代表一個驗證階段）
- Step rail 的節點順序（不可跳號顯示）：
  - `Step 3`：Notch input / stage readiness
  - `Step 4`：Diff assignment / mapping decision
  - `Step 5`：Notch table row integrity
  - `Step 6`：Validation trace consistency
  - `Simulation`：Before/After/Delta sanity check
- 每個節點最少要有四個欄位：
  - `status`：`pass / warning / blocked / pending`
  - `what to verify`：一句話驗證目標（不可超過兩行）
  - `key checks`：2~4 個可勾選驗證點
  - `open action`：打開對應 detail 視圖（table filter / report / trace / simulation focus）
- 點擊節點的行為：
  - 左側 table 自動套用對應 filter（同一 row identity）
  - 右側 detail 區切換到該 step 的檢視卡
  - 不可觸發平行重算；只讀既有 projector/snapshot

## 4.4 Summary 區（置頂）契約
- Summary 固定顯示在右側最上方，內容只放高頻總結，不放長段落：
  - `current mode`（geometry-only / csv-constrained）
  - `overall status`（pass / warning / blocked）
  - `open issues count`（按 reasonCode 聚合）
  - `last table revision`（對應 `_lastGeneratedNotchTable` 版本/時間戳）
  - `active step`（目前使用者正在 review 的節點）
- Summary 只讀，不可編輯；所有改動都要透過下層 step action 觸發。

## 5. 欄位契約
## 5.1 Assignment / Decision 欄位（Step4、Simulation、Runtime 共用）
| 欄位 | 來源（single source） | 可編輯 | 可改動 action | 下游影響 |
| --- | --- | --- | --- | --- |
| `mode` (`geometry-only`/`csv-constrained`) | `DxfRegularMaskAuditService` decision | 否 | 無 | 影響 reason 判讀與 repair 規則 |
| `reasonCode` | `DxfRegularMaskAuditService` | 否 | 無 | 決定 UI badge 與排序優先度 |
| `decisionSource` (`seed/auto-repaired/manual-override/propagated-override`) | `DxfRegularMaskAuditService` + overrides state | 否（派生） | `SetCadOutputFwDiffIndexOverride(...)`、clear override | 影響是否可被 auto-repair 覆蓋 |
| `rawBestDiff` | mapping analyze result | 否 | 無 | 比對 seed 與修復差異 |
| `maskedBestDiff` | mapping + active mask | 否 | 無 | CSV constrained 可見候選 |
| `primaryAssignedDiff` | assignment decision | 間接可編輯 | `OffsetSelectedCadOutputFwDiffIndicesCommand`、`Apply*DiffOverrides*`、單筆 apply | 直接影響 notch table generation |
| `passiveCompensationDiff` | local repair/passive compensation signal | 否（目前） | 無 | 供 review，必要時進 manual apply |
| `repairSuggestion` | local repair score output | 否（建議值） | `ApplyCadOutputFwDiffOverridesCommand`、`ApplySegmentDiffOverridesCommand` | 套用後轉成 manual override |
| `confidence` | repair/decision score | 否 | 無 | 決定 auto-apply/needs-review |
| `detectedOffset` / `offsetSupportRatio` | segment offset detector | 否 | 無 | segment repair 入口與可解釋性 |

## 5.2 Notch Export Row 欄位（Step5 + Export 視窗）
| 欄位 | 來源（single source） | 可編輯 | 可改動 action | 下游影響 |
| --- | --- | --- | --- | --- |
| `rowNumber` | `NotchTable.Rows` index | 否 | 無 | trace 與 firmware 對照鍵 |
| `version` (`V21`/`V22`) | `NotchTableRow.Version` | 否 | 無 | 匯出 payload 格式 |
| `icIndex` / `diffIndex` | `NotchTableRow` | 否 | 無 | row grouping 與檢索 |
| `regularPadIndex` / `cadPadId` | `NotchTableRow` | 否 | 無 | mapping 溯源 |
| `payload(values/v22 node)` | `NotchTableRow.Values` + `V22Node` | 否 | 無 | firmware 實際運算輸入 |
| `status` (`linked`/`warning`/`noCad`/`legacy`) | row projector | 否 | 無 | UI 過濾與驗收焦點 |
| `isSelected`（匯出勾選） | `NotchExportSelectionViewModel` UI state | 是（UI only） | row/group select/unselect | 只影響當次匯出範圍，不改演算法 |

## 5.3 Validation Trace 欄位（Step6）
| 欄位 | 來源（single source） | 可編輯 | 可改動 action | 下游影響 |
| --- | --- | --- | --- | --- |
| `validationRegularPadId` | Step6 input state | 是 | `UseSelectedRegularForNotchValidationCommand`、手動輸入 | 決定 trace 查詢目標 |
| `direct/incoming/outgoing rows` | `NotchValidationTraceService.BuildTrace(...)` | 否 | `AnalyzeNotchValidationCommand`（重建） | 僅顯示，不回寫 notch |
| `trace counts` | trace result aggregation | 否 | Analyze/clear | 驗證摘要與排序 |

## 5.4 Simulation 驗證欄位
| 欄位 | 來源（single source） | 可編輯 | 可改動 action | 下游影響 |
| --- | --- | --- | --- | --- |
| `Before/After/Delta view` | `SimulationWorkspaceUseCase` snapshot + `SimulationWorkspaceSession.Table` | 是（view mode） | canvas view switch | 僅改顯示，不改 table |
| `color scale min/max` | 當前可見 cell distribution（同一 projection） | 否（自動） | 視圖/篩選切換觸發重投影 | 只影響顏色，不改值 |
| `selected regular impact list` | snapshot impact projector | 否 | row/cell selection | 幫助溯源 notch row |

## 6. Action -> 欄位影響矩陣
| Action | 寫入欄位 | 重新計算層級 | 影響範圍 |
| --- | --- | --- | --- |
| `ImportRegularVisibilityMaskCommand` | mask state（path/content/enabled candidate set） | L0 -> L1 | Step4/Simulation decision surface |
| `ClearRegularVisibilityMaskCommand` | mask state 清空 | L0 -> L1 | Step4/Simulation 回 geometry-only |
| `OffsetSelectedCadOutputFwDiffIndicesCommand` | manual override diff | L1 | 影響 L2 notch table |
| `ApplyCadOutputFwDiffOverridesCommand` | manual override diff（批次） | L1 | 影響 L2/L3/L4 |
| `ApplySegmentDiffOverridesCommand` | manual override diff（segment） | L1 | 影響 L2/L3/L4 |
| `AnalyzeIndexMappingCommand` | 無持久寫入（產生 report snapshot） | L1 read | 只更新 Step4 report 顯示 |
| `ExportNotchCommand` | `_lastGeneratedNotchTable` | L2 | 影響 Step5 summary、Step6 source、Simulation session source |
| `AnalyzeNotchValidationCommand` | validation trace UI state | L3 read | 更新 Step6 顯示 |
| `RefreshNotchCanvasPreviewCommand` | Step3 preview cache | Step3 display path | 不應回寫 L1/L2 |

## 6.1 現有功能保留清單（不可退化）
以下行為在右側 IA 改版後必須全部保留，不可因 UI 改版移除：

- Step 3（Notch compensation）
  - `Recompute focused CAD (Step 3)`（`RefreshNotchCanvasPreviewCommand`）。
  - stage 切換（上一層/下一層）與 auto-play（含 interval）。
  - `Clear Step 3` 會清 Step 3 與 downstream state。
  - 仍可從 Step 3 入口跳轉到 Settings 對應分頁。

- Step 4（Index diagnostics）
  - duplicate group `Select` 後要能 focus 對應 CAD（`SelectStep4DuplicateDiffGroupCommand`）。
  - batch diff shift（`OffsetSelectedCadOutputFwDiffIndicesCommand`）保留。
  - `Analyze mapping` 仍會開啟 mapping report（不可消失）。
  - report 內 action 全保留：
    - locate row target
    - apply override / clear override
    - apply diff repair（單筆）
    - apply segment repairs（區段）
    - apply visible repairs（目前可見）

- Step 5（Export）
  - `Export` 主命令保留（`ExportNotchCommand`）。
  - row filter / search / sort / select / use shown only 契約保留。
  - 選列後 workspace 預覽/定位 callback 保留（`SelectPreviewRowCommand`）。
  - `Hide panel (inspect AA)` + `Ctrl+Shift+E` restore 流程保留。

- Step 6（Validation quick trace）
  - `Use Selected REG`、`Analyze`、`Clear` 三個主命令保留。
  - `DIRECT / IN / OUT` 分組保留。
  - 點 validation row 仍要 focus source/target pads（`FocusNotchValidationItemCommand`）。

- Simulation
  - canvas 點選 regular 後，右側 inspector 與 notch impact 要同步更新。
  - `Fit canvas`、playback（prev/play/next/slider/fps）保留。
  - manual override（apply selected / clear selected / clear all）保留。
  - `Before / After / Delta / Changed only` 與 color mode 切換保留。

## 6.2 互動事件契約（點擊/焦點）
- 任何 `Open action` 或 row click 觸發 focus 時，必須走既有 locate/focus path，不得引入第二套 focus 演算法。
- focus 失敗要保留明確 status text（例如 target not visible），不可 silent fail。
- Step rail 節點點擊只能做「切投影/切 filter/切 detail」，不得直接改動演算法資料。

## 7. 顯示規範（避免資訊過載）
- 主視圖只保留 3 類元素：
  - `Summary chips`（數量與狀態）
  - `Table`（可排序/過濾）
  - `Review actions`（單筆/批次）
- 禁止在主流程放超過兩行的自由文字說明；長說明必須放 info icon。
- reason/狀態一律 badge 化：`reasonCode`、`decisionSource`、`status`。
- 預設 table 欄位不超過 8 欄，進階欄位放右側 panel。

## 8. 目前缺口（需要在 UI 實作補齊）
- Step4、Step5、Step6 尚未統一成同一個 row identity 的驗證工作台。
- Export 與 Validation trace 的 cross-link 還不夠直覺（目前要切多個區塊才看全貌）。
- `before/after` 套用預覽在 Step4 report 與 Export window 還不是同一個呈現語法。
- Simulation inspector 與 Step4 report 欄位命名尚未完全一致（需要 contract 對齊）。

## 9. 實作切分建議
- Phase A（資料契約落地）
  - 建 `VerificationRow` projector（只讀整合 L1+L2+L3 key fields，不重算）。
  - 統一欄位命名與 badge token。
- Phase B（Step4 report 結構改版）
  - 改成 table + right review panel。
  - 將現有大段描述搬到 info icon。
- Phase C（Step5/Step6 接軌）
  - Export row 與 Validation trace 以同一 row key 串接。
  - 支援從 trace 直接定位對應 export row。
- Phase D（Simulation 對齊）
  - Simulation inspector 改讀同名欄位契約。
  - 對齊 color/impact 文案與 verification table 的 key identifiers。

## 10. 驗證標準
- 同一欄位在 Step4/Step5/Step6/Simulation 僅有一個 source projector。
- 任一 action 的影響範圍可由「Action -> 欄位影響矩陣」完整追溯。
- 主流程畫面不再出現大段描述文字；說明改為 info icon + tooltip。
- 使用者能在單一驗證工作流中回答三件事：
  - 目前值是什麼（current）
  - 為什麼是這個值（reason/source）
  - 改了會影響哪裡（impact）
