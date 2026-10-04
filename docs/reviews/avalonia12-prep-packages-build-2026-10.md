# Avalonia 12 升級準備：套件與建置盤點（2026-10）

## 範圍與證據

本文件只做升級前準備。依 `TODO.md` 的 owner 決定，Avalonia 11 → 12 排在 `1.3.5` 之前，目前不執行升級。現行 Avalonia 主套件版本為 **11.3.12**；沒有選定任何 12.x 版本，也沒有修改套件、程式、XAML、TODO 或 roadmap。

盤點基準：2026-10-03，commit `7477b07d`，分支 `feature/queue/avalonia12-prep-packages-build`。已先讀取 `AGENTS.md`、`docs/generated/project-dependency-graph.md`、`docs/agents/domain.md` 與 1.3.x roadmap；依賴圖的五個專案與目前 solution／ProjectReference 一致。

來源為 [中央套件版本](../../Directory.Packages.props)、[共用建置設定](../../Directory.Build.props)、下列五個 csproj、[SDK 設定](../../global.json)、[CI workflow](../../.github/workflows/ci.yml) 與現有驗證腳本。傳遞相依版本另由已還原的 `build/obj/FreeformHelper.UI/project.assets.json`、`build/obj/FreeformHelper.Tests/project.assets.json` 的 `net8.0` target 核對；這兩份是本機產物，不是鎖檔。本次未連網、未執行 restore、未讀取私有測試資料。

無網路環境無法核實 Avalonia 12 的變更。本文每個涉及 12 的相容性、最低版本或建置要求的待查項，均標示 **to check against the official migration guide**；現行 11 的本機證據不能當成 12 的相容保證。

## 中央版本與直接引用

`Directory.Build.props` 設定 `ManagePackageVersionsCentrally=true`。所有 csproj 的 PackageReference 都未填寫 `Version` 或 `VersionOverride`，版本來自 `Directory.Packages.props`。

| 套件 | 中央版本 | 直接引用專案 |
| --- | --- | --- |
| `Avalonia` | `11.3.12` | UI |
| `Avalonia.Desktop` | `11.3.12` | UI |
| `Avalonia.AvaloniaEdit` | `11.4.1` | UI |
| `Avalonia.Themes.Fluent` | `11.3.12` | UI |
| `Avalonia.Fonts.Inter` | `11.3.12` | UI |
| `Avalonia.Headless` | `11.3.12` | Tests |
| `Avalonia.Headless.XUnit` | `11.3.12` | Tests |

UI 為 [FreeformHelper.UI.csproj](../../src/FreeformHelper.UI/FreeformHelper.UI.csproj)，Tests 為 [FreeformHelper.Tests.csproj](../../tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj)。Tests 引用 UI，會繼承 UI 的桌面、theme、font、editor 與 renderer 相依。

其餘三個專案為 [Domain](../../src/FreeformHelper.Domain/FreeformHelper.Domain.csproj)、[Application](../../src/FreeformHelper.Application/FreeformHelper.Application.csproj)、[Infrastructure](../../src/FreeformHelper.Infrastructure/FreeformHelper.Infrastructure.csproj)，沒有直接引用 Avalonia。Application 唯一直接套件是 `Clipper2 2.0.0`；Infrastructure 引用 Application／Domain，沒有自己的套件引用。

| 須共同檢查的直接相依 | 中央版本／引用 | 升級時的核對點 |
| --- | --- | --- |
| `CommunityToolkit.Mvvm` | `8.3.2`／UI | 現有 MVVM source generator 與新工具鏈的相容性：to check against the official migration guide。沒有證據要求跟著 Avalonia 改成相同版本。 |
| `System.Reactive` | `6.0.0`／UI | 新 Avalonia 套件圖是否仍接受目前版本：to check against the official migration guide。 |
| `Microsoft.NET.Test.Sdk` | `17.11.1`／Tests | 新 Headless 測試工具鏈所需版本：to check against the official migration guide。 |
| `xunit` | `2.9.2`／Tests | `Avalonia.Headless.XUnit` 的測試框架相容性：to check against the official migration guide。 |
| `xunit.runner.visualstudio` | `3.0.0`／Tests | test discovery 與 runner 相容性：to check against the official migration guide。 |

