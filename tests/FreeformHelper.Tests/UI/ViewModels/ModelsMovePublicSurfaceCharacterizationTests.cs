// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ModelsMovePublicSurfaceCharacterizationTests
{
    // SettingsDraftTestScope uses TestAppSettingsIsolation's unique settings path and stops persistence on disposal.
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void GridOptionsKeepTheirValuesLabelsAndOrder()
    {
        using var scope = new SettingsDraftTestScope();
        var vm = scope.Owner;

        Assert.Equal(
            new[]
            {
                ("LeftToRight_TopToBottom", "L->R, T->B"),
                ("RightToLeft_TopToBottom", "R->L, T->B"),
                ("LeftToRight_BottomToTop", "L->R, B->T"),
                ("RightToLeft_BottomToTop", "R->L, B->T"),
            },
            vm.ScanOrderOptions.Select(static option => (option.Value.ToString(), option.Display)));
        Assert.Equal(
            new[] { ("FromPanelAa", "Use panel AA"), ("FromCadBounds", "Use DXF bounds") },
            vm.GridAlignmentOptions.Select(static option => (option.Value.ToString(), option.Display)));
        Assert.Equal(
            new[] { ("GeneratedGrid", "Generated grid"), ("FromDxfLayer", "DXF layer pads") },
            vm.RegularSourceModeOptions.Select(static option => (option.Value.ToString(), option.Display)));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void LoggingAndCadOptionsKeepTheirValuesLabelsAndOrder()
    {
        using var scope = new SettingsDraftTestScope();
        var vm = scope.Owner;

        Assert.Equal(
            new[] { ("Trace", "Trace"), ("Debug", "Debug"), ("Info", "Info"), ("Warn", "Warn"), ("Error", "Error"), ("Fatal", "Fatal") },
            vm.LogLevelOptions.Select(static option => (option.Value, option.Display)));
        Assert.Equal(
            new[] { ("BestMatchDirect", "Geometry seed"), ("StrictUnique", "Geometry seed (unique per IC)") },
            vm.CadOutputFwDiffAutoModeOptions.Select(static option => (option.Value.ToString(), option.Display)));
        Assert.Equal(
            new[] { ("SelectedPads", "Selected pads"), ("TargetLayer", "Target layer") },
            vm.DxfEditRotationScopeOptions.Select(static option => (option.Value.ToString(), option.Display)));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void NotchOptionsKeepTheirValuesLabelsAndOrder()
    {
        using var scope = new SettingsDraftTestScope();
        var vm = scope.Owner;

        Assert.Equal(
            new[] { ("Csv", "CSV review (.csv)"), ("Cv21", "C v2.1 (.c)"), ("Cv22", "C v2.2 (.c)") },
            vm.NotchExportFileTypeOptions.Select(static option => (option.Value.ToString(), option.Display)));
        Assert.Equal(
            new[] { ("Release", "Release (minimal FW)"), ("Debug", "Debug (trace + sim mask)") },
            vm.NotchExportProfileOptions.Select(static option => (option.Value.ToString(), option.Display)));
        Assert.Equal(
            new[] { ("CurrentGain", "Current (Gain)"), ("ConservativeNoGain", "Conservative (No Gain)"), ("Disabled", "Disabled") },
            vm.NotchCompensationModelOptions.Select(static option => (option.Value.ToString(), option.Display)));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void LayerOptionsAndNewToggleKeepTheirDisplayDefaults()
    {
        using var scope = new SettingsDraftTestScope();
        var vm = scope.Owner;

        vm.LayerToggles.Add(new("signal"));

        var toggle = Assert.Single(vm.LayerToggles);
        Assert.Equal("signal", toggle.Name);
        Assert.True(toggle.IsSelected);
        var bounds = Assert.Single(vm.BoundLayerOptions);
        Assert.Null(bounds.Name);
        Assert.Equal("Auto (visible layers)", bounds.Display);
        var regular = Assert.Single(vm.RegularSourceLayerOptions);
        Assert.Null(regular.Name);
        Assert.Equal("Select layer", regular.Display);
    }
}
