using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Handles CAD area-bucket based selection actions.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Selects all CAD output pads in the same area bucket as the specified pad.
    /// </summary>
    /// <param name="anchorCadPadId">Anchor CAD pad ID.</param>
    public void SelectCadAreaBucketFromPad(int anchorCadPadId)
    {
        if (!ColorCadByArea)
        {
            SetStatus("Area bucket select requires Color by area.");
            return;
        }

        var visiblePads = CadPads.ToList();
        var bucketIds = _cadAreaBucketService.ResolveBucketMembers(
            visiblePads,
            (double)AreaBucketTolerance,
            anchorCadPadId);

        if (bucketIds.Count == 0)
        {
            SetStatus("Area bucket select: no matching visible pad.");
            return;
        }

        _selectionCoordinator.ApplyProgrammaticSelection(
            bucketIds,
            Array.Empty<int>(),
            CanvasHost);

        SetStatus($"Area bucket selected: {bucketIds.Count} pads.");
        Logger.Debug(CultureInfo.InvariantCulture, "Area bucket selected from CAD {0} (count={1}).", anchorCadPadId, bucketIds.Count);
    }
}

