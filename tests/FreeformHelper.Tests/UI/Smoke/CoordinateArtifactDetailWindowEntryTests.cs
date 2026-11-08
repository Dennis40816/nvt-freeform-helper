// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinateArtifactDetailWindowEntryTests
{
    [AvaloniaFact]
    public async Task DetailCommandShowsRemainingArtifactFieldsAndCloseButtonReturnsToPlannerWithSelectionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 30d, cellHeight: 20d),
                [TestGeometryFactory.CreateCadPad(1, "AA.drawing", 100d, 200d, 400d, 600d)],
                ["AA.drawing"], SourceRevision: 0,
                DefaultMachineWidth: 150d, DefaultMachineHeight: 200d,
                DefaultPixelWidth: 1500, DefaultPixelHeight: 2000))
        {
            CopperPillarDiameter = 10m,
        };
        var owner = new Window { Content = new CoordinatePlannerWorkspaceView { DataContext = viewModel } };
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var row = Assert.Single(viewModel.ArtifactRows, artifact => artifact.Key == "aa-br");
            var detail = viewModel.ShowArtifactDetailCommand.ExecuteAsync(row);
            await HeadlessUiPump.FlushQueueAsync();
            var window = Assert.Single(owner.OwnedWindows.OfType<CoordinateArtifactDetailWindow>());
            Assert.True(window.IsVisible);
            window.FindControl<CollapsiblePanel>("SourceDetailSection")!.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            var text = window.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Where(block => block.IsVisible).Select(block => block.Text).ToArray();
            Assert.Contains(row.PixelText, text);
            Assert.Contains(row.SafeMachineText, text);
            Assert.Contains(row.WorldText, text);
            Assert.Contains(row.SourceBoundsText, text);
            Assert.Contains(row.DetailText, text);
            Assert.False(detail.IsCompleted);

            UiEventProbe.Click(Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content is "Close"));
            await detail.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(window.IsVisible);
            Assert.Empty(owner.OwnedWindows);
            Assert.True(owner.IsEnabled);
            Assert.Same(row, viewModel.SelectedArtifactRow);
        }
        finally
        {
            foreach (var child in owner.OwnedWindows.ToArray())
            {
                child.Close();
            }

            owner.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }
}
