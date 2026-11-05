# FreeformHelper 內部教學影片分鏡稿（3635 面板實作）

- 交付日：2026-11-25
- 觀眾：內部工程師（韌體、面板整合、驗證）
- 字幕：繁體中文（zh-TW），每行一句，每句至多 35 字
- 時間軸：`cloud-notes/video-timeline.json`（與本文件段落 id、類型、秒數一一對應）
- 狀態：草稿，待 owner 審閱

## 0. 製作原則

1. **每段可單獨重錄。** 每段寫明「起始狀態」與「重置方式」，錄製者不必先錄前一段。
2. **只用程式裡已有的功能。** 每個按鈕、選單和設定名稱都對照 `src/FreeformHelper.UI/Views` 下的 `.axaml` 字串與 `docs/guides/app-user-manual.md`。UI 是英文，字幕用中文說明，畫面標註保留英文原名，方便觀眾對照。
3. **兩種段落類型。**
   - `screen`：實機錄影（FreeformHelper UI 或 PowerShell）。
   - `motion-canvas`：Motion Canvas 動畫，說明畫面上看不清楚的概念，例如 pad 怎麼對應到 panel grid。
4. **真實資料。** 錄影一律使用 3635 範例專案 `example/BOE36.35/project_3635.json`。
5. **資料保密。** `example/` 是私有 submodule，內容屬客戶面板資料。影片只限內部觀看，不得上傳公開平台。畫面上只出現 `docs/performance/regression-baseline-3635.md` 已經公開的數字：CAD 4,838 pads、Regular 4,992 pads、mask 後 active 4,898 pads、hidden CAD IDs `4764/4765/4751/4750`。檔案總管若會露出 DXF 原始檔名，請裁切或模糊處理。

## 1. 錄影前共同準備（每次開錄都做）

| 項目 | 做法 |
|---|---|
| 取得範例資料 | `git submodule update --init example`（需有私有資料庫權限） |
| 建置 | `./scripts/build/fetch-core-packages.ps1`、`dotnet build FreeformHelper.sln` |
| 清理工作區 | `./scripts/dev/prepare-ui-workspace.ps1`（停止殘留的 UI 程序） |
| 啟動 | `dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj` |
| 視窗 | 1920×1080，全螢幕，Light 主題，`Global font size (%)` 維持預設 |
| 設定隔離 | 錄影使用獨立 Windows 帳號，或先備份 app 設定。`Save Project` 會寫入 app 層設定 |
| 標準重置 | `File -> Load Project` 載入 `project_3635.json`。需要較早的狀態時，按該 Step 的清除圖示（tooltip：`Clear Step N result and all downstream results.`） |

選用：已有 UI 在執行時，可從另一個終端用 Runtime CLI 快速重置，例如
`... -- query load-project --path example/BOE36.35/project_3635.json` 與 `... -- query run-step --step 1`。

## 2. 段落總覽

| # | id | 標題 | 類型 | 秒數 |
|---|---|---|---|---:|
| 1 | `S00-intro` | 片頭：這支影片教什麼 | motion-canvas | 30 |
| 2 | `A01-pipeline` | 動畫：整體流程一覽 | motion-canvas | 45 |
| 3 | `R01-launch-tour` | 啟動與畫面導覽 | screen | 60 |
| 4 | `R02-dxf-import` | 匯入 DXF | screen | 60 |
| 5 | `R03-layer-list` | 圖層清單與 DXF 檢查 | screen | 90 |
| 6 | `R04-general-settings` | General 設定：IC、通道、AA、掃描順序 | screen | 90 |
| 7 | `A02-grid-diff-index` | 動畫：Regular grid 與 diff 編號 | motion-canvas | 75 |
| 8 | `A03-pad-to-grid` | 動畫：CAD pad 如何對應到 panel grid | motion-canvas | 75 |
| 9 | `R05-step1-match` | Step 1：Geometry match 與 Visibility Mask | screen | 90 |
| 10 | `A04-visibility-mask` | 動畫：SeeRegular mask 的作用 | motion-canvas | 45 |
| 11 | `A05-freeform-rule` | 動畫：Freeform 方向怎麼判定 | motion-canvas | 60 |
| 12 | `R06-step2-freeform` | Step 2：Freeform tagging | screen | 75 |
| 13 | `A06-notch-tofull` | 動畫：To Regular 與 To Full 三階段 | motion-canvas | 75 |
| 14 | `R07-step3-notch` | Step 3：Notch 預覽與安全設定 | screen | 90 |
| 15 | `R08-step4-diagnostics` | Step 4：Index diagnostics | screen | 90 |
| 16 | `R09-step5-notch-table` | Step 5：Notch 表審閱與匯出 | screen | 120 |
| 17 | `R10-save-load` | 儲存與載入專案 | screen | 60 |
| 18 | `R11-simulation` | Simulation 頁：Before / After / Delta | screen | 75 |
| 19 | `R12-runtime-cli` | 進階：Runtime CLI 查詢 | screen | 60 |
| 20 | `S99-outro` | 結尾：驗證清單 | motion-canvas | 30 |

總長約 1,395 秒（23 分 15 秒）。
`R11`、`R12` 標為選用，剪成精簡版時可拿掉，精簡版約 1,260 秒（21 分 00 秒）。

---

## 3. 分鏡

### S00-intro：片頭

