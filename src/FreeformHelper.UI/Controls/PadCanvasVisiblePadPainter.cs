using Avalonia.Media;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Draws the visible pads of one <see cref="PadCanvas"/> frame in the fixed order:
/// unselected regular pads, unselected CAD pads, selected regular pads, selected CAD pads.
/// It gets the pad lists and the two single-pad draw operations from the canvas.
/// </summary>
internal sealed class PadCanvasVisiblePadPainter
{
    private readonly List<RegularPad> _regularUnselected;
    private readonly List<CadPad> _cadUnselected;
    private readonly List<RegularPad> _regularSelected;
    private readonly List<CadPad> _cadSelected;
    private readonly Action<DrawingContext, RegularPad, bool, RegularPadStyle> _drawRegular;
    private readonly Action<DrawingContext, CadPad, bool, CadPadStyle, bool> _drawCad;

    public PadCanvasVisiblePadPainter(
        List<RegularPad> regularUnselected,
        List<CadPad> cadUnselected,
        List<RegularPad> regularSelected,
        List<CadPad> cadSelected,
        Action<DrawingContext, RegularPad, bool, RegularPadStyle> drawRegular,
        Action<DrawingContext, CadPad, bool, CadPadStyle, bool> drawCad)
    {
        _regularUnselected = regularUnselected;
        _cadUnselected = cadUnselected;
        _regularSelected = regularSelected;
        _cadSelected = cadSelected;
        _drawRegular = drawRegular;
        _drawCad = drawCad;
    }

    public void Draw(
        DrawingContext context,
        bool showRegular,
        bool showCad,
        RegularPadStyle? regularStyle,
        CadPadStyle? cadStyle,
        bool lowDetailMode)
    {
        // 1. Draw Regular Pads (unselected)
        if (showRegular)
        {
            foreach (var pad in _regularUnselected)
            {
                _drawRegular(context, pad, false, regularStyle!);
            }
        }

        // 2. Draw CAD Pads (unselected)
        if (showCad)
        {
            foreach (var pad in _cadUnselected)
            {
                _drawCad(context, pad, false, cadStyle!, lowDetailMode);
            }
        }

        // 3. Draw Regular Pads (selected)
        if (showRegular)
        {
            foreach (var pad in _regularSelected)
            {
                _drawRegular(context, pad, true, regularStyle!);
            }
        }

        // 4. Draw CAD Pads (selected)
        if (showCad)
        {
            foreach (var pad in _cadSelected)
            {
                _drawCad(context, pad, true, cadStyle!, lowDetailMode);
            }
        }
    }
}
