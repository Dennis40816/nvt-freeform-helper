# FreeformHelper 使用手冊
最後更新：2026-08-08

這份文件給第一次接手專案、第一次驗證面板、或第一次跑完整 Step1~Step5 的使用者。  
目標是讓讀者只靠文件就能知道：

- 這個 app 在做什麼
- 每個頁面在哪裡
- 每一步要輸入什麼、產生什麼
- 驗證時應該看哪裡

## 1. 這個 app 在做什麼

FreeformHelper 的主用途是：

1. 載入 DXF CAD pad
2. 建立或匯入 regular grid
3. 建立 `CAD ↔ Regular` 幾何對應
4. 標記 freeform
5. 預覽與匯出 notch table
6. 用 diagnostics / simulation / export review 驗證結果

主流程可以概括成：

```text
Load DXF / Load Project
-> Step1 Geometry Match
-> Step2 Freeform
-> Step3 Notch Preview
-> Step4 Mapping Diagnostics
-> Step5 Export
```

## 2. 主要頁面

### 2.1 `Freeform Helper`
- 主工作頁。
- 左邊是 DXF / layer / geometry edit。
- 中間是 AA / CAD / regular 畫布。
- 右邊是 Step1~Step5 workflow 與 General 設定。
- 下方是 terminal / log。

### 2.2 `How To Use`
- UI 內建快速說明。
- 適合快速看操作快捷鍵與主要名詞。

### 2.3 `Simulation`
- 專門拿 regular pad 數值做前後比較、CSV 播放、Before/After/Delta 觀察。
- 使用的是同一份 workspace regular grid，不會另建第二份 grid。

### 2.4 `Dev`
- 預覽控制項、風格與 debug 行為用。
- 一般使用者通常不需要進來。

## 3. 基本操作與畫布快捷鍵

### 3.1 畫布導航
- 中鍵拖曳：平移
- `Space + 左鍵拖曳`：平移
- 滾輪：縮放
- `Ctrl + 滾輪`：快速縮放
- `F`：Fit 目前內容
- `Shift + 方向鍵`：微調平移

### 3.2 選取
- 左鍵：選取 pad
- `Ctrl/Shift + 左鍵`：加選/取消
- `Alt + 左鍵`：在 CAD / regular 疊合時優先選 regular
- 拖曳空白區：框選
- 雙擊：打開 pad info
- 點空白：清除選取

## 4. 第一次使用建議流程

### 4.1 載入
1. `File -> Open DXF`
2. 或 `File -> Load Project`

如果是第一次從 DXF 開始，建議順序：
1. 開 DXF
2. 確認 layer 顯示
3. 確認 grid / cascade / AA size
4. 再跑 Step1

### 4.2 如果是延續既有專案
1. `Load Project`
2. 檢查右側 workflow 狀態是否有正確 replay
3. 檢查畫布、selection、step summary 是否和預期一致

## 5. 左側面板：DXF / 編輯

### 5.1 Layer 顯示
- 可針對 layer 顯示/隱藏 CAD pads。
- 若 regular 來源是 `From DXF layer`，該 layer 會從 CAD 顯示層隱藏，避免畫面重疊。

### 5.2 DXF overlap 檢查
- `Check DXF overlaps` 用來找重複 pad、重疊 pad、異常幾何。
- `Details / Select / Clear` 會打開 review 頁。

### 5.3 DXF edit
常見行為：
- `Hide`：手動隱藏選取 CAD
- `Duplicate`：系統自動偵測的 exact duplicate，獨立管理
- `Combine`：建立 synthetic pad
- `Move`：移到指定 layer
- `Rotate`：把選中 group 或指定 layer 當剛體繞共同中心旋轉

診斷與 edit review 都採用和 export 類似的 review workspace：
- row click：選取 inspector
- `Locate/Focus`：顯式操作

## 6. 中央工作區：AA / CAD / Regular

### 6.1 主要顯示
- `CAD layer`
- `Regular grid`
- `Color by area`
- `Highlight unmatched`
- `Freeform / Notch overlay`

### 6.2 這裡最常看的三件事
1. 幾何重疊是否合理
2. regular / diff 編號是否合理
3. Step3 Notch overlay 是否符合預期

## 7. 右側面板：主 workflow

## 7.1 General settings

這裡主要控制：
- cascade / IC layout
- X/Y channels
- AA size
- grid padding
- scan order
- alignment mode

這些改動會影響後面的 Step1~Step5。

## 7.2 Step1 - Geometry Match

主要目的：
- 建立 `CAD ↔ Regular` 的幾何 overlap 關係

你會在這一步得到：
- `PadMatchResult`
- raw best-match 候選
- Step1 summary

### Step1 現在還有一個重要功能：`Regular Visibility Mask (SeeRegular.csv)`
- `Import Regular Visibility Mask (SeeRegular.csv)`
- `Use Regular Visibility Mask`
- `Clear mask`

這份 mask 的意義是：
- 若某顆 regular 在 `SeeRegular.csv` 全部 frame 都是 `0`
- 代表這顆 regular 沒有實際訊號 surface
- 之後 Step4 會用這個 mask 約束 diff assignment；Simulation 也會用 `regular grid ∩ mask` 建立 active surface，補償仍執行共同生成的 notch table

注意：
- mask 是顯式匯入，不會從 project 相鄰目錄自動搜尋；已保存的 project snapshot/embedded mask 仍可在 `Load Project` 還原
- mask 不會改寫 Step1 幾何真值
- 它在 Step1 之後作為 assignment 約束，並明確限制 Simulation active surface