- **類型：** motion-canvas
- **預估：** 30 秒
- **目標：** 說明影片範圍、使用的資料與觀眾在看完後能做到什麼。
- **起始狀態：** 無（純動畫）。
- **畫面動作：**
  1. 標題「FreeformHelper 3635 實作教學」淡入。
  2. 主流程五個字卡依序滑入：DXF → Grid → Match → Freeform → Notch。
  3. 右下角出現「僅限內部」標記。
- **旁白（zh-TW）：**
  - 這支影片示範 FreeformHelper 的完整流程。
  - 我們用真實的 3635 面板專案來操作。
  - 從匯入 DXF 一路做到匯出 Notch 表。
  - 中間會穿插動畫，解釋畫面上看不到的概念。
  - 每一段都能單獨觀看，需要時直接跳段即可。
- **畫面標註：**
  - 標題：FreeformHelper · 3635 panel
  - 角標：Internal only
- **素材：** 無。

### A01-pipeline：動畫：整體流程一覽

- **類型：** motion-canvas
- **預估：** 45 秒
- **目標：** 讓觀眾先有整體地圖：每一步的輸入與輸出。
- **起始狀態：** 無。
- **畫面動作：**
  1. 左側出現 DXF 圖示，拆成許多不規則 CAD pad 多邊形。
  2. 右側長出規則矩形格（Regular grid）。
  3. 兩者疊在一起，重疊區上色，標示 Step 1。
  4. 部分 regular 亮起 X / Y / XY 箭頭，標示 Step 2。
  5. 邊界 regular 外擴，標示 Step 3。
  6. 每顆 CAD pad 旁出現 diff 編號，標示 Step 4。
  7. 最後收斂成一張表格與 `.c` 檔圖示，標示 Step 5。
  8. 底部畫出回饋箭頭：上游一改，下游結果清空。
- **旁白（zh-TW）：**
  - 先看整體流程。
  - DXF 裡的 CAD pad 是不規則的多邊形。
  - 韌體看到的卻是規則的感測格，也就是 Regular grid。
  - Step 1 計算每顆 CAD pad 和哪些格子重疊。
  - Step 2 標出跨格的 pad，也就是 Freeform。
  - Step 3 計算 Notch 補償比例。
  - Step 4 檢查每顆 pad 的韌體 diff 編號。
  - Step 5 輸出韌體用的 Notch 表。
  - 上游任何一步改變，下游結果都會自動清除。
- **畫面標註：**
  - `Step 1 · Geometry match`
  - `Step 2 · Freeform tagging`
  - `Step 3 · Notch 2.2 compensation`
  - `Step 4 · Index diagnostics`
  - `Step 5 · Notch export`
  - 「上游變更 → 下游失效」
- **素材：** 依 `docs/core/freeform-helper-algorithms.md` 第 2 節繪製。

### R01-launch-tour：啟動與畫面導覽

- **類型：** screen
- **預估：** 60 秒
- **目標：** 認識主畫面四個區域、各頁面與畫布基本操作。
- **起始狀態：** UI 已啟動，已 `Load Project` 載入 `project_3635.json`。
- **重置：** 重新 `Load Project`。
- **畫面動作：**
  1. 鏡頭停在 `Freeform Helper` 主頁。
  2. 依序框出左側 DXF 面板、中央 AA 畫布、右側 Step 流程面板、底部 Console。
  3. 在畫布上滾輪縮放，再用中鍵拖曳平移。
  4. 按 `F` 讓畫面回到 Fit。
  5. 點選一顆 CAD pad，雙擊打開 pad info。
  6. 點空白處清除選取。
  7. 切換到 `How To Use` 頁停 2 秒，再切回 `Freeform Helper`。
- **旁白（zh-TW）：**
  - 主畫面分成四個區域。
  - 左邊是 DXF 圖層和編輯工具。
  - 中間是 AA 畫布，CAD 和 Regular 疊在一起。
  - 右邊是 Step 1 到 Step 5 的流程面板。
  - 底部的 Console 會顯示每一步的紀錄。
  - 滾輪縮放，中鍵拖曳平移，按 F 回到全圖。
  - 雙擊 pad 可以打開詳細資訊。
  - 快捷鍵整理在 How To Use 頁。
- **畫面標註：**
  - 四個區域的框線與名稱：Left DXF panel / AA canvas / Workflow / Console
  - 快捷鍵卡：`Wheel` 縮放、`Middle drag` 平移、`F` Fit、`Double click` Pad info
- **素材：** 無。

### R02-dxf-import：匯入 DXF

- **類型：** screen
- **預估：** 60 秒
- **目標：** 從原始 DXF 開始，正確設定匯入範圍。
- **起始狀態：** UI 剛啟動，尚未載入任何檔案。
- **重置：** 重新啟動 UI。
- **畫面動作：**
  1. 開啟 `Settings -> Open Settings`，切到 `General` 區的 DXF 匯入設定。
  2. 指出 `Only closed polylines` 與 `Import block polylines` 都要勾選。
  3. 按 `Save Settings` 關閉視窗。
  4. `File -> Open DXF`，選取 `example/BOE36.35/` 下的 3635 DXF（檔名需裁切）。
  5. 等待載入動畫結束，畫布出現 CAD pads。
  6. 鏡頭拉到右側 `Project / grid` 摘要，停在 CAD pad 數量。
