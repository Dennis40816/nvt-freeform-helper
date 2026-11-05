# FreeformHelper 使用手冊（3635 面板專案，草稿）

> 狀態：草稿。內容依 `main` 分支的 UI 程式碼整理，尚未在實機截圖驗證。
> 讀者：接手 3635 面板專案的內部工程師。
> 行為不明確的地方記錄在 `cloud-notes/user-guide-gaps.md`。

## 0. 本手冊的用詞

本手冊對同一件事只用一個詞。

| 用詞 | 意思 |
| --- | --- |
| 你 | 操作 FreeformHelper 的工程師 |
| 程式 | FreeformHelper UI（`FreeformHelper.UI.exe`） |
| project 檔 | `Save Project` 存出的 `.json` 檔 |
| CAD pad | 從 DXF 匯入的 pad |
| regular pad | 由 AA 尺寸與 channel 數產生的格狀 pad |
| layer | DXF 內的圖層 |
| notch table | Step 5 產生、可匯出成 C 或 CSV 的表 |
| 畫布 | 中央顯示 CAD pad 與 regular pad 的區域 |

UI 上的文字一律保留英文原樣，例如 `Open DXF`、`Match overlaps`。

## 1. 畫面總覽

程式主畫面分成五個區域。

1. 上方選單：`File`、`Export`、`Settings`。
2. 左側面板 `DXF Settings`：DXF 匯入、檢查、編輯與 layer 清單。
3. 中央畫布：顯示 CAD pad、regular pad 與各種 overlay。
4. 右側面板：`Settings` 分頁放各 Step，`Inspector` 分頁放 pad 細節。
5. 下方 Console：顯示程式的 log。

視窗最上方另有頁籤可切換頁面。

- `Freeform Helper`：主要工作頁，本手冊大多在這頁操作。
- `Simulation`：用 regular pad 數值比較套表前後的結果。
- `Coordinate`：座標規劃頁。
- `How To Use`：程式內建的快速說明。
- `Dev`：開發者預覽用，一般使用者不需要開。

[截圖: Freeform Helper 主畫面，已載入 3635 project，五個區域加上編號標註]

左右面板可以收起。
你按面板右上角的箭頭就會收起它。
收起後，畫布邊緣會出現按鈕讓你展開。

## 2. 建議的 3635 操作順序

第一次接手 3635 專案時，照下列順序做最快。

1. `File > Load Project`，開啟 3635 的 project 檔。
2. 確認 `Settings > General` 的 IC 與 AA 設定。
3. 在 Step 1 按 `Match overlaps`。
4. 在 Step 2 按 `Detect freeform`，再手動修正。
5. 在 Step 3 檢查 notch 預覽與 guard 設定。
6. 在 Step 5 按 `Export`，挑選列後匯出 C 檔。
7. `File > Save Project` 存回 project 檔。

有 `example/` 權限的工程師可以直接開範例 project 檔。
範例路徑是 `example/BOE36.35/project_3635.json`。
`example/` 是 private submodule，內容不可外流。

## 3. 開啟與建立 project

### 3.1 用途

project 檔保存一個面板專案的完整工作狀態。
它包含下列內容。

- grid 設定
- DXF 編輯
- freeform 標記
- notch 設定
- workflow 狀態

程式沒有 `New Project` 指令。
你要建立新 project，就先開 DXF，再用 `Save Project` 存檔。

### 3.2 步驟：建立新 project

1. 你在上方選單按 `File > Open DXF`。
2. 你在檔案對話框選一個 `.dxf` 檔。
3. 程式匯入 DXF，並重建 regular grid。
4. 你調整設定、執行各 Step。
5. 你按 `File > Save Project` 或 `Ctrl+S`。
6. 程式開出存檔對話框，預設檔名是 `freeform_helper_project.json`。
7. 程式詢問 `Embed DXF`，你選 `Embed` 或 `Skip`。

### 3.3 步驟：開啟既有 project

1. 你按 `File > Load Project`。
2. 你在對話框選 project 檔（`.json`）。
3. 程式載入 DXF、還原設定與 DXF 編輯。
4. 程式自動重跑 Step 1。
5. 若 `Load auto-run Step 2` 為開啟，程式也會自動重跑 Step 2。

### 3.4 你會看到

