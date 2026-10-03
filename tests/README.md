# 測試分類導覽
最後更新：2026-07-20

`tests/FreeformHelper.Tests` 已依責任分層整理，避免單一目錄塞滿測試檔。

## 目錄分類
- `Application/Cad`：CAD 匯入/桶化/合併相關邏輯
- `Application/Dxf`：DXF 匯入、mapping、overlap、layer grid builder
- `Application/Freeform`：Freeform 偵測與統計
- `Application/Notch`：Notch 計算與匯出核心
- `Application/Pad`：Pad match/override/grid 建立
- `Application/Project`：專案檔 migration/save/load store
- `Application/Sizing`：Manual sizing 與 scope 行為
- `Application/Workflow`：Workflow pipeline 依賴與 invalidation
- `Infrastructure/Console`：console link parser
- `Infrastructure/Logging`：log 格式與 NLog 設定
- `UI/TestHost`：Avalonia headless test app entry
- `UI/Smoke`：UI 啟動 smoke 測試
- `UI/ViewModels`：ViewModel 命令/狀態測試
- `UI/Canvas`：Pad canvas hit-test/cache/invalidation
- `UI/Snapshots`：UI layout guard + rendered/hash snapshot
- `Snapshots`：baseline json（快照基線）

## 常用測試命令
```powershell
# lint（預設僅檢查本次變更）
./scripts/tests/lint.ps1

# lint 全量掃描（含歷史檔案）
./scripts/tests/lint.ps1 -AllFiles

# lint 全量掃描（若遇到 FreeformHelper.UI.exe 鎖檔）
./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost

# lint 全 solution analyzer（給 CI/發版前用）
./scripts/tests/lint.ps1 -AllFiles -AnalyzerScope Solution

# 全量
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj

# Notch 相關（class name filter）
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~Notch"

# Notch 核心資料路徑（15 個 class；含 golden、generator、export、simulation、projection、settings、Runtime Query）
./scripts/tests/run-tests.ps1 -Group notch-core -UseNoAppHost

# Notch checked-in golden（3635 V21/V22 C、3635/TM8.1 snapshot、TM8.1 acceptance matrix）
./scripts/tests/run-tests.ps1 -Group notch-golden -UseNoAppHost

# UI snapshot/guard 相關
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~Ui"

# UI baseline dry-run（預覽變更，不寫檔）
./scripts/tests/update-ui-baseline.ps1 -Mode DryRun

# UI baseline apply（直接更新 baseline json）
./scripts/tests/update-ui-baseline.ps1 -Mode Apply
```

## 重構固定測試順序（最小集）
每次做重構時，請固定執行下列最小回歸順序：

1. `notch-golden`
2. `application`
3. `ui-core`
4. `smoke`

可直接用一鍵 gate 腳本：

```powershell
# 含 lint + build + notch-golden/application/ui-core/smoke
./scripts/tests/run-refactor-gate.ps1

# 同上，但統一使用 UseAppHost=false（避開 apphost/exe 鎖定）
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost

# 核心 Notch/Application/Domain/Settings/RuntimeQuery/VM slice：以 notch-core 取代 notch-golden stage
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -IncludeNotchCore

# lint 改跑全 solution analyzer
./scripts/tests/run-refactor-gate.ps1 -LintAllFiles -LintAnalyzerScope Solution

# 加跑啟動預算 gate（workspace.initial-grid-built <= 1000ms）
./scripts/tests/run-refactor-gate.ps1 -IncludeStartupBudget -InitialGridBudgetMs 1000

# 單獨執行啟動預算 gate
./scripts/tests/check-startup-budget.ps1 -SkipBuild -InitialGridBudgetMs 1000

# Production source code-size baseline 契約（line/path semantics + signed reference）
./scripts/tests/check-code-size-baseline.ps1

# 若只想重跑測試序列（略過 lint/build）
./scripts/tests/run-refactor-gate.ps1 -SkipLint -SkipBuild
```

輸出：
- `build/test-gate/refactor-gate-summary.json`