UI 另直接引用 `NLog 5.3.4`、`NLog.Extensions.Logging 5.3.4`；目前相依圖沒有證據要求它們隨 Avalonia 升版。

主套件、Desktop、Fluent、Inter、Headless、Headless.XUnit 的目前版本一致。未來應一起檢查這組引用與還原結果，避免 production 與 headless 使用不相容的 Avalonia 組件；12 的套件組合與版本對齊規則：to check against the official migration guide。`Avalonia.AvaloniaEdit` 有自己的版本序列，目前還原圖只宣告 Avalonia 最低相依 `11.0.0`；這不證明它支援 12，應核對相容發行版：to check against the official migration guide。

## 傳遞相依、renderer、原生庫與未引用套件

以下版本來自既有 restore assets；UI 與 Tests 均帶入這些相依。它們沒有中央 PackageVersion 或直接 PackageReference，不應只為盤點而新增 pin。

| 套件 | 已解析版本 | 現行來源／用途 |
| --- | --- | --- |
| `Avalonia.Skia` | `11.3.12` | Desktop、X11 帶入的 renderer |
| `Avalonia.Native` | `11.3.12` | Desktop 帶入 |
| `Avalonia.Win32` | `11.3.12` | Desktop 帶入 |
| `Avalonia.X11` | `11.3.12` | Desktop 帶入 |
| `Avalonia.FreeDesktop` | `11.3.12` | X11 帶入 |
| `Avalonia.Remote.Protocol` | `11.3.12` | Avalonia 主套件帶入 |
| `Avalonia.BuildServices` | `11.3.2` | Avalonia 主套件帶入的建置服務 |
| `Avalonia.Angle.Windows.Natives` | `2.1.25547.20250602` | Win32 帶入 |
| `SkiaSharp` | `2.88.9` | Avalonia.Skia 帶入 |
| `SkiaSharp.NativeAssets.Linux` | `2.88.9` | Avalonia.Skia 帶入 |
| `SkiaSharp.NativeAssets.WebAssembly` | `2.88.9` | Avalonia.Skia 帶入 |
| `SkiaSharp.NativeAssets.Win32` | `2.88.9` | SkiaSharp 帶入 |
| `SkiaSharp.NativeAssets.macOS` | `2.88.9` | SkiaSharp 帶入 |
| `HarfBuzzSharp` | `8.3.1.1` | Avalonia.Skia 帶入的文字渲染相依 |
| `HarfBuzzSharp.NativeAssets.Linux` | `8.3.1.1` | Avalonia.Skia 帶入 |
| `HarfBuzzSharp.NativeAssets.WebAssembly` | `8.3.1.1` | Avalonia.Skia 帶入 |
| `HarfBuzzSharp.NativeAssets.Win32` | `8.3.1.1` | HarfBuzzSharp 帶入 |
| `HarfBuzzSharp.NativeAssets.macOS` | `8.3.1.1` | HarfBuzzSharp 帶入 |
| `MicroCom.Runtime` | `0.11.0` | Avalonia 主套件帶入 |
| `Tmds.DBus.Protocol` | `0.21.2` | Avalonia.FreeDesktop 帶入 |
| `System.IO.Pipelines` | `8.0.0` | Tmds.DBus.Protocol 帶入 |

`Avalonia.BuildServices`、ANGLE、SkiaSharp、HarfBuzzSharp 與其他配套有各自的版本序列。12 所要求的 managed／native 套件配對、平台支援與相依版本：to check against the official migration guide。升版時重新審查整張還原圖與 native assets，不能由目前的 `11.3.12` 推算它們的目標版本。

`Avalonia.Diagnostics` 沒有中央宣告、csproj 引用或上述 assets 項目，因此沒有可列的現行版本；是否需要加入、替換或調整 Debug 建置的 diagnostics 配套：to check against the official migration guide。目前 theme／font 套件只有 Fluent／Fonts.Inter；沒有 `Avalonia.Themes.Simple` 或其他 Avalonia 字型套件。

