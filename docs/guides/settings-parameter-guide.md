# Settings Parameter Guide

最後更新：2026-05-23

這份文件整理 `SettingsWindow` 目前可見參數的用途、影響範圍與調適建議。既有文件已說明設定入口矩陣與部分演算法背景，但沒有集中列出每個 Settings 參數；本文件補齊這一層操作說明。

## 使用原則

- 先固定 General 的 grid / cascade / AA / source，再調 Step 1~5。幾何基礎變動後，後續 match、freeform、notch、mapping、export 都應重新跑一次。
- 每次只改一組參數，先看 Step summary、canvas overlay、Simulation safety，再決定是否繼續調。
- 優先保留 safety guard 預設值。關閉 guard 或降低 cap 只適合做診斷對照，不適合直接交付 FW。
- 顯示用參數不改變計算結果，例如 preview layer、labels、diff marker、font size、highlight width。
- 匯出前至少確認 Step 5 safety 摘要、Simulation EMS 結果、export versions 與 gate threshold。

## Cascade 與 IC X/Y

目前 Settings 已支援不同 IC 使用不同 X/Y 數量，入口在 `Settings > General > Cascade` 旁邊的 edit button。主表單上的 `Total X channels` 與 `Total Y channels` 是被動計算欄位，不能直接手填。

| 操作 | 行為 |
| --- | --- |
| 修改 `Cascade` | 可在 General 主表單或 `Cascade IC details` modal 內調整 IC row 數量；新增 row 會沿用最後一個既有 IC 的 X/Y，沒有既有 row 時會用目前總 X/Y 當 fallback。 |
| 開啟 `Cascade IC details` | 在 modal 內逐列設定 `IC1`, `IC2`, ... 的 `X channels` 與 `Y channels`，也可直接調整 `Cascade` 數量。 |
| 修改 per-IC X | `Total X channels = sum(per-IC X)`。 |
| 修改 per-IC Y | `Total Y channels = max(per-IC Y)`。 |
| 按 `Done` | 只關閉 modal，變更仍停留在 Settings draft。 |
| 按 `Save Settings` | 將 draft 套用到 project settings，並寫入 `Grid.PerIcXChannels` / `Grid.PerIcYChannels`。 |

展示例：

| IC | X channels | Y channels |
| --- | ---: | ---: |
| IC1 | 10 | 20 |
| IC2 | 12 | 18 |
| IC3 | 14 | 22 |

上例會顯示 `Total X channels = 36`，`Total Y channels = 22`。若 per-IC X 清單長度或加總與總欄數不一致，application 層的 IC column allocation 會退回平均切分，避免 downstream IC index 失效。

目前 runtime contract：per-IC X 會被 application 層用來解析 column 所屬 IC；per-IC Y 目前用來計算並保存全域 `Total Y channels = max(per-IC Y)`，不是 per-IC row mask。也就是說，regular grid 仍是全域 row model。

調適建議：先把 IC 實際串接數填進 `Cascade`，再填每顆 IC 的實際 X/Y。若只有某顆 IC 的 Y 比較長，總 Y 會取最大值；不要為了讓短 IC 對齊而手動壓低長 IC 的 Y。若 downstream 需要依 IC Y 長度裁切 rows，需另外擴充 application/export 使用 `Grid.PerIcYChannels`。

## General

