# Avalonia 12 upgrade preparation: C# dependency surface inventory (2026-10)

This document only inventories existing code; **no Avalonia 11 → 12 upgrade has been performed**. Avalonia, Desktop, Fluent, Inter, Headless, and Headless.XUnit are currently at version **11.3.12** (`Directory.Packages.props`); AvaloniaEdit has the independent version 11.4.1. Under the owner decision in `TODO.md`, the upgrade is scheduled **before 1.3.5**; only preparation is being done now.

The environment has no network access, and this document does not claim that Avalonia 12 has removed, renamed, or changed any API. Upgrade compatibility in every section is **"Pending verification against the official migration guide (to check against the official migration guide)"**; risk rankings are based on current dependencies, not confirmed Avalonia 12 breaking changes.

## Scope, counts, and priorities

- Baseline: the existing tracked C# on `feature/queue/avalonia12-prep-code-surface`, totaling **562 files**: **396 files** in `src/FreeformHelper.UI/**/*.cs` and **166 files** in `tests/**/*.cs`. Generated build／obj／bin files are excluded, and `example/` is not read.
- `AGENTS.md`, the domain entry point, dependency graph, and relevant roadmap／Runtime CLI contract were read at startup; the project references in the dependency graph still match the current five projects. This worktree has no `.codegraph/`.
- "Matching lines" in the table below and each section are **unique line counts** from per-file regex searches, including comments, strings, and declarations, not call counts or counts of required changes. The same file／line may occur in multiple sections. Attribute counts, control counts, and picker call counts are counted precisely and separately.
- Reference prefixes: `U/` = `src/FreeformHelper.UI/`; `T/` = `tests/FreeformHelper.Tests/`. `file:type／method／field` gives a directly locatable reference; symbols with the same name in partial classes are distinguished by the listed file.
- **P0 priority**: direct reads of private Avalonia fields, or dependencies on headless session／dispatcher teardown and recreation order, are the most likely to require adjustments.
- **P1 priority verification**: public APIs that depend on scheduling priorities, render／layout completion, pointer capture, platform services, or window closing order. Whether code changes are needed awaits confirmation from the guide and test evidence.
- **P2 routine checks**: public control／property APIs, or reflection on this repository's types only; current evidence is insufficient to require code changes for the upgrade.

| Area | UI files／matching lines | tests files／matching lines | Assessment |
|---|---:|---:|---|
| Dispatcher／UiThread／timer／context | 37／86 | 12／50 | P0: host order; P1: scheduling and deferred work |
| lifetime／AppBuilder／platform initialization | 2／6 | 1／5 | P1; font bootstrap listed separately |
| headless host／attribute／frame capture | 0／0 | 24／184 | P0: session; P1: actual frame capture |
| DrawingContext／SkiaSharp／bitmap | 13／48 | 1／16 | P1: custom renderers; headless capture listed separately |
| Input／routed event search candidates | 21／112 | 3／15 | **19／110, 2／14** after manually excluding 3 collection Key false positives |
| TopLevel／Window／clipboard／storage／dialog | 40／135 | 6／25 | P1: platform／owner／closing behavior; includes Window declarations |
| GetField／GetMethod／BindingFlags | 0／0 | 32／159 | Only **1 location** directly reflects on a private Avalonia field |
| obsolete／warning search candidates | 7／7 | 0／0 | All are existing CA1822 SuppressMessage uses; not Avalonia obsolete suppression |

## 1. Threads, Dispatcher, and deferred work

The first location to check is `U/Services/UiThread.cs:UiThread.TryGetRunningDispatcher／IsCurrent`: it checks `Application.Current` before obtaining the global `Dispatcher.UIThread`, then checks `SupportsRunLoops` and `CheckAccess()`. Its current purposes include preventing plain tests／background work from creating a dispatcher without a platform, and avoiding waits on an unprocessed queue. The upgrade should preserve these existing behavioral safeguards pending verification against the official migration guide, without replacing the dispatcher implementation in advance.

The following UI locations total **37 files／86 matching lines**. Except for the coupling between UiThread and the host, all use public scheduling APIs; verification should prioritize whether callbacks still run on the expected UI thread, whether they still write to the UI after invalidation／cancellation, and whether timers stop after detach／close.

