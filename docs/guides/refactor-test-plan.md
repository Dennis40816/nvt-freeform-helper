# Refactor 測試計畫（Phase 1~2 基線）
最後更新：2026-02-10

## 1. 目的
- 在進入最終 overlap / notch 重構前，先鎖定現有可接受行為，避免功能在重構期間回歸。
- 建立「功能對應測試」矩陣：每個使用者可見功能至少有一個可重複驗證入口（unit / integration / VM / UI guard）。

## 2. 測試分層與執行順序
1. **Domain / Application Unit**（最快、先跑）  
   `tests/FreeformHelper.Tests/Application/**/*Tests.cs`
2. **ViewModel 行為測試**（命令與狀態轉移）  
   `tests/FreeformHelper.Tests/UI/ViewModels/*Tests.cs`
3. **UI Guard 測試**（XAML layout/token 失誤防呆）  
   `tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs`
4. **整體 build gate**  
   `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`

Gate 規則（重構期間）：
- PR / 每日整合至少要通過 1~4 全部。
- 新增功能若無測試，不可替換既有流程（只能 behind flag）。

## 3. 功能覆蓋矩陣

### A. DXF 匯入/篩選/編輯
- A01 匯入 closed polylines 過濾正確。
- A02 layer toggle 後 CAD output pads 正確，且 hidden pads 不回流。
- A03 DXF hide / restore last / restore all 可逆且不破壞索引。
- A04 Export DXF（visible/all）輸出 pad 數與目前狀態一致。

### B. Grid 建構與索引
- B01 Panel AA 對位建 grid（row/col/count）正確。
- B02 DXF bounds 對位在無 DXF 時 fallback 到 Panel AA，且有明確 status。
- B03 指定 bound layer（隱藏仍可用）時 bounds 正確。
- B04 Scan order 變更後 regular diff idx 重新排序正確。
- B05 移除 legacy 上限後（高 X / 高 IC）仍可建 grid。

### C. CAD↔Regular overlap / mapping
- C01 overlap match 可回報 progress（0→1）且不中斷 UI。
- C02 一個 CAD 橫跨多個 regular 時，雙向 link 完整（CadToRegular + RegularToCad）。
- C03 unmatched threshold 只影響標示；direct/draft 修改只重畫，不重建 grid 或改變 links。
- C04 index diagnostics 能產生 count mismatch / low confidence / ambiguous 類別。
- C05 mapping override 的 apply/clear 可持久化且 load 後一致。

### D. Freeform 偵測與手動覆寫
- D01 auto-detect（X dominant）標為 XWay。
- D02 auto-detect（Y dominant）標為 YWay。
- D03 dominant spread 低於 TH 時維持 None。
- D04 開啟 `EnableAutoDetectXy` 後可判定 XYWay；關閉時不得自動產生 XYWay。
- D05 手動覆寫（None/X/Y/XY）只影響 selection 並可 undo。

### E. Notch 與輸出
- E01 v2.1 輸出格式穩定（fixture 比對）。
- E02 legacy 輸出仍可用（相容測試不可刪）。
- E03 notch TH gate 低於門檻者不輸出。
- E04 mode 切換（legacy/new）在相同輸入下可比較結果，不得 crash。

### F. Save/Load / Migration
- F01 Project save/load 後 grid/match/freeform/overrides 可回復。
- F02 embed DXF roundtrip 正確（無原始 dxf path 時仍可載入）。
- F03 migration append-only：舊版欄位保留、未知欄位不丟失。
- F04 設定視窗提交（Save）與取消（Cancel）行為一致，不應即時污染主狀態。

### F-Plus. Save Project 專項（重構前先守住）
- F05 Save project：使用者取消路徑選擇時，不寫檔、不改 `HasUnsavedChanges`、不污染 `_lastSavedPath`。
- F06 Save project：dialog handler 未綁定時，回傳失敗且 status 明確（避免靜默失敗）。
- F07 Save project：IO/序列化異常時，status 與 log 可追蹤，且既有記憶體狀態不可被半套覆蓋。
- F08 Save project：成功後 `HasUnsavedChanges=false`，後續再次修改任一 persisted setting 需重新變回 dirty。
- F09 Save project：embed DXF 的 yes/no 分支都需驗證（含警告訊息與輸出檔內容一致）。
- F10 Save -> Load roundtrip：hidden CAD / mapping overrides / UI snapshot / notch 設定值保持一致。

目前自動化基線（已存在）：
- `ProjectPersistenceUseCaseTests`：save/load、embed/no-embed、UI snapshot roundtrip。
- `ProjectStoreTests`：project json 存取與欄位 roundtrip。
- `FreeformHelperViewModelTests`：`SaveProjectAsync_WhenDialogHandlerMissing_ReturnsFalse`。

### G. UI/UX Guard（不測美術，測結構）
- G01 Scroll content 不可綁 `Bounds.Width`（避免裁切/重疊）。
- G02 關鍵容器必須採 viewport-bounded width pattern。
- G03 重要按鈕/輸入控件需使用 token（禁止硬編顏色/尺寸）。
- G04 分析區 Step 1~4 文案與操作順序保持一致。
- G05 視覺快照最小基線：關鍵 UI 檔案 hash 必須與 baseline 一致（intentional UI 變更需同步更新 baseline）。
- G06 視覺快照進階基線：headless 實際渲染後做 aHash + Hamming 容差比對（可捕捉排版/配色實際變化）。

## 4. 近期新增測試重點（本輪重構起手）
- P0：C02, C04, D01~D04, F01, F03（先守住 mapping/freeform/save-load 核心）
- P1：A03, C05, E01, E04（確保重構可逐步替換）
- P2：G 系列（持續加 guard，避免 UI 回歸）

## 5. 執行腳本
```powershell
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj

# UI baseline 更新（先 dry-run，再 apply）
./scripts/tests/update-ui-baseline.ps1 -Mode DryRun
./scripts/tests/update-ui-baseline.ps1 -Mode Apply
```

## 6. 完成定義（DoD）
- 測試矩陣中 P0 全部有自動化測試。
- 新流程（overlap-allocation notch）可在 flag 下與 legacy 結果並列比較。
- 無新增 hard-coded style；UI guard 測試通過。