- 載入 DXF 時，畫布上方會出現 loading spinner。
- 載入完成後，畫布會自動 fit 到全部內容。
- 狀態列會顯示 `Project loaded: <路徑>` 或 `Project saved: <路徑>`。
- 用 `Ctrl+S` 存檔時，視窗上方會跳出 toast 顯示結果。
- 關閉視窗時若有未存變更，程式會跳出 `Unsaved project` 對話框。

[截圖: Embed DXF 確認對話框]
[截圖: 關閉視窗時的 Unsaved project 對話框，顯示 Save and exit 與 Exit without saving]

### 3.5 常見錯誤

- 以為 `Ctrl+S` 會直接覆蓋原檔。
  程式每次存檔都會開存檔對話框，你要自己選路徑。
- 選了 `Skip` 卻把 project 檔交給別人。
  `Skip` 只存 DXF 路徑，對方電腦上沒有該 DXF 就無法載入。
- 以為 embed 過的 project 一定用內嵌 DXF。
  程式載入時會先讀原 DXF 路徑，檔案存在就用磁碟上的版本。
  你改過原 DXF，就會載入改過的內容。
- 開新 DXF 前沒有先存檔。
  `Open DXF` 會清掉目前的 DXF 編輯狀態。

### 3.6 結果存放位置

- project 檔：你在存檔對話框選的路徑。
- 若選 `Embed`，DXF 與 `SeeRegular.csv` mask 也會寫進同一個 project 檔。
- 程式層級設定：`%LOCALAPPDATA%\FreeformHelper\app-general-settings.json`。
- `Load Project` 之後，程式層級設定會延後寫入。
  下一次 `Save Project` 成功時，程式才把它寫回上述檔案。

## 4. DXF 匯入與 layer 清單

### 4.1 用途

左側 `DXF Settings` 面板負責 DXF 的匯入、檢查與 layer 顯示。
你在這裡決定哪些 CAD pad 進入後續流程。

### 4.2 步驟：匯入 DXF

1. 你先確認 `Import` 區的 `Only closed polylines` 勾選狀態。
2. 你按 `File > Open DXF` 選檔。
3. 程式依 `Settings > General` 的匯入設定讀取 DXF。

匯入相關設定共有三個。

- `Only closed polylines`：只匯入封閉的 polyline。
- `Import block polylines`：也匯入 DXF BLOCK 內的 polyline。
- `Auto bounds from visible layers`：切換 layer 後重算 DXF 邊界。

改了 `Only closed polylines` 之後，你必須重新開 DXF 才會生效。

### 4.3 步驟：切換 layer

1. 你在 `Layers` 清單點某個 layer，切換它的顯示。
2. 你按住 `Ctrl` 再點某個 layer，就只顯示這一個 layer。
3. 你按清單上方的總開關，一次開啟或關閉全部 layer。

`Settings > General > Layer categories` 也能依內容類別批次切換 layer。

### 4.4 步驟：檢查 DXF 重疊

1. 你在 `DXF check` 區按 `Overlap check`。
2. 程式掃描重複與重疊的 CAD pad，並顯示進度條。
3. 有問題時，畫面顯示 `Issues: <數量>`。
4. 你可以用旁邊的三個按鈕處理結果。
   - 開細節視窗（可複製內容）。
   - 重新選取有問題的 CAD pad。
   - 清除高亮與選取。

[截圖: 左側面板，Overlap check 後顯示 Issues 數量與三個按鈕]

### 4.5 步驟：編輯 DXF

`DXF edits` 區提供下列操作。

- `Hide selected`：隱藏選取的 CAD pad，快捷鍵是 `Delete`。
- `Undo hide`：還原最後一批隱藏的 CAD pad。
- `Show all`：還原全部手動隱藏的 CAD pad。
- `Combine`：把選取的 CAD pad 合併成一個。
- `Split selected`：把合併的 CAD pad 拆回原狀。
- `Rotate`：把選取範圍或指定 layer 當成一組旋轉。
- `Move to existing layer`：把選取的 CAD pad 移到既有 layer。
- `Create new layer`：建立新 layer 並移入選取的 CAD pad。

`Summary` 區的徽章會開啟變更清單視窗。
你可以在視窗內逐筆定位或還原。
重設按鈕會清掉所有 DXF 編輯，程式會先跳出確認對話框。

