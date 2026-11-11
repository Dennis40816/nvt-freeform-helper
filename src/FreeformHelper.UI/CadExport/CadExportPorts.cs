// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.CadExport;

// Live UI-thread readers. Each operation captures only the inputs it needs before its dialog await.
internal sealed record CadExportInputs(
    Func<IReadOnlyList<CadPad>> VisiblePads,
    Func<RegularSourceMode> RegularSourceMode,
    Func<string?> RegularSourceLayerName,
    Func<(int? X, int? Y)> ActiveAreaPixels,
    Func<DxfLayerImageExportRequest> ImagePreferences);

// The existing status and preference owners remain the only writers, including after project replacement.
internal sealed record CadExportEffects(
    Action<string> SetStatus,
    Action<string, Exception> SetStatusError,
    Action<DxfLayerImageExportRequest> RememberImagePreferences);

internal interface ICadExportWorkflow
{
    Task ExportDxfAsync(bool visibleOnly);
    Task ExportLayerImageAsync();
    LayerImageExportContext BuildLayerImageExportContext(
        IReadOnlyList<CadPad> pads, Func<string, string, Task<string?>> pickSavePathAsync);
}
