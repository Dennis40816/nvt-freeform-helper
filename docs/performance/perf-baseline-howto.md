# 非 Notch 效能 Baseline 使用方式

## 目的

建立一套可重複執行、可比對的效能基線，先聚焦在：
- 主要操作耗時（`finished in N ms`）
- log 噪音指標（`Selection updated`、`Grid built`、`ViewModel initialized`）
- 例外與警告數量（`ERROR`/`WARN`）

本文件處理 runtime latency/log。Production source LOC 與 secondary Release DLL size 是另一套 structural metric，請見 `docs/performance/code-size-baseline-1.3.0.md`；兩者不可互相替代或混用門檻。

---

## 一次 baseline 的建議流程

1. 啟動工具並執行固定操作腳本（你常用的流程）  
   例：載入 DXF -> 重建 grid -> run overlap/match -> 匯出。
2. 關閉工具（確保 log flush 完成）。
3. 執行 baseline 腳本：

```powershell
./scripts/perf/collect-perf-baseline.ps1
```

4. 查看輸出檔：
   - `build/perf/non-notch-perf-baseline-latest.md`

---

## 啟動/載入效能（Phase 1）

新增腳本：`scripts/perf/measure-startup-load.ps1`

用途：
- 自動啟動 app，量測「Runtime query 可用」時間
- 可選擇執行 `load-project`
- 產出 markdown/json 供基線比對

範例：

```powershell
./scripts/perf/measure-startup-load.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json
```

輸出：
- `build/perf/startup-load-latest.md`
- `build/perf/startup-load-latest.json`

補充：
- 預設 `PERF STARTUP` / `PERF LOAD_PROJECT` marker 記錄在 `Debug` level。
- `measure-startup-load.ps1` 會暫時設置 `FREEFORM_PERF_MARKERS_INFO=1`，確保量測 run 可在 `Info` level 也保留 marker。
- 若要手動量測非腳本啟動流程，可自行設定 `FREEFORM_PERF_MARKERS_INFO=1` 後啟動 app。
- 若已啟用「初始 grid 背景排程」，`Startup` 區塊的 `hasGrid` 可能是 `False`（屬預期）。
- 這時請搭配 `PERF STARTUP stage=workspace.initial-grid-built` 觀察首個 grid 完成時間。

---

## 冷啟動/暖啟動批次基線

新增腳本：`scripts/perf/measure-startup-load-batch.ps1`

用途：
- 重複執行 `measure-startup-load.ps1`，一次產生多次量測
- 自動彙整 cold（run1）與 warm（run2+）統計（avg/p50/p95/min/max）
- 保留每次 run 的原始 markdown/json，方便回溯

範例：

```powershell
./scripts/perf/measure-startup-load-batch.ps1 `
  -Runs 5 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -SkipBuild
```

輸出：
- `build/perf/startup-load-batch-latest.md`
- `build/perf/startup-load-batch-latest.json`
- `build/perf/runs/startup-load-run-*.md/json`

可選回歸比較：

```powershell
./scripts/perf/measure-startup-load-batch.ps1 `
  -Runs 5 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CompareJsonPath build/perf/startup-load-batch-prev.json `
  -RegressionThresholdPercent 15 `
  -FailOnRegression `
  -SkipBuild
```

- `CompareJsonPath`：前一版 batch JSON 路徑
- `RegressionThresholdPercent`：退化門檻（百分比，預設 15）
- `FailOnRegression`：若偵測到退化則腳本回傳失敗（可接 CI）

---

## 3635 固定回歸基線（Step1~Step5 + exact export + Runtime Query）

新增腳本：`scripts/perf/run-3635-regression-baseline.ps1`

用途：
- 固定跑 `project_3635.json` 的重構回歸流程（load + Step1~Step4 + Step5 export）
- 自動輸出 CSV / C v2.1 / C v2.2 匯出檔
- 預設將真實 UI/IPC 產生的 V21/V22 C，把 CRLF/LF/lone CR 統一為 LF 並容許最多一個 optional EOF newline 後，以 ordinal exact compare 對照 checked-in golden
- 同步產生 runtime query 基線（`status/selection/notch/notch-validation`）
- 以 runtime `selection.timings` 為主輸出 selection / inspector latency 統計，必要時 fallback 解析 `Selection updated` log
- 讀取 `docs/performance/regression-baseline-3635.budget.json`，輸出固定 budget gate 結果

範例：

```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CadId 4767 `
  -RegularId 4978 `
  -SelectionCadIds "4767,4808,7107" `
  -SelectionCycles 8 `
  -SkipNotchValidation `
  -EnforceBudget `
  -LaunchIsolatedUi `
  -SkipBuild
```

Golden gate 預設使用：

- `example/BOE36.35/notch_export_v21_current.c`
- `example/BOE36.35/notch_export_v22_current.c`
- `example/BOE36.35/notch_export_golden_manifest.json`（executable hash/bytes/nodes/input lock）

