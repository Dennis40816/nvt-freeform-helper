# PadCanvas Selection/Draw Perf Baseline（2026-03-06）

## 目的
- 為 `PadCanvas` 新增的觀測欄位建立第一版基線，後續優化可直接比較前後差異。
- 指標來源：
  - `Selection updated` 既有 timing（summary / inspector / notchPreview / total）
  - 新增 `canvasSelect(...)` / `draw(...)` 快照欄位

## 採樣資料與前置
- 專案：`example/BOE36.35/project_3635.json`
- 版本：`master`（含 `PadCanvas.Perf.cs` 埋點）
- 採樣前置：
  - App log level 需為 `Debug`（否則不會輸出 `Selection updated` debug line）
  - 啟動 app instance 後，以 runtime query 載入 3635 並做多次 `select-cad/clear-selection`

## 採樣命令（實際使用）
```powershell
# 啟動 app（使用含 Debug log level 的 app settings）
Start-Process -FilePath "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" `
  -WorkingDirectory (Resolve-Path ".") `
  -Environment @{ FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH = "$env:TEMP\freeform-app-general-a1bed0c3941b4fce9238efd6520a8fb4.json" }

# 載入 3635
& "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query load-project `
  --path "example/BOE36.35/project_3635.json" --json-compact --timeout-ms 120000

# 取樣（多次選取）
$ids = @(4767,3777,2089,274,291)
foreach ($id in $ids) {
  & "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query clear-selection --json-compact --timeout-ms 60000 | Out-Null
  & "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query select-cad --cad-id $id --json-compact --timeout-ms 60000 | Out-Null
}
& "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query clear-selection --json-compact --timeout-ms 60000 | Out-Null
```

## 指標擷取規則
- log line pattern（單行）：
  - `Selection updated: ... | canvasSelect(rev=...,ms=...,cadCand=...,regCand=...,...) draw(rev=...,ms=...,cadCand=...,regCand=...,visCad=...,visReg=...,stepCad=...,stepReg=...)`
- 本次擷取欄位：
  - `inspector ms`、`total ms`
  - `draw.ms`、`draw.cadCand`、`draw.regCand`、`draw.stepCad`、`draw.stepReg`
  - `canvasSelect.revision`（確認是否走到選取埋點）

## 基線結果（本次）
- 來源：`build/logs/app.log`（2026-03-06 22:44 local）
- 樣本數：`10`

| Metric | Value |
|---|---:|
| inspector p50 / p95 (ms) | 1 / 20 |
| total p50 / p95 (ms) | 1 / 132 |
| draw p50 / p95 (ms) | 2 / 5 |
| draw cad candidates | 4838 |
| draw regular candidates | 4992 |
| draw decimation step (cad / regular) | 1 / 1 |
| canvasSelect revision max | 0 |

## 解讀與限制
- `draw` 指標已可穩定觀測（候選數與 draw 耗時可比較）。
- 本次樣本 `canvasSelect revision = 0`：
  - 原因：runtime `select-cad` 屬程式化單點選取，不是框選/命中測試路徑。
  - 後續若要建立「框選起手延遲」基線，需加一輪手動框選或專用自動化輸入腳本。

## 後續比較建議
- 每次調整 `PadCanvas.SelectionEngine` 或 `PadCanvas.VisibleDrawListBuilder` 後，至少重跑一次上述流程。
- 比較門檻建議：
  - `draw p95` 不可高於基線 +20%
  - `inspector p95`、`total p95` 不可高於現有 3635 budget
