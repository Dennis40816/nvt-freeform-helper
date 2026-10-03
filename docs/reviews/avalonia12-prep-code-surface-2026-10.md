# Avalonia 12 升級準備：C# 相依表面盤點（2026-10）

本文件只盤點現有程式碼，**沒有執行 Avalonia 11 → 12 升級**。目前 Avalonia、Desktop、Fluent、Inter、Headless 與 Headless.XUnit 的版本是 **11.3.12**（`Directory.Packages.props`）；AvaloniaEdit 是獨立版本 11.4.1。依 `TODO.md` 的 owner 決定，升級排在 **1.3.5 之前**，現在只做準備。

環境沒有網路，本文件不宣稱 Avalonia 12 已移除、改名或改變任何 API。各區升級相容性均為 **「待依官方遷移指南核對（to check against the official migration guide）」**；風險排序根據目前相依方式，不是已確認的 Avalonia 12 breaking changes。

## 範圍、計數與優先順序

- 基準：`feature/queue/avalonia12-prep-code-surface` 的現有 tracked C#，共 **562 檔**：`src/FreeformHelper.UI/**/*.cs` **396 檔**、`tests/**/*.cs` **166 檔**。不含產生的 build／obj／bin 檔案，不讀取 `example/`。
- 啟動時已讀取 `AGENTS.md`、domain 入口、依賴圖及相關 roadmap／Runtime CLI contract；依賴圖的 project reference 關係仍符合目前五個專案。此 worktree 沒有 `.codegraph/`。
- 下表及各區「命中行」是逐檔 regex 搜尋的**不重複行數**，包含註解、字串與宣告，不是呼叫次數或必須修改的數量。同一檔／行可跨區重複。屬性數、控制項數、picker 呼叫數則另外精確計數。
- 參照前綴：`U/` = `src/FreeformHelper.UI/`；`T/` = `tests/FreeformHelper.Tests/`。`檔案:型別／方法／欄位` 可直接定位；partial class 的同名 symbol 以列出的檔案區分。
- **P0 優先**：直接讀取 Avalonia 私有欄位，或 headless session／dispatcher 收尾與重建順序的依賴，最可能需要調整。
- **P1 優先驗證**：公開 API，但依賴排程優先權、render／layout 完成、pointer capture、平台服務或視窗關閉順序。是否改碼待指南與測試證據確認。
- **P2 一般核對**：公開控制項／property API，或只對本庫型別做反射；目前沒有足夠證據要求升級改碼。

| 區域 | UI 檔／命中行 | tests 檔／命中行 | 判讀 |
|---|---:|---:|---|
| Dispatcher／UiThread／timer／context | 37／86 | 12／50 | P0：host 順序；P1：排程及延後工作 |
| lifetime／AppBuilder／平台初始化 | 2／6 | 1／5 | P1；字型 bootstrap 另列 |
| headless host／attribute／frame capture | 0／0 | 24／184 | P0：session；P1：實際畫面擷取 |
| DrawingContext／SkiaSharp／bitmap | 13／48 | 1／16 | P1：自訂 renderer；headless 擷取另列 |
| 輸入／routed event 搜尋候選 | 21／112 | 3／15 | 人工排除 3 個集合 Key 誤命中後為 **19／110、2／14** |
| TopLevel／Window／clipboard／storage／dialog | 40／135 | 6／25 | P1：平台／owner／關閉行為；包含 Window 宣告 |
| GetField／GetMethod／BindingFlags | 0／0 | 32／159 | 只有 **1 處**直接反射 Avalonia 私有欄位 |
| obsolete／warning 搜尋候選 | 7／7 | 0／0 | 全為既有 CA1822 SuppressMessage；不是 Avalonia obsolete suppression |

## 1. 執行緒、Dispatcher 與延後工作

最需先核對的是 `U/Services/UiThread.cs:UiThread.TryGetRunningDispatcher／IsCurrent`：先檢查 `Application.Current`，才取得全域 `Dispatcher.UIThread`，再檢查 `SupportsRunLoops` 與 `CheckAccess()`。目前目的包括避免 plain test／背景工作在沒有平台時建立 dispatcher，及避免等待無人處理的 queue。升級時應保留這些已存在的行為保護，待依官方遷移指南核對，不預先替換 dispatcher 實作。

下列 UI 位置合計 **37 檔／86 命中行**。除 UiThread 與 host 的耦合外，均為公開排程 API；優先驗證 callback 是否仍在預期的 UI thread、失效／取消後是否仍會寫入 UI，以及 detach／close 後 timer 是否停止。

