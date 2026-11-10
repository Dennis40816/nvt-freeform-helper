// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns canvas stroke and fill preferences and their synchronous transitions on the UI thread.</summary>
internal sealed class CanvasStrokeFillState
{
    private readonly Func<bool> _isLoadingSettings;
    private readonly Func<bool> _isUndoSuppressed;
    private readonly Func<IDisposable> _enterUndoSuppression;
    private readonly Action<Action, string> _registerUndo;
    private readonly Action _invalidate;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;

    private decimal _cadLineWidth = 0.2m;
    private decimal _cadLineOpacity = 1.0m;
    private decimal _cadFillOpacity = 1.0m;
    private decimal _regularLineWidth = 0.2m;
    private decimal _highlightStrokeWidthAdjust = -1m;
    private decimal _regularLineOpacity = 1.0m;
    private decimal _regularFillOpacity = 1.0m;
    private decimal _regularSelectedFillOpacity = 0.24m;

    internal CanvasStrokeFillState(
        Func<bool> isLoadingSettings,
        Func<bool> isUndoSuppressed,
        Func<IDisposable> enterUndoSuppression,
        Action<Action, string> registerUndo,
        Action invalidate,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        ArgumentNullException.ThrowIfNull(isLoadingSettings);
        ArgumentNullException.ThrowIfNull(isUndoSuppressed);
        ArgumentNullException.ThrowIfNull(enterUndoSuppression);
        ArgumentNullException.ThrowIfNull(registerUndo);
        ArgumentNullException.ThrowIfNull(invalidate);
        ArgumentNullException.ThrowIfNull(propertyChanging);
        ArgumentNullException.ThrowIfNull(propertyChanged);
        _isLoadingSettings = isLoadingSettings;
        _isUndoSuppressed = isUndoSuppressed;
        _enterUndoSuppression = enterUndoSuppression;
        _registerUndo = registerUndo;
        _invalidate = invalidate;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
    }

    public decimal CadLineWidth
    {
        get => _cadLineWidth;
        set => SetValue(ref _cadLineWidth, value, nameof(CadLineWidth), RestoreCadLineWidth, "CAD line width");
    }

    public decimal CadLineOpacity
    {
        get => _cadLineOpacity;
        set => SetValue(ref _cadLineOpacity, value, nameof(CadLineOpacity), changed: _ => Invalidate());
    }

    public decimal CadFillOpacity
    {
        get => _cadFillOpacity;
        set => SetValue(ref _cadFillOpacity, value, nameof(CadFillOpacity), changed: _ => Invalidate());
    }

    public decimal RegularLineWidth
    {
        get => _regularLineWidth;
        set => SetValue(ref _regularLineWidth, value, nameof(RegularLineWidth), RestoreRegularLineWidth, "Regular line width");
    }

    public decimal HighlightStrokeWidthAdjust
    {
        get => _highlightStrokeWidthAdjust;
        set => SetValue(ref _highlightStrokeWidthAdjust, value, nameof(HighlightStrokeWidthAdjust),
            RestoreHighlightStrokeWidthAdjust, "Highlight line width", ClampHighlightStrokeWidthAdjust);
    }

    public decimal RegularLineOpacity
    {
        get => _regularLineOpacity;
        set => SetValue(ref _regularLineOpacity, value, nameof(RegularLineOpacity), changed: _ => Invalidate());
    }

    public decimal RegularFillOpacity
    {
        get => _regularFillOpacity;
        set => SetValue(ref _regularFillOpacity, value, nameof(RegularFillOpacity), changed: _ => Invalidate());
    }

    public decimal RegularSelectedFillOpacity
    {
        get => _regularSelectedFillOpacity;
        set => SetValue(ref _regularSelectedFillOpacity, value, nameof(RegularSelectedFillOpacity),
            RestoreRegularSelectedFillOpacity, "Regular selection fill opacity", ClampRegularSelectedFillOpacity);
    }

    private void RestoreCadLineWidth(decimal value) => CadLineWidth = value;

    private void RestoreRegularLineWidth(decimal value) => RegularLineWidth = value;

    private void RestoreHighlightStrokeWidthAdjust(decimal value) => HighlightStrokeWidthAdjust = value;

    private void RestoreRegularSelectedFillOpacity(decimal value) => RegularSelectedFillOpacity = value;

    private void SetValue(
        ref decimal field,
        decimal value,
        string propertyName,
        Action<decimal>? assign = null,
        string? undoDescription = null,
        Action<decimal>? changed = null)
    {
        if (EqualityComparer<decimal>.Default.Equals(field, value))
        {
            return;
        }

        if (assign is not null && undoDescription is not null)
        {
            TrackUndo(field, value, assign, undoDescription);
        }

        _propertyChanging(propertyName);
        field = value;
        changed?.Invoke(value);
        _propertyChanged(propertyName);
    }

    private void TrackUndo(decimal oldValue, decimal newValue, Action<decimal> assign, string description)
    {
        if (_isUndoSuppressed() || _isLoadingSettings())
        {
            return;
        }

        if (EqualityComparer<decimal>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        _registerUndo(() => assign(oldValue), description);
    }

    private void Invalidate()
    {
        if (_isLoadingSettings()) return;
        _invalidate();
    }

    private void ClampRegularSelectedFillOpacity(decimal value)
    {
        if (_isLoadingSettings()) return;
        var clamped = Math.Clamp(value, 0.0m, 1.0m);
        if (clamped != value)
        {
            using (_enterUndoSuppression())
            {
                RegularSelectedFillOpacity = clamped;
            }
        }
    }

    private void ClampHighlightStrokeWidthAdjust(decimal value)
    {
        if (_isLoadingSettings()) return;
        var clamped = Math.Clamp(value, -2.0m, 4.0m);
        if (clamped != value)
        {
            using (_enterUndoSuppression())
            {
                HighlightStrokeWidthAdjust = clamped;
            }
        }
    }
}
