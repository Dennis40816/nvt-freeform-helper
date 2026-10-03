# Info Refactor 驗收指南（2026-02-23）

## 1. 範圍與目的
本輪重構聚焦在「資訊可讀性」與「Step5 row 選擇體驗」：

1. Inspector 改成分層呈現（summary / details / trace），降低一次顯示資訊量。
2. 引入 dot indicator 視覺語法，讓主資訊、次資訊、警示、背景計算狀態有層次。
3. `Select notch rows to export` 由扁平列表改為 IC 分組 + 右側預覽面板。
4. row 預覽與主畫面 selection 關聯：在 row 視窗選擇預覽項目時，主畫面會同步選取對應 CAD/REG。
5. selection 起手優化：移除框選起手（mouse-down）的大型 hit-test，改善大場景按下左鍵卡頓。

## 2. 驗收前準備
1. 切到分支：`refactor/non-notch-architecture`
2. Build：
```powershell
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release
```
3. 啟動程式（IDE 或既有啟動方式皆可）。

## 3. 驗收案例

### Case A：Selection 起手延遲（3635 專案）
目標：確認「按下左鍵開始拉框」不再明顯頓住。

1. 載入檔案：  
`example\BOE36.35\cz_36d2_6480x848_Lucid_panel_touch_block_CAD_20250729_NVTint_regular.dxf`
2. 放大到多 pad 密集區域。
3. 直接按住左鍵開始拉 selection 框（重複 5~10 次）。

預期：
1. 左鍵按下後框選可以立即開始，不應出現長時間停頓。
2. 放開滑鼠後才進行 selection 套用（框選結果更新）。

### Case B：Inspector 分層可讀性
目標：確認右側 Inspector 由「全部平鋪」改為「摘要優先 + 低頻折疊」。

1. 右側切到 `Inspector` 分頁。
2. 點選一個 CAD（再點一個 REG 各測一次）。
3. 觀察 `Inspector snapshot` 區塊。
4. 展開/收合 `Snapshot details`。
5. 展開/收合 `Rule trace`。

預期：
1. 先看到四條摘要（主鍵、來源、match、補償），每條前面有 dot indicator。
2. `Snapshot details` 預設收合，展開才看到低頻明細欄位。
3. `Rule trace` 預設收合，且 header 顯示 trace 筆數。
4. 若背景補算中，會有獨立狀態列提示。

### Case C：Step5 row selection 視窗改版
目標：確認 row 選擇由扁平列表改為 IC 分組 + 預覽。

1. 先跑到可產生 notch rows 的狀態（Step5 有輸出）。
2. 進入匯出流程並開啟 `Select notch rows` 視窗。
3. 檢查左側是否依 `IC` 分組顯示（每組有 `All/None`）。
4. 點選不同 row（非只勾選 checkbox，直接點 row 卡片）。
5. 觀察右側 `Row preview` 內容。

預期：
1. 左側為 IC 分組，不再是單一長表格。
2. 每個 row 卡片顯示核心摘要（版本、diff、REG、CAD、payload 摘要）。
3. 右側預覽可看到：pad relation、payload、flags、values/comment、quick analysis。

### Case D：row 與圖面關聯
目標：確認在 row 視窗選擇預覽時，主畫面 selection 會同步。

1. 保持 row selection 視窗開啟。
2. 在左側點 2~3 個不同 row（特別是不同 IC）。
3. 回看主畫面選取狀態（Selection summary / 畫布高亮）。

預期：
1. 點不同 row 時，主畫面 selection 會切到該 row 對應的 CAD/REG。
2. 若 row 無 CAD，至少 REG selection 會更新。

## 4. 迴歸檢查
1. `Select all / Select none / IC group All / IC group None` 都能正確更新計數。
2. `Export selected` 在無勾選時不可按；有勾選時可按。
3. 匯出內容仍依勾選 row 輸出，不受預覽切換影響。

## 5. 本輪測試記錄（開發端）
已執行：

```powershell
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Release --filter "FullyQualifiedName~NotchExportSelectionViewModelTests|FullyQualifiedName~UiLayoutGuardTests|FullyQualifiedName~WorkspaceInteractionStateTests"
```

結果：上述目標測試通過。

說明：`dotnet test FreeformHelper.sln -c Release --no-build` 在目前分支仍有既存失敗（logging policy / log formatting / UI snapshot baseline / Step3 既有測試），不屬本次功能改動新增問題，後續需獨立整理。