### 4.6 你會看到

- `Layers` 清單每列有一個眼睛圖示，代表顯示狀態。
- 總開關在全部開啟時顯示 `All on`，否則顯示 `All off`。
- 還沒載入 DXF 時，總開關顯示 `No layers`。
- `Bounds source layer` 下拉選單決定 DXF 邊界取自哪個 layer。

[截圖: Layers 清單，部分 layer 關閉，總開關顯示 All off]

### 4.7 常見錯誤

- 把 `All off` 當成「全部都關了」。
  它其實代表「沒有全部開啟」。
- 以為關掉 layer 就等於刪除 CAD pad。
  關 layer 只影響顯示與部分流程。
  只有 DXF 編輯會改變 CAD pad 集合。
- `Bounds source layer` 選錯 layer。
  它只在 `Grid alignment mode` 為 `Use DXF bounds` 時有作用。

### 4.8 結果存放位置

- layer 顯示與 DXF 編輯存在 project 檔內。
- 你可以用 `Export > Export DXF (Visible Layers)` 匯出目前可見的 CAD pad。
  預設檔名是 `cad_visible_layers.dxf`。
- 你可以用 `Export > Export DXF (All Layers)` 匯出未隱藏的全部 CAD pad。
  預設檔名是 `cad_all_layers.dxf`。

## 5. Pad 對應（Step 1 · Geometry match）

### 5.1 用途

Step 1 建立 CAD pad 與 regular pad 之間的重疊關係。
後面所有 Step 都依賴 Step 1 的結果。
一個 CAD pad 可以對應多個 regular pad。

### 5.2 步驟

1. 你在右側面板 `Settings` 分頁找到 `Step 1 · Geometry match`。
2. 你按 `Match overlaps`。
3. 程式計算重疊並更新 Step 1 摘要。
4. 你視需要調整 `Unmatched diagnostics threshold`。

若手上有 `SeeRegular.csv`，你可以再做下列步驟。

1. 你按 `Import mask`，選 `SeeRegular.csv`。
2. 你開啟 `Use Regular Visibility Mask`。
3. 不再需要時，你按 `Clear mask`。

### 5.3 你會看到

- Step 1 卡片顯示對應摘要。
- 畫布可用 `Highlight unmatched` 標出未對應的 regular pad。
- 你雙擊或右鍵某個 pad，會開出 pad info 視窗。
- 右側 `Inspector` 分頁顯示目前選取 pad 的細節。

[截圖: Step 1 卡片，Match overlaps 執行完成並顯示摘要]
[截圖: 畫布上的 pad info 視窗，顯示一個 CAD pad 對應的 regular pad]

### 5.4 常見錯誤

- 以為調 `Unmatched diagnostics threshold` 會改變對應結果。
  它只影響診斷顯示，不會改寫 Step 1 的連結。
- 以為程式會自動找 `SeeRegular.csv`。
  程式只用你手動匯入的檔案，或 project 檔內已存的 mask。
- 改了 grid 或 DXF 之後沒有重跑 Step 1。
  按 Step 卡片右上的清除圖示，會一併清掉下游 Step 的結果。

### 5.5 結果存放位置

- Step 1 結果在記憶體中，載入 project 時程式會重算。
- mask 的路徑與開關存在 project 檔。
- 選了 `Embed` 時，mask 內容也存在 project 檔。

## 6. Freeform 標記（Step 2 · Freeform tagging）

### 6.1 用途

Step 2 把每個 regular pad 標成四種類型之一。
四種類型是 `None`、`XWay`、`YWay`、`XYWay`。
Step 2 不會改變 Step 1 的對應連結。

### 6.2 步驟

1. 你確認 Step 1 已完成。
2. 你在 `Step 2 · Freeform tagging` 按 `Detect freeform`。
3. 程式依 `Freeform threshold` 自動標記。
4. 你在畫布選取要修正的 regular pad。
5. 你在 `Manual override (selected regular pads)` 按 `None`、`X`、`Y` 或 `XY`。

### 6.3 你會看到

- Step 2 卡片顯示 `Type`、`Count`、`Ratio` 統計表。
- 畫布可用 `Highlight freeform` 標出 freeform pad。

