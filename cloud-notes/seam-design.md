# 接縫（Seam）設計提案：時間、延遲、環境變數、檔案 IO、子程序

- 日期：2026-11-05（雲端 session，僅分析與設計）。基準：`origin/main` = `5e48bec`。
- 本檔是唯一變更。`src/`、`tests/`、`eng/` 都沒有改動。分支 `cloud/seam-design`，未開 PR，未碰 `main`。
- 範圍：`cloud-notes/warnings-audit.md`（`origin/cloud/warnings-audit`）中，RS0030 的下列幾類：
  `DateTime(Offset).Now/UtcNow`、`Stopwatch`（含 `GetTimestamp`、`Environment.TickCount64`）、`Task.Delay`、`Thread.Sleep`、
  `Environment.Get/SetEnvironmentVariable`（含 `GetFolderPath`）、直接的 `File`/`Directory` IO；`Process.Start` 作為相鄰類別一併列出。
  `BindingFlags.NonPublic`（124）、`Dispatcher.UIThread`（32）與 `VSTHRD*`、`IDE*` 不在本檔範圍，仍照審計的批次處理。

## 1. 結論（先看這裡）

1. 範圍內共 **140 個 RS0030 站點**（正式程式 57、測試 83），佔審計 296 個 RS0030 的 47%。
2. 每一類只用**一個**接縫：
   | 類別 | 接縫 | 新增型別？ |
   |---|---|---|
   | 時鐘、經過時間、延遲、逾時 | `System.TimeProvider` | 否（repo 已在用） |
   | 環境變數、`GetFolderPath` | `IEnvironmentSource`（2 個成員） | 是，UI 內部型別 |
   | 檔案 IO | `IFileStore`（5 個成員）＋少數登記在 `seam-owners.json` 的擁有者 | 是，UI 內部型別 |
   | 子程序 | 測試用 Nvt.Core 的 `IExternalProcessRunner`；正式程式只有 1 處，登記為擁有者 | 否 |
3. 測試端的共用輔助（`ManualTimeProvider`、`SignalWait`、`TestEnvironment`）放在 `tests/FreeformHelper.Tests/TestSupport/`。
   `eng/core-health/repo-health.ps1` 的規則 T2 只認路徑中含 `TestSupport` 的目錄或 `*.TestSupport` 專案為「假時鐘、等待輪詢的合法擁有者」。
   目前 `TestInfrastructure/` 不在此列。
4. 批次順序為**測試優先**：6 個純測試批次（S-01…S-06，共 76 站點）→ 8 個正式程式批次（P-01…P-08，各自帶對應測試）→ 1 個收尾批次（P-09）。
   每批 ≤ 20 站點，一批一個 PR。這 15 批取代審計的 B-08…B-20（兩邊都是 140 站點，已逐一核對）；B-01…B-07、B-21…B-30 不受影響。
5. 約 125 個站點可直接消失；約 15 個會變成登記在 `eng/code-health/seam-owners.json` 的 `permanent-seam`（該檔另計，不算債務）。
6. 審計有兩處需要更正（見 §3.1）：沒有 `TimeProvider.Delay` 這個 API；`Stopwatch` 型別在正式程式本來就沒被禁。
7. 有 7 個決定需要 Owner 先拍板（§8）。其中 D5（`AppGeneralSettingsStore`）依 AGENTS.md 屬於「不得在沒有明確任務下變更」的執行期機制。

## 2. 方法與限制

- 以 .NET SDK 10.0.301 重新建置 `FreeformHelper.sln`（Debug，`TreatWarningsAsErrors=false`，`--no-incremental`），把 RS0030 依 檔案/行/欄/符號/專案 去重，得到 **295** 筆。
  再加上審計指出的 Release 專屬 `src/FreeformHelper.UI/Program.cs:83`，共 **296**，與審計逐項一致（符號分佈也一致：`Stopwatch` 36、`Stopwatch.GetTimestamp` 14、`DateTime.UtcNow` 10、`Task.Delay` 共 23 等）。
- 「未被旗標」的同類用法（例如 `File.Exists`、`Directory.*`、正式程式的 `Stopwatch.StartNew()`）用 grep 盤點，會標示為「未旗標」。這些不是警告，但屬於同一個接縫。
- 讀過的設定與工具：`eng/core-health/BannedSymbols.*.txt`、`eng/code-health/*/BannedSymbols.txt`、`eng/code-health/seam-owners.json`（目前 `entries: []`）、`eng/code-health/test-debt.json`、`eng/core-health/schema.json`、`eng/core-health/repo-health.ps1`，以及 `artifacts/core-packages` 內 Nvt.Core 0.9.5 / Nvt.Core.Avalonia 0.9.5 的 XML 文件。
- **未執行**：測試、`repo-health.ps1 -Mode Verify`（沒有 `pwsh`）、Release 建置（沿用審計的 +1）、Windows 專屬程式碼。
  §5 的帳本影響是依 `repo-health.ps1` 與 `DebtBaselineTests` 的規則推算，實際數字要由 `-Mode LowerBaseline` 產生，不可手改。
- 受限於 `eng/core-health.lock.json` 的雜湊鎖定，`BannedSymbols.Common/Files/Tests.txt` 與 `repo-health.ps1` 不能在本 repo 修改。
  本 repo 能調整的只有各層的 `eng/code-health/<layer>/BannedSymbols.txt` 與 `seam-owners.json`。

## 3. 盤點

### 3.1 禁用清單怎麼套用（影響接縫該怎麼選）

| 禁用項 | 適用層 |
|---|---|
| `DateTime(Offset).Now/UtcNow`、`Environment.TickCount64`、`Stopwatch.GetTimestamp`、`Thread.Sleep`、`Task.Delay`（不含 TimeProvider 的 4 個多載）、`Process.Start`、`Environment.Get/SetEnvironmentVariable`、`Environment.GetFolderPath`、`Dispatcher.UIThread` | 全部層（`Common`） |
| `File.Copy/Move/ReadAll*/WriteAll*`（及 Async） | 全部層（`Files`） |
| `T:System.IO.File`（整個型別） | 僅 Domain、Application |
| `T:Stopwatch`、`Path.GetTempPath`、`BindingFlags.NonPublic` | 僅 Tests |

兩個後果：

