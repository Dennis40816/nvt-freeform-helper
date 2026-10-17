// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins that a change of any of the four setting options marks the project as changed, whether the
/// change comes through the enum property or through the dropdown option property. The regular
/// source mode pair was missing from the list of settings that mark the project as changed, so a
/// change of the source mode left the project looking saved (issue #64, Q20).
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class SettingsOptionUnsavedFlagTests
{
    public const string ScanOrder = "ScanOrder";
    public const string GridAlignment = "GridAlignment";
    public const string RegularSourceMode = "RegularSourceMode";
    public const string CadOutputFwDiffAutoMode = "CadOutputFwDiffAutoMode";

    [Theory]
    [InlineData(ScanOrder)]
    [InlineData(GridAlignment)]
    [InlineData(RegularSourceMode)]
    [InlineData(CadOutputFwDiffAutoMode)]
    public void ChangeEnum_MarksTheProjectAsChanged(string pair)
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        ChangeThroughEnum(vm, pair);

        Assert.True(vm.HasUnsavedChanges);
    }

    [Theory]
    [InlineData(ScanOrder)]
    [InlineData(GridAlignment)]
    [InlineData(RegularSourceMode)]
    [InlineData(CadOutputFwDiffAutoMode)]
    public void ChangeOption_MarksTheProjectAsChanged(string pair)
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        ChangeThroughOption(vm, pair);

        Assert.True(vm.HasUnsavedChanges);
    }

    private static void ChangeThroughEnum(FreeformHelperViewModel vm, string pair)
    {
        switch (pair)
        {
            case ScanOrder:
                vm.SelectedScanOrder = vm.ScanOrderOptions.First(o => o.Value != vm.SelectedScanOrder).Value;
                break;
            case GridAlignment:
                vm.GridAlignmentMode = vm.GridAlignmentOptions.First(o => o.Value != vm.GridAlignmentMode).Value;
                break;
            case RegularSourceMode:
                vm.RegularSourceMode = vm.RegularSourceModeOptions.First(o => o.Value != vm.RegularSourceMode).Value;
                break;
            case CadOutputFwDiffAutoMode:
                vm.CadOutputFwDiffAutoMode = vm.CadOutputFwDiffAutoModeOptions.First(o => o.Value != vm.CadOutputFwDiffAutoMode).Value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pair), pair, null);
        }
    }

    private static void ChangeThroughOption(FreeformHelperViewModel vm, string pair)
    {
        switch (pair)
        {
            case ScanOrder:
                vm.SelectedScanOrderOption = vm.ScanOrderOptions.First(o => o.Value != vm.SelectedScanOrder);
                break;
            case GridAlignment:
                vm.SelectedGridAlignmentOption = vm.GridAlignmentOptions.First(o => o.Value != vm.GridAlignmentMode);
                break;
            case RegularSourceMode:
                vm.SelectedRegularSourceModeOption = vm.RegularSourceModeOptions.First(o => o.Value != vm.RegularSourceMode);
                break;
            case CadOutputFwDiffAutoMode:
                vm.SelectedCadOutputFwDiffAutoModeOption = vm.CadOutputFwDiffAutoModeOptions.First(o => o.Value != vm.CadOutputFwDiffAutoMode);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pair), pair, null);
        }
    }
}
