// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns import choices on the UI thread; the two root properties remain the CAD import and snapshot port.</summary>
internal sealed class CadImportChoices(
    Action<bool, bool, Action<bool>, string> trackUndo,
    Action<string> propertyChanging,
    Action<string> propertyChanged)
{
    private bool _onlyClosedPolylines = CanvasDisplayToggles.Initial.ImportOnlyClosedPolylines;
    private bool _blockPolylines = CanvasDisplayToggles.Initial.ImportBlockPolylines;

    public bool ImportOnlyClosedPolylines
    {
        get => _onlyClosedPolylines;
        set => SetValue(ref _onlyClosedPolylines, value, nameof(ImportOnlyClosedPolylines),
            RestoreOnlyClosedPolylines, "Import closed polylines");
    }

    public bool ImportBlockPolylines
    {
        get => _blockPolylines;
        set => SetValue(ref _blockPolylines, value, nameof(ImportBlockPolylines),
            RestoreBlockPolylines, "Import block polylines");
    }

    private void RestoreOnlyClosedPolylines(bool value) => ImportOnlyClosedPolylines = value;
    private void RestoreBlockPolylines(bool value) => ImportBlockPolylines = value;

    private void SetValue(ref bool field, bool value, string propertyName, Action<bool> restore, string description)
    {
        if (field == value) return;
        trackUndo(field, value, restore, description);
        propertyChanging(propertyName);
        field = value;
        propertyChanged(propertyName);
    }
}
