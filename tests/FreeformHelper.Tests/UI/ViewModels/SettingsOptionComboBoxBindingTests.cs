// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what a real drop-down does with the four setting option properties. A ComboBox is bound
/// two-way to each option property the way the settings views bind it. A change of the value moves
/// the selected item, and a selection by the user moves the value. The option never stays behind
/// the value in this flow. This is the evidence that the state where option and value disagree can
/// only come from code that writes the option from inside the option change notification, which
/// no code of the application does.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class SettingsOptionComboBoxBindingTests
{
    [AvaloniaFact]
    public void ScanOrder_ComboBoxFollowsTheValueAndMovesIt()
    {
        var vm = new FreeformHelperViewModel();

        RoundTrip(
            vm,
            vm.ScanOrderOptions,
            nameof(FreeformHelperViewModel.SelectedScanOrderOption),
            option => option.Value,
            () => vm.SelectedScanOrder,
            value => vm.SelectedScanOrder = value,
            () => vm.SelectedScanOrderOption);
    }

    [AvaloniaFact]
    public void GridAlignment_ComboBoxFollowsTheValueAndMovesIt()
    {
        var vm = new FreeformHelperViewModel();

        RoundTrip(
            vm,
            vm.GridAlignmentOptions,
            nameof(FreeformHelperViewModel.SelectedGridAlignmentOption),
            option => option.Value,
            () => vm.GridAlignmentMode,
            value => vm.GridAlignmentMode = value,
            () => vm.SelectedGridAlignmentOption);
    }

    [AvaloniaFact]
    public void RegularSourceMode_ComboBoxFollowsTheValueAndMovesIt()
    {
        var vm = new FreeformHelperViewModel();

        RoundTrip(
            vm,
            vm.RegularSourceModeOptions,
            nameof(FreeformHelperViewModel.SelectedRegularSourceModeOption),
            option => option.Value,
            () => vm.RegularSourceMode,
            value => vm.RegularSourceMode = value,
            () => vm.SelectedRegularSourceModeOption);
    }

    [AvaloniaFact]
    public void CadOutputFwDiffAutoMode_ComboBoxFollowsTheValueAndMovesIt()
    {
        var vm = new FreeformHelperViewModel();

        RoundTrip(
            vm,
            vm.CadOutputFwDiffAutoModeOptions,
            nameof(FreeformHelperViewModel.SelectedCadOutputFwDiffAutoModeOption),
            option => option.Value,
            () => vm.CadOutputFwDiffAutoMode,
            value => vm.CadOutputFwDiffAutoMode = value,
            () => vm.SelectedCadOutputFwDiffAutoModeOption);
    }

    private static void RoundTrip<TOption, TValue>(
        FreeformHelperViewModel vm,
        IReadOnlyList<TOption> options,
        string optionProperty,
        Func<TOption, TValue> valueOf,
        Func<TValue> readValue,
        Action<TValue> writeValue,
        Func<TOption> readOption)
        where TOption : struct
        where TValue : struct, Enum
    {
        Assert.True(options.Count >= 2);
        var combo = new ComboBox { ItemsSource = options, DataContext = vm };
        combo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(optionProperty) { Mode = BindingMode.TwoWay });

        // The value moves first: the drop-down shows the matching option.
        writeValue(valueOf(options[0]));
        Assert.Equal(options[0], Assert.IsType<TOption>(combo.SelectedItem));
        Assert.Equal(options[0], readOption());

        writeValue(valueOf(options[1]));
        Assert.Equal(options[1], Assert.IsType<TOption>(combo.SelectedItem));
        Assert.Equal(options[1], readOption());

        // The user moves first: the value follows the selected item and the option agrees.
        combo.SelectedItem = options[0];
        Assert.Equal(valueOf(options[0]), readValue());
        Assert.Equal(options[0], readOption());
        Assert.Equal(options[0], Assert.IsType<TOption>(combo.SelectedItem));
    }
}