- **沒有 `TimeProvider.Delay`**。審計 §6 寫的「`TimeProvider.Delay`/fake timer」不存在。正確寫法是 `Task.Delay(TimeSpan, TimeProvider, CancellationToken)`，
  這個多載不在禁用清單內。逾時請用 `task.WaitAsync(TimeSpan, TimeProvider, CancellationToken)`。
- **正式程式的 `Stopwatch.StartNew()` 不會被旗標**（36 處未旗標，分佈在 17 個檔案），只有 `Stopwatch.GetTimestamp()` 被旗標（14 處）。
  所以若只修旗標的 14 處，同一個類別裡會留下混用的兩條計時路徑，違反 AGENTS.md「同一結果只能有一條推導路徑」。批次因此以「整個類別」為單位遷移。

### 3.2 範圍內站點數（RS0030，已旗標）

| 類別 | Application | Infrastructure | UI | 正式程式小計 | Tests | 合計 |
|---|---:|---:|---:|---:|---:|---:|
| 時鐘（`DateTime(Offset).UtcNow` 18、`TickCount64` 6、`Stopwatch.GetTimestamp` 14、`Stopwatch` 36） | 5 | 1 | 25 | 31 | 43 | **74** |
| 延遲（`Task.Delay` 23、`Thread.Sleep` 1） | 0 | 0 | 9 | 9 | 15 | **24** |
| 環境（`GetEnvironmentVariable` 18、`SetEnvironmentVariable` 3、`GetFolderPath` 1）\* | 0 | 0 | 3 | 3 | 19 | **22** |
| 檔案（`File.*` 與 `T:File`） | 3 | 1 | 8 | 12 | 3 | **15** |
| 子程序（`Process.Start`） | 0 | 0 | 1 | 1 | 3 | **4** |
| **合計** | 8 | 2 | 46 | 56 | 83 | **139** |

\* 另有 1 個 Release 專屬站點（`Program.cs:83`，`#else` 分支），加上後共 **140**（正式程式 57）。Debug 建置看不到它。

不在本檔範圍的 RS0030：`BindingFlags.NonPublic` 124、`Dispatcher.UIThread` 32（139 + 156 = 295，加 Release 專屬 1 = 296）。

### 3.3 同一類別但沒有被旗標的用法（會落在同一個接縫，需一起考慮）

| 用法 | 正式程式 | 測試 |
|---|---:|---:|
| `Stopwatch` 型別（`StartNew`、`Elapsed*`、`GetElapsedTime`） | 36（17 檔） | 0（測試端 `Stopwatch` 型別已全被旗標） |
| `File.Exists/Open/Create/Delete/OpenRead` | 14（9 檔） | 286（63 檔） |
| `Directory.CreateDirectory/Delete/Exists/Enumerate*/CreateTempSubdirectory` | 8（5 檔） | 34（16 檔） |
| `new StreamReader/StreamWriter/FileStream(path…)` | 8（5 檔） | 17（7 檔） |

測試端 286 處 `File.Exists` 等是對暫存目錄做的真實 IO 斷言，**不建議**轉成假檔案系統（AGENTS.md：Real inputs first）。測試端的檔案接縫已經存在，見 §4。

### 3.4 測試端的時間用法其實只有五種型態

| 型態 | 站點 | 例 |
|---|---:|---|
| A. 輪詢等待（`TickCount64`/`Stopwatch` + `Task.Delay`） | 11（其中 2 份是同名複製的 `WaitForConditionAsync`） | `FreeformHelperViewModelTests.Helpers.cs:227`、`ShellViewModelSimulationTests.cs:354`、`IndexMappingReportViewModelTests.cs:10` |
| B. 逾時守衛（`Task.WhenAny(t, Task.Delay(n))`） | 3 | `RuntimeQueryIpcTests.cs:31,59`、`CadLoadSpinnerProcessHostTests.cs:85` |
| C. 假工作／假對端的持續時間 | 3 | `ShellViewModelSimulationTests.cs:266`、`LoadingScopeCoordinatorTests.cs:20`、`RuntimeQueryIpcTests.cs:86` |
| D. 「等一下再確認沒發生」（負向等待） | 3 | `ShellViewModelSimulationTests.cs:275`、`…SettingsPersistence.AppGeneral.cs:175`（700 ms）、`HeadlessUiSmokeTests.cs:303` |
| E. 其他：邊界值取現在時間 2、`SavedAtUtc` 1、檔案鎖重試 `Thread.Sleep` 1、`LoadingScopeCoordinatorTests` 的經過毫秒斷言 4、基準測試（真實時鐘，刻意） 30 | 38 | `PadCanvasViewRefreshTests.cs:16,40`、`DxfLayerImageExportServiceTests.cs:212`、`Cad3635LoadBenchmarkTests.cs` |

型態 D 的 3 個站點必須先等正式程式的接縫（或決定 D6），其餘 55 個時間站點（58 − 3；含型態 A、B、C、E）只改測試即可。

## 4. 已經存在、應直接沿用的東西