| 參數 | 功能 | 調適建議 |
| --- | --- | --- |
| `Cascade` | IC cascade 數量，決定 per-IC row 數。 | 依硬體實際 IC 數設定；改完後檢查 per-IC modal。 |
| `Total X channels` | 全 panel X 欄數，被動由 per-IC X 加總。 | 不直接手填；若數字不對，回 modal 修各 IC X。 |
| `Total Y channels` | 全 panel Y 列數，被動由 per-IC Y 最大值決定。 | 依最長 IC 的 Y 設定；目前不是 per-IC row mask。 |
| `AA size X/Y (mm)` | panel active area 尺寸，用於 generated regular grid placement。 | 用實測或規格值；AA 變動後重新跑 Step 1 以更新匹配。 |
| `Scan order` | generated regular grid 的 diff-index 編號方向。 | 與 FW / CAD index 定義一致；不要用它修 geometry mismatch。 |
| `Regular source` | regular pad 來源，可用 generated grid 或 DXF layer。 | 有可靠 regular DXF layer 時可用 layer source；否則用 generated grid。 |
| `Regular layer` | Regular source 為 DXF layer 時使用的 layer。 | 僅在 DXF layer source 啟用；選錯會讓 Step 1 mapping 偏離。 |
| `Grid padding (%)` | generated regular grid 在 AA 內的 padding。 | CAD bounds 太貼邊時小幅增加；大幅調整前先確認 AA 尺寸正確。 |
| `Coordinate pixel X/Y` | FW coordinate planning / export diagnostics 的座標解析度。 | 只影響座標診斷與輸出規劃，不改 regular geometry 或 notch allocation。 |
| `Panel bias X/Y` | panel alignment mode 下的 AA 對位偏移。 | 只在 `Grid alignment mode = FromPanelAa` 時有效；用來修固定偏移，不用來修比例錯誤。 |
| `Only closed polylines` | DXF import 時只收 closed polyline。 | 通常保持開啟，避免 open line 被誤當 pad。 |
| `Import block polylines` | DXF import 也掃描 BLOCK 裡的 polyline。 | CAD 把 pad 放在 block definition 內時開啟；若出現重複 geometry，先檢查此項。 |
| `Auto bounds from visible layers` | layer visibility 變動後，用可見 layer 重算 bounds。 | 調 layer filter 做幾何隔離時開啟；需要固定 bounds 做對照時可暫關。 |
| `Apply visual preferences on load` | Load Project 後保留 project geometry，但套用 app-level 顯示偏好。 | 多專案共用顯示習慣時開啟；驗證專案 snapshot 時可關閉。 |
| `Grid alignment mode` | regular grid 對齊來源：panel AA 或 CAD bounds。 | CAD 位置可靠用 CAD bounds；需要依 panel spec 對齊時用 panel AA。 |
| `Log level` | console / log file 的最低輸出層級。 | 一般用 Info；追問題時改 Debug/Trace，完成後調回避免 log 噪音。 |
| `Global font size (%)` | UI 文字縮放。 | 只改視覺，不改資料；建議 90~120 範圍內調整。 |
| `Highlight line width adjust` | canvas selected/highlight pad 額外線寬。 | 大圖或高 DPI 可微增；不要用過大值遮住 pad 邊界。 |
| `Sizing scope` | 手動 sizing 時使用 Local 或 Global scope。 | 單列/單欄微調用 Local；整體均分或重分配用 Global。 |
| `Layer categories` | 依解析出的 DXF layer category 批次 On/Off。 | 用於快速隔離 CAD output、regular、annotation 等 layer；這是 visibility 操作，不改 project geometry。 |

## Step 1

| 參數 | 功能 | 調適建議 |
| --- | --- | --- |
| `Unmatched TH (overlap / regular area)` | unmatched regular pad 的診斷門檻，不會重寫 Step 1 links。 | 門檻低時較少 regular pad 被標為可疑；門檻高時會標出更多可疑 unmatched。用於診斷，不要期待它改變既有 link。 |

## Step 2

| 參數 | 功能 | 調適建議 |
| --- | --- | --- |
| `Freeform TH (dominant axis ratio)` | Step 2 判斷 XWay / YWay / XYWay 的主軸比例門檻。 | 偏低會更積極分類方向；偏高會更保守。若 XYWay 過多或過少，先調這項。 |
| `XY WAY EN` | 允許 Step 2 自動偵測 X/Y freeform direction。 | 一般保持開啟；只有在要用人工 override 驗證時才關閉。 |
| `Edge spill EN` | 啟用邊界 spill case 的 edge-specific freeform 規則。 | edge pad 常見誤判時保持開啟；若要比較 legacy 行為，可暫關做 A/B。 |
| `Load auto-run Step 2` | Load Project 後自動 replay Step 2 tagging。 | 專案常需要開啟即檢查最新 tagging 時開啟；大量載入只看原始 snapshot 時可關閉。 |

## Step 3

| 參數 | 功能 | 調適建議 |
| --- | --- | --- |
| `Compensation model` | Step 3 補償模型，單一來源控制 To Regular / To Full 語意。 | 以目前產品線規格為準；切換後要重新看 Step 3 summary 與 Simulation。 |
| `To Regular EN (derived)` | 由 compensation model 推導，表示是否先還原 area-proportional signal。 | 只讀狀態；要改行為請改 `Compensation model`。 |
| `To Full EN (derived)` | 由 compensation model 推導，表示是否啟用 boundary target support / cap。 | 只讀狀態；要改行為請改 `Compensation model`。 |
| `Multi-owner strict overlap (%)` | CAD 擁有或阻擋 boundary target 前，要求的 regular-area overlap 比例。 | 提高可更早拒絕 ambiguous boundary target；降低可保留更多邊界候選，但要用 Simulation 檢查。 |
| `Use boundary area cap` | 限制 To Full virtual expansion 面積，避免邊界 EMS 失控。 | 建議保持開啟；關閉只做診斷比較。 |
| `Virtual cap (%)` | virtual To Full area 相對原始 overlap area 的最大比例。 | 100% 表示外側 virtual area 不超過內側 overlap。放寬前先確認 EMS margin。 |
| `Use target coverage guard` | CurrentGain rows 選定後，限制每個 target FW diff 的總 coverage。 | 建議保持開啟；它是避免 uniform-field EMS overflow 的主要 guard。 |
| `Target cap (%)` | target-side coverage 上限；120% 對應 uniform 400 到 EMS 480 cap。 | 一般維持 120；提高會增加 EMS 風險，降低可能造成補償不足。 |
| `Use To Full rule engine` | 使用集中式 To Full boundary gate engine；關閉則走 legacy inline gate path。 | 建議保持開啟，除非要比對 legacy path。 |
| `Keep To Full trace` | 保存每個 regular 的 To Full rule decision，供 diagnostics / runtime query 使用。 | 需要查 rule decision 時開啟；一般可關閉減少診斷資料量。 |
| `Stage auto play` | AA canvas 自動輪播 Step 3 preview layer。 | 顯示用；不影響 notch row。 |
| `Interval (ms)` | auto play layer 間隔。 | 只調閱覽速度；不影響計算。 |
| `Show To Regular labels` | Step 3 preview canvas 顯示 To Regular ratio label。 | 顯示用；檢查個別比例時開啟。 |
| `Show To Full overlay` | Step 3 preview canvas 顯示 To Full support stages。 | 顯示用；檢查 boundary support 時開啟。 |
| `To Full layer stage` | 選擇 To Full preview layer：1 seed、2 candidate、3 final。 | 用於定位 rule gate 在哪一層排除候選；不改計算結果。 |