[截圖: Step 2 卡片，Detect freeform 後的類型統計表]
[截圖: 畫布選取數個 regular pad，Manual override 按鈕列]

### 6.4 常見錯誤

- 沒跑 Step 1 就按 `Detect freeform`。
  程式會顯示 `Step 2: run Step 1 (Match) first.`。
- 選到 CAD pad 再按 override。
  override 只作用在選取的 regular pad。
- 沒注意 `Freeform TH (dominant axis ratio)` 的值。
  這個門檻大幅影響自動標記結果。

### 6.5 結果存放位置

- 手動 override 存在 project 檔。
- 自動標記的結果在記憶體中。
- `Load auto-run Step 2` 開啟時，程式載入 project 後會自動重跑。

## 7. Notch 預覽（Step 3 · Notch 2.2 compensation）

### 7.1 用途

Step 3 讓你在匯出前檢查 notch 補償行為。
它的 guard 設定會影響 Simulation 與 C 匯出。

### 7.2 步驟

1. 你在畫布選一個 CAD pad。
2. 你在 `Step 3 · Notch 2.2 compensation` 查看 `Review before export`。
3. 你確認 `Target coverage guard` 與 `Boundary virtual-area cap` 的開關與百分比。
4. 你用左右箭頭切換 `To Full layer stage`。
5. 你可以開啟 `Auto` 讓三個 stage 自動輪播。

### 7.3 你會看到

畫布 overlay 分成三個 stage。

1. `1 · Seed`：CAD 多邊形重疊來源。
2. `2 · Candidate`：邊界候選 regular pad。
3. `3 · Final`：To Full 擴張結果。

[截圖: Step 3 卡片與畫布，stage 3 Final overlay 顯示中]

### 7.4 常見錯誤

- 沒選 CAD pad 就期待看到 overlay。
  預覽跟著目前選取的 CAD pad 走。
- 以為切換預覽會改變輸出。
  `AA preview display` 的開關只影響顯示。

### 7.5 結果存放位置

Step 3 的設定存在 project 檔。

## 8. Notch table 與 C／CSV 匯出（Step 5 · Notch export）

### 8.1 用途

Step 5 產生 notch table，讓你挑選列後輸出檔案。
韌體直接匯入要用 C 檔。
CSV 只用於人工檢查與比對。

可選的輸出格式有三種。

- `CSV review (.csv)`
- `C v2.1 (.c)`
- `C v2.2 (.c)`

### 8.2 步驟

1. 你確認 Step 1 已完成。
2. 你在 `Settings > Step 5 · Export` 確認 `Export versions`、`Export type` 與 `C export profile`。
3. 你在右側 Step 5 卡片按 `Export`，或按 `Export > Export Notch`。
4. 程式產生 notch table，並開出 `Export Notch Rows` 視窗。
5. 你在視窗上方選 `Notch version` 與 `Export format`。
6. 你勾選要輸出的列。
7. 你按 `Export selected`。
8. 程式開出存檔對話框，你選路徑。
9. 程式寫檔，狀態列顯示匯出的列數。

`C export profile` 有兩個選項。

- `Release (minimal FW)`：交給韌體的正式版本。
- `Debug (trace + sim mask)`：附帶追蹤資訊的除錯版本。

### 8.3 你會看到

`Export Notch Rows` 視窗分成左右兩半。

- 左側是列清單，欄位有 `#`、`IC`、`Diff`、`Combine`、`REG -> CAD`、`Status`。
- 每個欄位標題都能開 Excel 風格的篩選。
- 上方的篩選按鈕可只顯示特定狀態的列。
- 右側 `Inspector` 顯示目前列的 payload、mapping 與分析。
- `Simulation safety` 區顯示 Simulation 安全檢查結果。

你可以按 `Hide panel (inspect AA)` 暫時收起視窗，回畫布檢查。
收起後，你按畫布上的 `Restore (Ctrl+Shift+E)` 或 `Ctrl+Shift+E` 叫回視窗。

[截圖: Export Notch Rows 視窗，左側列清單與右側 Inspector]
[截圖: 收起匯出視窗後，畫布上的 Restore (Ctrl+Shift+E) 按鈕]

### 8.4 常見錯誤

- 把 CSV 檔交給韌體。
  CSV 不是韌體匯入格式，韌體要用 C 檔。
