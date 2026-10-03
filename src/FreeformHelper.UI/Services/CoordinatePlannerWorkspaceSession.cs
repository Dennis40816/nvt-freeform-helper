using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

public sealed record CoordinatePlannerWorkspaceSession(
    RegularGrid Grid,
    IReadOnlyList<CadPad> CadPads,
    IReadOnlyList<string> VisibleLayerNames,
    int SourceRevision,
    double DefaultMachineWidth,
    double DefaultMachineHeight,
    int DefaultPixelWidth,
    int DefaultPixelHeight,
    string? PreferredActiveAreaOutlineLayerName = null,
    bool DefaultShowRegular = true,
    bool DefaultHighlightUnmatched = true,
    bool DefaultHighlightFreeform = true)
{
    public int PadCount => Grid.Pads.Count;
}