- **旁白（zh-TW）：**
  - 先設定 DXF 的匯入範圍。
  - 只匯入封閉的 polyline。
  - BLOCKS 裡的 polyline 也要一起匯入。
  - 設定要按 Save Settings 才會生效。
  - 接著從 File 選單開啟 DXF。
  - 載入後，右側會顯示 CAD pad 數量。
  - 改了匯入設定，要重新開啟 DXF 才會套用。
- **畫面標註：**
  - `Only closed polylines` ✓
  - `Import block polylines` ✓
  - 「Closed polyline import; reopen DXF after changing this.」
  - `File -> Open DXF`
- **素材：** 3635 DXF（私有，畫面裁切檔名）。

### R03-layer-list：圖層清單與 DXF 檢查

- **類型：** screen
- **預估：** 90 秒
- **目標：** 選對圖層、找出重疊與重複的 pad、隱藏不該參與計算的 pad。
- **起始狀態：** 已載入 `project_3635.json`。
- **重置：** 重新 `Load Project`。
- **畫面動作：**
  1. 展開左側 `Layers` 清單，逐一切換幾個圖層的顯示。
  2. 指出 3635 使用的圖層：`NC.drawing`、`PGT.drawing`、`PINC.drawing`、`PLN1.drawing`、`Ref1.drawing`～`Ref5.drawing`。
  3. 開啟 `Settings > General`，展示 `Layer categories`（`Batch toggle DXF layers by parsed content category.`），按 `Cancel`。
  4. 在左側 `DXF check` 區按 `Overlap check`（`Scan the current DXF for duplicates or overlaps.`），看 `Issues: N`。
  5. 按詳細圖示（`Open overlap details window (copy supported).`），點一列，畫布定位到該 pad。
  6. 關閉視窗，按選取圖示（`Restore overlap highlight and select those CAD pads.`），再按清除圖示。
  7. 在畫布上選一顆 pad，按 `Delete` 隱藏，再按 `Undo hide` 復原。
  8. 展開 `Advanced edit`，帶過 `Combine`、`Move to existing layer`、`Rotate` 三個工具。
- **旁白（zh-TW）：**
  - 左側的 Layers 清單控制哪些圖層參與計算。
  - 3635 專案使用九個 drawing 圖層。
  - Layer categories 可以依類別批次切換圖層。
  - 接著按 Overlap check 檢查重複和重疊的 pad。
  - 詳細視窗會列出每一筆問題，點一列就能定位。
  - 不該參與計算的 pad，選取後按 Delete 隱藏。
  - 按錯了可以用 Undo hide 復原。
  - 進階編輯還能合併、搬圖層和旋轉。
  - 這些編輯都會存進專案檔。
- **畫面標註：**
  - 九個圖層名稱清單
  - `DXF check` → `Overlap check` / 詳細 / 選取 / 清除圖示
  - `Delete` = Hide selected，`Undo hide`
  - 3635 已隱藏：CAD `4764`、`4765`、`4751`、`4750`
- **素材：** 無。

### R04-general-settings：General 設定

- **類型：** screen
- **預估：** 90 秒
- **目標：** 理解會影響整條流程的 grid 設定。
- **起始狀態：** 已載入 `project_3635.json`。
- **重置：** 在設定視窗按 `Cancel` 放棄草稿，或重新 `Load Project`。
- **畫面動作：**
  1. 在右側 `Project / grid` 卡片按設定圖示（`Open Settings > General.`）。
  2. 依序指出：`Cascade`、`X channels`、`Y channels`、`Total X channels`、`Total Y channels`。
  3. 打開 `Cascade IC details`，展示每顆 IC 的 X/Y 通道分配，然後 `Cancel`。
  4. 指出 `AA size X (mm)`、`AA size Y (mm)`、`Grid padding (%)`。
  5. 展開 `Scan order` 下拉，停在四個選項上。
  6. 展開 `Grid alignment mode`，指出兩個選項：依 panel AA 或依 CAD bounds。
  7. 指出 `Regular source`：產生 grid 或使用 DXF 圖層。
  8. 指向頁首提示「Changes apply only after Save Settings.」，按 `Cancel` 不存。
- **旁白（zh-TW）：**
  - General 設定決定 Regular grid 長什麼樣子。
  - Cascade 是串接的 IC 數量。
  - 每顆 IC 有自己的 X 和 Y 通道數。
  - Cascade IC details 可以逐顆調整通道分配。
  - AA size 是面板可視區的實際尺寸。
  - Scan order 決定 diff 編號的掃描方向。
  - Alignment mode 決定 grid 原點的位置。
  - 這些值一改，Step 1 到 Step 5 都要重跑。
  - 設定要按 Save Settings 才會生效。
- **畫面標註：**
  - `Cascade` / `X channels` / `Y channels`
  - `AA size X (mm)` / `AA size Y (mm)` / `Grid padding (%)`
  - `Scan order`：LeftToRight_TopToBottom 等四種
  - `Grid alignment mode`：FromPanelAa / FromCadBounds
  - `Regular source`：GeneratedGrid / FromDxfLayer
  - 警示：「改這裡 → 下游全部重算」
- **素材：** 無。

### A02-grid-diff-index：動畫：Regular grid 與 diff 編號

