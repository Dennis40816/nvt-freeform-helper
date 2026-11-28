// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

/// <summary>Keeps accepted ARGB and editable text distinct on the UI thread, including recursive normalization.</summary>
internal sealed class EditableCanvasColor(
    string colorName,
    string hexName,
    string undoDescription,
    Func<string, uint?> parseColor,
    Func<uint, string> formatColor,
    Func<IDisposable> enterUndoSuppression,
    Action<string, string, Action<string>, string> trackUndo,
    Action<string> propertyChanging,
    Action<string> propertyChanged)
{
    private uint _value;
    private string _hexText = string.Empty;

    public uint Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            propertyChanging(colorName);
            _value = value;
            using (enterUndoSuppression())
            {
                HexText = formatColor(value);
            }
            propertyChanged(colorName);
        }
    }

    public string HexText
    {
        get => _hexText;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_hexText, value)) return;
            trackUndo(_hexText, value, RestoreHexText, undoDescription);
            propertyChanging(hexName);
            _hexText = value;
            if (parseColor(value) is { } color)
            {
                Value = color;
            }
            propertyChanged(hexName);
        }
    }

    private void RestoreHexText(string value) => HexText = value;
}
