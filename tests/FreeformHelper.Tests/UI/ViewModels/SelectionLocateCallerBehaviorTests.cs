// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SelectionLocateCallerBehaviorTests
{
    [AvaloniaFact]
    public async Task QuickFocusWithCadAndIcSelectsOnlyTheCadInThatIcAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        var path = CreateTempDxfFile(("left", 0, 0, 20, 10), ("right", 20, 0, 40, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        vm.ActiveAreaWidth = 40;
        vm.ActiveAreaHeight = 10;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.GridPaddingPercent = 0;
        vm.CascadeNum = 2;
        vm.CascadeIcSettings[0].XChannels = 1;
        vm.CascadeIcSettings[0].YChannels = 1;
        vm.CascadeIcSettings[1].XChannels = 1;
        vm.CascadeIcSettings[1].YChannels = 1;
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        var left = Assert.Single(vm.CadPads, static pad => pad.Layer == "left");
        var right = Assert.Single(vm.CadPads, static pad => pad.Layer == "right");
        Assert.True(vm.TryGetCadIcIndex(left.Id, out var leftIc));
        Assert.True(vm.TryGetCadIcIndex(right.Id, out var rightIc));
        Assert.Equal(0, leftIc);
        Assert.Equal(1, rightIc);
        vm.QuickFocusQueryText = $"ic2 cad {right.Id}";

        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(right.Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Equal($"Quick focus: IC 2 CAD {right.Id} focused (CAD output=1).", vm.StatusText);

        vm.QuickFocusQueryText = $"ic2 cad {left.Id}";
        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(right.Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal($"Quick focus: IC 2 CAD {left.Id} is not visible.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task HighlightGroupsUseDefaultOrCallerLabelsAndEmptyCadTargetsPreserveSelectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(
            TestFiles.NewTempFilePath("cov44-selection-app-", ".json")));
        await WaitForGridRebuildAsync(vm);
        vm.CadPads = new ObservableCollection<CadPad>
        {
            TestGeometryFactory.CreateCadPad(10, "signal", 0, 0, 10, 10),
            TestGeometryFactory.CreateCadPad(20, "signal", 10, 0, 20, 10),
        };
        vm.RegularPads = new ObservableCollection<RegularPad>
        {
            TestGeometryFactory.CreateRegularPad(0, 0, 100, 0, 0, 10, 10),
            TestGeometryFactory.CreateRegularPad(0, 1, 200, 10, 0, 20, 10),
        };
        vm.ApplyCanvasSelection([10], [100]);

        vm.HighlightCadPadGroup([20], "   ");

        Assert.Equal(20, Assert.Single(vm.SelectedCadPadIds));
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Equal("Owner CAD highlighted: 1 pad(s).", vm.StatusText);

        vm.HighlightCadPadGroup([]);

        Assert.Equal(20, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal("Owner CAD highlight skipped: no target ids.", vm.StatusText);

        vm.HighlightRegularPadGroup([200], "Mask audit");

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(200, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Mask audit: 1 pad(s).", vm.StatusText);
    }
}