- **類型：** motion-canvas
- **預估：** 75 秒
- **目標：** 說明 IC、通道和 Scan order 如何決定每一格的 IC 與 diff 編號。
- **起始狀態：** 無。
- **畫面動作：**
  1. 畫一個 AA 外框，依 X/Y 通道數切成規則格。
  2. 欄方向依每顆 IC 的 X 通道數分成色塊（IC0、IC1…）。
  3. 選一顆 IC，從起點開始逐格點亮並寫上 diff 編號。
  4. 顯示公式：`diff = scanRow × icCols + scanCol`。
  5. 切換 Scan order 四種方向，同一格的編號跟著改變。
  6. 最後強調：每顆 IC 的 diff 都從 0 起算。
- **旁白（zh-TW）：**
  - Regular grid 由 AA 尺寸和通道數切出來。
  - 每一欄先分配給某一顆 IC。
  - 同一顆 IC 裡，格子依掃描順序編號。
  - 編號公式是列號乘以欄數，再加上欄號。
  - Scan order 改變時，列和欄的起點跟著翻轉。
  - 所以同一格在不同順序下，diff 會不一樣。
  - 每顆 IC 的 diff 都從零開始。
  - 韌體就是用 IC 加 diff 來找到這一格。
- **畫面標註：**
  - `diff = scanRow × icCols + scanColLocal`
  - IC 色塊圖例
  - 四個 Scan order 的箭頭示意
- **素材：** 依 `src/FreeformHelper.Application/Services/AfeMapper.cs`（`DiffIndexMapper.ApplyMapping`）的規則製作。示意格數使用小尺寸（例如 2 顆 IC × 4×3），不使用 3635 實際格數。

### A03-pad-to-grid：動畫：CAD pad 如何對應到 panel grid

- **類型：** motion-canvas
- **預估：** 75 秒
- **目標：** 說明 Step 1 的重疊計算與三個覆蓋率指標。
- **起始狀態：** 無。
- **畫面動作：**
  1. 一顆不規則 CAD pad 落在 grid 上，蓋到 3 到 4 格。
  2. 先畫出 pad 的外接矩形，只把框到的格子標為候選。
  3. 每個候選格內，填色顯示實際重疊面積。
  4. 對其中一格展開數值卡：`OverlapArea`、`RegularCoverage`、`CadCoverage`。
  5. 太小的重疊（低於門檻）淡出，不建立連結。
  6. 畫出雙向連結：一顆 CAD 對多格、一格對多顆 CAD。
  7. 每一格標出覆蓋率最高的 CAD 作為 best match。
- **旁白（zh-TW）：**
  - Step 1 要回答一件事：每顆 pad 蓋到哪些格。
  - 先用外接矩形縮小候選範圍。
  - 再精算多邊形和每一格的實際重疊面積。
  - 重疊面積除以格子面積，是 Regular coverage。
  - 重疊面積除以 pad 面積，是 CAD coverage。
  - 太小的重疊會被當成雜訊濾掉。
  - 一顆 pad 可以連到多格，一格也可以連到多顆 pad。
  - 每一格會保留覆蓋率最高的 pad 作為最佳對應。
- **畫面標註：**
  - `RegularCoverage = overlap / regularArea`
  - `CadCoverage = overlap / cadArea`
  - 「CAD → 多個 REG」「REG → 多個 CAD」
- **素材：** 依 `docs/core/freeform-helper-algorithms.md` 第 3 節。

### R05-step1-match：Step 1：Geometry match 與 Visibility Mask

- **類型：** screen
- **預估：** 90 秒
- **目標：** 執行 Step 1、看懂摘要，並匯入 SeeRegular mask。
- **起始狀態：** 已載入 `project_3635.json`，再按 Step 1 的清除圖示，讓 Step 1 到 Step 5 都是空的。
- **重置：** 按 Step 1 的清除圖示（`Clear Step 1 result and all downstream results.`）。
- **畫面動作：**
  1. 在右側 Step rail 點 `Step 1 · Geometry match`。
  2. 按 `Match overlaps`。
  3. 畫面自動跳到 Step 2 後，點回 Step 1 看摘要。
  4. 指出 `Unmatched diagnostics threshold`（只影響顯示），再在畫布工具列打開 `Color by area`。
  5. 在 `Regular Visibility Mask` 區按 `Import mask`，選 `SeeRegular.csv`。
  6. 勾選 `Use Regular Visibility Mask`，指出 active regular 數量。
  7. 點一顆 regular pad，在 Inspector 看它連到哪些 CAD。
- **旁白（zh-TW）：**
  - 打開 Step 1，按 Match overlaps。
  - 完成後流程會自動跳到下一步。
  - 回到 Step 1 可以看到重疊結果的摘要。
  - Unmatched 門檻只影響診斷顯示，不改連結。
  - 接著匯入 SeeRegular.csv 這份可見遮罩。
  - 遮罩不會自動搜尋，必須手動匯入。
  - 勾選後，沒有訊號的格子就不參與後續分配。
  - 3635 有 4,992 格，套用遮罩後剩 4,898 格。
  - 遮罩不會改動 Step 1 的幾何結果。
- **畫面標註：**
  - `Match overlaps`
  - `Unmatched diagnostics threshold` / `Color by area`
  - `Import mask` / `Use Regular Visibility Mask` / `Clear mask`
  - 數字卡：Regular 4,992 → Active 4,898
- **素材：** `example/BOE36.35/` 的 `SeeRegular.csv`（私有）。

