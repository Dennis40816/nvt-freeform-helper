// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.CadExport;

/// <summary>UI-thread-only host callbacks. The view attaches and clears them with its lifetime.</summary>
public sealed class CadExportDialogs
{
    /// <summary>Gets or sets the attached view's export save picker.</summary>
    public Func<string, string, Task<string?>>? PickSaveExportPathAsync { get; set; }

    /// <summary>Gets or sets the warning dialog used when image export has no CAD document.</summary>
    public Func<string, string, Task>? ShowWarningAsync { get; set; }

    /// <summary>Gets or sets the layer image dialog, whose host supplies the image writer.</summary>
    public Func<LayerImageExportContext, Task<LayerImageExportRequest?>>? OpenDxfLayerImageExportAsync { get; set; }
}
