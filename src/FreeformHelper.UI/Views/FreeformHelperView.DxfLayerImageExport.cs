// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private Task<LayerImageExportRequest?> ShowDxfLayerImageExportWindowAsync(LayerImageExportContext context) =>
        TopLevel.GetTopLevel(this) is Window owner
            ? LayerImageExportWindowHost.ShowAsync(owner, context)
            : Task.FromResult<LayerImageExportRequest?>(null);
}