| 檔案:symbol | 命中行 | 等級與目前依賴 |
|---|---:|---|
| `U/Services/UiThread.cs:UiThread.TryGetRunningDispatcher／IsCurrent` | 4 | P0：全域 dispatcher／run-loop 判斷；`SupportsRunLoops` 行另由專項搜尋計數 |
| `U/App.axaml.cs:App.OnFrameworkInitializationCompleted` | 5 | P1：Background／ApplicationIdle 啟動 IPC；UnhandledException |
| `U/Logging/AppLogStore.cs:AppLogStore` 建構子的 UI callbacks | 2 | P1：CheckAccess／Post；不可在無 application 時建立 dispatcher |
| `U/Services/RuntimeQueryIpc.cs:RuntimeQueryIpcServer.HandleConnectionAsync` | 1 | P1：IPC 請求切回 UI；維持既有 RuntimeQueryUseCase 入口 |
| `U/Services/UiResourceResolver.cs:UiResourceResolver.TryResolve` | 1 | P1：只在目前 UI thread 讀 application resources |
| `U/Services/CanvasViewportAdapter.cs:CanvasViewportAdapter.QueueInitialFit` | 1 | P1：Loaded 後 fit，與初始 layout 順序有關 |
| `U/ViewModels/FreeformHelperViewModel.CadLoadOverlay.cs:YieldCadLoadCanvasOverlayFrameAsync` | 2 | P1：UI thread 以 Render 優先權讓出一幀；其他 thread 用 Task.Yield |
| `U/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs:RunDeferredCadInspectorSnapshotRefreshAsync／RunDeferredRegularInspectorSnapshotRefreshAsync` | 6 | P1：Background、cancellation token、延後完成 |
| `U/ViewModels/FreeformHelperViewModel.Selection.NotchPreview.cs:RunDeferredSelectionNotchPreviewRefreshAsync` | 2 | P1：Background 與取消／選取更新順序 |
| `U/ViewModels/FreeformHelperViewModel.NotchDetails.AutoPlay.cs:RunNotchPreviewAutoPlayLoopAsync` | 1 | P1：非同步播放回 UI |
| `U/ViewModels/FreeformHelperViewModel.Persistence.cs:ExportNotchTableAsync` | 2 | P1：Background queue yield |
| `U/ViewModels/FreeformHelperViewModel.Progress.cs:CreateUiProgressReporter` | 1 | P1：背景 progress 回 UI |
| `U/ViewModels/FreeformHelperViewModel.State.CanvasAndMapping.cs:ApplyCanvasColorDefaultsFromTokens／ResolveTokenColor` | 2 | P1：UI thread 才讀 theme／token |
| `U/ViewModels/FreeformHelperViewModel.AppSettings.cs:_appGeneralSettingsPersistTimer` | 1 | P1：DispatcherTimer；保留 Load 後 deferred、成功 Save 後 flush 契約 |
| `U/ViewModels/ShellViewModel.cs:ShellViewModel` 建構子 | 2 | P1：Background 初始化 console |
| `U/ViewModels/SimulationWorkspaceViewModel.cs:_playbackTimer` | 1 | P1：Background 播放 timer |
| `U/Controls/LoadingSpinner.axaml.cs:LoadingSpinner._timer`／建構子 | 2 | P1：動畫 DispatcherTimer |
| `U/Controls/PadCanvas.HoverDebug.cs:EnsureHoverDebugRevealTimer` | 1 | P1：hover 延後顯示 |
| `U/Controls/PadCanvas.State.cs:_hoverDebugRevealTimer` | 1 | P1：timer 欄位 |
| `U/Controls/PadCanvas.Input.Selection.cs:SchedulePendingSingleSelection` | 1 | P1：單擊延後確認，與雙擊／selection version 有關 |
| `U/Controls/PadCanvas.Input.cs:_pendingSingleTimer` | 1 | P1：timer 欄位 |
| `U/Controls/PadCanvas.View.cs:FitToContent` | 2 | P1：Loaded 時更新 viewport |
| `U/Controls/PadCanvas.ViewRefresh.cs:RequestVisualRefresh／CreateTransientLowDetailTimer` | 5 | P1：Render 合併 refresh、InvalidateVisual、navigation timer |
| `U/Controls/WorkspaceHeader.axaml.cs:ScheduleViewPopupHoverClose` | 2 | P1：Background 關閉 hover popup |
| `U/Views/CadLoadSpinnerWindow.axaml.cs:OnOpened／StartParentMonitor／RunIpcLoopAsync` | 6 | P1：Loaded、Render、parent-monitor timer 與 IPC |
| `U/Views/ConsolePanel.axaml.cs:OnShellViewModelPropertyChanged` | 2 | P1：Loaded 後同步文字／捲動 |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:AttachDataContext／ExportPreviewPngAsync` | 3 | P1：Loaded overlay；Render 後 bitmap export |
| `U/Views/DevView.axaml.cs:OnAttachedToVisualTree／BuildProbeText` | 2 | P2：字型及 resource probe |
| `U/Views/FreeformHelperView.CadLoadSpinner.cs:OnObservedViewModelPropertyChanged` | 2 | P1：Normal priority 同步 spinner |
| `U/Views/FreeformHelperView.Canvas.Layout.cs:QueuePadInfoLayoutUpdate` | 2 | P1：Background 合併 overlay layout |
| `U/Views/FreeformHelperView.Canvas.cs:ShowPadInfoOverlay` | 1 | P1：Post 後更新 popover |
| `U/Views/FreeformHelperView.Console.Events.cs:AttachConsoleAutoScroll` | 6 | P1：Background／Loaded、跨執行緒 collection 更新 |
| `U/Views/FreeformHelperView.ConsoleHost.cs:OnShellPropertyChanged` | 1 | P1：Render 後恢復 editor content |
| `U/Views/FreeformHelperView.Lifecycle.cs:ScheduleInitialFit／ScheduleDeferredUiHooks` | 5 | P1：Loaded／Background、attach 後掛接 |
| `U/Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml.cs:OnDataContextPropertyChanged` | 1 | P1：Post 後 BringIntoView |
| `U/Views/SettingsWindow.axaml.cs:NavigateToSection／OnOpened` | 2 | P1：Background scroll |
| `U/Views/SimulationWorkspaceView.axaml.cs:QueueInlineEditorLayoutUpdate／FocusInlineEditorAsync` | 4 | P1：Background layout／Loaded focus |

測試端 **12 檔／50 命中行**：

| 檔案:symbol | 命中行 | 重點 |
|---|---:|---|
| `T/UI/TestHost/HeadlessDispatcherSetup.cs:EnsureRunLoopDispatcher` | 4 | P0：含診斷字串／註解；真正檢查在 SupportsRunLoops |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:Before／After／WatchForLeftoverWork／CloseOpenWindowsAndRunQueuedWork` | 7 | P0：context、UnhandledException、RunJobs；詳見第 3 區 |
| `T/UI/TestHost/HeadlessSessionGuardTests.cs:HeadlessSessionGuardTests` | 12 | P0：測試上述收尾／context 契約 |
| `T/UI/Logging/AppLogStoreTests.cs:AddAndClear_AfterUiReadyWithoutApplication_DoNotCreateDispatcher` | 1 | P0：私有 dispatcher 欄位；詳見第 7 區 |
| `T/UI/Canvas/PadCanvasViewRefreshTests.cs:DrainUiQueueAsync` | 4 | P1：依序 Background → Loaded → Render → Background |
| `T/UI/Services/CanvasViewportAdapterTests.cs:DrainUiQueueAsync` | 4 | P1：同上，fit／viewport assertions |
| `T/UI/Smoke/HeadlessUiSmokeTests.cs:FlushUiQueueAsync` | 4 | P1：同上，tooltip／spinner／文字渲染 |
| `T/UI/Smoke/TerminalStartupPathTests.cs:FlushUiQueueAsync` | 4 | P1：同上，啟動／console |
| `T/UI/Snapshots/UiRenderedVisualSnapshotTests.cs:FlushUiQueueAsync` | 4 | P1：同上，畫面擷取前完成 queue |
| `T/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:FlushUiQueueAsync` | 4 | P1：同上；本次不執行、不讀其 confidential fixture |
| `T/UI/ViewModels/CadLoadOverlayFrameYieldPolicyTests.cs:IsUiThreadThatRunsALoop_NeedsBothThreadAccessAndARunLoop` | 1 | P2：純 policy seam |
| `T/UI/ViewModels/FreeformHelperViewModelTests.Basics.LoadOverlay.cs:YieldCadLoadCanvasOverlayFrame_OnTheUiThread_RunsQueuedRenderWorkFirst` | 1 | P1：Render 工作必須先執行 |