## 鎖檔、restore 與 CI

目前 **0 份受追蹤的 `packages.lock.json`**；根目錄與五個 csproj 所在目錄也沒有此檔。`Directory.Build.props`、`Directory.Packages.props`、csproj、workflow 與建置／測試腳本沒有啟用 `RestorePackagesWithLockFile`、`RestoreLockedMode` 或 `--locked-mode`。中央版本管理固定的是直接引用版本，不等同鎖住完整傳遞相依。

`build/obj/<專案名>/project.assets.json` 與產生的 `*.nuget.g.props`／`*.nuget.g.targets` 是還原產物，不能當成 `packages.lock.json`。`scripts/perf/measure-code-size.ps1` 會收集受追蹤的 `*packages.lock.json` 作證據；這個收集動作不會建立鎖檔或啟用 locked mode。

`.github/workflows` 目前只有 `ci.yml`，沒有直接點名 Avalonia、Headless、Fonts.Inter、Diagnostics 或 SkiaSharp 的 step，也沒有專用套件 cache／lock restore step。實際依賴在以下入口：

| CI job／step | 執行入口 | 與升級有關的現況 |
| --- | --- | --- |
| `policy / structure`／`Verify structure` | `verify.ps1 -StructureOnly -AllowMissingExampleData` | 檢查 SDK pin、action pins、shard 一致性與結構；不是 restore 或套件相容性驗證。 |
| `dotnet / build`、各 test shard／`Install pinned .NET SDK and runtime` | `scripts/ci/install-dotnet.ps1` | 從 global.json 取得 SDK，從 Tests csproj 取得 runtime channel。 |
| `dotnet / build`／`Format, analyzers and build` | `verify.ps1 -CiLane build -AllowMissingExampleData` | 委派 `lint.ps1 -AllFiles -UseNoAppHost -WarningsAsErrors`；format／build 沒有禁止 restore，也沒有 locked mode。 |
| `dotnet / test (...)`／`Run test shard` | `verify.ps1 -CiLane test -Shard ...` | `run-tests.ps1` 呼叫 dotnet test，沒有 `--no-restore` 或 `--no-build`，會經過還原與建置。 |
| `dotnet / build-test`／`Require the build and every test shard to succeed` | 彙總 build／test 結果 | 本身不還原套件；目前 viewmodel shard 的 `continue-on-error` 是既有暫時例外，不能把彙總通過說成所有 shard 全過。 |

test matrix 是 `core, ui, viewmodel, snapshots`；`verify.ps1` 將 snapshots 對應至 `ui-snapshots`，包含 `UiLayoutGuardTests`、`UiRenderedVisualSnapshotTests`、`UiSnapshotPersistenceContractTests`、`UiVisualSnapshotTests`。`HeadlessUiSmokeTests` 在 `ui-stable`，由 ui shard 執行。

未來升級若採用鎖檔，先以非 locked 的 restore 更新／建立各專案 `packages.lock.json`，審查版本、依賴與 content hash 差異，再以 locked mode 重跑確認重現性。**目前沒有既有鎖檔可更新，也沒有既有 locked CI 可直接沿用**；是否納入鎖檔與 CI locked restore，應由實際升級工作決定，本次不新增此機制。12 是否要求額外 NuGet feed 或 restore 設定：to check against the official migration guide。

## Target framework、SDK 與建置條件

