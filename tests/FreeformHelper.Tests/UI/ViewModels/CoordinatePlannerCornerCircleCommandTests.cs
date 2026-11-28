// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerCornerCircleCommandTests
{
    [Fact]
    public async Task ExportCommand_SendsAnImageWithTheActiveAreaPixelSizeAsync()
    {
        var viewModel = CreateViewModel(1000, 500);
        viewModel.CornerCircles.Radius = 40m;
        CornerCircleImage? received = null;
        viewModel.CornerCircles.RequestExportAsync = image =>
        {
            received = image;
            return Task.FromResult(true);
        };

        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);

        Assert.NotNull(received);
        Assert.Equal(1000, received.Width);
        Assert.Equal(500, received.Height);
        Assert.Equal("Corner circle image exported (1000 x 500 px).", viewModel.StatusText);
    }

    [Fact]
    public async Task ExportCommand_FollowsAChangeOfThePixelSizeAsync()
    {
        var viewModel = CreateViewModel(1000, 500);
        viewModel.PixelWidth = 640m;
        viewModel.PixelHeight = 480m;
        CornerCircleImage? received = null;
        viewModel.CornerCircles.RequestExportAsync = image =>
        {
            received = image;
            return Task.FromResult(true);
        };

        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);

        Assert.Equal(640, received!.Width);
        Assert.Equal(480, received.Height);
    }

    [Fact]
    public async Task ExportCommand_WhenTheUserCancels_ReportsItAsync()
    {
        var viewModel = CreateViewModel(1000, 500);
        viewModel.CornerCircles.RequestExportAsync = _ => Task.FromResult(false);

        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);

        Assert.Equal("Corner circle image export cancelled.", viewModel.StatusText);
    }

    [Fact]
    public async Task ExportCommand_WithoutAHost_ReportsThatExportIsUnavailableAsync()
    {
        var viewModel = CreateViewModel(1000, 500);

        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);

        Assert.Equal("Corner circle image export is unavailable in the current host.", viewModel.StatusText);
    }

    [Fact]
    public async Task ExportCommand_WhenNoCirclesFit_ShowsTheReasonAndDoesNotAskTheHostAsync()
    {
        var viewModel = CreateViewModel(1000, 500);
        viewModel.CornerCircles.Radius = 400m;
        var requests = 0;
        viewModel.CornerCircles.RequestExportAsync = _ =>
        {
            requests++;
            return Task.FromResult(true);
        };

        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);

        Assert.Equal(0, requests);
        Assert.Equal(viewModel.CornerCircles.StatusText, viewModel.StatusText);
    }

    [Fact]
    public async Task ExportCommand_WhenTheHostThrows_ReportsTheFailureAsync()
    {
        var viewModel = CreateViewModel(1000, 500);
        viewModel.CornerCircles.RequestExportAsync = _ => throw new IOException("The disk is full. (export target 'C:/out/a.png')");

        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);

        Assert.Equal(
            "Corner circle image export failed: The disk is full. (export target 'C:/out/a.png')",
            viewModel.StatusText);
    }

    [Fact]
    public async Task ExportCommand_AfterAFailure_CanExportAgainAsync()
    {
        var viewModel = CreateViewModel(1000, 500);
        var calls = 0;
        viewModel.CornerCircles.RequestExportAsync = _ =>
        {
            calls++;
            return calls == 1 ? throw new UnauthorizedAccessException("denied") : Task.FromResult(true);
        };

        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);
        await viewModel.ExportCornerCircleImageCommand.ExecuteAsync(null);

        Assert.Equal("Corner circle image exported (1000 x 500 px).", viewModel.StatusText);
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel(int pixelWidth, int pixelHeight)
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: pixelWidth,
                DefaultPixelHeight: pixelHeight));
    }
}