| File:symbol | Matching lines | Level and current dependencies |
|---|---:|---|
| `U/Services/UiThread.cs:UiThread.TryGetRunningDispatcher／IsCurrent` | 4 | P0: global dispatcher／run-loop check; the `SupportsRunLoops` line is counted separately by a targeted search |
| `U/App.axaml.cs:App.OnFrameworkInitializationCompleted` | 5 | P1: Background／ApplicationIdle IPC startup; UnhandledException |
| UI callbacks in the `U/Logging/AppLogStore.cs:AppLogStore` constructor | 2 | P1: CheckAccess／Post; must not create a dispatcher without an application |
| `U/Services/RuntimeQueryIpc.cs:RuntimeQueryIpcServer.HandleConnectionAsync` | 1 | P1: IPC requests switch back to the UI; preserve the existing RuntimeQueryUseCase entry point |
| `U/Services/UiResourceResolver.cs:UiResourceResolver.TryResolve` | 1 | P1: read application resources only on the current UI thread |
| `U/Services/CanvasViewportAdapter.cs:CanvasViewportAdapter.QueueInitialFit` | 1 | P1: fit after Loaded, dependent on initial layout order |
| `U/ViewModels/FreeformHelperViewModel.CadLoadOverlay.cs:YieldCadLoadCanvasOverlayFrameAsync` | 2 | P1: the UI thread yields a frame at Render priority; other threads use Task.Yield |
| `U/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs:RunDeferredCadInspectorSnapshotRefreshAsync／RunDeferredRegularInspectorSnapshotRefreshAsync` | 6 | P1: Background, cancellation token, deferred completion |
| `U/ViewModels/FreeformHelperViewModel.Selection.NotchPreview.cs:RunDeferredSelectionNotchPreviewRefreshAsync` | 2 | P1: Background and cancellation／selection update order |
| `U/ViewModels/FreeformHelperViewModel.NotchDetails.AutoPlay.cs:RunNotchPreviewAutoPlayLoopAsync` | 1 | P1: asynchronous playback returns to the UI |
| `U/ViewModels/FreeformHelperViewModel.Persistence.cs:ExportNotchTableAsync` | 2 | P1：Background queue yield |
| `U/ViewModels/FreeformHelperViewModel.Progress.cs:CreateUiProgressReporter` | 1 | P1: background progress returns to the UI |
| `U/ViewModels/FreeformHelperViewModel.State.CanvasAndMapping.cs:ApplyCanvasColorDefaultsFromTokens／ResolveTokenColor` | 2 | P1: read theme／token only on the UI thread |
| `U/ViewModels/FreeformHelperViewModel.AppSettings.cs:_appGeneralSettingsPersistTimer` | 1 | P1: DispatcherTimer; preserve the contract of deferral after Load and flush after successful Save |
| `U/ViewModels/ShellViewModel.cs:ShellViewModel` constructor | 2 | P1: Background console initialization |
| `U/ViewModels/SimulationWorkspaceViewModel.cs:_playbackTimer` | 1 | P1: Background playback timer |
| `U/Controls/LoadingSpinner.axaml.cs:LoadingSpinner._timer`／constructor | 2 | P1: animation DispatcherTimer |
| `U/Controls/PadCanvas.HoverDebug.cs:EnsureHoverDebugRevealTimer` | 1 | P1: deferred hover display |
| `U/Controls/PadCanvas.State.cs:_hoverDebugRevealTimer` | 1 | P1: timer field |
| `U/Controls/PadCanvas.Input.Selection.cs:SchedulePendingSingleSelection` | 1 | P1: deferred single-click confirmation, dependent on double-click／selection version |
| `U/Controls/PadCanvas.Input.cs:_pendingSingleTimer` | 1 | P1: timer field |
| `U/Controls/PadCanvas.View.cs:FitToContent` | 2 | P1: update viewport at Loaded |
| `U/Controls/PadCanvas.ViewRefresh.cs:RequestVisualRefresh／CreateTransientLowDetailTimer` | 5 | P1: coalesced refresh at Render, InvalidateVisual, navigation timer |
| `U/Controls/WorkspaceHeader.axaml.cs:ScheduleViewPopupHoverClose` | 2 | P1: close hover popup at Background |
| `U/Views/CadLoadSpinnerWindow.axaml.cs:OnOpened／StartParentMonitor／RunIpcLoopAsync` | 6 | P1: Loaded, Render, parent-monitor timer and IPC |
| `U/Views/ConsolePanel.axaml.cs:OnShellViewModelPropertyChanged` | 2 | P1: synchronize text／scroll after Loaded |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:AttachDataContext／ExportPreviewPngAsync` | 3 | P1: Loaded overlay; bitmap export after Render |
| `U/Views/DevView.axaml.cs:OnAttachedToVisualTree／BuildProbeText` | 2 | P2: font and resource probe |
| `U/Views/FreeformHelperView.CadLoadSpinner.cs:OnObservedViewModelPropertyChanged` | 2 | P1: synchronize spinner at Normal priority |
| `U/Views/FreeformHelperView.Canvas.Layout.cs:QueuePadInfoLayoutUpdate` | 2 | P1: coalesced overlay layout at Background |
| `U/Views/FreeformHelperView.Canvas.cs:ShowPadInfoOverlay` | 1 | P1: update popover after Post |
| `U/Views/FreeformHelperView.Console.Events.cs:AttachConsoleAutoScroll` | 6 | P1: Background／Loaded, cross-thread collection updates |
| `U/Views/FreeformHelperView.ConsoleHost.cs:OnShellPropertyChanged` | 1 | P1: restore editor content after Render |
| `U/Views/FreeformHelperView.Lifecycle.cs:ScheduleInitialFit／ScheduleDeferredUiHooks` | 5 | P1: Loaded／Background, hooks after attach |
| `U/Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml.cs:OnDataContextPropertyChanged` | 1 | P1: BringIntoView after Post |
| `U/Views/SettingsWindow.axaml.cs:NavigateToSection／OnOpened` | 2 | P1：Background scroll |
| `U/Views/SimulationWorkspaceView.axaml.cs:QueueInlineEditorLayoutUpdate／FocusInlineEditorAsync` | 4 | P1：Background layout／Loaded focus |

Tests: **12 files／50 matching lines**:

| File:symbol | Matching lines | Key points |
|---|---:|---|
| `T/UI/TestHost/HeadlessDispatcherSetup.cs:EnsureRunLoopDispatcher` | 4 | P0: includes diagnostic strings／comments; the actual check is SupportsRunLoops |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:Before／After／WatchForLeftoverWork／CloseOpenWindowsAndRunQueuedWork` | 7 | P0: context, UnhandledException, RunJobs; see section 3 |
| `T/UI/TestHost/HeadlessSessionGuardTests.cs:HeadlessSessionGuardTests` | 12 | P0: tests the teardown／context contracts above |
| `T/UI/Logging/AppLogStoreTests.cs:AddAndClear_AfterUiReadyWithoutApplication_DoNotCreateDispatcher` | 1 | P0: private dispatcher field; see section 7 |
| `T/UI/Canvas/PadCanvasViewRefreshTests.cs:DrainUiQueueAsync` | 4 | P1: Background → Loaded → Render → Background in order |
| `T/UI/Services/CanvasViewportAdapterTests.cs:DrainUiQueueAsync` | 4 | P1: same as above, fit／viewport assertions |
| `T/UI/Smoke/HeadlessUiSmokeTests.cs:FlushUiQueueAsync` | 4 | P1: same as above, tooltip／spinner／text rendering |
| `T/UI/Smoke/TerminalStartupPathTests.cs:FlushUiQueueAsync` | 4 | P1: same as above, startup／console |
| `T/UI/Snapshots/UiRenderedVisualSnapshotTests.cs:FlushUiQueueAsync` | 4 | P1: same as above, queue completion before frame capture |
| `T/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:FlushUiQueueAsync` | 4 | P1: same as above; not run this time, and its confidential fixture is not read |
| `T/UI/ViewModels/CadLoadOverlayFrameYieldPolicyTests.cs:IsUiThreadThatRunsALoop_NeedsBothThreadAccessAndARunLoop` | 1 | P2: pure policy seam |
| `T/UI/ViewModels/FreeformHelperViewModelTests.Basics.LoadOverlay.cs:YieldCadLoadCanvasOverlayFrame_OnTheUiThread_RunsQueuedRenderWorkFirst` | 1 | P1: Render work must run first |