## 7.3 Step2 - Freeform

主要目的：
- 把 regular 標成 `None / XWay / YWay / XYWay`

來源有兩種：
1. auto detect
2. manual override

常見驗證點：
- 某顆 CAD 若物理上只對一顆 regular，不應被尾巴污染成 freeform
- `freeformAxisThreshold` 對 auto detect 很敏感，要確認設定值

## 7.4 Step3 - Notch Preview

主要目的：
- 預覽 `ToRegular / ToFull / Combined`
- 看 Stage1 / Stage2 / Stage3 overlay

要點：
- `ToRegular`：CAD 與 regular 的面積關係
- `ToFull`：是否能從 boundary / owner / gate 規則安全擴張
- `Combined`：綜合比例

常見觀察位置：
- AA 畫布上的 Stage overlay
- pad inspector 的 notch 區塊
- `query notch` / `query notch-stage`

## 7.5 Step4 - Mapping Diagnostics

主要目的：
- 看 `CAD Output FW Diff` 是否合理
- 看 `raw best / masked best / suggested diff`
- 找出低信心、歧義、未映射、被 mask 改變的 case

這個頁面目前是 decision-first：
- 上方 summary chips
- 左側 decision list
- 右側 inspector
- row color / focus 行為跟 export review 一致

常見欄位：
- `Current`
- `Raw best`
- `Masked best`
- `Suggested`
- `Reason`

常見驗證題目：
- 這顆 CAD 為什麼不是它最佳 match 的 diff？
- 是 `mask` 改掉，還是目前 current assignment 本來就不同？

## 7.6 Step5 - Export

主要目的：
- 生成 notch table
- 預覽 row
- 手動 include/exclude
- 匯出 CSV / `C v2.1` / `C v2.2`

重點：
- 預設支援 `V21 + V22`
- `V22` 是主線
- `V21` 保留 legacy row 與幾何對照
- CSV review 主要用於 trace / diff review，不是 FW 直接導入契約
- 若要交給 FW 直接導入，請匯出 `C v2.1` 或 `C v2.2`
- CSV review 的排序固定為 `IC -> FW Diff -> Version -> REG -> CAD`，欄位固定到 `payload_01..payload_09`

`Select notch rows` 視窗是目前 review UX 的參考實作：
- row color
- inspector
- group strip
- 明確 `Locate/Preview`

## 8. Simulation 頁

Simulation 是一個和 `Freeform Helper` 同 workspace 的數值工作頁。

主要用途：
- 用 regular pad 數值做 Before / After / Delta 觀察
- 匯入 CSV frame 播放
- 檢查某張 table 套用前後的數值分布

### 8.1 顯示模式
- `Before`
- `After`
- `Delta`
- `Changed only`

### 8.2 著色模式
- `AUTO`
- `TH`

著色只是輔助，核心資訊是：
- 每顆 regular 正中央的數值

### 8.3 輸入來源
- Manual
- Global baseline
- CSV import

### 8.4 CSV 播放
- 可以逐 frame 播放
- 可調 FPS
- 只改投影數值，不會重建 grid

## 9. 匯出 / 檢查 / 驗證時應看哪裡

### 9.1 想驗證幾何
- 看 Step1
- 看 pad info
- 看 `query pad`

### 9.2 想驗證 freeform
- 看 Step2 summary
- 看 regular / CAD inspector
- 看 freeform override 是否存在

### 9.3 想驗證 notch
- 看 Step3 overlay
- 看 `query notch`
- 看 `query notch-stage`
- 看 Step5 export row preview

### 9.4 想驗證 diff 編號
- 看 Step4 diagnostics
- 比：
  - current
  - raw best
  - masked best
  - suggested

### 9.5 想驗證實測面板訊號
- 匯入 `SeeRegular.csv` 到 Step1 mask
- 看它怎麼改變 Step4、最終 notch table 與 Simulation active surface

## 10. Save / Load Project

### 10.1 Save Project
- 保存：
  - grid
  - freeform override
  - DXF edit
  - notch 設定
  - workflow 狀態

### 10.2 Load Project
- 載入後會嘗試 replay workflow
- app-level 設定採 deferred flush 契約

## 11. Runtime CLI

若 UI 已啟動，可在另一個 terminal 查詢：

- `query status`
- `query pad`
- `query notch`
- `query notch-stage`
- `query notch-validation`
- `query simulation`

完整指令：
- `docs/reference/runtime-cli-plan.md`

## 12. 第一次驗證時的建議 checklist

1. 載入專案或 DXF
2. 確認 grid / AA / cascade 正確
3. 跑 Step1，檢查 overlap 是否合理
4. 若有 `SeeRegular.csv`，手動匯入並決定是否啟用 mask
5. 跑 Step2，檢查 freeform 是否合理
6. 看 Step3 overlay 與 inspector
7. 開 Step4 diagnostics，看 changed / removed / ambiguous
8. 開 Step5 row preview，確認輸出列與目標版本
9. 若要看數值分布，再切到 Simulation

## 13. 相關文件

- 主演算法：`docs/core/freeform-helper-algorithms.md`
- Notch canonical reference：`docs/reference/notch-system-reference.md`
- Notch flow deep-dive：`docs/core/notch-v21-v22-flow.md`
- Notch V22 演算法：`docs/core/notch-v22-algorithm.md`
- Workflow pipeline：`docs/core/workflow-pipeline.md`
- Runtime CLI：`docs/reference/runtime-cli-plan.md`
