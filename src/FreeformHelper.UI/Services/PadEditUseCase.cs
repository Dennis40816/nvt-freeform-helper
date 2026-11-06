using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Coordinates pad edit operations such as manual sizing and per-pad sizing overrides.
/// </summary>
public static class PadEditUseCase
{
    public static ManualSizingResult ApplySizing(
        GridSettings settings,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        IReadOnlyCollection<int> rows,
        IReadOnlyCollection<int> cols,
        decimal? widthMm,
        decimal? heightMm)
    {
        return ManualSizingService.ApplySizing(
            settings,
            alignmentMode,
            cad,
            rows,
            cols,
            (double?)widthMm,
            (double?)heightMm);
    }

    public static ManualSizingResult ResetSizing(
        GridSettings settings,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        IReadOnlyCollection<int> rows,
        IReadOnlyCollection<int> cols)
    {
        return ManualSizingService.ResetSizing(settings, alignmentMode, cad, rows, cols);
    }
}