Exact gate 強制使用 `-LaunchIsolatedUi`：腳本以暫時的 `FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH` 啟動 hidden UI，避免個人 import/view 設定改變 CAD 數量或輸出，結束時只停止自己啟動且路徑已驗證的 process。Mismatch 會保留 actual C，並在錯誤訊息列出 actual/golden path 與第一個差異；腳本不會更新 golden。`-SkipGoldenCheck` 僅供非 gate 的自訂效能實驗。

`-SkipBuild` 需要既有 apphost executable；若剛執行過 `-UseNoAppHost` 測試／lint，請移除 `-SkipBuild`，讓腳本以 `UseAppHost=true` 重建後再啟動 isolated UI。

輸出（預設）：
- `build/perf/3635-regression-latest/regression-baseline-summary.md`
- `build/perf/3635-regression-latest/regression-baseline-summary.json`
- `build/perf/3635-regression-latest/notch_table.csv`
- `build/perf/3635-regression-latest/notch_v2.1.c`
- `build/perf/3635-regression-latest/notch_v2.2.c`
- `build/perf/3635-regression-latest/runtime-export-c-v21.json`
- `build/perf/3635-regression-latest/runtime-export-c-v22.json`
- `build/perf/3635-regression-latest/runtime-*.json`
- `build/perf/3635-regression-latest/selection-latency.json`

詳細驗收與輸出說明請見：
- `docs/performance/regression-baseline-3635.md`

---

## 啟動同步熱點盤點（Marker 分解）

新增腳本：`scripts/perf/analyze-startup-markers.ps1`

用途：
- 從 `PERF STARTUP` log 自動找出「最新 app run」（排除 query 子流程）
- 產生 stage timeline（elapsed/delta）
- 列出 top delta stages，快速定位同步熱點
- 注意：若不是透過 `measure-startup-load.ps1` 啟動，且目前 log level 非 Debug、也未設 `FREEFORM_PERF_MARKERS_INFO=1`，腳本可能找不到最新 run marker。

範例：

```powershell
./scripts/perf/analyze-startup-markers.ps1 `
  -LogPath build/logs/app.log `
  -OutPath build/perf/startup-markers-latest.md
```

可直接啟用預算檢查（`workspace.initial-grid-built <= 1000ms`）：

```powershell
./scripts/perf/analyze-startup-markers.ps1 `
  -LogPath build/logs/app.log `
  -OutPath build/perf/startup-markers-latest.md `
  -InitialGridBudgetMs 1000 `
  -FailOnBudgetViolation
```

或用測試 gate 包裝腳本：

```powershell
./scripts/tests/check-startup-budget.ps1 -SkipBuild -InitialGridBudgetMs 1000
```

---

## 腳本參數

```powershell
./scripts/perf/collect-perf-baseline.ps1 `
  -LogPath build/logs/app.log `
  -OutPath build/perf/non-notch-perf-baseline-latest.md `
  -OutJsonPath build/perf/non-notch-perf-baseline-latest.json `
  -CompareJsonPath build/perf/non-notch-perf-baseline-prev.json `
  -RegressionThresholdPercent 20 `
  -TopOperations 30
```

- `LogPath`：來源 log 檔
- `OutPath`：輸出 markdown 路徑
- `OutJsonPath`：輸出 JSON 路徑（供後續比對/自動化）
- `CompareJsonPath`：要比較的舊 baseline JSON（可選）
- `RegressionThresholdPercent`：退化門檻（超過才列入 regression 表）
- `TopOperations`：依 `p95` 排序後，輸出前 N 個操作

---

## 新增可觀測欄位

`collect-perf-baseline.ps1` 現在也會整理：
- `PERF STARTUP` markers（程式啟動階段）
- `PERF LOAD_PROJECT SUMMARY` markers（載入專案分段耗時）

可在 markdown 中看到兩個新區塊：
- `Startup Markers`
- `Load Project Markers`

---

## 快速比對建議

1. 先備份上一版 JSON：

```powershell
Copy-Item build/perf/non-notch-perf-baseline-latest.json build/perf/non-notch-perf-baseline-prev.json -Force
```

2. 跑完新流程後再重跑基線（帶 `-CompareJsonPath`）。
3. 直接看 markdown 的 `Regression Check` 區塊。

---

## 讀表重點

- 優先看 `p95`：比平均值更能反映卡頓體感。
- `Count` 太少時，先不要下結論。
- `ERROR`/`WARN` 若上升，先排除功能異常，再談效能。
- `Selection updated` 異常偏高，通常代表互動過度觸發或 log 噪音。

---

## 版本比對建議

- 每次做 perf/refactor 前後都跑一次。
- 以 `build/perf/` 產出比較檔，必要時再挑選重點貼到 PR/issue。
- 若 `p95` 退化超過 20%，應先停下來做 root cause 分析。