| 東西 | 位置 | 對本提案的意義 |
|---|---|---|
| `TimeProvider` 注入慣例 | `JsonProjectStore(TimeProvider? timeProvider = null)`，`ConsoleLinkOpener` | 沿用「選用的尾端參數、預設 `TimeProvider.System`」。316 處 `new FreeformHelperViewModel(…)`、64 處 `new NotchTableGenerator(`/`NotchV22CompensationService(` 都不需要改。 |
| Nvt.Core 已接受 `TimeProvider` | `LoadingScopeCoordinator(Func<Task>, TimeSpan, TimeProvider)`、`ThrottledProgress<T>(…, TimeProvider)`、`StartupTrace.Create(…, TimeProvider, …)`、`Time.DelegateTimeProvider` | 使用這些 Core 型別的測試可以直接餵假時鐘。需在 S-01 驗證 Core 0.9.5 的 `LoadingScopeCoordinator` 是用該 provider 等待，而不是只收下參數。 |
| Nvt.Core 的子程序、原子寫入 | `Processes.IExternalProcessRunner`／`SystemExternalProcessRunner`、`IO.AtomicOutput`、`Files.BoundedFileReader` | 測試跑 gcc 可用 `IExternalProcessRunner`。它是「跑到結束並回傳結果」的模型，**不適合**常駐的 spinner 子程序（見 §6.4）。 |
| 測試檔案接縫 | `TestInfrastructure/TestFiles.cs`（近三個 commit 已把測試 IO 導到這裡）、`TestDirectoryCleanup.cs` | 測試端的檔案接縫已存在。剩下的 3 個旗標站點（golden 基準更新）改走 `TestFiles.WriteText` 即可。 |
| 設定檔的私有接縫 | `AppGeneralSettingsStore` 的 `_readTextOverride`／`_saveOverride` | 目前已有一個專用的函式覆寫接縫。`IFileStore` 可以取代它（D5）。 |
| 純函式式的時間參數 | `PadCanvas.ComputeLowDetailMode(…, nowTicks)`、`ShouldDeferSecondaryVisuals(…, nowTicks)` | `PadCanvasViewRefreshTests` 的 2 個 `DateTime.UtcNow.Ticks` 只要改成常數，**不需要任何接縫**。 |
| 帳本機制 | `eng/code-health/seam-owners.json`（schema：`path`、`member`、`symbol`、`owner`、`reason`、`kind = "permanent-seam"`、`review`；檔案層級用 `member = "*"`；`symbol` 不接受萬用字元） | 這是「唯一允許呼叫被禁 API 的地方」的正式登記處。計數另列，不算債務。 |
| 測試輔助的合法擁有者規則 | `repo-health.ps1` 的 `OwnsSharedHelpers`：專案名為 `TestSupport` 或 `*.TestSupport`，或路徑中含 `TestSupport` 目錄 | 私有 `FixedTimeProvider`（`JsonProjectStoreAtomicSaveTests.cs:183`）目前就是帳本中的 `fakeClockDuplicates` 1 筆。共用的假時鐘必須放在這個位置才不會被記為重複。 |
| 帳本提示訊息 | `repo-health.ps1:880`：「Use the Nvt.Core.TestSupport helper (SignalWait, ManualTimeProvider, TestWorkspace, ChildProcessFixture) or one shared fake」 | Nvt.Core 0.9.5 / Nvt.Core.Avalonia 0.9.5 / Nvt.Core.Fonts 0.1.0 這三個已釘版的套件**沒有** TestSupport。所以 NFH 要自己放一份，名稱對齊 Core，之後可直接換成 Core 版（D1）。 |

## 5. 每類一個接縫

### 5.1 時間（時鐘、經過時間、延遲、逾時）→ `System.TimeProvider`

**用法對照**

| 現在 | 之後 |
|---|---|
| `DateTimeOffset.UtcNow` | `_time.GetUtcNow()` |
| `DateTime.UtcNow.Ticks`、`Environment.TickCount64` | `_time.GetTimestamp()`（需要壁鐘時用 `GetUtcNow()`） |
| `Stopwatch.StartNew()` … `.ElapsedMilliseconds` | `var t0 = _time.GetTimestamp();` … `_time.GetElapsedTime(t0).TotalMilliseconds` |
| `Stopwatch.GetTimestamp()`、`Stopwatch.GetElapsedTime(t)` | `_time.GetTimestamp()`、`_time.GetElapsedTime(t)` |
| `Task.Delay(ms, ct)` | `Task.Delay(TimeSpan.FromMilliseconds(ms), _time, ct)` |
| `Task.WhenAny(t, Task.Delay(n))` 當逾時 | `await t.WaitAsync(TimeSpan, _time, ct)` |
| `Thread.Sleep(n)` 重試 | 測試：共用重試輔助；正式程式：目前無 |

**注入方式（沿用 `JsonProjectStore` 的既有慣例）**

```csharp
// 服務與 ViewModel：選用的尾端參數，既有呼叫端不用改
public NotchV22CompensationService(/* 既有參數 */, TimeProvider? timeProvider = null)
{
    _time = timeProvider ?? TimeProvider.System;
}

// Avalonia 控制項（XAML 建立，只能有無參數建構式）：一般 CLR 屬性，預設 System
public sealed partial class PadCanvas
{
    internal TimeProvider Time { get; set; } = TimeProvider.System;
}
```

**分兩層規則（避免把 `TimeProvider.System` 灑滿全 repo）**

- *行為時間*（會影響程式行為：去抖、重試間隔、最短顯示時間、寫進檔案的時間戳、節流）→ 必須注入。
- *純診斷時間*（只寫進 log 的時間戳或耗時，沒有任何分支依賴它，例如 `CadLoadSpinnerDebugState`、`StartupPerfTracker`）→ 可直接用 `TimeProvider.System`；`ConsoleLinkOpener` 已是這樣做。
- 可選的硬化步驟（D2）：全部遷移後，在 `eng/code-health/presentation/BannedSymbols.txt` 加 `P:System.TimeProvider.System`，把允許的組合根與診斷靜態類別登記進 `seam-owners.json`。這會讓「哪裡用真實時間」變成可 grep、可棘輪的清單。

**測試端**

```csharp
// tests/FreeformHelper.Tests/TestSupport/ManualTimeProvider.cs
internal sealed class ManualTimeProvider : TimeProvider
{
    public ManualTimeProvider(DateTimeOffset start);
    public void Advance(TimeSpan by);                // 依到期順序觸發計時器，Task.Delay(…, provider, …) 也由此完成
    public override DateTimeOffset GetUtcNow();
    public override long GetTimestamp();             // TimestampFrequency = TimeSpan.TicksPerSecond
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period);
}
```

示範（改寫 `LoadingScopeCoordinatorTests.RunAsync_RespectsMinimumVisibleDuration`，現況用真實 `Stopwatch` 並斷言「≥ 45 ms」，在負載高的機器上可能不穩）：

```csharp
var time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero));
var states = new List<bool>();
var coordinator = new LoadingScopeCoordinator(
    static () => Task.CompletedTask, TimeSpan.FromMilliseconds(60), time);

var run = coordinator.RunAsync(static () => Task.CompletedTask, states.Add);

Assert.Equal([true], states);                     // 最短顯示時間內不會關閉
time.Advance(TimeSpan.FromMilliseconds(60));
await run.WaitAsync(TimeSpan.FromSeconds(5));     // 守衛逾時，不是被測行為
Assert.Equal([true, false], states);              // 斷言狀態，不是經過毫秒
```

