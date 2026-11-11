// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.UI.CadExport;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageExportSettingsViewModelTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void FileModeSelectsOneJobOrOneJobPerLayer(bool oneFilePerLayer, int expectedJobs)
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        vm.FileSettings.OneFilePerLayer = oneFilePerLayer;
        var request = vm.BuildRequest(vm.FileSettings);

        var jobs = LayerImageOutputPlanner.Plan(request.Layers,
            request.OneFilePerLayer ? LayerImageOutputMode.OnePerLayer : LayerImageOutputMode.OneImage, "dxf_layers");

        Assert.Equal(expectedJobs, jobs.Count);
        Assert.Equal(DxfLayerImageFormat.Png, vm.FileSettings.Format);
    }

    [Fact]
    public async Task CancelledPickerKeepsSettingsOpenAndRetrySavesAsync()
    {
        using var vm = new LayerImageExportViewModel(
            LayerImageExportTestData.CreateContext(6480, 848), LayerImageExportTestData.CreateClock());
        var choices = new Queue<string?>(new List<string?> { null, "layers.bmp" });
        var suggestions = new List<(string Name, string Extension)>();
        var settings = new LayerImageExportSettingsViewModel(vm, (name, extension) =>
        {
            suggestions.Add((name, extension));
            return Task.FromResult(choices.Dequeue());
        });
        settings.FileSettings.Format = DxfLayerImageFormat.Bmp;
        bool? closed = null;
        settings.RequestClose += saved => closed = saved;

        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Null(closed);
        Assert.Null(settings.Request);
        await settings.SaveCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.Equal("layers.bmp", settings.Request?.SavePath);
        Assert.Equal(("dxf_layers", "bmp"), suggestions[0]);
        Assert.Equal("6,480 × 848 px", settings.FooterText);
    }

    [Fact]
    public async Task SingleLayerSaveUsesLayerNameAndBackReturnsWithoutRequestAsync()
    {
        using var vm = new LayerImageExportViewModel(LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        vm.ToggleAllLayersCommand.Execute(null);
        vm.VisibleRows[1].IsChecked = true;
        string? suggestion = null;
        var settings = new LayerImageExportSettingsViewModel(vm, (name, _) =>
        {
            suggestion = name;
            return Task.FromResult<string?>(null);
        });
        bool? closed = null;
        settings.RequestClose += saved => closed = saved;

        await settings.SaveCommand.ExecuteAsync(null);
        settings.BackCommand.Execute(null);

        Assert.Equal("dxf_nc.pads", suggestion);
        Assert.False(closed);
        Assert.Null(settings.Request);
    }
}
