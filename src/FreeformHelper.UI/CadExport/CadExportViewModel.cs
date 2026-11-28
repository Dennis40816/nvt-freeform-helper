// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.CadExport;

/// <summary>Stable export commands and host callbacks for one workspace.</summary>
public sealed class CadExportViewModel
{
    private readonly ICadExportWorkflow _workflow;

    internal CadExportViewModel(
        Func<CadExportDialogs, ICadExportWorkflow> createWorkflow,
        Func<Func<Task>, IAsyncRelayCommand> createEditingCommand)
    {
        _workflow = createWorkflow(Dialogs);
        ExportDxfVisibleCommand = createEditingCommand(() => _workflow.ExportDxfAsync(visibleOnly: true));
        ExportDxfAllCommand = createEditingCommand(() => _workflow.ExportDxfAsync(visibleOnly: false));
        ExportDxfLayerImageCommand = createEditingCommand(_workflow.ExportLayerImageAsync);
    }

    /// <summary>Gets the callbacks owned by the currently attached view.</summary>
    public CadExportDialogs Dialogs { get; } = new();

    /// <summary>Gets the command that exports the current CAD output and regular-source append.</summary>
    public IAsyncRelayCommand ExportDxfVisibleCommand { get; }

    /// <summary>Gets the command that exports all pads except effectively hidden pads.</summary>
    public IAsyncRelayCommand ExportDxfAllCommand { get; }

    /// <summary>Gets the command that opens the layer image export dialog.</summary>
    public IAsyncRelayCommand ExportDxfLayerImageCommand { get; }

    internal LayerImageExportContext BuildLayerImageExportContext(
        IReadOnlyList<CadPad> pads, Func<string, string, Task<string?>> pickSavePathAsync) =>
        _workflow.BuildLayerImageExportContext(pads, pickSavePathAsync);
}
