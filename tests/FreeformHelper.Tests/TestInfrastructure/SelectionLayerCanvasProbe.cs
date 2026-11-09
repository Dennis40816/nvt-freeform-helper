// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.Tests.TestInfrastructure;

internal sealed class SelectionLayerCanvasProbe : ICanvasHost
{
    public IReadOnlyCollection<int> SelectedCadIds { get; private set; } = [];
    public IReadOnlyCollection<int> SelectedRegularIds { get; private set; } = [];
    public int FitToContentCount { get; private set; }
    public int ResetViewCount { get; private set; }
    public int FocusSelectionCount { get; private set; }
    public int FocusWithMinZoomCount { get; private set; }
    public double LastMinZoom { get; private set; }

    public void FitToContent() => FitToContentCount++;

    public void ResetView() => ResetViewCount++;

    public void Invalidate()
    {
    }

    public void ClearSelection()
    {
        SelectedCadIds = [];
        SelectedRegularIds = [];
    }

    public void SetSelection(IReadOnlyCollection<int> cadPadIds, IReadOnlyCollection<int> regularPadIndices)
    {
        SelectedCadIds = cadPadIds.ToArray();
        SelectedRegularIds = regularPadIndices.ToArray();
    }

    public void FocusSelection() => FocusSelectionCount++;

    public void FocusSelectionWithMinZoom(double minZoom)
    {
        FocusWithMinZoomCount++;
        LastMinZoom = minZoom;
    }

    public PadCanvasSelectionPerfSnapshot GetSelectionPerfSnapshot() => default;

    public PadCanvasVisibleDrawPerfSnapshot GetVisibleDrawPerfSnapshot() => default;
}