## 2. Application lifetime、AppBuilder 與平台設定

| 檔案:symbol | 數量 | 等級與核對內容 |
|---|---|---|
| `U/Program.cs:Program.Main／BuildAvaloniaApp` | 1 production builder、1 StartWithClassicDesktopLifetime；5 候選命中行 | P1：STAThread、UsePlatformDetect、Configure<App>、字型註冊、LogToTrace；query CLI 可提前離開 UI 啟動 |
| `U/App.axaml.cs:App.Initialize／OnFrameworkInitializationCompleted` | 1 App subclass、1 classic desktop lifetime 分支 | P1：MainWindow／spinner-mode window、Opened fallback、desktop.Exit、全域 exception subscription；目前只有 desktop lifetime 路徑 |
| `U/Services/AppFontBootstrapper.cs:CreateFontManagerOptions／InterSystemFontSourceUri` | 1 options factory、2 family mappings | P1：FontManagerOptions、Inter embedded source；與 production／headless builder 共用 |
| `T/UI/TestHost/AvaloniaTestApp.cs:BuildAvaloniaApp` | 1 test builder；5 候選命中行 | P0/P1：UseSkia → UseHeadless、AfterPlatformServicesSetup、AfterSetup；詳見第 3 區 |

這些是現有公開初始化 API。平台偵測、字型 source／family mapping、framework initialization callback 的先後順序及 desktop 退出行為，均待依官方遷移指南核對。沒有找到直接使用 `Avalonia.Win32`／`Avalonia.X11`／`Avalonia.Native` 或 `AvaloniaLocator` 的 C# 位置。

## 3. Headless 測試 host 與 xUnit attributes（P0）

**優先順序最高的是 host 與 session guard，不是把所有 headless attributes 判為已失效。** 本庫已有 dispatcher 建立 race、遺留 layout 工作及過期 context 的防護；相關背景見 `TODO.md` 的 S15.002 根因五～八。目前程式將 session teardown 的若干實作順序當成行為前提，升級前必須逐項核對。

| 檔案:symbol | 數量／相依 | 核對重點 |
|---|---|---|
| `T/UI/TestHost/AvaloniaTestApp.cs:BuildAvaloniaApp` | 1 `[assembly: AvaloniaTestApplication]`；UseHeadlessDrawing = false | P0/P1：Skia 真實 render host、平台註冊後檢查 dispatcher；AfterSetup 補 Initialize |
| `T/UI/TestHost/HeadlessDispatcherSetup.cs:EnsureRunLoopDispatcher` | 1 SupportsRunLoops 判斷 | P0：在 application 建立前快速拒絕沒有 run-loop 的 dispatcher；註解明示 media context／compositor 會保存初始化 dispatcher，但本檔沒有反射讀它們 |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:Before／ThrowIfContextWasUsedBefore` | 1 assembly guard；1 ConditionalWeakTable | P0：假設預設 PerTest isolation，每個測試會換 synchronization context；同 assembly 共用 application 會違反此 guard 的前提 |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:WatchForLeftoverWork／IsSessionTeardown` | 1 UnhandledException hook；以 SupportsRunLoops = false 辨認 teardown | P0：只攔下測試結束後／收尾期間例外；執行中的測試例外仍須失敗 |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:TrackOpenedWindows／CloseAtTestEnd／CloseOpenWindowsAndRunQueuedWork` | WindowOpenedEvent class handler；最多 3 close passes；1 RunJobs 呼叫點 | P0：DataContext 清除後 Close、再排空 queue，避免仍存活的視窗破壞 session 收尾；含只量測但未 Show 的 window |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:IsHeadlessTest` | 2 attribute-type 檢查 | P1：辨認 AvaloniaFactAttribute／AvaloniaTheoryAttribute，不反射 Avalonia 私有 member |
| `T/UI/TestHost/HeadlessUiCollection.cs:HeadlessUiCollectionDefinition` | 1 DisableParallelization collection；16 檔標記 HeadlessUiSerial | P1：headless／plain test 的隔離與排序 |
| `T/TestInfrastructure/HeadlessAppBootstrap.cs:EnsureInitialized` | 1 WeakReference<App> cache | P1：以 Application.Current 與 app identity 避免重複 Initialize |
| `T/UI/Snapshots/UiRenderedVisualSnapshotTests.cs:RenderControlToBgraBufferAsync` | 1 CaptureRenderedFrame 呼叫點 | P1：Window.Show、queue flush、framebuffer／BGRA 讀取 |
| `T/UI/Smoke/HeadlessUiSmokeTests.cs:CadLoadSpinnerWindow_RendersVisibleSpinner_AndAnimates／MainWindow_WithExpandedConsole_RendersVisibleConsoleText／MainWindow_WithSimulationWorkspace_RendersVisibleConsoleText` | 3 CaptureRenderedFrame 呼叫點 | P1：像素／動畫／文字 evidence |
| `T/UI/Smoke/TerminalStartupPathTests.cs:TerminalStartupPathTests` | 1 CaptureRenderedFrame 呼叫點 | P1：console 字型／glyph evidence |
| `T/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:Cad3635EndToEndBenchmarkTests` | 1 CaptureRenderedFrame 呼叫點 | P1：benchmark 畫面 evidence；本次不執行 |

Attributes 共 **98 個宣告**（**94 AvaloniaFact + 4 AvaloniaTheory**），分布 **20 個檔案、17 個 test class**；4 個 Theory 不是 4 個展開後 test case。下表列出每檔所在 class（同名 partial 合併計 class）：