### A04-visibility-mask：動畫：SeeRegular mask 的作用

- **類型：** motion-canvas
- **預估：** 45 秒
- **目標：** 說明 mask 的來源和它影響哪些步驟。
- **起始狀態：** 無。
- **畫面動作：**
  1. 一組 CSV frames 疊成一疊，逐格掃過。
  2. 每一幀都為 0 的格子變灰。
  3. 灰格從 Step 4 的 diff 分配與 Simulation 範圍中移除。
  4. Step 1 的重疊連結保持原樣，用鎖頭圖示表示「不改幾何」。
- **旁白（zh-TW）：**
  - SeeRegular.csv 來自面板的實測資料。
  - 如果某一格在每一幀都是零，代表它沒有訊號。
  - 這些格子會被標成 inactive。
  - Step 4 分配 diff 時會避開它們。
  - Simulation 也只計算 active 的格子。
  - 但 Step 1 的幾何重疊不會被改寫。
- **畫面標註：**
  - 「所有 frame 皆為 0 → inactive」
  - 影響：Step 4、Simulation
  - 不影響：Step 1 幾何
- **素材：** 依 `docs/guides/app-user-manual.md` 7.2 節。

### A05-freeform-rule：動畫：Freeform 方向怎麼判定

- **類型：** motion-canvas
- **預估：** 60 秒
- **目標：** 說明 XWay、YWay、XYWay、None 的判定方式與門檻的影響。
- **起始狀態：** 無。
- **畫面動作：**
  1. 一顆 CAD pad 跨在 2×2 格上，每格標上 CAD coverage。
  2. 按欄加總，最大欄占比為 colDominance。按列加總，最大列占比為 rowDominance。
  3. 顯示 `xSpread = 1 − colDominance`、`ySpread = 1 − rowDominance`。
  4. 一條門檻線（Freeform TH）左右移動。
  5. 依結果分別亮起 None、XWay、YWay、XYWay 四個標籤。
  6. 補一個反例：pad 只多出一點尾巴，spread 低於門檻，結果是 None。
- **旁白（zh-TW）：**
  - Step 2 看 pad 的面積分散在哪個方向。
  - 先把覆蓋率按欄和按列加總。
  - 最大那一欄占多少，決定 X 方向的集中度。
  - 一減集中度，就是這個方向的分散度。
  - 分散度都低於門檻，就是 None。
  - 兩個方向都超過門檻，而且開啟 XY，就是 XYWay。
  - 否則取分散較大的方向，XWay 或 YWay。
  - 只多出一點尾巴的 pad，不應該被判成 Freeform。
- **畫面標註：**
  - `xSpread = 1 − colDominance`
  - `ySpread = 1 − rowDominance`
  - `Freeform TH (dominant axis ratio)`
  - 四個結果標籤：None / XWay / YWay / XYWay
- **素材：** 依 `docs/core/freeform-helper-algorithms.md` 第 4 節。

### R06-step2-freeform：Step 2：Freeform tagging

- **類型：** screen
- **預估：** 75 秒
- **目標：** 自動偵測 freeform，再手動修正個別格子。
- **起始狀態：** 已載入 `project_3635.json`，按 Step 2 的清除圖示。
- **重置：** 按 Step 2 的清除圖示（`Clear Step 2 result and all downstream results.`）。
- **畫面動作：**
  1. 點 `Step 2 · Freeform tagging`，指出說明文字。
  2. 按 `Detect freeform`。
  3. 點回 Step 2，看摘要中各類型的數量。
  4. 在畫布上框選幾顆 regular pad（必要時按住 `Alt` 優先選 regular）。
  5. 在 `Manual override (selected regular pads)` 按 `X`，再按 `Clear override` 還原。
  6. 打開 `Settings > Step 2 · Freeform`，指出 `Freeform TH (dominant axis ratio)` 與 `Load auto-run Step 2`，按 `Cancel`。
- **旁白（zh-TW）：**
  - Step 2 只幫 Regular 格子加上 Freeform 標籤。
  - 它不會改動 Step 1 的重疊連結。
  - 按 Detect freeform 自動偵測。
  - 摘要會列出 XWay、YWay、XYWay 的數量。
  - 自動結果不對時，可以手動覆寫。
  - 按住 Alt 點選，會優先選到 Regular 格子。
  - 選好後按 X、Y、XY 或 None 套用。
  - 門檻值在 Settings 的 Step 2 頁調整。
- **畫面標註：**
  - 「Step 2 labels regular pads (None/XWay/YWay/XYWay) without changing Step 1 links.」
  - `Detect freeform`
  - `None` / `X` / `Y` / `XY` / `Clear override`
  - `Alt` = Regular priority
- **素材：** 無。

### A06-notch-tofull：動畫：To Regular 與 To Full 三階段

- **類型：** motion-canvas
- **預估：** 75 秒
- **目標：** 讓觀眾看懂 Step 3 畫布上的 Stage 1、2、3 疊圖，以及 To Full 的檢查條件。
- **起始狀態：** 無。
- **畫面動作：**
  1. 一顆位於面板邊界的 CAD pad，蓋住邊界 regular 的一部分。
  2. 標示 To Regular：每格的 `overlap / regularArea` 加總。
  3. Stage 1（Seed）：只亮出 pad 與該格的重疊多邊形。
  4. Stage 2（Candidate）：亮出整個 regular 矩形。
  5. Stage 3（Final）：CAD 與擴展區合併成最終外框。
  6. 旁邊列出檢查清單，逐條打勾或打叉：是否邊界格、是否有來源面積、是否被其他 pad 擁有、是否還需要擴展。
  7. 示範一格因為被另一顆 pad 擁有而不擴展（`GATE_MULTI_OWNER`）。
