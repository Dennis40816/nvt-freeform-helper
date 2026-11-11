// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the view callback delegates for one workspace.</summary>
internal sealed class ViewCallbacks
{
    public Func<Task<string?>>? PickOpenDxfPathAsync { get; set; }
    public Func<Task<string?>>? PickLoadProjectPathAsync { get; set; }
    public Func<Task<string?>>? PickSaveProjectPathAsync { get; set; }
    public Func<string, string, Task<string?>>? PickSaveNotchPathAsync { get; set; }
    public Func<string, string, Task<string?>>? PickSaveExportPathAsync { get; set; }
    public Func<Task<bool>>? ConfirmEmbedDxfAsync { get; set; }
    public Func<string, string, Task>? ShowWarningAsync { get; set; }
    public Func<bool>? EnsureNoPendingEdits { get; set; }
    public Func<NotchDetailViewModel, Task>? OpenNotchDetailAsync { get; set; }
    public Func<NotchExportSelectionViewModel, Task<NotchTable?>>? OpenNotchExportSelectionAsync { get; set; }
    public Func<SimulationSafetyAuditResult?>? GetCurrentSimulationSafetyAudit { get; set; }
    public Func<DxfEditChangeListViewModel, Task>? OpenDxfEditChangeListAsync { get; set; }
    public Func<IndexMappingReportViewModel, Task>? OpenIndexMappingReportAsync { get; set; }
    public Func<DxfOverlapReportViewModel, Task>? OpenDxfOverlapReportAsync { get; set; }
    public Func<LayerImageExportContext, Task<LayerImageExportRequest?>>? OpenDxfLayerImageExportAsync { get; set; }
}