| `T/` 下的檔案:class | Fact | Theory |
|---|---:|---:|
| `UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:Cad3635EndToEndBenchmarkTests` | 1 | 0 |
| `UI/Canvas/PadCanvasCacheInvalidationTests.cs:PadCanvasCacheInvalidationTests` | 15 | 0 |
| `UI/Canvas/PadCanvasHitTestTests.cs:PadCanvasHitTestTests` | 8 | 0 |
| `UI/Canvas/PadCanvasRenderPerfTests.cs:PadCanvasRenderPerfTests` | 2 | 0 |
| `UI/Canvas/PadCanvasViewRefreshTests.cs:PadCanvasViewRefreshTests` | 5 | 0 |
| `UI/Services/CadLoadSpinnerProcessHostTests.cs:CadLoadSpinnerProcessHostTests` | 1 | 0 |
| `UI/Services/CanvasViewportAdapterTests.cs:CanvasViewportAdapterTests` | 3 | 0 |
| `UI/Services/RuntimeQueryIpcTests.cs:RuntimeQueryIpcTests` | 1 | 0 |
| `UI/Smoke/HeadlessUiSmokeTests.cs:HeadlessUiSmokeTests` | 10 | 0 |
| `UI/Smoke/IndexMappingReportWindowSmokeTests.cs:IndexMappingReportWindowSmokeTests` | 1 | 0 |
| `UI/Smoke/NotchExportSelectionWindowSmokeTests.cs:NotchExportSelectionWindowSmokeTests` | 1 | 0 |
| `UI/Smoke/TerminalStartupPathTests.cs:TerminalStartupPathTests` | 7 | 0 |
| `UI/Smoke/WorkspaceViewsSmokeTests.cs:WorkspaceViewsSmokeTests` | 2 | 0 |
| `UI/Snapshots/UiRenderedVisualSnapshotTests.cs:UiRenderedVisualSnapshotTests` | 1 | 0 |
| `UI/TestHost/HeadlessSessionGuardTests.cs:HeadlessSessionGuardTests` | 10 | 0 |
| `UI/ViewModels/FreeformHelperViewModelTests.Basics.LoadOverlay.cs:FreeformHelperViewModelTests` | 2 | 0 |
| `UI/ViewModels/FreeformHelperViewModelTests.NotchExportCache.cs:FreeformHelperViewModelTests` | 4 | 2 |
| `UI/ViewModels/FreeformHelperViewModelTests.NotchGenerationStaleCompletion.cs:FreeformHelperViewModelTests` | 8 | 1 |
| `UI/ViewModels/FreeformHelperViewModelTests.NotchResolvedSnapshot.cs:FreeformHelperViewModelTests` | 9 | 1 |
| `UI/ViewModels/FreeformHelperViewModelTokenDefaultsTests.cs:FreeformHelperViewModelTokenDefaultsTests` | 3 | 0 |

**尚待核對**：官方 migration guide 對 headless／xUnit isolation、callback、dispatcher reset、字型／renderer disposal、SynchronizationContext 安裝與還原是否有說明。此處只記錄本庫目前的假設，沒有把 Avalonia 11 的收尾描述當成 Avalonia 12 已知行為。

## 4. 自訂 Control、PadCanvas、DrawingContext 與 SkiaSharp

UI 共 **4 個直接繼承 Control 並 override Render 的型別**：PadCanvas、NotchPreviewCanvas、NotchApplySimulationAaView、NotchApplySimulationHeatmapView。另有 **2 個 ContentControl、39 個 UserControl、17 個 Window** 的直接 subclass 宣告；一般 XAML host 的繼承本身不等於使用 internals，本次不盤點 XAML／styles。

`U/Controls/PadCanvas*.cs` 共 **28 檔**（包含事件／view-frame 模型）；下列 **8 檔**承載其 DrawingContext 表面。這些 renderer 使用公開 API，但 viewport matrix、clip、文字 layout、brush／geometry cache、失效通知與低細節切換是升級時的 P1 回歸重點。