- **旁白（zh-TW）：**
  - Step 3 計算 Notch 補償。
  - To Regular 看 pad 蓋住多少格子面積。
  - To Full 處理面板邊界上被切掉的格子。
  - Stage 1 是 pad 和格子的實際重疊。
  - Stage 2 是整個格子的候選範圍。
  - Stage 3 是合併後的最終外框。
  - 不是每一格都能擴展，要通過一組檢查。
  - 必須是邊界格，而且 pad 真的有蓋到它。
  - 如果格子同時屬於其他 pad，就不擴展。
- **畫面標註：**
  - `1 · Seed` / `2 · Candidate` / `3 · Final`
  - 檢查代碼：`GATE_NOT_BOUNDARY`、`GATE_SOURCE_EMPTY`、`GATE_MULTI_OWNER`、`NO_EXPANSION_NEEDED`、`EXPAND_CLEAR_PATH`
- **素材：** 依 `docs/core/freeform-helper-algorithms.md` 第 5 節與 `docs/reference/notch-system-reference.md`。

### R07-step3-notch：Step 3：Notch 預覽與安全設定

- **類型：** screen
- **預估：** 90 秒
- **目標：** 在實際畫布上檢查 Stage 疊圖，並認識匯出前要確認的安全設定。
- **起始狀態：** 已載入 `project_3635.json`，Step 1 和 Step 2 已完成（Load 後自動重播）。
- **重置：** 按 Step 3 的清除圖示，再重新 `Load Project`。
- **畫面動作：**
  1. 點 `Step 3 · Notch 2.2 compensation`。
  2. 在畫布選一顆邊界的 CAD pad。
  3. 用 `Previous stage` / `Next stage` 箭頭切換 `1 · Seed`、`2 · Candidate`、`3 · Final`。
  4. 開啟 `Auto` 自動輪播，3 秒後關閉。
  5. 按 `To Full expansion result` 的詳細切換，展開說明。
  6. 指出 `Export-critical guards`：`Target coverage guard`、`Target cap (%)`、`Boundary virtual-area cap`、`Virtual cap (%)`。
  7. 雙擊同一顆 pad，在 pad info 的 Notch 區對照數字。
- **旁白（zh-TW）：**
  - Step 3 會在畫布上疊出 Notch 預覽。
  - 先選一顆位在邊界的 pad。
  - 用左右箭頭切換三個階段。
  - Auto 可以自動輪播，方便簡報時說明。
  - 這兩組 guard 會影響最終匯出的 C 檔。
  - Target cap 避免太多來源疊到同一個 diff。
  - Virtual cap 限制邊界外擴的虛擬面積。
  - 交給韌體之前，一定要確認這兩個設定。
- **畫面標註：**
  - `Previous stage` / `Next stage` / `Auto`
  - `Export-critical guards`
  - 引用 UI 文字：「These Step 3 guards affect Simulation and Step 5 C export. Review them before FW handoff.」
- **素材：** 無。

### R08-step4-diagnostics：Step 4：Index diagnostics

- **類型：** screen
- **預估：** 90 秒
- **目標：** 找出 diff 編號有疑慮的 CAD pad，並理解 raw best、masked best、suggested 的差別。
- **起始狀態：** 已載入 `project_3635.json`，Step 1 到 Step 3 已完成，且已啟用 mask。
- **重置：** 按 Step 4 的清除圖示（`Clear Step 4 result and downstream Step 5/6 results.`）。
- **畫面動作：**
  1. 點 `Step 4 · Index diagnostics`，按 `Analyze mapping`。
  2. 報告視窗開啟，指出上方摘要 chips。
  3. 點左側清單一列，右側 Inspector 顯示 `Current`、`Raw best`、`Masked best`、`Suggested`、`Reason`。
  4. 按 `Locate`，主畫布定位到該 CAD pad。
  5. 在報告右側指出 `Apply diff repair`，本段不實際套用。
  6. 回到 Step 4 面板，在 `Duplicate CAD Output FW Diff groups` 選一組，指出 `Offset selected CAD Output FW Diff`（若 3635 沒有重複，改為口頭帶過）。
- **旁白（zh-TW）：**
  - Step 4 檢查每顆 CAD pad 的韌體 diff 編號。
  - 按 Analyze mapping 會打開報告視窗。
  - 上方的摘要會列出低信心、模糊與未對應的數量。
  - 點一列，右邊會顯示判斷依據。
  - Raw best 是純幾何的最佳對應。
  - Masked best 是套用可見遮罩之後的結果。
  - Suggested 是系統建議使用的 diff。
  - 三者不一致時，看 Reason 欄位找原因。
  - 確認後再決定要不要套用修正。
- **畫面標註：**
  - `Analyze mapping`
  - `Current` / `Raw best` / `Masked best` / `Suggested` / `Reason`
  - `Locate` / `Focus`
  - `Apply diff repair`（本段不套用）
- **素材：** 無。

