// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Holds the coordinate planner's host callbacks.</summary>
internal sealed class CoordinatePlannerHostCallbacks
{
    public Func<CoordinateArtifactRow, Task>? RequestArtifactDetailAsync { get; set; }

    public Func<Task<bool>>? RequestExportPreviewPngAsync { get; set; }

    public Func<string, Task<bool>>? RequestSetClipboardTextAsync { get; set; }

    public Func<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerTextExportRequest, Task<bool>>? RequestSaveTextFileAsync { get; set; }
}