| 檔案:symbol | rendering 命中行 | 等級／目前用途 |
|---|---:|---|
| `U/Controls/PadCanvas.Rendering.cs:PadCanvas.Render` | 2 | P1：主 renderer，含 1 行 XML 註解；transform／clip／分層繪圖 |
| `U/Controls/PadCanvas.Rendering.Cad.cs:DrawCadPad／DrawDiffIndexOverlay／DrawDiffIndexMarkerForPad` | 3 | P1：CAD geometry 與 overlay |
| `U/Controls/PadCanvas.Rendering.Regular.cs:DrawRegularPad／DrawRegularFreeformHatch／DrawDiagonalHatch／DrawSelectionOverlay` | 4 | P1：regular／hatch／selection |
| `U/Controls/PadCanvas.Rendering.Labels.cs:DrawMatchAllocationLabels／DrawRegularCoverageLabels／DrawCadCoverageLabels／DrawMatchRatioLabel` | 4 | P1：文字與 label placement |
| `U/Controls/PadCanvas.Rendering.NotchPreview.cs:DrawNotchToFullOverlay／DrawNotchToFullSeedOverlay／DrawNotchToFullCandidateOverlay／DrawNotchToRegularLabels` | 4 | P1：notch preview |
| `U/Controls/PadCanvas.Rendering.Simulation.cs:DrawSimulationRegularLabels／DrawSimulationCopperPillar／DrawSimulationCellText` | 3 | P1：simulation rendering |
| `U/Controls/PadCanvas.Rendering.HoverDebug.cs:DrawHoverDebugOverlay` | 1 | P1：hover debug 文字 |
| `U/Controls/PadCanvas.View.AxisLabels.cs:DrawAxisLabels／GetAxisLabelLayout` | 1 | P1：axis labels；另有 TextLayout cache |
| `U/Controls/NotchPreviewCanvas.cs:NotchPreviewCanvas.Render／DrawHatch` | 2 | P1：獨立 preview renderer |
| `U/Controls/NotchApplySimulationAaView.cs:NotchApplySimulationAaView.Render／DrawCellText` | 2 | P1：cells、文字與 hit regions |
| `U/Controls/NotchApplySimulationHeatmapView.cs:NotchApplySimulationHeatmapView.Render` | 1 | P1：heatmap |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:ExportPreviewPngAsync` | 1 | P1：RenderTargetBitmap，在 Render queue 後擷取 preview |
| `U/Services/DxfLayerImageExportService.cs:ExportLayerImage／DrawPadOutline／WorldToPixel／SaveImage` | 20 | P1：唯一直接 using SkiaSharp 的 UI 檔；SKBitmap／SKCanvas／SKPaint／SKPath／SKImage／encoding，屬離線圖片輸出，不是 Avalonia 私有 renderer |
| `T/Application/Dxf/DxfLayerImageExportServiceTests.cs:DxfLayerImageExportServiceTests` | 16 | P1：Skia output／像素檢查；不是 headless host |

另外的公開 property／control 擴充為 P2：`U/Controls/PadInfoSectionFrame.cs:PadInfoSectionFrame.TitleProperty`、`U/Controls/HidePanelBlock.axaml.cs:HidePanelBlock`（兩個 ContentControl）；`U/Services/SharedToolTipStyleService.cs:Register／NormalizeStringToolTip` 的 TipProperty class handler 為 P1，需用既有 tooltip-open smoke 與 static guard 保護，不在本次改樣式。

文字相依位置包括 AaView、PadCanvas 的 Labels／NotchPreview／Simulation／HoverDebug／AxisLabels 與 `PadCanvas.State.cs` 的 TextLayout cache，以及 `U/Views/DevView.axaml.cs:LogFontProbe`。沒有找到 `ICustomDrawOperation`；目前不應宣稱必須換成新的 renderer。Skia package／ABI 與 Avalonia 12 的要求同樣待依官方遷移指南核對。

## 5. Pointer、keyboard、focus 與 routed events

精確輸入候選搜尋為 **24 檔／127 行**。人工排除 `group.Key.ToDisplayLabel()`、`group.Key.ToContractString()`、`row.Key.StartsWith()` 三個集合 Key 誤命中後，剩 **21 檔／124 行**（UI **19／110**，tests **2／14**）。測試的來源字串 assertions 仍計入，但不視為執行期輸入處理。

| 檔案:symbol | 命中行 | 等級／驗證重點 |
|---|---:|---|
| `U/Controls/PadCanvas.Input.cs:OnPointerPressed／OnPointerReleased` | 16 | P1：mouse button、click count、capture／release、修飾鍵與 selection |
| `U/Controls/PadCanvas.Input.PointerMove.cs:OnPointerMoved／OnPointerExited` | 4 | P1：pan／box selection／hover，pointer 座標與 handled |
| `U/Controls/PadCanvas.Input.Navigation.cs:OnPointerWheelChanged／ShouldTreatWheelAsPan／OnKeyDown／OnKeyUp` | 20 | P1：wheel、Space／Ctrl、zoom／pan 與 keyboard scope |
| `U/Controls/PadCanvas.HoverDebug.cs:UpdateHoverDebugHit／RefreshHoverDebugHitFromCurrentPointer` | 3 | P1：修飾鍵與 current pointer |
| `U/Controls/NotchApplySimulationAaView.cs:OnPointerPressed` | 1 | P1：cell hit regions；須與 Render 保持一致 |
| `U/Controls/NumberScrubber.Input.cs:OnInputKeyDown／OnPointerWheelChanged` | 5 | P1：Enter／Escape、wheel step |
| `U/Controls/NumberScrubber.Scrub.cs:OnScrubAreaPointerPressed／OnScrubAreaPointerMoved／OnScrubAreaPointerReleased／OnScrubAreaPointerCaptureLost` | 4 | P1：drag capture／capture lost 與值提交 |
| `U/Controls/NumberScrubber.axaml.cs:NumberScrubber` 建構子 | 1 | P1：PointerWheelChanged Bubble hook |
| `U/Controls/WorkspaceHeader.axaml.cs:OnTopLevelPointerPressed／ViewMenuButton_PointerPressed` 等 pointer handlers | 6 | P1：popup hover／外部點擊及 top-level hook |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:OnArrayCornerHandlePointerPressed／OnArrayCornerPointerMoved／OnArrayCornerPointerReleased／MoveDraggedArrayCorner／CompleteDraggedArrayCorner` | 5 | P1：overlay handle capture／drag |
| `U/Views/SimulationWorkspaceView.axaml.cs:SimulationPadCanvas_PointerMoved／SimulationPadCanvas_PointerExited／SimulationPadCanvas_PointerPressed／SimulationPadCanvas_KeyDown` | 9 | P1：inline edit／context／選取快捷鍵 |
| `U/Views/FreeformHelperView.InputAndShortcuts.cs:OnTopLevelKeyDown／TryHandleGlobalShortcut／SaveProjectFromShortcutAsync／ClosePadInfoIfOutside／OnConsolePointerPressed` | 16 | P1：全域快捷鍵、文字輸入排除、Ctrl+S、console 字級 |
| `U/Views/FreeformHelperView.Lifecycle.cs:OnRootPointerPressed／OnLayerTogglePointerPressed` | 3 | P1：root／toggle event propagation |
| `U/Views/FreeformHelperView.Console.Events.cs:OnConsoleEditorPointerMoved／OnConsoleTextViewPointerPressed` 等 handlers | 6 | P1：文字區／AvaloniaEdit pointer 路徑 |
| `U/Views/FreeformHelperView.ConsoleLinks.cs:OnConsoleEditorPointerReleased／TryHandleConsoleLinkActivation` | 2 | P1：修飾鍵＋連結命中 |
| `U/Views/ConsolePanel.axaml.cs:ConsolePanel` 建構子／`OnConsolePointerPressedInternal／OnConsoleEditorPointerReleasedInternal` | 4 | P1：Tunnel／Bubble、handledEventsToo |
| `U/Views/LeftDxfPanel.axaml.cs:LeftDxfPanel` 建構子／`OnLayerTogglePointerPressedInternal` | 2 | P1：layer event hook |
| `U/Views/DxfEditChangeListWindow.axaml.cs:OnKeyDown` | 2 | P1：Escape 關閉視窗 |
| `U/Views/PadInfoPopover.axaml.cs:OnPopoverPointerPressed` | 1 | P1：popover event handled |
| `T/UI/Snapshots/UiLayoutGuardTests.cs:ShortcutContract_DoesNotExposeResetViewR／ShortcutContract_GlobalAndCanvasScopesRemainConsistent` | 13 | P2：source guard，不能證明實際 routed event 順序 |
| `T/UI/Snapshots/UiVisualSnapshotTests.cs:CriticalUiFiles_MatchMinimalVisualBaseline` | 1 | P2：baseline key 字串，沒有直接發送輸入事件 |

相關 focus surface：`U/Services/CanvasFocusViewportService.cs:TryGetFocusTarget／TryGetWindowAwareFocusTarget`（Window 候選 2 行）、`U/Views/FreeformHelperView.Canvas.cs:EnumerateFocusAwareFloatingWindows`、`U/Views/SimulationWorkspaceView.axaml.cs:FocusInlineEditorAsync`。focus、Tunnel／Bubble、Handled、capture lost、pointer 座標、double-click 與 wheel 行為均待依官方遷移指南核對並實際驗證，不能只靠 static guard 宣稱相容。

## 6. TopLevel、Window、clipboard、storage 與 dialogs