### R09-step5-notch-table：Step 5：Notch 表審閱與匯出

- **類型：** screen
- **預估：** 120 秒
- **目標：** 在 `Export Notch Rows` 視窗審閱 Notch 表，並匯出 CSV 與 C 檔。
- **起始狀態：** 已載入 `project_3635.json`，Step 1 到 Step 4 已完成。
- **重置：** 按 Step 5 的清除圖示（`Clear Step 5 export result and Step 6 validation result.`）。輸出檔寫到暫存資料夾，錄完刪除。
- **畫面動作：**
  1. 點 `Step 5 · Notch export`。
  2. 指出 `Export versions`（v2.1、v2.2 都勾）與 profile（`Release` / `Debug`）。
  3. 按 `Export`，`Export Notch Rows` 視窗開啟。
  4. 介紹欄位：`#`、`IC`、`Diff`、`REG -> CAD`、`Status`。
  5. 點 `Diff` 欄的篩選圖示，用 Excel 式篩選選一個 diff，再按重設。
  6. 依序點 `Warning`、`No CAD`、`Linked` 快速篩選，再清除。
  7. 點一列，右側顯示 `Payload` 與 mapping。按 `Locate` 回到畫布。
  8. 回主畫布點一顆 CAD pad，按 `Select linked rows`，表格選到對應的列。
  9. `Export file` 選 `CSV review (.csv)` 匯出一次，再選 `C v2.2 (.c)` 匯出一次。
  10. 用 `Hide panel (inspect AA)` 收起視窗，再按 `Ctrl+Shift+E` 叫回。
- **旁白（zh-TW）：**
  - Step 5 產生最終的 Notch 表。
  - 預設同時輸出 v2.1 和 v2.2，v2.2 是主線。
  - Release 會移除沒有作用的列，適合給韌體。
  - Debug 保留所有列，方便追查。
  - 每一列代表一筆 IC 加 diff 的補償資料。
  - 每個欄位都能像 Excel 一樣篩選。
  - Warning 和 No CAD 可以快速找出要注意的列。
  - 點一列看 payload，按 Locate 回到畫布。
  - 反過來，選 pad 也能找到它對應的列。
  - CSV 只用來審閱和比對差異。
  - 韌體要直接匯入的是 C v2.1 或 C v2.2。
  - 視窗收起後，按 Ctrl+Shift+E 可以叫回來。
- **畫面標註：**
  - 視窗標題 `Export Notch Rows`
  - `Release` = 移除 no-op 列 / `Debug` = 全部保留
  - 欄位：`#` `IC` `Diff` `REG -> CAD` `Status`
  - 快速篩選：`Warning` / `No CAD` / `Linked` / `Legacy`
  - 匯出格式：`CSV review (.csv)` / `C v2.1 (.c)` / `C v2.2 (.c)`
  - 3635 參考值（Release）：v2.1 692 nodes、v2.2 548 nodes（`docs/performance/regression-baseline-3635.md`）
  - 快捷鍵：`Ctrl+Shift+E` Restore
- **素材：** 無。匯出的 `.c` 不得出現在影片以外的地方。

### R10-save-load：儲存與載入專案

- **類型：** screen
- **預估：** 60 秒
- **目標：** 儲存工作狀態，並確認載入後流程會自動重播。
- **起始狀態：** 已載入 `project_3635.json`。
- **重置：** 刪除錄影產生的專案副本，再重新 `Load Project` 原始檔。
- **畫面動作：**
  1. 在畫布上隱藏一顆 pad（製造一個變更）。
  2. 按 `Ctrl+S`，畫面上方出現儲存結果 toast。
  3. 說明：`Save Project` 會存 grid、freeform override、DXF edit、notch 設定和流程狀態。
  4. `File -> Load Project` 載入剛存的副本。
  5. 鏡頭停在右側 Step rail，看流程自動重播，畫布恢復到儲存時的樣子。
- **旁白（zh-TW）：**
  - 做到一半要存檔，直接按 Ctrl+S。
  - 畫面上方會跳出儲存結果。
  - 專案檔會記住 grid、覆寫、DXF 編輯和 Notch 設定。
  - 用 Load Project 打開專案檔。
  - 載入後流程會自動重播到儲存時的狀態。
  - App 層設定要等下一次儲存成功後才會寫入。
- **畫面標註：**
  - `Ctrl+S` → top toast
  - `File -> Save Project` / `File -> Load Project`
  - 「載入後 app 設定延後寫入，直到下一次 Save Project 成功」
- **素材：** 錄影用的專案副本（暫存資料夾，不提交）。

### R11-simulation：Simulation 頁（選用）

- **類型：** screen
- **預估：** 75 秒
- **目標：** 用 Before / After / Delta 確認 Notch 表套用後的數值分布。
- **起始狀態：** 已載入 `project_3635.json`，Step 1 到 Step 5 已完成，mask 已啟用。
- **重置：** 在 Simulation 頁按 `Clear CSV`，切回 `Before`。
- **畫面動作：**
  1. 切到 `Simulation` 頁。
  2. 指出 `Input source`，選 `Global value`。
  3. 按鍵盤 `B`、`A`、`D` 切換 Before、After、Delta。
  4. 按 `C` 只看有變化的格子。
  5. `Import CSV` 匯入一組 frame，按播放，調整 `FPS`。
  6. 指出 `Simulation safety` 卡片的 EMS cap 與超標數量。