輪詢等待（型態 A，取代 2 份同名的 `WaitForConditionAsync`、`WaitForSpinnerIdleAsync`，以及 `IndexMappingReportViewModelTests`、`RuntimeQueryCoreBehaviorTests` 內的迴圈）：

```csharp
// TestSupport/SignalWait.cs：repo-health T2 指定的「等待輪詢」唯一擁有者
public static Task UntilAsync(Func<bool> condition, TimeSpan? timeout = null, CancellationToken ct = default);

// 呼叫端沒有任何時間 API
await SignalWait.UntilAsync(() => Volatile.Read(ref prewarmCount) > 0);
```

邏輯上能用訊號（`TaskCompletionSource`、既有的 `RecordingBackgroundWorkRunner`）的地方優先用訊號，輪詢是退而求其次。基準測試（30 個站點）刻意量真實時間，經由 `TestSupport/BenchmarkClock` 統一使用 `TimeProvider.System`，不是假時鐘。

**已知限制**：有 17 處 `DispatcherTimer`（例如 `AppSettings` 的 450 ms 去抖、`PadCanvas` 的暫態低細節計時器）由 Avalonia 的 Dispatcher 驅動，`TimeProvider` 無法控制。這些不是 RS0030 站點，但型態 D 的 3 個測試站點依賴它們，見 D6。

### 5.2 環境 → `IEnvironmentSource`

```csharp
// src/FreeformHelper.UI/Services/IEnvironmentSource.cs（internal；只有 UI 用到，所以不放 Application）
internal interface IEnvironmentSource
{
    string? GetVariable(string name);
    string GetFolderPath(Environment.SpecialFolder folder);
}

// SystemEnvironmentSource：全 repo 中唯一直接呼叫 Environment.GetEnvironmentVariable / GetFolderPath 的類別
// → 在 seam-owners.json 登記 2 筆
```

- 組合根（`Program.BuildAvaloniaApp`、`StartupPerfTracker` 的靜態初始化）在 DI 之前就要讀變數，無法注入。這 2 處直接登記為 `permanent-seam`，不改程式。
- `AppGeneralSettingsStore.BuildDefaultSettingsPath()` 改為吃 `IEnvironmentSource`。
  **環境變數的名稱與 `{pid}`/`{guid}` 樣板維持原樣**（見 D5：這是 AGENTS.md 列出的四個執行期機制之一）。
- 測試端：`AppGeneralSettingsStoreTests` 目前用 `Environment.SetEnvironmentVariable` 改整個行程的環境，再還原（3 站點）。改成注入 `FakeEnvironmentSource`（放在 TestSupport，僅此一個檔案宣告，不會觸發 `sameNameFakes`）就不再改動行程全域狀態，平行執行也不會互相干擾。

```csharp
var env = new FakeEnvironmentSource { [AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable] = path };
var store = new AppGeneralSettingsStore(env);      // 不再 Set/Restore 行程環境變數
```

- `TestAppSettingsIsolation` 的 `Environment.SetEnvironmentVariable`（1 站點）是**刻意**的行程級保險：萬一某個測試漏用注入，仍不會寫到使用者真實設定。這個機制要保留，登記為測試端擁有者。
- 其餘 15 個測試端 `GetEnvironmentVariable`（基準測試開關、golden 基準更新開關、字型路徑、範例資料開關）讀的是「測試執行器的設定」，沒有可注入的假值。統一收進 `TestSupport/TestEnvironment`：`Flag(name)`、`Int(name)`、`Path(name)`。
  同時消除三份幾乎相同的 `ReadInt`（`Cad3635LoadBenchmarkTests.cs:176`、`Cad3635EndToEndBenchmarkTests.cs:224`、`Notch3635GenerationBenchmarkTests.cs:145`）。此類只需登記 1 筆擁有者。

### 5.3 檔案 IO → 先把 IO 移出 Application，其餘走 `IFileStore`

不是所有 15 個站點都該用同一個接縫。依規則分三種處理：

| 處理 | 站點 | 說明 |
|---|---:|---|
| 把 IO 移出 Application（不需要接縫） | 3 | `DiffFrameCsvImporter`（`File.ReadAllLines`、型別 `File`）與 `RegularVisibilityMaskService`（`File.Exists`）改成收「已讀好的內容」（行或 `TextReader`），由 UI 呼叫端讀檔。順便清掉 `debt-baseline.json` 的 `domainAndApplicationPlatformIo` 2 筆。 |
| 已經是接縫擁有者的類別，登記而非改寫 | 3 | `JsonProjectStore.Load`（`File.ReadAllText`）、`AppGeneralSettingsStore.TryLoad/Save`（`ReadAllText`、`WriteAllText`）。前者本身就是專案檔的 IO 邊界，後者已有私有覆寫接縫。 |
| 其餘正式程式站點走 `IFileStore` | 6 | `ProjectPersistenceUseCase`（`ReadAllBytes` ×2）、`FreeformHelperViewModel.DxfEditing.Export`/`Persistence`（`WriteAllText` ×2）、`SimulationWorkspaceView`/`CoordinatePlannerWorkspaceView`（`WriteAllTextAsync` ×2） |
| 測試 golden 基準更新 | 3 | `NotchGoldenBaselineTests` ×2、`Tm81NotchAcceptanceMatrixTests` ×1 → 走既有的 `TestFiles.WriteText` |

```csharp
// src/FreeformHelper.UI/Services/IFileStore.cs（internal）：恰好是「非擁有者的正式程式今天用到的被禁呼叫」
internal interface IFileStore
{
    bool Exists(string path);
    byte[] ReadAllBytes(string path);
    string[] ReadAllLines(string path);
    void WriteAllText(string path, string text);
    Task WriteAllTextAsync(string path, string text, CancellationToken cancellationToken);
}
// SystemFileStore：唯一直接呼叫 File.* 的新類別 → seam-owners.json 登記 5 筆
```

測試：IO 格式或檔案行為的測試仍用真實暫存目錄（`TestFiles`）。只有「行為與 IO 無關、卻因 IO 難測」的情況才用 `InMemoryFileStore`（TestSupport），例如匯出失敗時的錯誤路徑。