此區候選為 **46 檔／160 行**；包含 Window 型別宣告與文件註解。精確平台呼叫為 **6 個 SetTextAsync 剪貼簿入口**、**11 個 file picker 呼叫點**（**5 Open + 6 Save**）。沒有找到 OpenFileDialog／SaveFileDialog／OpenFolderDialog 舊型別或 FolderPicker 呼叫。

| 檔案:symbol | 數量 | 等級／目前依賴 |
|---|---|---|
| `U/Views/FreeformHelperView.Pickers.cs:GetStorageProvider／ConfigureFilePickers／BuildOpenDxfOptions／BuildOpenProjectOptions／BuildOpenDiffCsvOptions／BuildOpenRegularVisibilityMaskOptions／BuildSaveProjectOptions／BuildSaveNotchOptions／BuildSaveExportOptions` | 54 候選行；4 Open + 3 Save | P1：TopLevel.StorageProvider、filter／extension、IStorageFile.Path.LocalPath；delegate 在 attach 時設置、detach 時清除；另有 confirm／warning dialog |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:SetClipboardTextAsync／SaveTextFileAsync／ExportPreviewPngAsync／OnArtifactDetailRequested` | 9 候選行；1 clipboard + 2 Save | P1：平台服務可為 null、文字檔／bitmap export、owner window |
| `U/Views/SimulationWorkspaceView.axaml.cs:PickOpenDiffCsvPathsAsync／SetClipboardTextAsync／SaveTextFileAsync` | 7 候選行；1 clipboard + 1 Open + 1 Save | P1：storage、clipboard、取消與本機路徑 |
| `U/Views/ConsolePanel.axaml.cs:OnConsoleCopyAllInternal` | 1 clipboard | P1：TopLevel.Clipboard、SetTextAsync |
| `U/Views/FreeformHelperView.InputAndShortcuts.cs:OnConsoleCopyAll` | 1 clipboard | P1：同上；Ctrl+S 的 toast owner 也在此檔 |
| `U/Views/DxfOverlapReportWindow.axaml.cs:CopyAll_Click` | 1 clipboard | P1：同上 |
| `U/Views/NotchExportSelectionWindow.axaml.cs:CopyPayloadButton_Click／ExportButton_Click／ShowFilterBuilderButton_Click／OpenHeaderFilterButton_Click` | 1 clipboard；3 ShowDialog 呼叫點 | P1：export 確認、filter helper／column dialog |
| `U/MainWindow.axaml.cs:OnClosingAsync` | 3 候選行（含 class 宣告） | P1：async closing／cancel／save prompt；不能由 headless Close 成功推論真實互動已驗證 |
| `U/Views/FreeformHelperView.Lifecycle.cs:OnAttachedToVisualTree／OnDetachedFromVisualTree／ScheduleInitialFit` | 2 候選行 | P1：TopLevel.KeyDown 掛接／解除、owner layout |
| `U/Controls/WorkspaceHeader.axaml.cs:OnAttachedToVisualTree／OnDetachedFromVisualTree` | 5 候選行 | P1：TopLevel pointer／LayoutUpdated hook |
| `U/Services/CadLoadSpinnerHostService.cs:Show`、`U/Services/CadLoadSpinnerProcessHost.cs:Show／BuildShowRequest`、`U/Views/FreeformHelperView.CadLoadSpinner.cs:ShowCadLoadSpinnerHost` | 5 候選行，分布 3 檔 | P1：Window owner 的位置／可見狀態傳給 spinner host |
| `U/Views/FreeformHelperView.DxfLayerImageExport.cs:ShowDxfLayerImageExportWindowAsync`、`U/Views/LeftDxfPanel.axaml.cs:ResetAllDxfEditsButton_Click`、`U/Views/SettingsWindow.axaml.cs:ResetAllSettingsAsync` | 各 1 ShowDialog 呼叫點 | P1：typed bool 結果與 parent ownership |
| `U/Views/SettingsSections/SettingsGeneralSectionView.axaml.cs:EditCascadeDetailsButton_Click`、`U/Views/WorkspaceSections/NotchExportSelectionRowsPaneView.axaml.cs:OpenHeaderFilterButton_Click` | 各 1 ShowDialog 呼叫點 | P1：設定／filter dialog owner |
| `U/Views/FreeformHelperView.DxfEditChangeList.cs:ShowDxfEditChangeListWindowAsync`、`FreeformHelperView.DxfOverlap.cs:ShowDxfOverlapReportWindowAsync`、`FreeformHelperView.IndexMapping.cs:ShowIndexMappingReportWindowAsync`、`FreeformHelperView.NotchDetails.cs:ShowNotchDetailWindowAsync`、`FreeformHelperView.NotchExportSelection.cs:ShowNotchExportSelectionWindowAsync`、`FreeformHelperView.SettingsWindow.cs:OpenSettingsWindowCore`（後五檔亦位於 `U/Views/`） | 6 檔的 Window owner 入口 | P1：非 modal／floating windows 的 Show、hide、owner 與 focus |

直接 Window subclass 共 **17 個**：`U/MainWindow.axaml.cs:MainWindow`，以及 `U/Views/` 下同名 `.axaml.cs:class` 的 **16 個**：`CadLoadSpinnerWindow`、`CadPadDialog`、`CascadeIcSettingsWindow`、`ConfirmDialog`、`CoordinateArtifactDetailWindow`、`DxfEditChangeListWindow`、`DxfLayerImageExportWindow`、`DxfOverlapReportWindow`、`IndexMappingReportWindow`、`NotchDetailWindow`、`NotchExportColumnFilterWindow`、`NotchExportFilterBuilderWindow`、`NotchExportSelectionWindow`、`RegularPadDialog`、`SettingsWindow`、`WarningDialog`。其中 owner／async closing／clipboard 行為為 P1；單純繼承與 dialog result 公開 API 為 P2。

測試端的 6 個 Window 候選檔為 `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:HeadlessSessionGuardAttribute`（8 行）、`HeadlessSessionGuardTests.cs:HeadlessSessionGuardTests`（6 行，同目錄）、`T/UI/Smoke/HeadlessUiSmokeTests.cs:HeadlessUiSmokeTests`（6 行）、`T/UI/Snapshots/UiRenderedVisualSnapshotTests.cs:RenderControlToBgraBufferAsync`（2 行）、`T/UI/Services/CadLoadSpinnerProcessHostTests.cs:Show_WhileHideSendIsInFlight_RestoresVisibleState`（1 行）、`T/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:MeasureStageAsync`（2 行）。Headless 測試不能證明原生 clipboard、file picker 或 desktop focus 相容；這些仍待依官方遷移指南核對與平台實測。

## 7. 反射／私有欄位：Avalonia 與本庫型別須分開

指定搜尋 `GetField|GetMethod|BindingFlags` 得到 **32 個 test 檔／159 命中行**；UI production C# **0 檔／0 行**。其中有 **65 個 .GetField 呼叫點 + 68 個 .GetMethod 呼叫點 = 133 個**，其餘命中含 GetProperty／GetProperties／GetFields／GetMethods 使用的 BindingFlags；不要把 159 行當成 159 個 Avalonia internals 依賴。

**唯一直接反射 Avalonia 私有 member 的位置（P0）**：`T/UI/Logging/AppLogStoreTests.cs:AppLogStoreTests.AddAndClear_AfterUiReadyWithoutApplication_DoNotCreateDispatcher`，以 `typeof(Dispatcher).GetField("s_uiThread", BindingFlags.NonPublic | BindingFlags.Static)` 取得私有 static 欄位，再 **2 次 GetValue(null)** 比較前後 identity；**0 次 SetValue**。測試想鎖住「沒有 application 時 logging 不建立 dispatcher」，但欄位名稱／儲存方式不屬於公開 API，最可能需要重新核對。待依官方遷移指南及對應版本原始碼核對，不在準備階段改測試。

其餘 **132 個 GetField／GetMethod 呼叫點**指向本庫型別／test methods，並不是 Avalonia 私有 API。尤其 `typeof(PadCanvas)` 雖是 Control subclass，反射的都是本庫宣告的 members。下列是可定位的主要群組與完整檔案分布：

| `T/` 下的檔案:symbol／群組 | 檔數 | GetField／GetMethod 呼叫點 | 判讀 |
|---|---:|---:|---|
| `UI/Logging/AppLogStoreTests.cs:AddAndClear_AfterUiReadyWithoutApplication_DoNotCreateDispatcher` | 1 | 1／0 | P0：Dispatcher.s_uiThread |
| `UI/Canvas/PadCanvasCacheInvalidationTests.cs:PadCanvasCacheInvalidationTests` | 1 | 2／8 | P2：本庫 PadCanvas cache／render predicates |
| `UI/Canvas/PadCanvasHitTestTests.cs:PadCanvasHitTestTests` | 1 | 5／10 | P2：本庫 selection／hit-test／world transform；GetProperty 另讀本庫 hit models |
| `UI/Canvas/PadCanvasRenderPerfTests.cs:PadCanvasRenderPerfTests` | 1 | 0／2 | P2：本庫 RecordRenderPerf／BuildVisibleDrawLists |
| `UI/Canvas/PadCanvasViewRefreshTests.cs:PadCanvasViewRefreshTests` | 1 | 3／4 | P2：本庫 refresh flags／viewport methods |
| `UI/TestHost/HeadlessSessionGuardTests.cs:HeadlessSessionGuardTests` | 1 | 0／4 | P2：反射自身 public test method，供 guard hook 使用 |
| `UI/Services/CadLoadSpinnerProcessHostTests.cs:CadLoadSpinnerProcessHostTests` | 1 | 1／0 | P2：本庫 process host 欄位 |
| `UI/Services/RuntimeQueryIpcTests.cs:RuntimeQueryIpcTests` | 1 | 1／0 | P2：本庫 VM project 欄位 |
| `UI/Services/RuntimeQueryUseCaseTests.cs:SetPrivateField／GetPrivateDictionary` | 1 | 2／0 | P2：本庫 target 的共用反射 helper |
| `UI/Snapshots/UiSnapshotPersistenceContractTests.cs:GetSettablePropertyNames` | 1 | 0／0 | P2：BindingFlags 用於本庫 snapshot public properties |
| `Application/Cad/Cad3635LoadBenchmarkTests.cs:InvokePrivate` | 1 | 0／1 | P2：本庫 VM；不執行 confidential benchmark |
| `Application/Notch/NotchExampleCExportDriftTests.cs:NotchExampleCExportDriftTests` | 1 | 1／1 | P2：本庫 VM |
| `Application/Notch/NotchGoldenBaselineTests.cs:NotchGoldenBaselineTests` | 1 | 3／0 | P2：本庫 VM |
| `Application/Notch/NotchTableGeneratorTests.cs:NotchTableGeneratorTests` | 1 | 0／1 | P2：本庫 generator／nested types，不是 Avalonia |
| `Application/Notch/NotchToFullCoverageSnapshotBuilder.cs:NotchToFullCoverageSnapshotBuilder` | 1 | 2／2 | P2：本庫 VM |
| `Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:Tm81NotchAcceptanceMatrixTests` | 1 | 3／0 | P2：本庫 VM |
| `Workflow/DiffFrameCsvFixtureTests.cs:DiffFrameCsvFixtureTests` | 1 | 1／3 | P2：本庫 VM |
| `UI/ViewModels/FreeformHelperViewModelTests` 的下列 15 個 partial 檔 | 15 | 40／32 | P2：本庫 VM；upgrade 回歸可能觸發，但不是 Avalonia private-field dependency |

上表最後一列的檔案均位於 `T/UI/ViewModels/`，symbol 均為 `FreeformHelperViewModelTests`：`FreeformHelperViewModelTests.Basics.GridPitch.cs`（1／0）、`.Basics.LocateAndMatchActions.cs`（2／0）、`.Basics.Step3AndOverlap.cs`（7／0）、`.CommandsAndUndo.CadEditing.cs`（2／3）、`.CommandsAndUndo.Duplicates.cs`（2／11）、`.CommandsAndUndo.GeometryTransforms.cs`（1／6）、`.CommandsAndUndo.InputAndExport.cs`（1／0）、`.CommandsAndUndo.cs`（6／2）、`.Helpers.cs`（5／0）、`.NotchResolvedSnapshot.cs`（2／0）、`.PadInspectorCache.cs`（3／1）、`.RegularVisibilityMask.cs`（2／0）、`.SettingsPersistence.ProjectReplay.cs`（4／7）、`.SettingsPersistence.cs`（2／0）、`.WorkflowSnapshot.cs`（0／2）。省略前綴的檔名都補上 `FreeformHelperViewModelTests`。

額外搜尋沒有找到 AvaloniaLocator、UnsafeAccessor、GetRuntimeField／GetRuntimeMethod 或以 `GetType("Avalonia...` 取得內部型別的路徑。`U/Properties/AssemblyInfo.cs:InternalsVisibleTo` 只開放**本庫** internals 給 FreeformHelper.Tests，並非 Avalonia 的存取授權。

## 8. Obsolete suppression 與 warning-prone 呼叫

- 指定 C# 範圍中，`[Obsolete]`、`#pragma warning`、CS0618／CS0612 類 obsolete suppression、OpenFileDialog／SaveFileDialog／OpenFolderDialog、FormattedText 搜尋結果為 **0**。這不保證 Avalonia 12 沒有新增 obsolete 或 signature warning；仍待依官方遷移指南核對。
- 既有 **7 個 `[SuppressMessage]`** 都是 `CA1822:Mark members as static`、用於保留 instance service API；**不是 Avalonia obsolete suppression**。位置是 `U/Services/DxfImportService.cs:DxfImportService`、`DxfRegularMappingUseCase.cs:DxfRegularMappingUseCase`、`GridBuildService.cs:GridBuildService`、`ManualSizingService.cs:ManualSizingService`、`NotchExportService.cs:NotchExportService`、`PadMatchService.cs:PadMatchService`、`ProjectPersistenceUseCase.cs:ProjectPersistenceUseCase`（後六檔同目錄）。本次沒有新增、刪除或放寬 suppression。
- 目前最應留意重新編譯診斷的 API 是 `Program.BuildAvaloniaApp`／test builder 的 setup extensions、`UiThread` 與 guard 的 `SupportsRunLoops`／`RunJobs`、renderer 的 DrawingContext／TextLayout／RenderTargetBitmap、storage／clipboard 與 Window.ShowDialog。這是**核對清單**，不是宣稱它們在 Avalonia 12 已被標為 obsolete。
- 目前 11.3.12 的 UI 與 test project build 均為 **0 warning／0 error**；不能把這個結果當成 Avalonia 12 的編譯證明。

## 9. 本次驗證與後續核對邊界

本次只新增此文件；沒有變更 packages／build files、C#、XAML／styles、TODO 或 roadmap，沒有 commit／push、git config 變更或網路查詢。沒有新增相容層、設定選項、擴充點或未被要求的架構設計。

執行 build／test 前使用既有 `prepare-ui-workspace.ps1 -SkipStopApp -SkipNormalizeLineEndings`，避免 sandbox 不允許的 process listing，且避免額外產生整理報告；此文件另以 UTF-8／CRLF 寫入並檢查。每個 PowerShell 命令先設定 `AVALONIA_TELEMETRY_OPTOUT=1` 與 `GIT_CONFIG_GLOBAL=NUL`。沒有 restore，也沒有執行全測試專案。

| 已執行項目 | 結果 |
|---|---|
| UI project build：`--no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false` | 成功；0 warning／0 error |
| Tests project build：同上參數 | 成功；0 warning／0 error |
| `FullyQualifiedName~HeadlessSessionGuardTests` | 12 passed／0 failed／0 skipped |
| `FullyQualifiedName~AppLogStoreTests` | 4 passed／0 failed／0 skipped |
| `FullyQualifiedName~PadCanvasViewRefreshTests` | 8 passed／0 failed／0 skipped |
| `FullyQualifiedName~CanvasViewportAdapterTests` | 3 passed／0 failed／0 skipped |
| `pwsh -NoProfile -File ./scripts/verify.ps1 -StructureOnly` | 通過；56 listed classes、12 workspace-ownership cases、13 cleanup-selection cases；973 文字檔掃描、0 行尾變更；55 XAML 檔／1836 elements、0 issues |

合計 **4 組 class-filtered tests、27 passed／0 failed／0 skipped**。lint、真實 process listing 與 git writes 依 sandbox 限制略過；全套 tests、confidential fixture、原生平台 UI／picker／clipboard 實測及 Avalonia 12 套件驗證沒有執行。此文件非空、UTF-8／CRLF；最終執行 `git diff --check`，另用 `git diff --no-index --check` 檢查尚未加入 index 的新文件。StructureOnly 產生的暫存 JSON 在 gate 完成後移除，僅保留本文件。

後續尚待確認的問題只有：官方 Avalonia 12 migration guide 與對應 API／headless 原始碼，是否要求上述 P0／P1 位置調整，以及原生平台實測能否維持既有行為。此文件不改排程、不把準備工作標為升級完成。

## 附錄：重現搜尋口徑

以 `git ls-files -- src/FreeformHelper.UI tests` 選出 `.cs`，對每檔逐行套用下列 regex；檔數是至少一行命中的檔案數。保留原始註解／字串，人工誤命中在第 5 區說明。Attributes 以 `[AvaloniaFact`／`[AvaloniaTheory` 宣告計數，反射及 picker／clipboard 數量以呼叫點另計。

```text
threading: \b(?:Dispatcher|DispatcherTimer|DispatcherPriority|UiThread|SynchronizationContext|AvaloniaSynchronizationContext)\b
lifetime/platform: \b(?:AppBuilder|ApplicationLifetime|IClassicDesktopStyleApplicationLifetime|ISingleViewApplicationLifetime|UsePlatformDetect|StartWithClassicDesktopLifetime|AfterPlatformServicesSetup|UseHeadless|UseSkia|SetupWithoutStarting)\b
headless: \b(?:AvaloniaFact|AvaloniaTheory|AvaloniaTestApplication|AvaloniaFactAttribute|AvaloniaTheoryAttribute|HeadlessDispatcherSetup|HeadlessSessionGuardAttribute|AvaloniaHeadlessPlatformOptions|UseHeadless|UseHeadlessDrawing|CaptureRenderedFrame|HeadlessAppBootstrap)\b
rendering: \b(?:DrawingContext|SkiaSharp|SK\w+|RenderTargetBitmap|WriteableBitmap|ICustomDrawOperation)\b
input: \b(?:Pointer\w*EventArgs|KeyEventArgs|KeyModifiers|KeyGesture|RoutingStrategies|IPointer|Raw\w*InputEventArgs)\b|\bKey\.[A-Z]\w*|\b(?:AddHandler|RemoveHandler)\(
window/storage: \b(?:TopLevel|Window|Clipboard|StorageProvider|IStorage\w+|FilePicker\w+|FolderPicker\w+|OpenFileDialog|SaveFileDialog|OpenFolderDialog|ShowDialog|WindowClosingEventArgs)\b
reflection: \b(?:GetField|GetMethod|BindingFlags)\b
warning: #pragma\s+warning|\b(?:Obsolete|SuppressMessage|OpenFileDialog|SaveFileDialog|OpenFolderDialog|FormattedText)\b
```
