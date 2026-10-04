# FreeformHelper

授權與使用限制請見 [LICENSE](LICENSE)。

自 2026-10-04 起，新工作（分支、PR、新 issue）在公開 repo [Dennis40816/nvt-freeform-helper](https://github.com/Dennis40816/nvt-freeform-helper) 進行，PR 目標為 `1.3.x`。

FreeformHelper 是 DXF 分析工具，主流程是：
`DXF 匯入 -> Regular Grid 建立 -> CAD/Regular 對應 -> Freeform/Notch 輸出`

## 0. 先看這裡（90 秒）

- 要直接啟動 UI：看「1) 傻瓜模式：直接跑起來」
- 要打包給別人用（免安裝 .NET）：看「2) 傻瓜模式：單檔 EXE」
- 是的，README 內有「build 單檔 exe」流程，而且是可整段複製貼上。

## 1. 傻瓜模式：直接跑起來（整段複製貼上）

> 條件：Windows + PowerShell + .NET SDK 10.0.3xx（版本由 `global.json` 鎖定）+ .NET 8 runtime（專案 target 為 `net8.0`）

```powershell
# 1) 到 repo 根目錄（請改成你的路徑）
Set-Location <FreeformHelper repo 路徑>

# 2) 還原 + 建置
dotnet restore
dotnet build FreeformHelper.sln

# 3) 啟動 UI
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj
```

## 2. 傻瓜模式：打包「單檔 EXE」（整段複製貼上）

> 這是免安裝版本（self-contained），最適合交付測試。

```powershell
# 1) 到 repo 根目錄（請改成你的路徑）
Set-Location <FreeformHelper repo 路徑>

# 2) 打包單檔 EXE（預設 win-x64、Release）
./scripts/build/publish-exe-single-file.ps1

# 3) 檢查產物
Get-Item .\build\publish\win-x64\single-file\FreeformHelper.UI.exe

# 4) 直接執行
.\build\publish\win-x64\single-file\FreeformHelper.UI.exe
```

常用變體（ARM64）：

```powershell
./scripts/build/publish-exe-single-file.ps1 -Runtime win-arm64
```

## 3. 傻瓜模式：打包「資料夾版 EXE」（整段複製貼上）

> 啟動通常更穩、除錯較容易（但檔案較多）。

```powershell
Set-Location <FreeformHelper repo 路徑>
./scripts/build/publish-exe-folder.ps1
Get-Item .\build\publish\win-x64\folder\FreeformHelper.UI.exe
.\build\publish\win-x64\folder\FreeformHelper.UI.exe
```

## 4. 第一次使用建議流程（UI）

1. `File -> Open DXF`
2. 左側確認 layer 可見性與 overlap 檢查
3. 右側調整 Grid / Scan order / Alignment
4. 執行 `Match`，查看 mapping 摘要與報告
5. 視需要標記 freeform，最後匯出 Notch / DXF
6. `Save Project` 存成 JSON，後續可 `Load Project`

## 5. Runtime CLI（進階，可跳過）

UI 啟動後，可在另一個 terminal 查詢執行中狀態。

```powershell
# 列出命令
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query help

# 狀態
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query status

# 載入 project
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query load-project --path example/BOE36.35/project_3635.json

# 跑 Step1~3
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query run-step --step 1 --timeout-ms 120000
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query run-step --step 2 --timeout-ms 120000
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query run-step --step 3
```

完整 CLI 請看：`docs/reference/runtime-cli-plan.md`

## 6. 常見問題

- `MSB3027` / `file is being used by another process`
  - 先關閉正在執行的 `FreeformHelper.UI.exe` 再 build/publish。
- `INSTANCE_NOT_RUNNING`
  - 代表目前沒有執行中的 UI instance（先開 UI）。
- 匯出 Notch 失敗
  - 先確認 DXF 已載入、grid 已建立、match 已執行。
- 問到 `To Regular` 演算法時
  - 先看 `docs/reference/notch-system-reference.md`。beta0.9 起，`ToRegularRatio = Σ(overlapArea / targetRegularArea)` 是 CAD-level 診斷值；Gain / No Gain 的 v2.2 row payload 改用 per-target regular coverage，不再把整顆 CAD 的 `R` 當 source-wide gain 乘到每個 target share。

## 6.1 範例資料（private submodule）

`example/` 是 git submodule，指向 private repo `FreeformHelper-testdata`（客戶面板 CAD、IC mapping，以及由它們產生的 project JSON 與韌體 C 匯出）。沒有該 repo 權限也能 build 與執行 UI。

```powershell
# 有權限：clone 後或新建 worktree 後抓資料
git submodule update --init example
```

- 沒有資料時，讀取 `example/` 的測試會標示為略過（Skipped），不會失敗。
- 直接執行 `dotnet test` 時是略過；透過 repo 腳本（`run-tests.ps1`、`run-refactor-gate.ps1`、`run-pre-push-gate.ps1`、`verify.ps1` 的 test lane、`build.ps1`）執行時預設要求資料存在，沒有權限時加 `-AllowMissingExampleData`。即使加了這個參數，只要資料存在就仍會檢查。
- 修改資料要在 `example/` 內 commit 並 push 到資料 repo，再回到本 repo commit 新的 submodule 指標。gate 會檢查 `example/` 是否正好在本 repo 釘住的 commit 且沒有未提交的變更。
- 兩份 Notch golden snapshot 位於 `example/golden-snapshots/`。設定 `FREEFORMHELPER_UPDATE_NOTCH_BASELINE=1` 或 `FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX=1` 會直接更新資料 repo checkout 中的對應檔案；維護者須先在 `FreeformHelper-testdata` commit 並 push，再更新本 repo 的 submodule 指標，否則 `assert-example-data.ps1` 會拒絕未提交的 `example/` 變更。
- Before switching to a historical branch that predates the `example/` submodule and still stores it as an ordinary directory, run `git submodule deinit -f example` to avoid Git refusing the switch because files already exist in that directory.

## 7. 主要路徑

- 專案檔：`*.json`（預設 `freeform_helper_project.json`）
- logs：`logs/app.log`
- 建置與產物：`build/`

## 8. 專案結構

- `src/FreeformHelper.Domain`：幾何與 pad domain model
- `src/FreeformHelper.Application`：grid/match/notch 演算法與設定模型
- `src/FreeformHelper.Infrastructure`：DXF 匯入、project JSON store、migration
- `src/FreeformHelper.UI`：Avalonia UI（View/ViewModel/Controls/Styles）
- `tests/FreeformHelper.Tests`：單元測試與 headless UI 測試
- `docs/`：規格與行為盤點文件

## 9. 延伸文件

- 文件導覽：`docs/README.md`
- Notch canonical reference：`docs/reference/notch-system-reference.md`
- 主演算法文件：`docs/core/freeform-helper-algorithms.md`
- Notch V21：`docs/core/notch-v21-algorithm.md`
- Notch V22：`docs/core/notch-v22-algorithm.md`
- Notch 流程圖：`docs/core/notch-v21-v22-flow.md`
- 目前 C export example：`example/BOE36.35/notch_export_v21_current.c` / `example/BOE36.35/notch_export_v22_current.c`
- 使用手冊：`docs/guides/app-user-manual.md`
- 開發待辦：`TODO.md`
- 腳本總覽：`scripts/README.md`

## 10. 重構 Gate（開發者）

```powershell
# 固定 gate（lint + build + 全部測試分組：notch-core/application/infrastructure/ui-core/ui-snapshots/uncategorized）
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost

# 沒有 example/ 資料權限時
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -AllowMissingExampleData
```