- **旁白（zh-TW）：**
  - Simulation 頁和主畫面共用同一個 grid。
  - 它直接使用匯出的 Notch 表來計算。
  - 按 B、A、D 切換套用前、套用後和差值。
  - 按 C 只顯示有變化的格子。
  - 也可以匯入 CSV，逐幀播放實測數值。
  - 播放只改變顯示的數字，不會重建 grid。
  - Simulation safety 會列出超過 EMS 上限的格子。
- **畫面標註：**
  - 鍵盤：`B` Before、`A` After、`D` Delta、`C` Changed、`V` 切換 Before/After
  - 「Simulation uses exported notch table payload.」
  - `Import CSV` / `FPS` / `Clear CSV`
- **素材：** 3635 的 CSV frames（私有；若無，改用 `Global value` 示範即可）。

### R12-runtime-cli：進階：Runtime CLI 查詢（選用）

- **類型：** screen
- **畫面配置：** PowerShell 與 UI 並排
- **預估：** 60 秒
- **目標：** 讓工程師知道可以用指令查詢與驅動執行中的 UI，方便除錯與自動化。
- **起始狀態：** UI 已啟動（可為空白狀態），旁邊開一個 PowerShell 視窗在 repo 根目錄。
- **重置：** 重新啟動 UI。
- **畫面動作：**
  1. 執行 `... -- query help`，捲動列出的指令。
  2. 執行 `... -- query load-project --path example/BOE36.35/project_3635.json`，UI 同步載入。
  3. 執行 `... -- query run-step --step 1 --timeout-ms 120000`，UI 的 Step 1 完成。
  4. 執行 `... -- query status`，指出回傳的流程狀態。
  5. 在 UI 選一顆 pad，執行 `... -- query pad`。
- **旁白（zh-TW）：**
  - UI 執行時，可以從另一個終端下指令查詢。
  - query help 會列出所有可用指令。
  - load-project 和 run-step 可以直接驅動流程。
  - status 回報目前每一步的狀態。
  - pad、notch 等指令適合除錯和寫測試。
  - 完整指令表在 runtime-cli-plan 文件裡。
- **畫面標註：**
  - `dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query <command>`
  - 常用：`status` / `pad` / `notch` / `notch-stage` / `notch-validation` / `simulation`
  - 文件：`docs/reference/runtime-cli-plan.md`
- **素材：** 無。

### S99-outro：結尾：驗證清單

- **類型：** motion-canvas
- **預估：** 30 秒
- **目標：** 收斂成一張第一次驗證時可照做的清單。
- **起始狀態：** 無。
- **畫面動作：**
  1. 九項清單逐條打勾（依 `docs/guides/app-user-manual.md` 第 12 節）。
  2. 最後顯示延伸閱讀文件路徑。
- **旁白（zh-TW）：**
  - 最後整理一份驗證清單。
  - 確認 grid、AA 和 cascade 設定正確。
  - 檢查 Step 1 重疊與 Step 2 的 Freeform。
  - 確認 Step 3 疊圖與 Step 4 的診斷結果。
  - 匯出前檢查 Notch 表的每一列。
  - 詳細規則請看使用手冊和 Notch 參考文件。
- **畫面標註：**
  - 清單九項（手冊第 12 節）
  - `docs/guides/app-user-manual.md`
  - `docs/reference/notch-system-reference.md`
- **素材：** 無。

---

## 4. 製作時程（交付 11-25）

| 日期 | 工作 |
|---|---|
| 11-06 ～ 11-07 | Owner 審閱分鏡稿，確認段落與旁白 |
| 11-10 ～ 11-12 | 錄製 R01 ～ R12（每段獨立錄，可分天） |
| 11-10 ～ 11-14 | 製作 Motion Canvas 段落 S00、A01 ～ A06、S99 |
| 11-17 ～ 11-18 | zh-TW 旁白配音與字幕時間軸 |
| 11-19 ～ 11-20 | 粗剪，依 `video-timeline.json` 排序 |
| 11-21 | 內部試看，收集修改意見 |
| 11-24 | 重錄有問題的段落、定剪 |
| 11-25 | 交付 |

## 5. 待確認事項

1. 3635 原始 DXF 在 `example/BOE36.35/` 的確切檔名（R02 需要；畫面上會裁切檔名）。
2. 3635 是否有 `Duplicate CAD Output FW Diff groups`。沒有的話，R08 第 5 步改為口頭說明。
3. 是否有可公開於內部的 3635 CSV frames 供 R11 播放。
4. R11、R12 要放進正式版，還是只放在進階版。
5. 影片存放位置與觀看權限（建議限內部群組）。

## 6. 依據

- `README.md`、`README.zh-TW.md`
- `docs/guides/app-user-manual.md`
- `docs/core/freeform-helper-algorithms.md`
- `docs/performance/regression-baseline-3635.md`
- `docs/reference/runtime-cli-plan.md`
- `src/FreeformHelper.UI/Views/**/*.axaml`（按鈕、標題與 tooltip 文字）
- `src/FreeformHelper.UI/Views/FreeformHelperView.InputAndShortcuts.cs`（快捷鍵）
- `src/FreeformHelper.Application/Services/AfeMapper.cs`、`src/FreeformHelper.Application/Settings/*.cs`（列舉值與 diff 編號規則）