| 項目 | 現況與受影響位置 | 升級待查 |
| --- | --- | --- |
| Target framework | Domain、Application、Infrastructure、UI、Tests 五個 csproj 全是 `net8.0`，沒有 multi-target。 | 12 的最低 TFM／runtime 是否迫使變更，以及專案引用相容性：to check against the official migration guide。 |
| SDK | `global.json`：`10.0.301`、`rollForward=latestPatch`、`allowPrerelease=false`。本機 `dotnet --version` 為 `10.0.303`，是允許的 patch roll-forward。SDK 與 `net8.0` 是不同設定。 | 12 的最低 SDK／MSBuild 是否符合此範圍：to check against the official migration guide。 |
| CI 安裝 | `install-dotnet.ps1` 安裝 global.json 指定版本的 SDK，並依 Tests 的 `net8.0` 安裝 `8.0` runtime channel；不是在 workflow 寫死 SDK／TFM。 | 若需調整 TFM，核對所有專案與此推導邏輯；12 的 runtime 要求：to check against the official migration guide。 |
| C#／輸出 | UI、Domain、Application、Infrastructure 為 `LangVersion=latest`；Tests 未明訂。UI 為 `WinExe`，帶 application manifest／icon。 | 新 SDK 對 analyzer／generator 的影響與 12 的平台建置條件：to check against the official migration guide。 |
| 輸出位置 | `BaseOutputPath=build/bin/<專案名>/`、`BaseIntermediateOutputPath=build/obj/<專案名>/`，條件為 `ArtifactsPath` 未設定；`DefaultItemExcludes` 排除 build。 | 12 的 MSBuild targets 與產物位置假設：to check against the official migration guide。 |
| 發佈 | `scripts/build/publish-exe.ps1` 預設 `win-x64`、Release、self-contained、`PublishTrimmed=false`；single-file 另開 `IncludeNativeLibrariesForSelfExtract=true`。 | 新 renderer／native assets 的發佈與載入要求：to check against the official migration guide；一般 build 通過不能代替發佈驗證。 |

`verify.ps1 -StructureOnly` 只檢查 SDK version 為穩定的三段數字，不能證明該 SDK 或 TFM 適用 Avalonia 12。

## Avalonia analyzers 與 MSBuild 整合

UI csproj 明訂 `<AvaloniaResource Include="Assets\**" />`，另以 `None` 複製 NLog.config。沒有明訂 compiled-binding、XAML compilation 或 name-generator 的 Avalonia 屬性，也沒有自訂 Avalonia build task／analyzer 套件引用。

既有 `Avalonia 11.3.12` 套件內含 `Avalonia.Analyzers.dll`、`Avalonia.Generators.dll`、`Avalonia.Build.Tasks.dll`，以及 `build/`、`buildTransitive/` 的 props／targets；它們隨主套件帶入，不是三個獨立的中央版本。12 的 analyzer、generator、task 與 props／targets 契約：to check against the official migration guide。

| 目前 11.3.12 套件所提供的設定／流程 | 本機可核實的值 | 升級待查 |
| --- | --- | --- |
| `EnableAvaloniaXamlCompilation` | 未覆寫時 `true` | 12 的支援與預設：to check against the official migration guide。 |
| `AvaloniaUseCompiledBindingsByDefault`、`AvaloniaXamlIlVerifyIl` | 未覆寫時均為 `false` | 12 的支援與預設：to check against the official migration guide。本文不盤點個別 XAML／binding。 |
| `AvaloniaNameGeneratorIsEnabled`、`AvaloniaNameGeneratorBehavior`、`AvaloniaNameGeneratorDefaultFieldModifier` | `true`、`InitializeComponent`、`internal` | 12 的 generator 設定契約：to check against the official migration guide。 |
| `AvaloniaNameGeneratorAttachDevTools` | 套件預設 `true`；不代表本庫有引用 Diagnostics | 12 的 diagnostics／generator 整合：to check against the official migration guide。 |
| resource／XAML targets | `GenerateAvaloniaResources`、`CompileAvaloniaXaml`；套件預設收集 axaml／paml，參與編譯及 up-to-date inputs | 12 的資源編譯與增量建置要求：to check against the official migration guide。 |
| `Avalonia.BuildServices` | `11.3.2`，帶入 `AvaloniaStats` target；本次命令設定 `AVALONIA_TELEMETRY_OPTOUT=1` | 12 的建置服務與 opt-out 支援：to check against the official migration guide。 |

共用 .NET analyzer 政策是 `EnableNETAnalyzers=true`、`AnalysisLevel=latest-recommended`、`EnforceCodeStyleInBuild=true`、`GenerateDocumentationFile=true`，既有 `NoWarn` 只追加 `1572;1573;1591`。`.editorconfig` 沒有 Avalonia／AVLN 專用 severity 設定。不得為升級新增 warning suppression 或降低 lint 標準。