## 2. Application lifetime, AppBuilder, and platform setup

| File:symbol | Count | Level and items to check |
|---|---|---|
| `U/Program.cs:Program.Main／BuildAvaloniaApp` | 1 production builder, 1 StartWithClassicDesktopLifetime; 5 candidate matching lines | P1: STAThread, UsePlatformDetect, Configure<App>, font registration, LogToTrace; query CLI can exit UI startup early |
| `U/App.axaml.cs:App.Initialize／OnFrameworkInitializationCompleted` | 1 App subclass, 1 classic desktop lifetime branch | P1: MainWindow／spinner-mode window, Opened fallback, desktop.Exit, global exception subscription; currently only a desktop lifetime path |
| `U/Services/AppFontBootstrapper.cs:CreateFontManagerOptions／InterSystemFontSourceUri` | 1 options factory, 2 family mappings | P1: FontManagerOptions, Inter embedded source; shared with production／headless builders |
| `T/UI/TestHost/AvaloniaTestApp.cs:BuildAvaloniaApp` | 1 test builder; 5 candidate matching lines | P0/P1: UseSkia → UseHeadless, AfterPlatformServicesSetup, AfterSetup; see section 3 |

These are existing public initialization APIs. Platform detection, font source／family mappings, framework initialization callback order, and desktop exit behavior all await verification against the official migration guide. No C# locations directly using `Avalonia.Win32`／`Avalonia.X11`／`Avalonia.Native` or `AvaloniaLocator` were found.

## 3. Headless test host and xUnit attributes (P0)

**The host and session guard have the highest priority; this does not mean all headless attributes are considered broken.** This repository already has safeguards against dispatcher creation races, leftover layout work, and stale contexts; see S15.002 root causes five～eight in `TODO.md` for background. The current code assumes several aspects of session teardown implementation order as behavioral prerequisites, and each must be checked before the upgrade.