- 在 `Notch version` 只選一個版本後直接存 project。
  程式會把另一個版本在 `Export versions` 中關掉，並在下次存檔時寫入。
- Simulation 安全檢查不通過。
  程式會在 stage 3 擋下匯出。
  你要先回 Simulation 頁處理。
- 一列都沒勾就按匯出。
  程式會顯示 `no rows selected` 並取消匯出。
- 把 Step 4 當成必要步驟。
  Step 4 是選用的診斷，不是匯出前提。

### 8.5 結果存放位置

- 檔案存到你在存檔對話框選的路徑。
- CSV 預設檔名是 `notch_table.csv`。
- C 檔只含單一版本時，預設檔名是 `notch_v2.1.c` 或 `notch_v2.2.c`。
- 3635 的參考 C 檔在 `example/BOE36.35/` 下（需 `example/` 權限）。

## 9. Layer 圖片匯出

### 9.1 用途

`Export DXF Layer Image` 把單一 layer 的 CAD pad 輪廓輸出成圖片。
它適合放進報告或交給其他團隊比對外形。

### 9.2 步驟

1. 你確認已載入 DXF。
2. 你按 `Export > Export DXF Layer Image`。
3. 你在 `Export DXF layer image` 視窗設定下列欄位。
   - `DXF layer`：要輸出的 layer。
   - `X pixels`、`Y pixels`：內部幾何的解析度。
   - `Line width (px)`：輪廓線寬。
   - `Padding X (px)`、`Padding Y (px)`：四周留白。
   - `Format`：`PNG`、`BMP` 或 `JPG`。
   - 主題：`Dark` 或 `Light`。
4. 你按 `Export`。
5. 程式開出存檔對話框，你選路徑。

### 9.3 你會看到

- 輸出圖片的寬度是 `X pixels + 2 × Padding X`。
- 輸出圖片的高度是 `Y pixels + 2 × Padding Y`。
- `Dark` 主題是深灰底加白色輪廓。
- 完成後，狀態列顯示格式、layer 名稱與 pad 數量。

[截圖: Export DXF layer image 視窗，已填入 3635 常用參數]

### 9.4 常見錯誤

- 以為 `X pixels` 就是最終圖寬。
  最終圖寬還要加上兩側的 padding。
- 沒載入 DXF 就按匯出。
  程式會跳出 `DXF not loaded` 警告。
- 以為 layer 關閉顯示後就不會出現在清單中。
  清單依未隱藏的 CAD pad 產生，與 layer 顯示開關無關。

### 9.5 結果存放位置

- 圖片存到你在存檔對話框選的路徑。
- 預設檔名是 `dxf_<layer 名稱>.<副檔名>`。
- 你上次用的匯出參數會被記住，下次開視窗時自動帶入。

## 10. 設定（Settings 視窗）

### 10.1 用途

`Settings` 視窗集中管理 General 與 Step 1～Step 5 的參數。
所有修改要按 `Save Settings` 才生效。

### 10.2 步驟

1. 你按 `Settings > Open Settings`。
   你也可以按右側各 Step 卡片上的設定圖示，直接跳到該段。
2. 你在左側 `Navigation` 選分頁。
3. 你修改數值。
4. 你按 `Save Settings` 套用，或按 `Cancel` 放棄。
5. 你要回到預設值，就按 `Reset all settings`。

分頁共有六個。

- `General`
- `Step 1 · Geometry`
- `Step 2 · Freeform`
- `Step 3 · Notch`
- `Step 4 · Index`
- `Step 5 · Export`

### 10.3 General：Grid and mapping

3635 專案最常改的是這一段。

- `Cascade`：IC cascade 數量。
- `Total X channels`、`Total Y channels`：由各 IC 的 channel 數自動算出。
- `AA size X (mm)`、`AA size Y (mm)`：面板 active area 尺寸。
- `Scan order`：diff 編號方向，可選 `L->R, T->B` 等四種。
- `Regular source`：`Generated grid` 或 `DXF layer pads`。
- `Regular layer`：`Regular source` 為 `DXF layer pads` 時使用的 layer。
- `Grid padding (%)`：grid 在 AA 內的留白比例。

### 10.4 General：Advanced mapping