## Step 4

| 參數 | 功能 | 調適建議 |
| --- | --- | --- |
| `Weight IoU` | mapping diagnostic score 中幾何 overlap quality 的權重。 | CAD/regular overlap 品質可靠時提高；若 shape 差異大可降低。 |
| `Weight distance` | CAD/regular centroid distance 的權重。 | 幾何位置比面積更可靠時提高；CAD 有偏移時不要過高。 |
| `Weight area` | CAD/regular area similarity 的權重。 | pad 面積穩定時提高；edge / notch 造成面積差異大時降低。 |
| `Candidate number` | Step 4 mapping diagnostics 保留的候選數。 | 正確候選被截掉時提高；候選太多造成報表噪音時降低。 |
| `Low confidence threshold` | 低於此 confidence 的 mapping 會標記 review。 | 提高會抓出更多風險；降低會減少人工 review 數量。 |
| `Ambiguous margin (best - 2nd)` | best score 與 second score 差距低於此值時標記 ambiguous。 | 提高會更敏感地標 ambiguous；降低會更信任 best candidate。 |
| `CAD Output FW Diff strategy` | 依 geometry seed / override 指派 CAD Output FW Diff index 的策略。 | 一般依 project convention 選定後不要頻繁切換；切換後重新檢查 overlay/report。 |
| `CAD Output FW Diff start` | automatic CAD Output FW Diff numbering 起始 index。 | 與 FW numbering range 對齊；變更會影響輸出診斷 index。 |
| `Future validation path` | 保留欄位，預留 CSV-confirmed geometry seed。 | 目前不可調。 |
| `Show CAD Output FW Diff markers` | canvas 顯示 CAD output diff markers。 | 顯示用；用來檢查 numbering，不改 mapping result。 |

## Step 5

| 參數 | 功能 | 調適建議 |
| --- | --- | --- |
| `Safety before FW handoff` | 顯示 notch export / EMS safety policy 摘要。 | 匯出前必看；若顯示 unsafe，先回 Step 3 / Simulation 修正。 |
| `Export versions` | 選擇匯出的 notch table version，例如 v2.1 / v2.2。 | 依 FW 需求選；同時開啟可產生對照，但要確認下游使用哪版。 |
| `Export type` | 選擇產出格式，例如 C source 或 CSV diagnostics。 | 交付 FW 用 C profile；分析與 diff review 用 CSV diagnostics。 |
| `C export profile` | 選擇 downstream FW integration 期待的 C 輸出 profile。 | 與目標 codebase 版本一致；profile 不一致會造成 integration friction。 |
| `Null value` | notch payload 中空 entry 的 placeholder。 | 一般使用 FW 約定值，例如 65535；改前需確認 FW parser。 |
| `Notch gate TH v2.1 (Q7/128)` | v2.1 gate threshold，Q7 單位，128 表示 100%。 | 需要 v2.1 相容時調整；與 v2.2 連動時由 `Link TH` 同步。 |
| `Notch gate TH v2.2 (%)` | v2.2 gate threshold，直接以百分比表示。 | 用百分比較直覺；若 FW 同時吃 v2.1/v2.2，建議保持 linkage。 |
| `Link TH (v2.1 Q7 <-> v2.2 %)` | 編輯時同步 v2.1 Q7 與 v2.2 percent threshold。 | 建議開啟，除非刻意比較不同版本 threshold。 |

## 相關程式入口

- `src/FreeformHelper.UI/Views/SettingsSections/SettingsGeneralSectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep1SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep2SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep3SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep4SectionView.axaml`
- `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep5SectionView.axaml`
- `src/FreeformHelper.UI/Views/CascadeIcSettingsWindow.axaml`
- `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.Cascade.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs`
- `src/FreeformHelper.UI/Services/CascadeIcLayoutService.cs`
- `src/FreeformHelper.Application/Services/GridIcChannelAllocationService.cs`
