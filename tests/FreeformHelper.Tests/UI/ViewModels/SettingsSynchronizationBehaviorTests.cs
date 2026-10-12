// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.ComponentModel;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsSynchronizationBehaviorTests
{
    [Theory]
    [InlineData("ScanOrder")]
    [InlineData("GridAlignment")]
    [InlineData("RegularSourceMode")]
    [InlineData("CadOutputFwDiffAutoMode")]
    public void ValueSetter_ChangedValue_SynchronizesOptionBeforeValueNotification(string domain)
    {
        var vm = new FreeformHelperViewModel();
        var pair = SelectionPair.Create(vm, domain);
        pair.SetValue(0);
        var changes = Observe(vm, pair.ValueName, pair.OptionName);

        pair.SetValue(1);

        Assert.Equal(pair.Expected(1), pair.Value());
        Assert.Equal(pair.Expected(1), pair.OptionValue());
        Assert.Equal<string?>([pair.OptionName, pair.ValueName], changes);
    }

    [Theory]
    [InlineData("ScanOrder")]
    [InlineData("GridAlignment")]
    [InlineData("RegularSourceMode")]
    [InlineData("CadOutputFwDiffAutoMode")]
    public void OptionSetter_ChangedOption_SynchronizesValueAndRestoresReverseHandler(string domain)
    {
        var vm = new FreeformHelperViewModel();
        var pair = SelectionPair.Create(vm, domain);
        pair.SetValue(0);
        var changes = Observe(vm, pair.ValueName, pair.OptionName);

        pair.SetOption(1);

        Assert.Equal(pair.Expected(1), pair.Value());
        Assert.Equal(pair.Expected(1), pair.OptionValue());
        Assert.Equal<string?>([pair.ValueName, pair.OptionName], changes);
        pair.SetValue(0);
        Assert.Equal(pair.Expected(0), pair.OptionValue());
    }

    [Theory]
    [InlineData("ScanOrder")]
    [InlineData("GridAlignment")]
    [InlineData("RegularSourceMode")]
    [InlineData("CadOutputFwDiffAutoMode")]
    public void ValueSetter_ReentrantOptionChange_DoesNotRunGuardedReverseHandler(string domain)
    {
        var vm = new FreeformHelperViewModel();
        var pair = SelectionPair.Create(vm, domain);
        pair.SetValue(0);
        OnNextChange(vm, pair.OptionName, () => pair.SetOption(0));

        pair.SetValue(1);

        Assert.Equal(pair.Expected(1), pair.Value());
        Assert.Equal(pair.Expected(0), pair.OptionValue());
        pair.SetOption(1);
        pair.SetOption(0);
        Assert.Equal(pair.Expected(0), pair.Value());
    }

    [Theory]
    [InlineData("ScanOrder")]
    [InlineData("GridAlignment")]
    [InlineData("RegularSourceMode")]
    [InlineData("CadOutputFwDiffAutoMode")]
    public void ValueSetter_UnchangedValue_RaisesNoPairNotification(string domain)
    {
        var vm = new FreeformHelperViewModel();
        var pair = SelectionPair.Create(vm, domain);
        pair.SetValue(0);
        var changes = Observe(vm, pair.ValueName, pair.OptionName);

        pair.SetValue(0);

        Assert.Empty(changes);
        Assert.Equal(pair.Expected(0), pair.OptionValue());
    }

    [Fact]
    public void NotchThresholdQ7_LinkedThreshold_RoundsPercentWithoutFeedbackAndPreservesOrder()
    {
        var vm = new FreeformHelperViewModel { LinkNotchThresholds = true };
        var changes = Observe(vm, nameof(vm.NotchThresholdQ7), nameof(vm.NotchThresholdPercent));

        vm.NotchThresholdQ7 = 65;

        Assert.Equal(65m, vm.NotchThresholdQ7);
        Assert.Equal(50.78m, vm.NotchThresholdPercent);
        Assert.Equal<string?>([nameof(vm.NotchThresholdPercent), nameof(vm.NotchThresholdQ7)], changes);
        vm.NotchThresholdPercent = 25;
        Assert.Equal(32m, vm.NotchThresholdQ7);
    }

    [Fact]
    public void NotchThresholdPercent_UnlinkedThreshold_KeepsQ7Independent()
    {
        var vm = new FreeformHelperViewModel { LinkNotchThresholds = false, NotchThresholdQ7 = 64 };
        var changes = Observe(vm, nameof(vm.NotchThresholdQ7), nameof(vm.NotchThresholdPercent));

        vm.NotchThresholdPercent = 20;

        Assert.Equal(64m, vm.NotchThresholdQ7);
        Assert.Equal<string?>([nameof(vm.NotchThresholdPercent)], changes);
        vm.LinkNotchThresholds = true;
        Assert.Equal(50m, vm.NotchThresholdPercent);
    }

    [Fact]
    public void NotchThresholdQ7_ReentrantPercentChange_SuppressesFeedbackUntilExit()
    {
        var vm = new FreeformHelperViewModel { LinkNotchThresholds = true };
        OnNextChange(vm, nameof(vm.NotchThresholdPercent), () => vm.NotchThresholdPercent = 25);

        vm.NotchThresholdQ7 = 65;

        Assert.Equal(65m, vm.NotchThresholdQ7);
        Assert.Equal(25m, vm.NotchThresholdPercent);
        vm.NotchThresholdPercent = 50;
        Assert.Equal(64m, vm.NotchThresholdQ7);
    }

    [Fact]
    public void UseLocalSizing_ChangedMode_SynchronizesBothScopesInOrder()
    {
        var vm = new FreeformHelperViewModel { UseLocalSizing = true };
        var changes = Observe(vm, nameof(vm.UseLocalSizing), nameof(vm.IsWidthRowLocal), nameof(vm.IsHeightColumnLocal));

        vm.UseLocalSizing = false;

        Assert.False(vm.IsWidthRowLocal);
        Assert.False(vm.IsHeightColumnLocal);
        Assert.Equal<string?>([nameof(vm.IsWidthRowLocal), nameof(vm.IsHeightColumnLocal), nameof(vm.UseLocalSizing)], changes);
        vm.UseLocalSizing = true;
        Assert.True(vm.IsWidthRowLocal);
        Assert.True(vm.IsHeightColumnLocal);
    }

    [Fact]
    public void IsWidthRowLocal_IndependentScope_DoesNotOverwriteHeightScope()
    {
        var vm = new FreeformHelperViewModel { UseLocalSizing = true };

        vm.IsWidthRowLocal = false;

        Assert.False(vm.UseLocalSizing);
        Assert.True(vm.IsHeightColumnLocal);
        vm.IsWidthRowLocal = true;
        Assert.True(vm.UseLocalSizing);
    }

    [Fact]
    public void UseLocalSizing_ReentrantWidthChange_DoesNotRunGuardedScopeSynchronization()
    {
        var vm = new FreeformHelperViewModel { UseLocalSizing = true };
        OnNextChange(vm, nameof(vm.IsWidthRowLocal), () => vm.IsWidthRowLocal = true);

        vm.UseLocalSizing = false;

        Assert.True(vm.IsWidthRowLocal);
        Assert.False(vm.IsHeightColumnLocal);
        Assert.False(vm.UseLocalSizing);
        vm.IsHeightColumnLocal = true;
        Assert.True(vm.UseLocalSizing);
    }

    [Fact]
    public void CascadeIcSetting_ChannelChange_PreservesPerIcObjectsDuringGlobalSynchronization()
    {
        var vm = new FreeformHelperViewModel { CascadeNum = 2 };
        var first = vm.CascadeIcSettings[0];
        var second = vm.CascadeIcSettings[1];
        var changes = Observe(vm, nameof(vm.CascadeIcSettings), nameof(vm.XChannels));
        var originalTotal = vm.XChannels;

        first.XChannels += 1;

        Assert.Equal(originalTotal + 1, vm.XChannels);
        Assert.Same(first, vm.CascadeIcSettings[0]);
        Assert.Same(second, vm.CascadeIcSettings[1]);
        Assert.Equal<string?>([nameof(vm.XChannels)], changes);
        vm.XChannels += 2;
        Assert.NotSame(first, vm.CascadeIcSettings[0]);
    }

    [Fact]
    public void CadOutputFwDiffIndexAnchorCadId_InvisibleCad_RestoresSentinelWithoutClearStatus()
    {
        var vm = new FreeformHelperViewModel();
        var changes = Observe(vm, nameof(vm.CadOutputFwDiffIndexAnchorCadId), nameof(vm.StatusText));

        vm.CadOutputFwDiffIndexAnchorCadId = 123;

        Assert.Equal(-1m, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.Equal("CAD Output FW Diff anchor ignored: CAD 123 is not visible.", vm.StatusText);
        Assert.Equal<string?>([nameof(vm.CadOutputFwDiffIndexAnchorCadId), nameof(vm.StatusText),
            nameof(vm.CadOutputFwDiffIndexAnchorCadId)], changes);
        vm.CadOutputFwDiffIndexAnchorCadId = 124;
        Assert.Contains("CAD 124 is not visible", vm.StatusText, StringComparison.Ordinal);
    }

    internal static ConcurrentQueue<string?> Observe(FreeformHelperViewModel vm, params string[] names)
    {
        var changes = new ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, args) =>
        {
            if (names.Contains(args.PropertyName, StringComparer.Ordinal))
            {
                changes.Enqueue(args.PropertyName);
            }
        };
        return changes;
    }

    internal static void OnNextChange(INotifyPropertyChanged vm, string name, Action action)
    {
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (!string.Equals(args.PropertyName, name, StringComparison.Ordinal))
            {
                return;
            }
            vm.PropertyChanged -= handler;
            action();
        };
        vm.PropertyChanged += handler;
    }

    private sealed record SelectionPair(Action<int> SetValue, Action<int> SetOption, Func<object> Value,
        Func<object> OptionValue, Func<int, object> Expected, string ValueName, string OptionName)
    {
        public static SelectionPair Create(FreeformHelperViewModel vm, string domain) => domain switch
        {
            "ScanOrder" => new(i => vm.SelectedScanOrder = vm.ScanOrderOptions[i].Value,
                i => vm.SelectedScanOrderOption = vm.ScanOrderOptions[i], () => vm.SelectedScanOrder,
                () => vm.SelectedScanOrderOption.Value, i => vm.ScanOrderOptions[i].Value,
                nameof(vm.SelectedScanOrder), nameof(vm.SelectedScanOrderOption)),
            "GridAlignment" => new(i => vm.GridAlignmentMode = vm.GridAlignmentOptions[i].Value,
                i => vm.SelectedGridAlignmentOption = vm.GridAlignmentOptions[i], () => vm.GridAlignmentMode,
                () => vm.SelectedGridAlignmentOption.Value, i => vm.GridAlignmentOptions[i].Value,
                nameof(vm.GridAlignmentMode), nameof(vm.SelectedGridAlignmentOption)),
            "RegularSourceMode" => new(i => vm.RegularSourceMode = vm.RegularSourceModeOptions[i].Value,
                i => vm.SelectedRegularSourceModeOption = vm.RegularSourceModeOptions[i], () => vm.RegularSourceMode,
                () => vm.SelectedRegularSourceModeOption.Value, i => vm.RegularSourceModeOptions[i].Value,
                nameof(vm.RegularSourceMode), nameof(vm.SelectedRegularSourceModeOption)),
            "CadOutputFwDiffAutoMode" => new(i => vm.CadOutputFwDiffAutoMode = vm.CadOutputFwDiffAutoModeOptions[i].Value,
                i => vm.SelectedCadOutputFwDiffAutoModeOption = vm.CadOutputFwDiffAutoModeOptions[i], () => vm.CadOutputFwDiffAutoMode,
                () => vm.SelectedCadOutputFwDiffAutoModeOption.Value, i => vm.CadOutputFwDiffAutoModeOptions[i].Value,
                nameof(vm.CadOutputFwDiffAutoMode), nameof(vm.SelectedCadOutputFwDiffAutoModeOption)),
            _ => throw new ArgumentOutOfRangeException(nameof(domain)),
        };
    }
}