`Advanced mapping` 只在座標輸出或面板對位時才需要調整。
它包含四個欄位。

- `Coordinate pixel X`：韌體回報的 X 座標寬度。
- `Coordinate pixel Y`：韌體回報的 Y 座標高度。
- `Panel bias X`：面板水平對位偏移。
- `Panel bias Y`：面板垂直對位偏移。

`Coordinate pixel X/Y` 只用於 `Coordinate` 頁與匯出診斷。
它們不會改變 regular grid 幾何或 notch 分配。
`Panel bias X/Y` 只在 `Grid alignment mode` 為 `Use panel AA` 時可編輯。

[截圖: Settings 視窗 General 分頁，Advanced mapping 區塊展開]

### 10.5 General：其他設定

- `Grid alignment mode`：`Use panel AA` 或 `Use DXF bounds`。
- `Log level`：Console 與 log 檔的最低等級。
- `Global font size (%)`：整體 UI 字級。
- `Highlight line width adjust`：選取 pad 的外框加粗量。
- `Sizing scope`：`Local` 調整選取的列或欄，`Global` 重排整個 grid。
- `Apply visual preferences on load`：載入 project 後套用你的顯示偏好。

### 10.6 Step 分頁重點

- `Step 1 · Geometry`：`Unmatched TH`，只影響診斷。
- `Step 2 · Freeform`：`Freeform TH`、`XY WAY EN`、`Edge spill EN`、`Load auto-run Step 2`。
- `Step 3 · Notch`：`Compensation model`、guard、cap 與預覽設定。
- `Step 4 · Index`：mapping 分數權重與 `CAD Output FW Diff` 編號設定。
- `Step 5 · Export`：輸出版本、格式、profile、`Null value` 與 gate 門檻。

`Compensation model` 有三個選項。

- `Current (Gain)`
- `Conservative (No Gain)`
- `Disabled`

### 10.7 你會看到

- 視窗底部顯示 `Changes apply only after Save Settings.`。
- 右側 `Settings` 分頁頂端的 `Project / grid` 卡片顯示 CAD、REG、IC、X、Y 數量。

### 10.8 常見錯誤

- 改完直接關視窗。
  沒按 `Save Settings` 的修改不會生效。
- 為了對位去改 `Coordinate pixel X/Y`。
  對位要改 `Panel bias X/Y`，而且要先選 `Use panel AA`。
- 改了 `Cascade` 或 AA 尺寸後沒有重跑 Step 1。

### 10.9 結果存放位置

- 專案相關設定存在 project 檔。
- 顯示與匯入偏好另存在 `%LOCALAPPDATA%\FreeformHelper\app-general-settings.json`。

## 11. 鍵盤與滑鼠快捷鍵

### 11.1 用途

快捷鍵讓你不用離開畫布就能完成常用操作。
在文字輸入框內，下列全域快捷鍵不會作用。

### 11.2 全域快捷鍵

| 按鍵 | 動作 |
| --- | --- |
| `Ctrl+S` | 存 project（開存檔對話框，結果用 toast 顯示） |
| `Ctrl+Z` | 復原上一個操作 |
| `Delete` | 隱藏選取的 CAD pad |
| `F` | 畫布 fit 到全部內容 |
| `Ctrl+A` | 選取全部可見 pad |
| `Ctrl+Shift+E` | 叫回收起的 `Export Notch Rows` 視窗 |

### 11.3 畫布操作

| 操作 | 動作 |
| --- | --- |
| 中鍵拖曳 | 平移 |
| `Space` + 左鍵拖曳 | 平移（筆電用） |
| 觸控板雙指滑動 | 平移 |
| 滾輪 | 以游標為中心縮放 |
| `Ctrl` + 滾輪 | 快速縮放 |
| `Shift` + 方向鍵 | 微調平移 |
| 左鍵點擊 | 選取 pad |
| `Ctrl` 或 `Shift` + 左鍵 | 加入或移除選取 |
| `Alt` + 左鍵 | CAD 與 regular 重疊時優先選 regular pad |
| 空白處拖曳 | 框選 |
| 雙擊 | 開 pad info |
| 右鍵 | 開 pad info |
| 點空白處 | 清除選取 |

### 11.4 數值欄位

