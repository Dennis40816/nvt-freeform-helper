// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns display choices and color editors on the UI thread; import choices have a separate owner.</summary>
internal sealed class CanvasAppearanceState
{
    private readonly Action<bool, bool, Action<bool>, string> _trackUndo;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly WorkspaceToggleRegistry _header;
    private readonly EditableCanvasColor _cadLine;
    private readonly EditableCanvasColor _regularLine;
    private readonly EditableCanvasColor _regularSelected;
    private bool _showCad = CanvasDisplayToggles.Initial.ShowCad;
    private bool _showRegular = CanvasDisplayToggles.Initial.ShowRegular;
    private bool _highlightUnmatched = CanvasDisplayToggles.Initial.HighlightUnmatched;
    private bool _highlightFreeform = CanvasDisplayToggles.Initial.HighlightFreeform;
    private bool _colorCadByArea = CanvasDisplayToggles.Initial.ColorCadByArea;

    internal CanvasAppearanceState(
        CanvasStrokeFillState strokeFill,
        WorkspaceToggleRegistry header,
        Func<string, uint?> parseColor,
        Func<uint, string> formatColor,
        Func<IDisposable> enterUndoSuppression,
        Action<bool, bool, Action<bool>, string> trackToggleUndo,
        Action<string, string, Action<string>, string> trackTextUndo,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        StrokeFill = strokeFill;
        _header = header;
        _trackUndo = trackToggleUndo;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _cadLine = new EditableCanvasColor(nameof(CadLineColor), nameof(CadLineColorHex), "CAD line color",
            parseColor, formatColor, enterUndoSuppression, trackTextUndo, propertyChanging, propertyChanged);
        _regularLine = new EditableCanvasColor(nameof(RegularLineColor), nameof(RegularLineColorHex), "Regular line color",
            parseColor, formatColor, enterUndoSuppression, trackTextUndo, propertyChanging, propertyChanged);
        _regularSelected = new EditableCanvasColor(nameof(RegularSelectedColor), nameof(RegularSelectedColorHex), "Regular selection color",
            parseColor, formatColor, enterUndoSuppression, trackTextUndo, propertyChanging, propertyChanged);
    }

    internal CanvasStrokeFillState StrokeFill { get; }

    public bool ShowCad
    {
        get => _showCad;
        set => SetToggle(ref _showCad, value, nameof(ShowCad), "cadLayer", RestoreShowCad, "Toggle CAD layer");
    }

    public bool ShowRegular
    {
        get => _showRegular;
        set => SetToggle(ref _showRegular, value, nameof(ShowRegular), "regularGrid", RestoreShowRegular, "Toggle regular grid");
    }

    public bool HighlightUnmatched
    {
        get => _highlightUnmatched;
        set => SetToggle(ref _highlightUnmatched, value, nameof(HighlightUnmatched),
            "highlightUnmatched", RestoreHighlightUnmatched, "Toggle unmatched highlight");
    }

    public bool HighlightFreeform
    {
        get => _highlightFreeform;
        set => SetToggle(ref _highlightFreeform, value, nameof(HighlightFreeform),
            "highlightFreeform", RestoreHighlightFreeform, "Toggle freeform highlight");
    }

    public bool ColorCadByArea
    {
        get => _colorCadByArea;
        set => SetToggle(ref _colorCadByArea, value, nameof(ColorCadByArea),
            "colorByArea", RestoreColorCadByArea, "Toggle CAD area colors");
    }

    public uint CadLineColor { get => _cadLine.Value; set => _cadLine.Value = value; }
    public uint RegularLineColor { get => _regularLine.Value; set => _regularLine.Value = value; }
    public uint RegularSelectedColor { get => _regularSelected.Value; set => _regularSelected.Value = value; }
    public string CadLineColorHex { get => _cadLine.HexText; set => _cadLine.HexText = value; }
    public string RegularLineColorHex { get => _regularLine.HexText; set => _regularLine.HexText = value; }
    public string RegularSelectedColorHex { get => _regularSelected.HexText; set => _regularSelected.HexText = value; }

    private void RestoreShowCad(bool value) => ShowCad = value;
    private void RestoreShowRegular(bool value) => ShowRegular = value;
    private void RestoreHighlightUnmatched(bool value) => HighlightUnmatched = value;
    private void RestoreHighlightFreeform(bool value) => HighlightFreeform = value;
    private void RestoreColorCadByArea(bool value) => ColorCadByArea = value;

    private void SetToggle(ref bool field, bool value, string propertyName, string id, Action<bool> restore, string description)
    {
        if (field == value) return;
        _trackUndo(field, value, restore, description);
        _propertyChanging(propertyName);
        field = value;
        _header.SyncWorkspaceToggle(id, value);
        _propertyChanged(propertyName);
    }
}