**需 Owner 注意的取捨（D4）**：把 `File.ReadAllText` 換成 `new StreamReader(path)`，分析器就不再報告，但呼叫端依然無法替換。
`TestFiles` 近三個 commit 已經用這種方式處理測試端。AGENTS.md 禁止「以等價方法壓制警告」。我的建議是：在正式程式裡用 `seam-owners.json` 明確登記，而不是悄悄換成未被禁的等價呼叫。
這樣被禁 API 的剩餘呼叫仍然可見、可計數。

附帶說明：兩個 View 的 `File.WriteAllTextAsync(file.Path.LocalPath, …)` 其實拿到的是 Avalonia `IStorageFile`。以 `OpenWriteAsync()` 寫入才是平台正確的做法，但這會改變非本機 URI 的行為，所以不放在本批次的範圍內，只在此記錄。

### 5.4 子程序（相鄰類別，4 站點）

- 測試（`NotchTableExporterTests.cs:1359,1432,1475`，gcc 編譯檢查）：改用 Nvt.Core 的 `IExternalProcessRunner`／`SystemExternalProcessRunner`，包在 TestSupport 的 `GccRunner` 內。對應 `test-debt.json` 的 `childProcessInTests`（6 筆）。
- 正式程式（`CadLoadSpinnerProcessHost.cs:453`）：spinner 是常駐子程序，透過具名管道通訊。`IExternalProcessRunner` 的模型是「跑到結束再回傳結果」，不適合。
  它已經隱身在 `ICadLoadSpinnerHost` 後面，並有 `DisableProcessLaunchSwitch` 讓測試不會真的啟動。建議直接把這唯一一處 `Process.Start(ProcessStartInfo)` 登記為擁有者，並附上資安審查紀錄（審計已標為 high）。

## 6. 為什麼是這個順序（測試優先）

1. 測試端 83 站點中有 76 個**完全不需要改正式程式**（S-01…S-06），也不會改變任何產品行為。
2. S-01 先建立 `TestSupport/` 與 `ManualTimeProvider`、`SignalWait`，後續批次都用它。這一批同時把 `JsonProjectStoreAtomicSaveTests` 的私有 `FixedTimeProvider` 換成共用版（帳本 `fakeClockDuplicates` −1）。
3. 正式程式的接縫一旦建立，對應的測試才能變成確定性的。所以 P 批次先動「對測試最有幫助、風險最低」的，風險高的（PadCanvas 熱路徑、spinner 子程序）排後面。
4. 不要在同一個 PR 改同一個檔案（AGENTS.md「一批一 PR」）。與其他規則批次有檔案重疊的有 14 個檔案：
   | 檔案 | 本批次 | 重疊的審計批次 |
   |---|---|---|
   | `UI/Services/CadLoadSpinnerProcessHost.cs` | P-08 | B-01（IDE1006）、B-03（VSTHRD002/200）、B-05（VSTHRD003） |
   | `UI/Services/StartupPerfTracker.cs` | P-05 | B-01（IDE1006） |
   | `UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs` | P-01 | B-05（VSTHRD003） |
   | `UI/Controls/PadCanvas.ViewRefresh.cs` | P-03 | B-21（`Dispatcher.UIThread`） |
   | `UI/Views/SimulationWorkspaceView.axaml.cs`、`UI/Views/CoordinatePlannerWorkspaceView.axaml.cs` | P-07 | B-21 |
   | `Application/Cad/Cad3635LoadBenchmarkTests.cs` | S-03 | B-02（IDE1006）、B-23…B-30（BindingFlags） |
   | `Application/Notch/Tm81NotchAcceptanceMatrixTests.cs` | S-05 | B-02、B-23…B-30 |
   | `Application/Notch/NotchGoldenBaselineTests.cs` | S-05 | B-23…B-30 |
   | `UI/Services/CadLoadSpinnerProcessHostTests.cs`、`UI/Services/RuntimeQueryIpcTests.cs` | S-02 | B-23…B-30 |
   | `UI/Services/RuntimeQueryCoreBehaviorTests.cs` | S-01 | B-07（VSTHRD003）、B-23…B-30 |
   | `UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs` | S-01 | B-23…B-30 |
   | `UI/ViewModels/ShellViewModelSimulationTests.cs` | S-01、P-04 | B-07 |
   處理方式：重疊檔案的批次依序合併（後者從更新後的 `main` 分出），或把同一檔案的兩批合成同一個 PR。P-08 建議與 B-03/B-05 合併成同一個高風險 PR，因為三者都在動 `CadLoadSpinnerProcessHost`。

## 7. 批次表（測試優先；每批 ≤ 20 站點；站點 = 一個 RS0030 警告）