CI build lane 的 lint 會以 `dotnet format --severity warn` 驗證，既有 formatter 排除 IDE0059／IDE0060；analyzer build 使用 `-warnaserror --no-incremental`。新套件或 SDK 產生的診斷要在原 gate 下處理；12 的診斷變更：to check against the official migration guide。Tests 另解析 `xunit.analyzers 1.16.0`，須連同 runner／Headless 相容性重驗。

## 實際升版後必須重跑的順序

下列是未來經授權升版的執行清單，**不是本次已執行的命令**。所有 12 的套件目標版本、相依配對與工具鏈要求，先完成上述官方指南待查項。

1. **Restore 與 lock 差異。** 在可連網且具備所需 feed 的環境重新還原 solution，核對 UI／Tests 的完整相依圖，包含 SkiaSharp／HarfBuzzSharp native assets、AvaloniaEdit 與 test runner。若升級採用鎖檔，以 `dotnet restore FreeformHelper.sln --use-lock-file --force-evaluate` 建立／更新鎖檔；審查後再 `dotnet restore FreeformHelper.sln --locked-mode`，驗證 lock 與宣告一致。未採鎖檔時仍須重新 restore，但應明列無 locked-mode 保證。本次沒有執行這些命令。
2. **準備工作區並 build。** 依 AGENTS 執行 `scripts/dev/prepare-ui-workspace.ps1`；重建 UI 與 Tests，避免使用舊 Avalonia 編譯產物。可用 `dotnet build <專案> --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false`；UI 與 Tests 路徑見上文。另依現有發佈腳本檢查 win-x64 folder／single-file 的原生庫載入。
3. **完整 repo gate。** 在具備授權私有資料與完整工具鏈的環境執行 `pwsh -NoProfile -File ./scripts/verify.ps1 -All`，涵蓋 structure、全檔 lint／analyzer build、core／ui／viewmodel／snapshots 四個 shards。此入口可能隱含 restore，不適合本次禁止 restore 的 sandbox。確認 viewmodel 的實際結果，不能只看 CI aggregator。
4. **UI guards 與快照。** 至少確認 `HeadlessUiSmokeTests`、`UiLayoutGuardTests`、`UiSnapshotPersistenceContractTests`、`UiVisualSnapshotTests`、`UiRenderedVisualSnapshotTests`，並在 Dev page／真實 UI 核對目前的外觀契約。12 的 headless／renderer／字型行為：to check against the official migration guide。
5. **Baseline 先比較再決定。** 以既有 `scripts/tests/update-ui-baseline.ps1 -Mode DryRun` 比較；此腳本第一個 test 會 build 並可能 restore，應在外部完整環境執行。`FH_UI_BASELINE_MODE` 未設定時是 Check，`dry-run` 不寫入，只有 `apply` 會寫入。升版可能造成的差異須留存與人工核對，不能自動覆蓋 expected 或放寬距離門檻來讓 gate 通過；只有經確認的必要視覺差異才在獨立變更中採用 `-Mode Apply`，之後恢復 Check 並重跑 gate。

兩份 baseline 是 `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json`（來源檔正規化 SHA-256）與 `tests/FreeformHelper.Tests/Snapshots/ui-rendered-visual-baseline.json`（headless 渲染的 average hash／Hamming distance）。只改套件時前者可能不變，仍須重跑；後者須重驗 renderer／字型結果。升級不得默認取得 baseline 更新授權。

## 尚待確認

- 12 的正式套件清單、對齊規則、AvaloniaEdit 相容版本及 Diagnostics 要求：to check against the official migration guide。
- SkiaSharp、HarfBuzzSharp、ANGLE、BuildServices 與平台配套的目標版本：to check against the official migration guide。
- 最低 SDK／TFM／runtime、analyzer／generator、MSBuild、restore 與 publish 要求：to check against the official migration guide。
- Headless.XUnit／runner 組合與可接受的快照差異：to check against the official migration guide；外觀差異另依現有人工確認規則處理。
- 實際升級是否納入 `packages.lock.json` 與 CI locked restore：目前未實作，留給升級工作決定。本文件不建立新的設定或驗證機制。
