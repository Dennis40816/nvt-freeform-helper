using Avalonia;

namespace FreeformHelper.UI.Controls;

public sealed partial class NumberScrubber
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<NumberScrubber, decimal>(
            nameof(Value),
            0m,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<decimal> MinimumProperty =
        AvaloniaProperty.Register<NumberScrubber, decimal>(nameof(Minimum), decimal.MinValue);

    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<NumberScrubber, decimal>(nameof(Maximum), decimal.MaxValue);

    public static readonly StyledProperty<decimal> SmallChangeProperty =
        AvaloniaProperty.Register<NumberScrubber, decimal>(nameof(SmallChange), 1m);

    public static readonly StyledProperty<decimal> LargeChangeProperty =
        AvaloniaProperty.Register<NumberScrubber, decimal>(nameof(LargeChange), 10m);

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<NumberScrubber, string>(nameof(FormatString), "0.###");

    public static readonly StyledProperty<bool> RequireAltForWheelProperty =
        AvaloniaProperty.Register<NumberScrubber, bool>(nameof(RequireAltForWheel), true);

    public static readonly StyledProperty<bool> SnapToStepProperty =
        AvaloniaProperty.Register<NumberScrubber, bool>(nameof(SnapToStep), true);

    public static readonly StyledProperty<double> ScrubPixelsPerStepProperty =
        AvaloniaProperty.Register<NumberScrubber, double>(nameof(ScrubPixelsPerStep), 6.0);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<NumberScrubber, bool>(nameof(IsReadOnly), false);

    public static readonly StyledProperty<bool> IsMixedProperty =
        AvaloniaProperty.Register<NumberScrubber, bool>(nameof(IsMixed), false);

    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public decimal Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public decimal Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public decimal SmallChange
    {
        get => GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public decimal LargeChange
    {
        get => GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    public bool RequireAltForWheel
    {
        get => GetValue(RequireAltForWheelProperty);
        set => SetValue(RequireAltForWheelProperty, value);
    }

    public bool SnapToStep
    {
        get => GetValue(SnapToStepProperty);
        set => SetValue(SnapToStepProperty, value);
    }

    public double ScrubPixelsPerStep
    {
        get => GetValue(ScrubPixelsPerStepProperty);
        set => SetValue(ScrubPixelsPerStepProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool IsMixed
    {
        get => GetValue(IsMixedProperty);
        set => SetValue(IsMixedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            var newValue = (decimal)change.NewValue!;
            var clamped = Clamp(newValue);
            if (clamped != newValue)
            {
                SetCurrentValue(ValueProperty, clamped);
                return;
            }

            UpdateTextFromValue(force: !_isEditing);
            return;
        }

        if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            SetCurrentValue(ValueProperty, Clamp(Value));
            return;
        }

        if (change.Property == FormatStringProperty)
        {
            UpdateTextFromValue(force: true);
            return;
        }

        if (change.Property == IsReadOnlyProperty)
        {
            UpdateReadOnlyState();
        }

        if (change.Property == IsMixedProperty)
        {
            UpdateTextFromValue(force: true);
        }
    }
}