| 批次 | 內容 | 站點 | 層 | 風險 | 需先決定 |
|---|---|---:|---|---|---|
| **S-00** | 建立 `tests/FreeformHelper.Tests/TestSupport/`：`ManualTimeProvider`、`SignalWait`、`TestEnvironment`、`BenchmarkClock`；`JsonProjectStoreAtomicSaveTests` 改用共用假時鐘。可與 S-01 合併為同一個 PR。 | 0（`fakeClockDuplicates` −1） | tests | 低 | D1 |
| **S-01** | 測試時間，只改測試：`PadCanvasViewRefreshTests`（常數）2、`AppGeneralSettingsStoreTests:101` 1、`LoadingScopeCoordinatorTests` 5（假時鐘進 Core 建構式）、三份輪詢輔助與迴圈（`Helpers.cs` 3、`ShellViewModelSimulationTests` 4、`IndexMappingReportViewModelTests` 3）、`HeadlessUiSmokeTests:439` 1、`RuntimeQueryCoreBehaviorTests:185` 1 | 20 | tests | 低 | D1 |
| **S-02** | 測試逾時與假對端：`RuntimeQueryIpcTests` 3（`WaitAsync`、訊號）、`CadLoadSpinnerProcessHostTests:85` 1、`DxfLayerImageExportServiceTests:212` 的 `Thread.Sleep` 重試 1 | 5 | tests | 中（IPC 逾時語意） | — |
| **S-03** | 基準測試 A：`Cad3635LoadBenchmarkTests`（時鐘 15、環境 3） | 18 | tests | 低 | D7 |
| **S-04** | 基準測試 B 與其他環境開關：`Cad3635EndToEndBenchmarkTests`（時鐘 12、環境 3）、`FontAdoptionTests` 2、`ExampleData` 1、`UiBaselineUpdateMode` 1 | 19 | tests | 低 | D7 |
| **S-05** | `Notch3635GenerationBenchmarkTests`（時鐘 3、環境 3）、`NotchGoldenBaselineTests`（環境 1、檔案 2）、`Tm81NotchAcceptanceMatrixTests`（環境 1、檔案 1） | 11 | tests | 低 | D4 |
| **S-06** | gcc 子程序：`NotchTableExporterTests` 改用 Nvt.Core 的 `IExternalProcessRunner` | 3 | tests | 中 | — |
| **P-01** | 正式程式時間戳（行為時間）：`ProjectFile.cs:24`、`CadLoadSpinnerDebugState:163`、`FreeformHelperViewModel.NotchTableGeneration:99,212`、`…NotchExport.Helpers:159,210`。<br>`ProjectFile.SavedAt` 的預設初始化式與 `JsonProjectStore` 在存檔時設定的值是兩條推導路徑，應刪除前者；**但缺欄位的舊檔案反序列化後的預設值會改變（由「現在」變成 `DateTimeOffset.MinValue`），要先查讀取端並在 PR 中明講預設值有變。** | 6 | UI、Infra | 低–中 | — |
| **P-02** | Application 計時：`NotchV22CompensationService.Stages.cs` 5，整個 `NotchV22CompensationService` 與 `NotchTableGenerator` 的未旗標 `Stopwatch.StartNew()` 約 12 處一併改（建構式選用 `TimeProvider? timeProvider = null`，64 處呼叫端不變）。要用 3635 基準比對前後耗時（見 §9）。 | 5（另約 12 未旗標） | Application | 中 | D3 |
| **P-03** | `PadCanvas` 熱路徑：`Rendering.cs` 9、`ViewRefresh.cs` 3、`Input.PointerMove.cs` 2、`RenderFrame.cs` 1；同類別未旗標的 `Perf.cs`、`VisibleDrawListBuilder`、`SelectionEngine` 的 `Stopwatch` 一併改。屬性注入。要量測渲染耗時與配置量。 | 15（另約 3 未旗標） | UI | 中–高 | D3 |
| **P-04** | 正式程式延遲：`PadInspectorDeferred` 2、`Selection.NotchPreview` 1、`NotchDetails.AutoPlay` 1、`ShellViewModel.Workspaces:186` 1、`DevView.axaml.cs:50` 1（`Task.Delay(…, _time, ct)`）；同批把 `ShellViewModelSimulationTests:275`（負向等待）改成 `Advance`。 | 6 + 測試 1 | UI | 中 | — |
| **P-05** | 環境接縫與 `AppGeneralSettingsStore`（時間 3、環境 2、檔案 2）、`StartupPerfTracker:17` 與 `Program.cs:83`（登記擁有者）、`AppGeneralSettingsStoreTests`（環境 3）、`TestAppSettingsIsolation`（環境 1，登記擁有者）。**需要 Owner 的明確任務。** | 9 + 測試 4 | UI、tests | 高（觸及執行期機制） | D5 |
| **P-06** | 把 IO 移出 Application：`DiffFrameCsvImporter` 2、`RegularVisibilityMaskService` 1；`domainAndApplicationPlatformIo` 2→0 | 3 | Application | 低–中 | — |
| **P-07** | `IFileStore` 與其使用者：`ProjectPersistenceUseCase` 2、`FreeformHelperViewModel.DxfEditing.Export` 1、`…Persistence.cs` 1、兩個 Workspace View 2、`JsonProjectStore` 1（登記擁有者） | 7 | UI、Infra | 中 | D4 |
| **P-08** | `CadLoadSpinnerProcessHost`：時間 2、延遲 3、`Process.Start` 1（登記擁有者）。與 B-01/B-03/B-05 合併成同一個 PR，需 headless 冒煙測試與關閉流程驗證。 | 6 | UI | 高 | — |
| **P-09** | 剩下依賴 `DispatcherTimer` 的兩個負向等待：`…SettingsPersistence.AppGeneral.cs:175`（700 ms）、`HeadlessUiSmokeTests.cs:303`（120 ms） | 2 | tests | 中 | D6 |
| 合計 | | **140**（正式 57、測試 83） | | | |

核對：S-01…S-06 = 76，P-04 測試 1，P-05 測試 4，P-09 = 2 → 測試 83。P-01…P-08 正式程式 = 6+5+15+6+9+3+7+6 = 57。
與審計 B-08…B-20 的合計一致（20+11+15+19+9 + 9+15 + 4+19 + 12+3 + 1+3 = 140）。

## 8. 需要 Owner 決定的事項

