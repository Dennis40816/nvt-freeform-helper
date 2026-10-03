# 3635 Selection / Export Historical Baseline（2026-03-05）

> 狀態：歷史快照，已由 `docs/performance/regression-baseline-3635.md` 的 2026-08-08 signed/isolated measurement 取代；本文件不得再稱為 latest 或用來更新 golden。

## 目的
- 固定量測 3635 專案在「selection latency」與「Step5 匯出耗時」。
- 保存 2026-03-04 舊 non-isolated command 與數值，供歷史趨勢查閱；新的可重現模板與 exact gate 只看 current baseline 文件。

## 測試資料與前置
- 專案：`example/BOE36.35/project_3635.json`
- 目標 CAD：`4767`
- 目標 Regular（notch-validation）：`4792`
- 歷史前置：先啟動 FreeformHelper UI（IPC query 連到既有 instance）。這不符合目前 managed PID/app-settings isolation gate，禁止作 1.3.x acceptance。

## 重現命令
```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CadId 4767 `
  -RegularId 4792 `
  -SelectionCadIds "4767" `
  -SelectionCycles 2 `
  -OutDir build/perf/3635-regression-verify `
  -SkipBuild
```

## 量測欄位（固定）
- Selection：
  - `sampleCount`
  - `totalMs.{min,p50,p95,max,avg}`
  - `inspectorMs.{min,p50,p95,max,avg}`
  - `notchPreviewMs.{min,p50,p95,max,avg}`
- Export：
  - `runtime-export-csv.json:data.elapsedMs`
  - `runtime-export-c-v22.json:data.elapsedMs`
  - 輸出檔 size（csv/c-v22）

## 歷史量測（來源：`build/perf/3635-regression-verify`，2026-03-04 16:47 UTC）

### Selection latency
| 指標 | 值 |
|---|---:|
| sampleCount | 2 |
| totalMs p50 / p95 | 2 / 6 |
| inspectorMs p50 / p95 | 0 / 0 |
| notchPreviewMs p50 / p95 | 2 / 6 |

### Step5 export
| 指標 | 值 |
|---|---:|
| CSV elapsedMs | 9682 |
| C v2.2 elapsedMs | 80 |
| CSV size | 44897 bytes |
| C v2.2 size | 65820 bytes |

### Budget gate（同次量測）
- 來源：`docs/performance/regression-baseline-3635.budget.json`
- 結果：`pass = true`

## 產物路徑
- `build/perf/3635-regression-verify/regression-baseline-summary.md`
- `build/perf/3635-regression-verify/selection-latency.json`
- `build/perf/3635-regression-verify/runtime-export-csv.json`
- `build/perf/3635-regression-verify/runtime-export-c-v22.json`

## 後續規則
1. 不覆寫本歷史快照，也不以其舊 output size/hash 作 golden。
2. 新量測使用 `run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget`，並在 current baseline 文件記錄 forward/reverse command、環境與 signed hashes。
3. 若 current baseline 的 `totalMs p95` 或 `CSV elapsedMs` 相對最近一次可比環境退化超過 20%，在 TODO 新增 perf 調查項；不可直接拿本文件的 non-isolated 數字作同環境比較。
