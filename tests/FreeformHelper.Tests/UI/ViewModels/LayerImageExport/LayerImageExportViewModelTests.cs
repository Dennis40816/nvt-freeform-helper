// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageExportViewModelTests
{
    [Fact]
    public async Task EveryRenderedPictureNotifiesPreviewPngAsync()
    {
        var clock = LayerImageExportTestData.CreateClock();
        byte[] first = [1];
        byte[] second = [2];
        byte[] current = first;
        var context = LayerImageExportTestData.CreateContext() with
        {
            RenderPngAsync = (_, _, _) => Task.FromResult<byte[]?>(current),
        };
        using var vm = new LayerImageExportViewModel(context, clock);
        var pictures = new List<byte[]?>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LayerImageExportViewModel.PreviewPng))
            {
                pictures.Add(vm.PreviewPng);
            }
        };
        Assert.Null(vm.PreviewPng);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        current = second;
        vm.PreviewSettings.PaddingX = 1;
        clock.Advance(TimeSpan.FromMilliseconds(150));
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Collection(pictures, picture => Assert.Same(first, picture), picture => Assert.Same(second, picture));
        Assert.Same(second, vm.PreviewPng);
    }

    [Fact]
    public void SearchUsesCaseInsensitiveAndTermsWithSharedHighlights()
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());

        vm.SearchText = "NC DRAW";

        var row = Assert.Single(vm.VisibleRows);
        Assert.Equal("nc.drawing", row.Name);
        Assert.Equal(LayerListSearch.FindRanges(row.Name, LayerListSearch.ParseTerms(vm.SearchText)), row.HighlightRanges);
    }

    [Fact]
    public void ClearSearchRestoresTheSameRowsAndClearsHighlights()
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        var rows = vm.VisibleRows.ToArray();
        vm.SearchText = "pads";

        vm.ClearSearchCommand.Execute(null);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(rows, vm.VisibleRows);
        Assert.Empty(vm.VisibleRows[1].HighlightRanges);
    }

    [Fact]
    public void ToggleAllTouchesOnlyVisibleRowsAndCountStillUsesAllRows()
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        vm.SearchText = "nc";
        Assert.Equal(AllLayersState.Checked, vm.AllLayersState);

        vm.ToggleAllLayersCommand.Execute(null);

        Assert.Equal(AllLayersState.Unchecked, vm.AllLayersState);
        Assert.Equal("1 / 3", vm.CountText);
        vm.VisibleRows[0].IsChecked = true;
        Assert.Equal(AllLayersState.Indeterminate, vm.AllLayersState);
        Assert.Equal("2 / 3", vm.CountText);
        vm.ToggleAllLayersCommand.Execute(null);
        Assert.Equal(AllLayersState.Checked, vm.AllLayersState);
        Assert.Equal("3 / 3", vm.CountText);
    }

    [Fact]
    public void ProjectPixelsAndUnpaddedFooterMatchTheDesign()
    {
        using var vm = new LayerImageExportViewModel(
            LayerImageExportTestData.CreateContext(6480, 848), LayerImageExportTestData.CreateClock());

        Assert.Equal(LayerImageSizeSource.Project, vm.PreviewSettings.CurrentSize.Source);
        Assert.Equal(6480m, vm.PreviewSettings.PixelsX);
        Assert.Equal(848m, vm.PreviewSettings.PixelsY);
        Assert.Equal(0m, vm.PreviewSettings.PaddingX);
        Assert.Equal(0m, vm.PreviewSettings.PaddingY);
        Assert.Equal("6,480 × 848 px", vm.PreviewSettings.OutputText);
        Assert.Equal("3 layers selected · 6,480 × 848 px", vm.FooterText);
    }

    [Fact]
    public void AutoDefaultsFollowCheckedBoundsUntilPixelEditing()
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        Assert.Equal(LayerImageSizeSource.Auto, vm.PreviewSettings.CurrentSize.Source);
        Assert.Equal(4096m, vm.PreviewSettings.PixelsX);

        vm.VisibleRows[1].IsChecked = false;
        Assert.Equal(1365m, vm.PreviewSettings.PixelsY);
        vm.PreviewSettings.PixelsX = 800m;
        var customSize = vm.PreviewSettings.CurrentSize;
        vm.VisibleRows[1].IsChecked = true;

        Assert.Equal(customSize, vm.PreviewSettings.CurrentSize);
        Assert.Equal(LayerImageSizeSource.Custom, vm.PreviewSettings.CurrentSize.Source);
    }

    [Fact]
    public void BuildRequestSnapshotsSelectionInEntryOrderAndAllSettings()
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        vm.ToggleAllLayersCommand.Execute(null);
        Assert.False(vm.ConfirmCommand.CanExecute(null));
        vm.VisibleRows[2].IsChecked = true;
        vm.VisibleRows[0].IsChecked = true;
        vm.PreviewSettings.PixelsX = 64;
        vm.PreviewSettings.PaddingY = 3;
        vm.PreviewSettings.LineWidthPixels = 2.5m;
        vm.PreviewSettings.UseDarkTheme = true;
        var files = new LayerImageFileSettings { Format = DxfLayerImageFormat.Bmp, OneFilePerLayer = true };

        var request = vm.BuildRequest(files);
        vm.ToggleAllLayersCommand.Execute(null);

        Assert.Equal(new List<string> { "nc.drawing", "other.drawing" }, request.Layers);
        Assert.Equal(vm.PreviewSettings.CurrentSize, request.Size);
        Assert.Equal(2.5m, request.LineWidthPixels);
        Assert.True(request.UseDarkTheme);
        Assert.Equal(DxfLayerImageFormat.Bmp, request.Format);
        Assert.True(request.OneFilePerLayer);
        Assert.True(vm.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public void CommandsRequestSettingsAndCancelWithoutChangingSelection()
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        int settingsRequests = 0;
        bool? closed = null;
        vm.RequestExportSettings += () => settingsRequests++;
        vm.RequestClose += confirmed => closed = confirmed;

        vm.ConfirmCommand.Execute(null);
        vm.CancelCommand.Execute(null);

        Assert.Equal(1, settingsRequests);
        Assert.False(closed);
        Assert.Equal("3 / 3", vm.CountText);
    }
}