| # | 問題 | 建議 | 若不照建議 |
|---|---|---|---|
| D1 | 共用測試輔助放哪裡？ | 新建 `tests/FreeformHelper.Tests/TestSupport/` 資料夾，名稱對齊 Core（`ManualTimeProvider`、`SignalWait`、`TestWorkspace`）。路徑含 `TestSupport` 即可被 `OwnsSharedHelpers` 視為擁有者（需在 Verify 實跑確認）。不搬動 `TestInfrastructure/`：搬移會讓帳本指紋改變（AGENTS.md 債務規則 2）。 | 另開 `FreeformHelper.Tests.TestSupport` 專案：隔離更乾淨，但需要 `projects.props` 的 `HealthLayer`、解決方案與 lock 檔變更。或等 Core 提供 `Nvt.Core.TestSupport` 套件（目前不存在）。 |
| D2 | `TimeProvider.System` 可以出現在哪裡？是否禁止其餘位置？ | 先採兩層規則（§5.1），遷移完成後再決定要不要用 `BannedSymbols.txt` 加棘輪。 | 不加棘輪：接縫靠慣例維持，沒有工具擋。 |
| D3 | 正式程式的 `Stopwatch` 是否整批遷移，並在遷移完成後禁用 `T:Stopwatch`？ | 以類別為單位遷移（避免同一類別混用兩條路徑），全部完成後在 `application`/`presentation` 的 `BannedSymbols.txt` 加 `T:System.Diagnostics.Stopwatch`。 | 只修 14 個旗標的 `GetTimestamp`：範圍小，但同類別混用兩種計時。 |
| D4 | 檔案接縫採 `IFileStore`＋明確登記，還是沿用 `TestFiles` 的「改用 Stream 類 API」做法？ | 正式程式用 `IFileStore`＋`seam-owners.json` 登記（§5.3）。測試端維持 `TestFiles`。 | 全部改用 `StreamReader/File.OpenRead`：站點數降得更快，但 AGENTS.md 的「等價方法」條款可能適用，且呼叫端仍不可替換。 |
| D5 | 可以動 `AppGeneralSettingsStore` 與 `TestAppSettingsIsolation` 嗎？ | 可以，但只換「如何讀環境變數」與「檔案接縫」。環境變數名稱、`{pid}`/`{guid}` 樣板、延後寫入行為都不變。需要 Owner 在聊天中明確交辦這個任務。 | 先不動：P-05 的 13 個站點延後，其餘批次不受影響（環境接縫仍可先用於 `Program.cs`/`StartupPerfTracker` 的登記）。 |
| D6 | 依賴 `DispatcherTimer` 的負向等待（17 處計時器，3 個測試站點）怎麼處理？ | 先不抽象計時器。P-09 評估：能否改成等待 ViewModel 已經發出的訊號；不行再討論把 450 ms 去抖改成 `TimeProvider.CreateTimer` 加 UI 回到執行緒。 | 現在就抽象計時器：觸及 `Dispatcher` 與執行緒，與 `Dispatcher.UIThread` 的 32 站點（B-21/B-22）互相牽動。 |
| D7 | 基準測試（3 個檔案，真實計時，由環境變數開啟）留在單元測試專案嗎？ | 留著，經由 `BenchmarkClock`（`TimeProvider.System`）與 `TestEnvironment` 收斂。 | 移到獨立的選擇性專案：RS0030 與帳本壓力會下降，但要新增專案、CI 與腳本。 |

## 9. 預期的帳本與指標變化（以規則推算，實際數字由 `LowerBaseline` 產生）

| 帳本／指標 | 現在 | 預期 |
|---|---:|---|
| RS0030（本檔範圍） | 140 | 約 125 直接消失；約 15 登記為 `permanent-seam`（env 6：`SystemEnvironmentSource` 2、`Program`、`StartupPerfTracker`、`TestEnvironment`、`TestAppSettingsIsolation`；file 8：`JsonProjectStore` 1、`AppGeneralSettingsStore` 2、`SystemFileStore` 5；process 1） |
| `baseline.json` `fakeClockDuplicates` | 1 | 0 |
| `debt-baseline.json` `directClockReads`（正式程式 `DateTime*.Now/UtcNow`） | 15 | 0 |
| `debt-baseline.json` `fileWriteAllText` | 5 | 1–2（`SystemFileStore` 與可能的 `AppGeneralSettingsStore`；視 D4/D5） |
| `debt-baseline.json` `processStart` | 1 | 1（只是改為已登記的擁有者） |
| `debt-baseline.json` `domainAndApplicationPlatformIo` | 2 | 0（P-06） |
| `test-debt.json` `timeWaitsInTests` | 15 | 3 → 0（S-01/S-02 移除 12 個；P-04/P-09 移除其餘 3 個） |
| `test-debt.json` `childProcessInTests` | 6 | 0（S-06，若 `GccRunner` 放在 TestSupport） |
| `test-debt.json` `elapsedAssertionsInTests` | 29 | 部分下降（至少 `LoadingScopeCoordinatorTests` 1 筆），其餘在基準測試與 `NotchTableGeneratorTests` |

每個批次的 PR 都要寫明：

- 改變了哪個指標（AGENTS.md：Name the metric）。
- P-02、P-03 這類熱路徑：附上 3635 基準前後數據（`scripts/perf`）。`TimeProvider.System.GetTimestamp()` 多了一次虛擬呼叫，預期可忽略，但要有量測才算數。
- P-01 若改了預設值，要明講「預設值改變，輸出會變」（AGENTS.md：Changing a default changes the output）。
- 每批提交前跑 `./scripts/dev/prepare-ui-workspace.ps1` 與 `./scripts/tests/lint.ps1 -UseNoAppHost`；合併前才跑 25 分鐘的 `repo-health.ps1 -Mode Verify`。

## 10. 這份提案沒有涵蓋的

- `BindingFlags.NonPublic`（124）、`Dispatcher.UIThread`（32）、`VSTHRD002/003/103/200`、`IDE*`：維持審計的原批次。
- `DispatcherTimer`、`Task.Run` 等其他非時間類的並行機制。
- 把 `StartupPerfTracker` 換成 Nvt.Core 的 `StartupTrace`（Core 已有 `StartFromEnvironment(string)` 與 `TimeProvider` 參數）：屬於 Core 整合軌，可另開，這裡只當選項。
- 預估工時：未提供。AGENTS.md 要求超過一天的工作先送一頁計畫；各批次開始時再記錄估計值與實際值。
- 未實測：`ManualTimeProvider` 的 `CreateTimer` 與 `Task.Delay(…, TimeProvider, …)` 的互動、Core 0.9.5 `LoadingScopeCoordinator` 是否以注入的 `TimeProvider` 等待、`TestSupport` 目錄在 `repo-health.ps1 -Mode Verify` 下確實被視為擁有者。這三項應在 S-00/S-01 以測試與一次 Verify 確認，再展開後續批次。

## 附錄 A：站點清單（Debug 建置，RS0030，已去重；`T:` 代表 `tests/FreeformHelper.Tests/`）

### 時鐘（正式程式 31）
- `UI/Controls/PadCanvas.Rendering.cs` ×9 `Stopwatch.GetTimestamp`：29, 33, 192, 199, 240, 258, 267, 271, 307
- `Application/Services/NotchV22CompensationService.Stages.cs` ×5 `Stopwatch.GetTimestamp`：97, 108, 121, 144, 160
- `UI/Controls/PadCanvas.ViewRefresh.cs` ×3 `DateTime.UtcNow`：58, 97, 110
- `UI/Services/AppGeneralSettingsStore.cs` ×3 `DateTimeOffset.UtcNow`：133, 194, 207
- `UI/Controls/PadCanvas.Input.PointerMove.cs` ×2 `DateTime.UtcNow`：27, 55
- `UI/Services/CadLoadSpinnerProcessHost.cs` ×2 `DateTimeOffset.UtcNow`：396, 635
- `UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs` ×2 `DateTime.UtcNow`：99, 212
- `UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs` ×2 `Environment.TickCount64`：159, 210
- `Infrastructure/Project/ProjectFile.cs` ×1 `DateTimeOffset.UtcNow`：24
- `UI/Controls/PadCanvas.RenderFrame.cs` ×1 `DateTime.UtcNow`：18
- `UI/Services/CadLoadSpinnerDebugState.cs` ×1 `DateTimeOffset.UtcNow`：163

