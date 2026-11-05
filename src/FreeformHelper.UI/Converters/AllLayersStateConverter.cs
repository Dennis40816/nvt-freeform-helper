// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Converters;

public sealed class AllLayersStateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AllLayersState.Checked => true,
        AllLayersState.Unchecked => false,
        _ => null,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
