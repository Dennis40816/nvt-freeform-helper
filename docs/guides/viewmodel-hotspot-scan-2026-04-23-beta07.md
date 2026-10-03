# ViewModel Hotspot Scan（Beta 0.7 / S11.151）

日期：2026-04-23
分支：`codex/beta0.7`
範圍：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel*.cs`

## 結論

Beta 0.7 已完成多個大型 ViewModel / XAML / test hotspot 拆分，但 `FreeformHelperViewModel` 仍是 repo 中最大的 orchestration 聚合點。剩餘風險不是單一檔案行數，而是「UI state、workflow command、projection/cache、persistence request builder」仍在同一 partial class family 中互相讀寫 private fields。

本 slice 不改行為，目的只建立下一輪拆分候選與順序。

## 最高優先檔案

| Rank | File | Lines | Primary responsibility | 建議方向 |
| --- | ---: | ---: | --- | --- |
| 1 | `FreeformHelperViewModel.Operations.cs` | 508 | CAD/grid/workflow 操作 glue、狀態更新、UI side effects | 拆 `WorkspaceOperationUseCase`，讓 VM 只負責 command binding 與 status projection |
| 2 | `FreeformHelperViewModel.Persistence.Project.cs` | 452 | Save/Load project request、deferred app settings flush、project replay | 拆 `ProjectLoadOrchestrator` / `ProjectSaveOrchestrator`，明確分離 file IO request 與 UI side effects |
| 3 | `FreeformHelperViewModel.State.CanvasAndMapping.cs` | 452 | Canvas + mapping observable state | 只保留 state；將 derived summary / mapping projection 搬到 projector |
| 4 | `FreeformHelperViewModel.Operations.LayerFiltering.cs` | 446 | Layer filter、visible CAD projection、bounds/selection side effects | 拆 `LayerFilteringUseCase`，統一 `FilterCadPadsByLayer` 結果模型 |
| 5 | `FreeformHelperViewModel.Core.cs` | 427 | constructor、service wiring、option list、global entry fields | 將 option construction 與 service wiring 移到 factory，降低 ctor 寬度 |
| 6 | `FreeformHelperViewModel.PadInspectorSummary.cs` | 424 | inspector summary text / chips / detail aggregation | 轉成 `PadInspectorSummaryProjector`，VM 只持有 result model |
| 7 | `FreeformHelperViewModel.State.Configuration.cs` | 403 | observable configuration state | 保留 state，但拆出 Step-specific state container 或 Settings bridge |
| 8 | `FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs` | 389 | Step5 export target/cache/fingerprint helper | 拆 `NotchExportRequestBuilder` 與 `NotchExportCacheKeyBuilder` |
| 9 | `FreeformHelperViewModel.PadInspectorDeferred.cs` | 388 | deferred inspector computation / cache orchestration | 拆 `PadInspectorRefreshUseCase`，與 Summary projector 合併輸出契約 |
| 10 | `FreeformHelperViewModel.DxfEditing.ChangeList.cs` | 353 | DXF edit change list projection + apply/focus command path | 拆 `DxfEditChangeListUseCase`，保留單一 apply entry |

## 單一路徑風險

目前可接受但應持續收斂：

- `WorkflowDataSnapshot` 已集中 `CadOutputFwDiffIndexByCadId`，但 ViewModel 仍持有多個 raw field，後續應只允許 snapshot builder 讀取 private field。
- `RuntimeQueryUseCase` 已走 VM 單一 command entry，但 query payload 仍直接讀 VM 多個 observable state；應逐步改成共享 result model。
- Step5 export UI / runtime query / C/CSV export 已同源，但 export target、cache fingerprint、file type wording 分散在 helper partial 與 metadata class。
- Pad inspector summary、trace、deferred refresh 已拆檔但仍共享大量 private state；下一步應先建立 `PadInspectorSnapshot` 作唯一 result model。

## Beta 0.8 候選切片

1. `S12.001 Project load/save orchestration extraction`
   - Scope：`FreeformHelperViewModel.Persistence.Project.cs`、`ProjectPersistenceUseCase`
   - Done：VM 不直接組大型 load/save flow，只呼叫 orchestrator 並套用明確 side-effect result。

2. `S12.002 Layer filtering result model`
   - Scope：`FreeformHelperViewModel.Operations.LayerFiltering.cs`
   - Done：`FilterCadPadsByLayer()` 回傳 typed result，selection clear / bounds rebuild / status text 由單一 policy 套用。

3. `S12.003 Pad inspector snapshot projector`
   - Scope：`PadInspector*.cs`
   - Done：inspector UI、runtime query、summary chips 共用同一 snapshot，不再各自從 partial private state 組文字。

4. `S12.004 Notch export request/cache extraction`
   - Scope：`Persistence.NotchExport.Helpers.cs`、`NotchExportFileTypeMetadata`
   - Done：export target、format kind、cache key、file extension 在一個 request model 中表達。

5. `S12.005 Core constructor slimming`
   - Scope：`Core.cs`、option list / service factory
   - Done：constructor 只注入 factories / services，不再 inline 建立大量 option list 與 delegates。

## 不建議在 Beta 0.7 收尾階段直接做的事

- 不直接拆 `State.*` 到多個 nested object，因為會牽涉 XAML binding 大量改名，風險高但收益有限。
- 不在同一 commit 同時改 Project load、Layer filtering、Pad inspector，這些都會觸發 selection/rebuild/cache side effects。
- 不把 `FreeformHelperViewModel` 一次搬成多個 ViewModel；應先抽 UseCase / projector，讓 binding surface 穩定。