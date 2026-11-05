// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Windows.Input;
using Avalonia.Data.Converters;

namespace FreeformHelper.UI.Converters;

/// <summary>Projects the export predicate independently of the owner's modal enabled state.</summary>
public sealed class LayerImageSelectionVisibilityConverter : IMultiValueConverter
{
    public bool Negate { get; set; }

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // CountText is only a change notification input; the command owns the selection predicate.
        bool canExport = values.Count > 0 && values[0] is ICommand command && command.CanExecute(null);
        return Negate ? !canExport : canExport;
    }
}