- 你輸入數值後按 `Enter` 或點別處，數值才會套用。
- 你按 `Esc` 可放棄輸入。
- 你拖曳欄位右側的區域可快速調整。
- 你按住 `Alt` 再滾滾輪也能改數值。

### 11.5 常見錯誤

- 游標停在文字框時按 `Delete`，結果刪到文字。
  你要先點畫布，再按 `Delete`。
- 以為 `Ctrl+Z` 能復原所有操作。
  可復原的範圍以 `cloud-notes/user-guide-gaps.md` 的紀錄為準。

### 11.6 結果存放位置

快捷鍵不產生檔案。
`Ctrl+S` 的結果同 3.6 節。

## 12. Console 與 query 功能

### 12.1 用途

Console 讓你在程式內看 log。
query 讓你從另一個終端機查詢或操作執行中的程式。

### 12.2 步驟：使用 Console

1. 你按 Console 左上角的切換鈕，展開或收起 Console。
2. 你用 Console 工具列篩選與搜尋 log。
3. 你用標題列的按鈕複製選取內容、複製可見內容或存 log。
4. 存 log 時，程式開出 `Save console log` 對話框。

[截圖: 展開的 Console，顯示 Match overlaps 執行後的 log]

### 12.3 步驟：使用 query

1. 你先啟動程式 UI，並保持開啟。
2. 你另開一個 PowerShell。
3. 你執行 query 指令，例如：

```powershell
FreeformHelper.UI.exe query status
FreeformHelper.UI.exe query load-project --path example/BOE36.35/project_3635.json
FreeformHelper.UI.exe query run-step --step 1 --timeout-ms 120000
FreeformHelper.UI.exe query pad --cad-id <id>
FreeformHelper.UI.exe query export-notch --format c-v22 --path build/perf/notch_v2.2.c
```

從原始碼執行時，把 `FreeformHelper.UI.exe` 換成下列寫法。

```powershell
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query status
```

常用指令如下。

- `query help`：列出全部指令。
- `query status`：CAD 與 regular 數量、workflow 狀態。
- `query selection`：目前選取的 pad。
- `query terminal --tail N`：Console 最後 N 行。
- `query pad`、`query notch`、`query notch-stage`：單一 pad 的細節。
- `query run-step --step <1|2|3|4>`：執行指定 Step。
- `query clear-step --step <1|2|3|4|5>`：清除指定 Step 與下游結果。
- `query export-notch --format <csv|c-v21|c-v22> --path <路徑>`：直接匯出 notch table。
- `query simulation`：Simulation 頁的狀態。

完整清單在 `docs/reference/runtime-cli-plan.md`。

### 12.4 你會看到

- 每個 query 回傳一段 JSON。
- 成功時 JSON 含 `"ok": true` 與 `data`。
- 失敗時 JSON 含 `"ok": false` 與錯誤代碼。

### 12.5 常見錯誤

- 沒開 UI 就下 query。
  程式回傳 `INSTANCE_NOT_RUNNING`。
- 沒選 CAD pad 就跑 `run-step --step 3`。
  程式回傳 `STEP_NOT_READY`。
- 沒跑 Step 1 就跑 `run-step --step 4`。
  程式回傳 `STEP_NOT_READY`。
- 長時間的 Step 沒加 `--timeout-ms`。
  預設逾時只有 1500 ms，程式會回傳 `IPC_TIMEOUT`。
- 以為 `export-notch` 會開挑選視窗。
  它直接寫檔，不經過 `Export Notch Rows` 視窗。

### 12.6 結果存放位置

- query 結果印在你下指令的終端機。
- `export-notch` 的檔案寫到 `--path` 指定的路徑。
- Console 存出的 log 預設檔名是 `freeform-helper-console.log`。
- 程式的 log 檔是 `app.log`，每天輪替，保留 14 份。
  log 資料夾位置相對於執行檔，詳見 gap 紀錄。

## 13. 相關文件

- 英文版使用手冊：`docs/guides/app-user-manual.md`
- Runtime CLI：`docs/reference/runtime-cli-plan.md`
- 設定入口對照：`docs/guides/settings-entry-matrix.md`
- Notch 參考：`docs/reference/notch-system-reference.md`
- 行為不明確紀錄：`cloud-notes/user-guide-gaps.md`
