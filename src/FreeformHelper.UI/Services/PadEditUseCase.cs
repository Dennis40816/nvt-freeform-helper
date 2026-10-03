using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Coordinates pad edit operations such as manual sizing and per-pad sizing overrides.
/// </summary>
public sealed class PadEditUseCase
{
    private readonly ManualSizingService _manualSizingService;

    public PadEditUseCase(ManualSizingService manualSizingService)
    {
        _manualSizingService = manualSizingService ?? throw new ArgumentNullException(nameof(manualSizingService));
    }

    public ManualSizingResult ApplySizing(
        GridSettings settings,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        IReadOnlyCollection<int> rows,
        IReadOnlyCollection<int> cols,
        decimal? widthMm,
        decimal? heightMm)
    {
        return _manualSizingService.ApplySizing(
            settings,
            alignmentMode,
            cad,
            rows,
            cols,
            (double?)widthMm,
            (double?)heightMm);
    }

    public ManualSizingResult ResetSizing(
        GridSettings settings,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        IReadOnlyCollection<int> rows,
        IReadOnlyCollection<int> cols)
    {
        return _manualSizingService.ResetSizing(settings, alignmentMode, cad, rows, cols);
    }

    public ManualSizingResult SetPadDimensions(
        GridSettings settings,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        int row,
        int col,
        double widthMm,
        double heightMm)
    {
        return _manualSizingService.SetPadDimensions(
            settings,
            alignmentMode,
            cad,
            row,
            col,
            widthMm,
            heightMm);
    }
}