| File:symbol | Count／dependencies | Key checks |
|---|---|---|
| `T/UI/TestHost/AvaloniaTestApp.cs:BuildAvaloniaApp` | 1 `[assembly: AvaloniaTestApplication]`; UseHeadlessDrawing = false | P0/P1: host with actual Skia rendering, dispatcher check after platform registration; AfterSetup supplies Initialize |
| `T/UI/TestHost/HeadlessDispatcherSetup.cs:EnsureRunLoopDispatcher` | 1 SupportsRunLoops check | P0: reject a dispatcher without a run-loop early, before application creation; comments explicitly state that the media context／compositor retain the initialization dispatcher, but this file does not read them through reflection |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:Before／ThrowIfContextWasUsedBefore` | 1 assembly guard; 1 ConditionalWeakTable | P0: assumes default PerTest isolation, with a new synchronization context for each test; sharing an application within the same assembly would violate this guard's premise |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:WatchForLeftoverWork／IsSessionTeardown` | 1 UnhandledException hook; identifies teardown by SupportsRunLoops = false | P0: intercepts only exceptions after test completion／during teardown; exceptions in running tests must still cause failure |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:TrackOpenedWindows／CloseAtTestEnd／CloseOpenWindowsAndRunQueuedWork` | WindowOpenedEvent class handler; at most 3 close passes; 1 RunJobs call site | P0: clear DataContext, then Close, then drain the queue to prevent surviving windows from disrupting session teardown; includes windows that were only measured but never shown with Show |
| `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:IsHeadlessTest` | 2 attribute-type checks | P1: identifies AvaloniaFactAttribute／AvaloniaTheoryAttribute without reflecting on private Avalonia members |
| `T/UI/TestHost/HeadlessUiCollection.cs:HeadlessUiCollectionDefinition` | 1 DisableParallelization collection; 16 files marked HeadlessUiSerial | P1: headless／plain test isolation and ordering |
| `T/TestInfrastructure/HeadlessAppBootstrap.cs:EnsureInitialized` | 1 WeakReference<App> cache | P1: uses Application.Current and app identity to avoid duplicate Initialize |
| `T/UI/Snapshots/UiRenderedVisualSnapshotTests.cs:RenderControlToBgraBufferAsync` | 1 CaptureRenderedFrame call site | P1: Window.Show, queue flush, framebuffer／BGRA reads |
| `T/UI/Smoke/HeadlessUiSmokeTests.cs:CadLoadSpinnerWindow_RendersVisibleSpinner_AndAnimates／MainWindow_WithExpandedConsole_RendersVisibleConsoleText／MainWindow_WithSimulationWorkspace_RendersVisibleConsoleText` | 3 CaptureRenderedFrame call sites | P1: pixel／animation／text evidence |
| `T/UI/Smoke/TerminalStartupPathTests.cs:TerminalStartupPathTests` | 1 CaptureRenderedFrame call site | P1: console font／glyph evidence |
| `T/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:Cad3635EndToEndBenchmarkTests` | 1 CaptureRenderedFrame call site | P1: benchmark frame evidence; not run this time |

There are **98 attribute declarations** (**94 AvaloniaFact + 4 AvaloniaTheory**) across **20 files and 17 test classes**; 4 Theory declarations are not 4 expanded test cases. The table below lists the class in each file (partials with the same name count as one class):

| File:class under `T/` | Fact | Theory |
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

**Pending checks**: whether the official migration guide covers headless／xUnit isolation, callbacks, dispatcher reset, font／renderer disposal, and SynchronizationContext installation and restoration. This section only records the repository's current assumptions; it does not treat descriptions of Avalonia 11 teardown as known Avalonia 12 behavior.

## 4. Custom Control, PadCanvas, DrawingContext, and SkiaSharp

The UI has **4 types that directly inherit Control and override Render**: PadCanvas, NotchPreviewCanvas, NotchApplySimulationAaView, and NotchApplySimulationHeatmapView. There are also direct subclass declarations for **2 ContentControl, 39 UserControl, and 17 Window** types; ordinary XAML host inheritance does not itself imply use of internals, and XAML／styles are not inventoried this time.

`U/Controls/PadCanvas*.cs` contains **28 files** (including event／view-frame models); the following **8 files** contain its DrawingContext surface. These renderers use public APIs, but the viewport matrix, clip, text layout, brush／geometry cache, invalidation notifications, and low-detail switching are P1 regression priorities for the upgrade.

| File:symbol | rendering matching lines | Level／current uses |
|---|---:|---|
| `U/Controls/PadCanvas.Rendering.cs:PadCanvas.Render` | 2 | P1: main renderer, includes 1 XML comment line; transform／clip／layered drawing |
| `U/Controls/PadCanvas.Rendering.Cad.cs:DrawCadPad／DrawDiffIndexOverlay／DrawDiffIndexMarkerForPad` | 3 | P1: CAD geometry and overlay |
| `U/Controls/PadCanvas.Rendering.Regular.cs:DrawRegularPad／DrawRegularFreeformHatch／DrawDiagonalHatch／DrawSelectionOverlay` | 4 | P1：regular／hatch／selection |
| `U/Controls/PadCanvas.Rendering.Labels.cs:DrawMatchAllocationLabels／DrawRegularCoverageLabels／DrawCadCoverageLabels／DrawMatchRatioLabel` | 4 | P1: text and label placement |
| `U/Controls/PadCanvas.Rendering.NotchPreview.cs:DrawNotchToFullOverlay／DrawNotchToFullSeedOverlay／DrawNotchToFullCandidateOverlay／DrawNotchToRegularLabels` | 4 | P1：notch preview |
| `U/Controls/PadCanvas.Rendering.Simulation.cs:DrawSimulationRegularLabels／DrawSimulationCopperPillar／DrawSimulationCellText` | 3 | P1：simulation rendering |
| `U/Controls/PadCanvas.Rendering.HoverDebug.cs:DrawHoverDebugOverlay` | 1 | P1: hover debug text |
| `U/Controls/PadCanvas.View.AxisLabels.cs:DrawAxisLabels／GetAxisLabelLayout` | 1 | P1: axis labels; also has a TextLayout cache |
| `U/Controls/NotchPreviewCanvas.cs:NotchPreviewCanvas.Render／DrawHatch` | 2 | P1: standalone preview renderer |
| `U/Controls/NotchApplySimulationAaView.cs:NotchApplySimulationAaView.Render／DrawCellText` | 2 | P1: cells, text, and hit regions |
| `U/Controls/NotchApplySimulationHeatmapView.cs:NotchApplySimulationHeatmapView.Render` | 1 | P1：heatmap |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:ExportPreviewPngAsync` | 1 | P1: RenderTargetBitmap, captures the preview after the Render queue |
| `U/Services/DxfLayerImageExportService.cs:ExportLayerImage／DrawPadOutline／WorldToPixel／SaveImage` | 20 | P1: the only UI file directly using SkiaSharp; SKBitmap／SKCanvas／SKPaint／SKPath／SKImage／encoding, for offline image output, not a private Avalonia renderer |
| `T/Application/Dxf/DxfLayerImageExportServiceTests.cs:DxfLayerImageExportServiceTests` | 16 | P1: Skia output／pixel checks; not a headless host |

The other public property／control extensions are P2: `U/Controls/PadInfoSectionFrame.cs:PadInfoSectionFrame.TitleProperty` and `U/Controls/HidePanelBlock.axaml.cs:HidePanelBlock` (the two ContentControl types); the TipProperty class handler in `U/Services/SharedToolTipStyleService.cs:Register／NormalizeStringToolTip` is P1 and needs protection from the existing tooltip-open smoke test and static guard; styles are not changed this time.

Text dependencies include AaView, PadCanvas's Labels／NotchPreview／Simulation／HoverDebug／AxisLabels, the TextLayout cache in `PadCanvas.State.cs`, and `U/Views/DevView.axaml.cs:LogFontProbe`. No `ICustomDrawOperation` was found; there is currently no basis to claim a new renderer is required. Skia package／ABI and Avalonia 12 requirements likewise await verification against the official migration guide.

## 5. Pointer, keyboard, focus, and routed events

