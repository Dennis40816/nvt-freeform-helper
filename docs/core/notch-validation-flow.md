# Notch Validation Flow（Step 5）
最後更新：2026-08-10

> 目的：說明目前「Validation quick trace」的資料來源、快取與行為，避免後續 AI/開發者誤判。

## 範圍
- UI：Step 5 `Validation quick trace`
- VM：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchValidation.cs`
- UseCase：`src/FreeformHelper.UI/Services/NotchValidationUseCase.cs`
- Trace mapper（UI/CLI 同源）：`src/FreeformHelper.UI/Services/NotchValidationTraceService.cs`

## 輸入前提
1. 先以 `ProjectSettings.ValidateOrThrow()` 驗證 current settings；非法 `NullValue` 會在 readiness lookup 前 fail-fast
2. Grid 已建立（否則回 `Validation: build grid first.`）
3. 目標 `REG id` 必須存在
4. 必須先做過一次 Step 5 Export，且有 `_lastGeneratedNotchTable`

Direct VM／`RuntimeQueryUseCase` 呼叫保留 validation exception；Named Pipe server 只在最外層將未處理的 command execution exception 轉成單行 `IPC_ERROR` failure envelope，並保留原 message。正常 grid／REG／table readiness failure 仍走既有回傳契約，不得混成 invalid-settings error。

## 核心資料源（single source）
- Validation 只讀 `_lastGeneratedNotchTable`，不重跑 notch 生成。
- 當 `_lastGeneratedNotchTable` 改變，會重建 bucket；否則重用快取。

## Bucket 快取行為
- 快取鍵：
  - `ReferenceEquals(_notchValidationBucketTable, table)`
  - `nullDiffValue`
- 命中：直接使用 `_notchValidationBucketCache`
- 失效重建：`NotchValidationUseCase.BuildBucket(...)`
- 重建時會寫 log：
  - `Validation bucket rebuilt: tableRows=..., null=..., elapsed=...ms`

## 分類邏輯（UseCase）
- `directRowsByRegularPadId`
  - 直接落在該 REG 的 row（所有版本）
- `incomingRowsByIcDiff`
  - 其他來源 row 的 leg 指向 `(ic, diff)`（目前主要來自 v2.2 row 解碼）
- `outgoingRowsByRegularPadId`
  - 該 REG 作為 source 轉出的 leg

### v2.2 leg 解碼重點
- 目前採 **typed-first** 解碼：
  - 若 `row.V22Node` 存在，優先用 typed 欄位（`TargetDiffIndex1/2`、`TargetRatioPercent1/2`）
  - 否則 fallback 到 `row.Values`（slot1/slot2）
- `targetDiff == nullDiffValue` 視為空 leg，忽略
- `ratioPercent` 會 clamp 到 `[-100, 100]`

## UI 顯示模型
- VM 與 Runtime CLI 都先透過 `NotchValidationTraceService.BuildTrace(...)` 產生同一份 trace。
- trace 會輸出三組項目（沿用同一套 mapper）：
  - `DIRECT`
  - `IN`
  - `OUT`
- Step5 右側面板以 `DIRECT / IN / OUT` 三個分組面板顯示；每組各自可展開並顯示 count。
- VM 仍保留 merged list（`NotchValidationItems`）作為摘要與空狀態判斷。
- 排序：`DIRECT -> IN -> OUT`，再按 `IC / source diff / target diff`（分組內也沿用此順序）。
- row payload 的 v2.2 解碼同樣採 typed-first；CLI 不再自己重複解碼。

## Focus 行為
- 點 row 會嘗試 focus：
  - source cad
  - source reg
  - target reg（若不同）
- 若目標 pad 不可見，顯示 `Validation focus: target pads are not visible.`

## 與 Export 的關係
- Validation 結果完全依賴「最近一次生成出的 notch table」。
- 只要重跑 Export（即使不寫檔），Validation 視圖應視為新版本資料。
- Runtime CLI `query notch-validation --regular-id ...` 與 Step5 UI 使用同源 trace（避免 UI/CLI 規則漂移）。

## 變更守則（重要）
- 若修改以下任一項，需同步更新本檔與 `docs/reference/runtime-cli-plan.md`（若 CLI 受影響）：
  - v2.2 leg 欄位定義（slot/order/null 判定）
  - bucket 快取鍵
  - DIRECT/IN/OUT 分類條件
  - Validation row 排序規則