### 時鐘（測試 43）
- `T:Application/Cad/Cad3635LoadBenchmarkTests.cs` ×15 `Stopwatch`：72, 74, 76, 79, 82, 84, 87, 89, 92, 94, 99–103
- `T:UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs` ×12 `Stopwatch`：102, 119, 127, 148, 150, 152, 154, 156, 162, 165, 166, 167
- `T:UI/Services/LoadingScopeCoordinatorTests.cs` ×4 `Stopwatch`：34, 40, 44, 45
- `T:Application/Notch/Notch3635GenerationBenchmarkTests.cs` ×3 `Stopwatch`：54, 58, 60
- `T:UI/Canvas/PadCanvasViewRefreshTests.cs` ×2 `DateTime.UtcNow`：16, 40
- `T:UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs` ×2 `Environment.TickCount64`：227, 230
- `T:UI/ViewModels/IndexMappingReportViewModelTests.cs` ×2 `Stopwatch`：10, 11
- `T:UI/ViewModels/ShellViewModelSimulationTests.cs` ×2 `Environment.TickCount64`：354, 357
- `T:UI/Services/AppGeneralSettingsStoreTests.cs` ×1 `DateTimeOffset.UtcNow`：101

### 延遲（正式程式 9）
- `UI/Services/CadLoadSpinnerProcessHost.cs` ×3：367, 400, 488
- `UI/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs` ×2：178, 288
- `UI/ViewModels/FreeformHelperViewModel.NotchDetails.AutoPlay.cs`：98
- `UI/ViewModels/FreeformHelperViewModel.Selection.NotchPreview.cs`：27
- `UI/ViewModels/ShellViewModel.Workspaces.cs`：186
- `UI/Views/DevView.axaml.cs`：50

### 延遲（測試 15）
- `T:UI/Services/RuntimeQueryIpcTests.cs` ×3：31, 59, 86
- `T:UI/ViewModels/ShellViewModelSimulationTests.cs` ×3：266, 275, 362
- `T:UI/Smoke/HeadlessUiSmokeTests.cs` ×2：303, 439
- `T:Application/Dxf/DxfLayerImageExportServiceTests.cs`：212（`Thread.Sleep`）
- `T:UI/Services/CadLoadSpinnerProcessHostTests.cs`：85
- `T:UI/Services/LoadingScopeCoordinatorTests.cs`：20
- `T:UI/Services/RuntimeQueryCoreBehaviorTests.cs`：185
- `T:UI/ViewModels/FreeformHelperViewModelTests.Helpers.cs`：235
- `T:UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.AppGeneral.cs`：175
- `T:UI/ViewModels/IndexMappingReportViewModelTests.cs`：18

### 環境（正式程式 3，另有 Release 專屬 1）
- `UI/Services/AppGeneralSettingsStore.cs`：140（`GetEnvironmentVariable`）、146（`GetFolderPath`）
- `UI/Services/StartupPerfTracker.cs`：17
- `UI/Program.cs`：83（僅 Release）

### 環境（測試 19）
- `T:Application/Cad/Cad3635LoadBenchmarkTests.cs` ×3：25, 37, 176
- `T:Application/Notch/Notch3635GenerationBenchmarkTests.cs` ×3：22, 29, 145
- `T:UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs` ×3：28, 38, 224
- `T:UI/Services/AppGeneralSettingsStoreTests.cs` ×3：14（Get）、19、27（Set）
- `T:UI/Smoke/FontAdoptionTests.cs` ×2：103, 124
- `T:Application/Notch/NotchGoldenBaselineTests.cs`：37
- `T:Application/Notch/Tm81NotchAcceptanceMatrixTests.cs`：33
- `T:TestInfrastructure/ExampleData.cs`：28
- `T:TestInfrastructure/TestAppSettingsIsolation.cs`：20（Set）
- `T:UI/Snapshots/UiBaselineUpdateMode.cs`：16

### 檔案（正式程式 12）
- `Application/Services/DiffFrameCsvImporter.cs` ×2：12（`File.ReadAllLines`、型別 `File`）
- `UI/Services/AppGeneralSettingsStore.cs` ×2：66（`ReadAllText`）、135（`WriteAllText`）
- `UI/Services/ProjectPersistenceUseCase.cs` ×2：79, 98（`ReadAllBytes`）
- `Application/Services/RegularVisibilityMaskService.cs`：67（型別 `File`）
- `Infrastructure/Project/JsonProjectStore.cs`：41（`ReadAllText`）
- `UI/ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs`：159（`WriteAllText`）
- `UI/ViewModels/FreeformHelperViewModel.Persistence.cs`：114（`WriteAllText`）
- `UI/Views/CoordinatePlannerWorkspaceView.axaml.cs`：195（`WriteAllTextAsync`）
- `UI/Views/SimulationWorkspaceView.axaml.cs`：264（`WriteAllTextAsync`）

### 檔案（測試 3）
- `T:Application/Notch/NotchGoldenBaselineTests.cs` ×2：39, 49
- `T:Application/Notch/Tm81NotchAcceptanceMatrixTests.cs`：37

### 子程序
- 正式程式：`UI/Services/CadLoadSpinnerProcessHost.cs`：453
- 測試：`T:Application/Notch/NotchTableExporterTests.cs` ×3：1359, 1432, 1475

## 附錄 B：重現方式

```bash
python3 scripts/fetch_core_packages.py
dotnet restore FreeformHelper.sln
dotnet build FreeformHelper.sln -c Debug --no-restore --no-incremental -p:TreatWarningsAsErrors=false
```

建置輸出中的 `warning RS0030: The symbol '…' is banned` 依 檔案/行/欄/符號/專案 去重後即為附錄 A 的來源（Debug 295 筆）。