The precise input candidate search found **24 files／127 lines**. After manually excluding the three collection Key false positives `group.Key.ToDisplayLabel()`, `group.Key.ToContractString()`, and `row.Key.StartsWith()`, **21 files／124 lines** remain (UI **19／110**, tests **2／14**). Test assertions on source strings are still counted, but are not treated as runtime input handling.

| File:symbol | Matching lines | Level／verification priorities |
|---|---:|---|
| `U/Controls/PadCanvas.Input.cs:OnPointerPressed／OnPointerReleased` | 16 | P1: mouse button, click count, capture／release, modifiers and selection |
| `U/Controls/PadCanvas.Input.PointerMove.cs:OnPointerMoved／OnPointerExited` | 4 | P1: pan／box selection／hover, pointer coordinates and handled |
| `U/Controls/PadCanvas.Input.Navigation.cs:OnPointerWheelChanged／ShouldTreatWheelAsPan／OnKeyDown／OnKeyUp` | 20 | P1: wheel, Space／Ctrl, zoom／pan and keyboard scope |
| `U/Controls/PadCanvas.HoverDebug.cs:UpdateHoverDebugHit／RefreshHoverDebugHitFromCurrentPointer` | 3 | P1: modifiers and current pointer |
| `U/Controls/NotchApplySimulationAaView.cs:OnPointerPressed` | 1 | P1: cell hit regions; must remain consistent with Render |
| `U/Controls/NumberScrubber.Input.cs:OnInputKeyDown／OnPointerWheelChanged` | 5 | P1：Enter／Escape、wheel step |
| `U/Controls/NumberScrubber.Scrub.cs:OnScrubAreaPointerPressed／OnScrubAreaPointerMoved／OnScrubAreaPointerReleased／OnScrubAreaPointerCaptureLost` | 4 | P1: drag capture／capture lost and value commit |
| `U/Controls/NumberScrubber.axaml.cs:NumberScrubber` constructor | 1 | P1: PointerWheelChanged Bubble hook |
| Pointer handlers such as `U/Controls/WorkspaceHeader.axaml.cs:OnTopLevelPointerPressed／ViewMenuButton_PointerPressed` | 6 | P1: popup hover／outside clicks and top-level hook |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:OnArrayCornerHandlePointerPressed／OnArrayCornerPointerMoved／OnArrayCornerPointerReleased／MoveDraggedArrayCorner／CompleteDraggedArrayCorner` | 5 | P1：overlay handle capture／drag |
| `U/Views/SimulationWorkspaceView.axaml.cs:SimulationPadCanvas_PointerMoved／SimulationPadCanvas_PointerExited／SimulationPadCanvas_PointerPressed／SimulationPadCanvas_KeyDown` | 9 | P1: inline edit／context／selection shortcuts |
| `U/Views/FreeformHelperView.InputAndShortcuts.cs:OnTopLevelKeyDown／TryHandleGlobalShortcut／SaveProjectFromShortcutAsync／ClosePadInfoIfOutside／OnConsolePointerPressed` | 16 | P1: global shortcuts, text input exclusion, Ctrl+S, console font size |
| `U/Views/FreeformHelperView.Lifecycle.cs:OnRootPointerPressed／OnLayerTogglePointerPressed` | 3 | P1：root／toggle event propagation |
| Handlers such as `U/Views/FreeformHelperView.Console.Events.cs:OnConsoleEditorPointerMoved／OnConsoleTextViewPointerPressed` | 6 | P1: text area／AvaloniaEdit pointer paths |
| `U/Views/FreeformHelperView.ConsoleLinks.cs:OnConsoleEditorPointerReleased／TryHandleConsoleLinkActivation` | 2 | P1: modifiers + link hit |
| `U/Views/ConsolePanel.axaml.cs:ConsolePanel` constructor／`OnConsolePointerPressedInternal／OnConsoleEditorPointerReleasedInternal` | 4 | P1: Tunnel／Bubble, handledEventsToo |
| `U/Views/LeftDxfPanel.axaml.cs:LeftDxfPanel` constructor／`OnLayerTogglePointerPressedInternal` | 2 | P1: layer event hook |
| `U/Views/DxfEditChangeListWindow.axaml.cs:OnKeyDown` | 2 | P1: Escape closes the window |
| `U/Views/PadInfoPopover.axaml.cs:OnPopoverPointerPressed` | 1 | P1：popover event handled |
| `T/UI/Snapshots/UiLayoutGuardTests.cs:ShortcutContract_DoesNotExposeResetViewR／ShortcutContract_GlobalAndCanvasScopesRemainConsistent` | 13 | P2: source guard; cannot prove actual routed event order |
| `T/UI/Snapshots/UiVisualSnapshotTests.cs:CriticalUiFiles_MatchMinimalVisualBaseline` | 1 | P2: baseline key strings; does not directly send input events |

Related focus surface: `U/Services/CanvasFocusViewportService.cs:TryGetFocusTarget／TryGetWindowAwareFocusTarget` (2 Window candidate lines), `U/Views/FreeformHelperView.Canvas.cs:EnumerateFocusAwareFloatingWindows`, and `U/Views/SimulationWorkspaceView.axaml.cs:FocusInlineEditorAsync`. Focus, Tunnel／Bubble, Handled, capture lost, pointer coordinates, double-click, and wheel behavior all await verification against the official migration guide and practical testing; compatibility cannot be claimed on the basis of static guards alone.

## 6. TopLevel, Window, clipboard, storage, and dialogs

Candidates in this section total **46 files／160 lines**, including Window type declarations and documentation comments. The precise platform call counts are **6 SetTextAsync clipboard entry points** and **11 file picker call sites** (**5 Open + 6 Save**). No legacy OpenFileDialog／SaveFileDialog／OpenFolderDialog types or FolderPicker calls were found.

| File:symbol | Count | Level／current dependencies |
|---|---|---|
| `U/Views/FreeformHelperView.Pickers.cs:GetStorageProvider／ConfigureFilePickers／BuildOpenDxfOptions／BuildOpenProjectOptions／BuildOpenDiffCsvOptions／BuildOpenRegularVisibilityMaskOptions／BuildSaveProjectOptions／BuildSaveNotchOptions／BuildSaveExportOptions` | 54 candidate lines; 4 Open + 3 Save | P1: TopLevel.StorageProvider, filter／extension, IStorageFile.Path.LocalPath; delegates set on attach and cleared on detach; also has confirm／warning dialogs |
| `U/Views/CoordinatePlannerWorkspaceView.axaml.cs:SetClipboardTextAsync／SaveTextFileAsync／ExportPreviewPngAsync／OnArtifactDetailRequested` | 9 candidate lines; 1 clipboard + 2 Save | P1: platform services may be null, text file／bitmap export, owner window |
| `U/Views/SimulationWorkspaceView.axaml.cs:PickOpenDiffCsvPathsAsync／SetClipboardTextAsync／SaveTextFileAsync` | 7 candidate lines; 1 clipboard + 1 Open + 1 Save | P1: storage, clipboard, cancellation and local paths |
| `U/Views/ConsolePanel.axaml.cs:OnConsoleCopyAllInternal` | 1 clipboard | P1：TopLevel.Clipboard、SetTextAsync |
| `U/Views/FreeformHelperView.InputAndShortcuts.cs:OnConsoleCopyAll` | 1 clipboard | P1: same as above; the Ctrl+S toast owner is also in this file |
| `U/Views/DxfOverlapReportWindow.axaml.cs:CopyAll_Click` | 1 clipboard | P1: same as above |
| `U/Views/NotchExportSelectionWindow.axaml.cs:CopyPayloadButton_Click／ExportButton_Click／ShowFilterBuilderButton_Click／OpenHeaderFilterButton_Click` | 1 clipboard; 3 ShowDialog call sites | P1: export confirmation, filter helper／column dialog |
| `U/MainWindow.axaml.cs:OnClosingAsync` | 3 candidate lines (including the class declaration) | P1: async closing／cancel／save prompt; successful headless Close does not imply actual interaction has been verified |
| `U/Views/FreeformHelperView.Lifecycle.cs:OnAttachedToVisualTree／OnDetachedFromVisualTree／ScheduleInitialFit` | 2 candidate lines | P1: TopLevel.KeyDown hook／unhook, owner layout |
| `U/Controls/WorkspaceHeader.axaml.cs:OnAttachedToVisualTree／OnDetachedFromVisualTree` | 5 candidate lines | P1: TopLevel pointer／LayoutUpdated hook |
| `U/Services/CadLoadSpinnerHostService.cs:Show`, `U/Services/CadLoadSpinnerProcessHost.cs:Show／BuildShowRequest`, `U/Views/FreeformHelperView.CadLoadSpinner.cs:ShowCadLoadSpinnerHost` | 5 candidate lines across 3 files | P1: Window owner position／visibility passed to the spinner host |
| `U/Views/FreeformHelperView.DxfLayerImageExport.cs:ShowDxfLayerImageExportWindowAsync`, `U/Views/LeftDxfPanel.axaml.cs:ResetAllDxfEditsButton_Click`, `U/Views/SettingsWindow.axaml.cs:ResetAllSettingsAsync` | 1 ShowDialog call site each | P1: typed bool results and parent ownership |
| `U/Views/SettingsSections/SettingsGeneralSectionView.axaml.cs:EditCascadeDetailsButton_Click`, `U/Views/WorkspaceSections/NotchExportSelectionRowsPaneView.axaml.cs:OpenHeaderFilterButton_Click` | 1 ShowDialog call site each | P1: settings／filter dialog owner |
| `U/Views/FreeformHelperView.DxfEditChangeList.cs:ShowDxfEditChangeListWindowAsync`, `FreeformHelperView.DxfOverlap.cs:ShowDxfOverlapReportWindowAsync`, `FreeformHelperView.IndexMapping.cs:ShowIndexMappingReportWindowAsync`, `FreeformHelperView.NotchDetails.cs:ShowNotchDetailWindowAsync`, `FreeformHelperView.NotchExportSelection.cs:ShowNotchExportSelectionWindowAsync`, `FreeformHelperView.SettingsWindow.cs:OpenSettingsWindowCore` (the latter five files are also under `U/Views/`) | Window owner entry points in 6 files | P1: Show, hide, owner, and focus for non-modal／floating windows |

There are **17 direct Window subclasses**: `U/MainWindow.axaml.cs:MainWindow`, and **16** in matching `.axaml.cs:class` files under `U/Views/`: `CadLoadSpinnerWindow`, `CadPadDialog`, `CascadeIcSettingsWindow`, `ConfirmDialog`, `CoordinateArtifactDetailWindow`, `DxfEditChangeListWindow`, `DxfLayerImageExportWindow`, `DxfOverlapReportWindow`, `IndexMappingReportWindow`, `NotchDetailWindow`, `NotchExportColumnFilterWindow`, `NotchExportFilterBuilderWindow`, `NotchExportSelectionWindow`, `RegularPadDialog`, `SettingsWindow`, and `WarningDialog`. Their owner／async closing／clipboard behavior is P1; simple inheritance and public dialog result APIs are P2.

The 6 Window candidate test files are `T/UI/TestHost/HeadlessSessionGuardAttribute.cs:HeadlessSessionGuardAttribute` (8 lines), `HeadlessSessionGuardTests.cs:HeadlessSessionGuardTests` (6 lines, same directory), `T/UI/Smoke/HeadlessUiSmokeTests.cs:HeadlessUiSmokeTests` (6 lines), `T/UI/Snapshots/UiRenderedVisualSnapshotTests.cs:RenderControlToBgraBufferAsync` (2 lines), `T/UI/Services/CadLoadSpinnerProcessHostTests.cs:Show_WhileHideSendIsInFlight_RestoresVisibleState` (1 line), and `T/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs:MeasureStageAsync` (2 lines). Headless tests cannot prove native clipboard, file picker, or desktop focus compatibility; these still await verification against the official migration guide and testing on the platform.

## 7. Reflection／private fields: distinguish Avalonia from repository types

The specified search for `GetField|GetMethod|BindingFlags` found **32 test files／159 matching lines**; UI production C# had **0 files／0 lines**. These include **65 .GetField call sites + 68 .GetMethod call sites = 133**; the remaining matches include BindingFlags used by GetProperty／GetProperties／GetFields／GetMethods. Do not treat 159 lines as 159 dependencies on Avalonia internals.

**The only location directly reflecting on a private Avalonia member (P0)** is `T/UI/Logging/AppLogStoreTests.cs:AppLogStoreTests.AddAndClear_AfterUiReadyWithoutApplication_DoNotCreateDispatcher`, which uses `typeof(Dispatcher).GetField("s_uiThread", BindingFlags.NonPublic | BindingFlags.Static)` to obtain the private static field, then makes **2 GetValue(null) calls** to compare identity before and after; **0 SetValue calls**. The test is intended to enforce "logging does not create a dispatcher when there is no application," but the field name／storage mechanism is not part of the public API and is the most likely to need rechecking. Verification against the official migration guide and the corresponding version's source code is pending; tests are not changed during preparation.

The remaining **132 GetField／GetMethod call sites** target this repository's types／test methods, not private Avalonia APIs. In particular, although `typeof(PadCanvas)` is a Control subclass, all members accessed through reflection are declared in this repository. The following are the main locatable groups and the complete file distribution:

| File:symbol／group under `T/` | File count | GetField／GetMethod call sites | Assessment |
|---|---:|---:|---|
| `UI/Logging/AppLogStoreTests.cs:AddAndClear_AfterUiReadyWithoutApplication_DoNotCreateDispatcher` | 1 | 1／0 | P0：Dispatcher.s_uiThread |
| `UI/Canvas/PadCanvasCacheInvalidationTests.cs:PadCanvasCacheInvalidationTests` | 1 | 2／8 | P2: repository PadCanvas cache／render predicates |
| `UI/Canvas/PadCanvasHitTestTests.cs:PadCanvasHitTestTests` | 1 | 5／10 | P2: repository selection／hit-test／world transform; GetProperty also reads repository hit models |
| `UI/Canvas/PadCanvasRenderPerfTests.cs:PadCanvasRenderPerfTests` | 1 | 0／2 | P2: repository RecordRenderPerf／BuildVisibleDrawLists |
| `UI/Canvas/PadCanvasViewRefreshTests.cs:PadCanvasViewRefreshTests` | 1 | 3／4 | P2: repository refresh flags／viewport methods |
| `UI/TestHost/HeadlessSessionGuardTests.cs:HeadlessSessionGuardTests` | 1 | 0／4 | P2: reflects on its own public test method for use by the guard hook |
| `UI/Services/CadLoadSpinnerProcessHostTests.cs:CadLoadSpinnerProcessHostTests` | 1 | 1／0 | P2: repository process host field |
| `UI/Services/RuntimeQueryIpcTests.cs:RuntimeQueryIpcTests` | 1 | 1／0 | P2: repository VM project field |
| `UI/Services/RuntimeQueryUseCaseTests.cs:SetPrivateField／GetPrivateDictionary` | 1 | 2／0 | P2: shared reflection helper for repository targets |
| `UI/Snapshots/UiSnapshotPersistenceContractTests.cs:GetSettablePropertyNames` | 1 | 0／0 | P2: BindingFlags used for repository snapshot public properties |
| `Application/Cad/Cad3635LoadBenchmarkTests.cs:InvokePrivate` | 1 | 0／1 | P2: repository VM; confidential benchmark not run |
| `Application/Notch/NotchExampleCExportDriftTests.cs:NotchExampleCExportDriftTests` | 1 | 1／1 | P2: repository VM |
| `Application/Notch/NotchGoldenBaselineTests.cs:NotchGoldenBaselineTests` | 1 | 3／0 | P2: repository VM |
| `Application/Notch/NotchTableGeneratorTests.cs:NotchTableGeneratorTests` | 1 | 0／1 | P2: repository generator／nested types, not Avalonia |
| `Application/Notch/NotchToFullCoverageSnapshotBuilder.cs:NotchToFullCoverageSnapshotBuilder` | 1 | 2／2 | P2: repository VM |
| `Application/Notch/Tm81NotchAcceptanceMatrixTests.cs:Tm81NotchAcceptanceMatrixTests` | 1 | 3／0 | P2: repository VM |
| `Workflow/DiffFrameCsvFixtureTests.cs:DiffFrameCsvFixtureTests` | 1 | 1／3 | P2: repository VM |
| The following 15 partial files for `UI/ViewModels/FreeformHelperViewModelTests` | 15 | 40／32 | P2: repository VM; upgrade regressions may trigger them, but they are not Avalonia private-field dependencies |

All files in the last row of the table are under `T/UI/ViewModels/`, and their symbol is `FreeformHelperViewModelTests`: `FreeformHelperViewModelTests.Basics.GridPitch.cs` (1／0), `.Basics.LocateAndMatchActions.cs` (2／0), `.Basics.Step3AndOverlap.cs` (7／0), `.CommandsAndUndo.CadEditing.cs` (2／3), `.CommandsAndUndo.Duplicates.cs` (2／11), `.CommandsAndUndo.GeometryTransforms.cs` (1／6), `.CommandsAndUndo.InputAndExport.cs` (1／0), `.CommandsAndUndo.cs` (6／2), `.Helpers.cs` (5／0), `.NotchResolvedSnapshot.cs` (2／0), `.PadInspectorCache.cs` (3／1), `.RegularVisibilityMask.cs` (2／0), `.SettingsPersistence.ProjectReplay.cs` (4／7), `.SettingsPersistence.cs` (2／0), and `.WorkflowSnapshot.cs` (0／2). Add `FreeformHelperViewModelTests` to every filename whose prefix is omitted.

Additional searches found no AvaloniaLocator, UnsafeAccessor, GetRuntimeField／GetRuntimeMethod, or paths obtaining internal types through `GetType("Avalonia...`. `U/Properties/AssemblyInfo.cs:InternalsVisibleTo` exposes only **this repository's** internals to FreeformHelper.Tests; it does not grant access to Avalonia.

## 8. Obsolete suppression and warning-prone calls

- Within the specified C# scope, searches for `[Obsolete]`, `#pragma warning`, CS0618／CS0612-style obsolete suppression, OpenFileDialog／SaveFileDialog／OpenFolderDialog, and FormattedText returned **0** results. This does not guarantee that Avalonia 12 introduces no obsolete or signature warnings; verification against the official migration guide is still pending.
- All **7 existing `[SuppressMessage]` uses** are for `CA1822:Mark members as static`, to preserve instance service APIs; they are **not Avalonia obsolete suppression**. The locations are `U/Services/DxfImportService.cs:DxfImportService`, `DxfRegularMappingUseCase.cs:DxfRegularMappingUseCase`, `GridBuildService.cs:GridBuildService`, `ManualSizingService.cs:ManualSizingService`, `NotchExportService.cs:NotchExportService`, `PadMatchService.cs:PadMatchService`, and `ProjectPersistenceUseCase.cs:ProjectPersistenceUseCase` (the latter six files are in the same directory). No suppression was added, removed, or relaxed this time.
- The APIs whose recompilation diagnostics deserve the most attention are the setup extensions in `Program.BuildAvaloniaApp`／the test builder, `SupportsRunLoops`／`RunJobs` in `UiThread` and the guard, DrawingContext／TextLayout／RenderTargetBitmap in renderers, storage／clipboard, and Window.ShowDialog. This is a **checklist**, not a claim that Avalonia 12 has marked them obsolete.
- The current 11.3.12 UI and test project builds both report **0 warning／0 error**; this result cannot serve as compilation evidence for Avalonia 12.

## 9. Verification performed and boundaries for follow-up checks

Only this document was added this time; packages／build files, C#, XAML／styles, TODO, and the roadmap were not changed, and no commit／push, git config change, or network lookup was performed. No compatibility layer, configuration option, extension point, or unrequested architectural design was added.

Before build／test, the existing `prepare-ui-workspace.ps1 -SkipStopApp -SkipNormalizeLineEndings` was used to avoid process listing prohibited by the sandbox and avoid generating an additional cleanup report; this document was separately written and checked as UTF-8／CRLF. `AVALONIA_TELEMETRY_OPTOUT=1` and `GIT_CONFIG_GLOBAL=NUL` were set before every PowerShell command. No restore was performed, and the full test project was not run.

| Item run | Result |
|---|---|
| UI project build: `--no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false` | Succeeded; 0 warning／0 error |
| Tests project build: same arguments as above | Succeeded; 0 warning／0 error |
| `FullyQualifiedName~HeadlessSessionGuardTests` | 12 passed／0 failed／0 skipped |
| `FullyQualifiedName~AppLogStoreTests` | 4 passed／0 failed／0 skipped |
| `FullyQualifiedName~PadCanvasViewRefreshTests` | 8 passed／0 failed／0 skipped |
| `FullyQualifiedName~CanvasViewportAdapterTests` | 3 passed／0 failed／0 skipped |
| `pwsh -NoProfile -File ./scripts/verify.ps1 -StructureOnly` | Passed; 56 listed classes, 12 workspace-ownership cases, 13 cleanup-selection cases; 973 text files scanned, 0 line-ending changes; 55 XAML files／1836 elements, 0 issues |

Total: **4 class-filtered test groups, 27 passed／0 failed／0 skipped**. Lint, actual process listing, and git writes were skipped under sandbox restrictions; the full test suite, confidential fixtures, native platform UI／picker／clipboard testing, and Avalonia 12 package verification were not run. This document is nonempty and uses UTF-8／CRLF; `git diff --check` was run at the end, with `git diff --no-index --check` also used to check the new document not yet added to the index. Temporary JSON generated by StructureOnly was removed after the gate completed, leaving only this document.

The only remaining questions are whether the official Avalonia 12 migration guide and corresponding API／headless source code require adjustments to the P0／P1 locations above, and whether native platform testing can confirm existing behavior is preserved. This document does not change the schedule or mark preparation as a completed upgrade.

## Appendix: reproducing the search criteria

Use `git ls-files -- src/FreeformHelper.UI tests` to select `.cs` files, then apply the following regexes line by line to each file; the file count is the number of files with at least one matching line. Original comments／strings are retained; manually identified false positives are explained in section 5. Attributes are counted by `[AvaloniaFact`／`[AvaloniaTheory` declarations, while reflection and picker／clipboard counts are counted separately by call site.

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
