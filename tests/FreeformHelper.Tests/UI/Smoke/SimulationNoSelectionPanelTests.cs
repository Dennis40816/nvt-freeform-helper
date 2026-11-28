// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.WorkspaceSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationNoSelectionPanelTests
{
    private static readonly int[] _diffIndices = [10, 11];

    [AvaloniaFact]
    public async Task ValueFieldsAppearOnlyWhenARegularPadIsSelectedAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_diffIndices);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(icIndex: 0, diffIndex: 10, regularPadIndex: 0, cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), comment: "sample"),
        });
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(grid, table, 65535, SourceRevision: 0,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
                CadPadsForCopperProjection: [],
                CadOutputFwDiffIndexByCadId: new Dictionary<int, int>(),
                CadIcIndexByCadId: new Dictionary<int, int>()));
        var view = new SimulationWorkspaceDetailsPaneView { DataContext = viewModel };
        var window = new Window { Content = view, Width = 400, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var empty = Find<TextBlock>(view, "SimulationNoSelectionText");
            var values = Find<Grid>(view, "SimulationSelectedValues");
            Assert.False(viewModel.HasSelectedRegular);
            Assert.True(empty.IsEffectivelyVisible);
            Assert.False(values.IsEffectivelyVisible);

            viewModel.SelectRegularPad(0);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(empty.IsEffectivelyVisible);
            Assert.True(values.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static T Find<T>(Control root, string name)
        where T : Control =>
        Assert.Single(root.GetVisualDescendants().OfType<T>(), item => item.Name == name);
}
