// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using SkiaSharp;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CadExportCharacterizationTests
{
    private static readonly string[] _alphaBeta = ["alpha", "beta"];
    private static readonly DxfLayerImageExportRequest _priorPreferences = new(
        "alpha", 80, 60, 1.5m, 4, 6, DxfLayerImageFormat.Jpg, false);
    private static readonly DxfLayerImageExportRequest _requestedPreferences = new(
        "beta", 32, 24, 3m, 2, 3, DxfLayerImageFormat.Png, true);

    [AvaloniaFact]
    public async Task AllExportExcludesAutoHiddenDuplicatesEvenWhenTheirLayerIsDeselectedAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await OpenDuplicatePadsAsync(fixture);
        var canonical = Assert.Single(vm.CadPads, pad => pad.Layer == "alpha");
        Assert.Equal(1, vm.DuplicateCadPadCount);
        vm.LayerToggles.Single(layer => layer.Name == "alpha").IsSelected = false;
        await WaitForGridRebuildAsync(vm);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);

        var exported = await ExportDxfAsync(vm, files.PathFor("all.dxf"), visibleOnly: false);

        Assert.Equal(2, exported.Pads.Count);
        var alpha = Assert.Single(exported.Pads, pad => pad.Layer == "alpha");
        Assert.Equal(canonical.Polygon.Vertices, alpha.Polygon.Vertices);
        Assert.Single(exported.Pads, pad => pad.Layer == "beta");
        Assert.Equal("DXF exported (all). Pads=2.", vm.StatusText);
        Assert.Equal(1, vm.DuplicateCadPadCount);
    }

    [AvaloniaFact]
    public async Task VisibleExportAppendsTheRegularSourceLayerWithoutItsAutoHiddenDuplicateAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await OpenDuplicatePadsAsync(fixture);
        vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(option => option.Name == "alpha");
        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        await WaitForGridRebuildAsync(vm);
        Assert.False(vm.LayerToggles.Single(layer => layer.Name == "alpha").IsSelected);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);

        var exported = await ExportDxfAsync(vm, files.PathFor("visible.dxf"), visibleOnly: true);

        Assert.Equal(2, exported.Pads.Count);
        Assert.Single(exported.Pads, pad => pad.Layer == "alpha");
        Assert.Single(exported.Pads, pad => pad.Layer == "beta");
        Assert.Equal("DXF exported (visible). Pads=2.", vm.StatusText);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
    }

    [AvaloniaFact]
    public async Task VisibleExportWithNoOutputPadsDoesNotAppendTheRegularSourceLayerAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(option => option.Name == "alpha");
        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        vm.DeselectAllLayers();
        await WaitForGridRebuildAsync(vm);
        var pickerCalls = 0;
        vm.PickSaveExportPathAsync = (_, _) =>
        {
            pickerCalls++;
            return Task.FromResult<string?>(null);
        };

        await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

        Assert.Empty(vm.CadPads);
        Assert.Equal(0, pickerCalls);
        Assert.Equal("Export DXF: no CAD output pads.", vm.StatusText);
    }

    [AvaloniaTheory]
    [InlineData(false, 0d)]
    [InlineData(true, 20d)]
    public async Task DeletedBoundsLayerFallbackDependsOnRecalculationWhileAllExportExcludesItAsync(
        bool recalculate, double expectedMinX)
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        ConfigureSingleCellBounds(vm);
        vm.RecalcBoundsOnLayerFilter = recalculate;
        vm.SelectedBoundLayerOption = vm.BoundLayerOptions.Single(option => option.Name == "alpha");
        await SelectCadPadsAsync(vm, [Assert.Single(vm.CadPads, pad => pad.Layer == "alpha").Id]);

        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var exported = await ExportDxfAsync(vm, files.PathFor("bounds.dxf"), visibleOnly: false);

        Assert.Equal(0, vm.DuplicateCadPadCount);
        Assert.Equal("alpha", vm.SelectedBoundLayerOption.Name);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal("beta", Assert.Single(exported.Pads).Layer);
        // KNOWN DEFECT: Without auto-hidden duplicates, disabling recalculation puts manually hidden pads back in fallback bounds.
        Assert.Equal(new Rect2(expectedMinX, 0, 30, 10), Assert.Single(vm.RegularPads).Bounds);
    }

    [AvaloniaFact]
    public async Task ExplicitBoundsLayerStillDefinesTheGridWhenVisibleExportHasNoPadsAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        ConfigureSingleCellBounds(vm);
        vm.RecalcBoundsOnLayerFilter = true;
        vm.SelectedBoundLayerOption = vm.BoundLayerOptions.Single(option => option.Name == "beta");
        vm.PickSaveExportPathAsync = static (_, _) => Task.FromResult<string?>(null);

        vm.DeselectAllLayers();
        await WaitForGridRebuildAsync(vm);
        await vm.ExportDxfVisibleCommand.ExecuteAsync(null);

        Assert.Empty(vm.CadPads);
        Assert.Equal(new Rect2(20, 0, 30, 10), Assert.Single(vm.RegularPads).Bounds);
        Assert.Equal("Export DXF: no CAD output pads.", vm.StatusText);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DxfExportUsesPadsCapturedBeforeTheSavePickerAwaitAsync(bool visibleOnly)
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        var original = Assert.Single(vm.CadPads, pad => pad.Layer == "alpha");
        var pickerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pickedPath = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickSaveExportPathAsync = (_, _) =>
        {
            pickerEntered.SetResult();
            return pickedPath.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        var command = visibleOnly ? vm.ExportDxfVisibleCommand : vm.ExportDxfAllCommand;
        var export = command.ExecuteAsync(null);
        try
        {
            await pickerEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await SelectCadPadsAsync(vm, [original.Id]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);

            pickedPath.SetResult(files.PathFor("captured.dxf"));
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            var exported = ReadDxf(files.PathFor("captured.dxf"));
            Assert.Equal(2, exported.Pads.Count);
            Assert.Equal(original.Polygon.Vertices,
                Assert.Single(exported.Pads, pad => pad.Layer == "alpha").Polygon.Vertices);
            Assert.Equal(1, vm.DeletedCadPadCount);
            Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
        }
        finally
        {
            pickedPath.TrySetResult(null);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task ImageDialogKeepsSourcePixelsPreferencesAndPickerCapturedBeforeAwaitAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        vm.ActiveAreaPixelsX = 120;
        vm.ActiveAreaPixelsY = 80;
        vm.RememberDxfLayerImageExportRequest(_priorPreferences);
        await WaitForGridRebuildAsync(vm);
        var original = vm.CadPads.ToArray();
        var opened = new TaskCompletionSource<LayerImageExportContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new TaskCompletionSource<LayerImageExportRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(files.PathFor("original.png"));
        vm.OpenDxfLayerImageExportAsync = context =>
        {
            opened.SetResult(context);
            return dialog.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        var export = vm.ExportDxfLayerImageCommand.ExecuteAsync(null);
        try
        {
            var context = await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await SelectCadPadsAsync(vm, original.Select(pad => pad.Id).ToArray());
            vm.DxfEditNewLayerName = "replacement";
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            vm.ActiveAreaPixelsX = 240;
            vm.ActiveAreaPixelsY = 160;
            vm.RememberDxfLayerImageExportRequest(_requestedPreferences);
            vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(files.PathFor("new.png"));
            await WaitForGridRebuildAsync(vm);

            Assert.Equal(2, vm.CadPads.Count);
            Assert.All(vm.CadPads, pad => Assert.Equal("replacement", pad.Layer));
            Assert.Equal(_alphaBeta, context.Entries.Select(entry => entry.Name));
            Assert.All(original, pad =>
            {
                var captured = Assert.Single(context.PadsOfLayer(pad.Layer));
                Assert.Equal((pad.Id, pad.Layer), (captured.Id, captured.Layer));
                Assert.Equal(pad.Polygon.Vertices, captured.Polygon.Vertices);
            });
            Assert.Equal("alpha", Assert.Single(context.InitialCheckedLayers));
            Assert.Equal((1.5m, false, DxfLayerImageFormat.Jpg),
                (context.LineWidthPixels, context.UseDarkTheme, context.Format));
            Assert.Equal((120, 80), (context.InitialSize.PixelsX, context.InitialSize.PixelsY));
            var defaults = context.CreateDefaultSize(["beta"]);
            Assert.Equal((120, 80), (defaults.PixelsX, defaults.PixelsY));
            Assert.Equal(files.PathFor("original.png"), await context.PickSavePathAsync("image", "png"));
        }
        finally
        {
            dialog.TrySetResult(null);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task ImageDialogCompletionAfterProjectReplacementWritesOldPadsAndPreferencesAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        var replacementPath = await CreateReplacementProjectAsync(files);
        var request = CreateImageRequest(files.PathFor("old.png"));
        var opened = new TaskCompletionSource<LayerImageExportContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new TaskCompletionSource<LayerImageExportRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new List<LayerImageWriteRequest>();
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(request.SavePath);
        vm.OpenDxfLayerImageExportAsync = context =>
        {
            context.WriteLayerImagesAsync = write =>
            {
                writes.Add(write);
                return Task.FromResult<IReadOnlyList<string>>([request.SavePath]);
            };
            opened.SetResult(context);
            return dialog.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        var export = vm.ExportDxfLayerImageCommand.ExecuteAsync(null);
        try
        {
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await ReplaceProjectAsync(vm, replacementPath);

            dialog.SetResult(request);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            var write = Assert.Single(writes);
            Assert.Equal(new Rect2(0, 0, 10, 10), Assert.Single(write.PadsOfLayer("alpha")).Bounds);
            Assert.Equal(new Rect2(20, 0, 30, 10), Assert.Single(write.PadsOfLayer("beta")).Bounds);
            Assert.Empty(vm.CadPads);
            // KNOWN DEFECT: An old dialog completion writes its preferences and success status into the replacement project.
            Assert.True(vm.HasUnsavedChanges);
            Assert.Equal("Layer image exported (1 file, PNG).", vm.StatusText);
            Assert.True(await vm.SaveProjectAsync());
            Assert.Equal(_requestedPreferences, ReadPreferences(replacementPath));
        }
        finally
        {
            dialog.TrySetResult(null);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task ImageWriterCompletionAfterProjectReplacementOverwritesReplacementPreferencesAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        var replacementPath = await CreateReplacementProjectAsync(files);
        var request = CreateImageRequest(files.PathFor("old.png"));
        var entered = new TaskCompletionSource<LayerImageWriteRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        WireDelayedWriter(vm, request, entered, writer);
        var export = vm.ExportDxfLayerImageCommand.ExecuteAsync(null);
        try
        {
            var write = await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await ReplaceProjectAsync(vm, replacementPath);
            Assert.Equal("replacement", ReadPreferences(replacementPath).LayerName);

            writer.SetResult([request.SavePath]);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            Assert.Equal("beta", Assert.Single(write.PadsOfLayer("beta")).Layer);
            Assert.Empty(vm.CadPads);
            // KNOWN DEFECT: A writer started for the old project also overwrites preferences after project replacement.
            Assert.True(vm.HasUnsavedChanges);
            Assert.Equal("Layer image exported (1 file, PNG).", vm.StatusText);
            Assert.True(await vm.SaveProjectAsync());
            Assert.Equal(_requestedPreferences, ReadPreferences(replacementPath));
        }
        finally
        {
            writer.TrySetException(new IOException("Writer released during test cleanup."));
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task DelayedImageWriterFailureKeepsAllPriorPreferencesAndTheSavedStateAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        vm.RememberDxfLayerImageExportRequest(_priorPreferences);
        var savedPath = files.PathFor("saved.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(savedPath);
        Assert.True(await vm.SaveProjectAsync());
        var request = CreateImageRequest(files.PathFor("failed.png"));
        var entered = new TaskCompletionSource<LayerImageWriteRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        WireDelayedWriter(vm, request, entered, writer);
        var export = vm.ExportDxfLayerImageCommand.ExecuteAsync(null);
        try
        {
            var write = await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal(request.Size, write.Size);
            Assert.Equal(request.Format, write.Format);
            Assert.False(vm.HasUnsavedChanges);
            Assert.Equal(_priorPreferences, ReadPreferences(savedPath));

            writer.SetException(new IOException("Synthetic layer image writer failure."));
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            Assert.StartsWith("Export DXF image failed", vm.StatusText, StringComparison.Ordinal);
            Assert.Contains("Synthetic layer image writer failure.", vm.StatusText, StringComparison.Ordinal);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(File.Exists(request.SavePath));
            Assert.True(await vm.SaveProjectAsync());
            Assert.Equal(_priorPreferences, ReadPreferences(savedPath));
        }
        finally
        {
            writer.TrySetException(new IOException("Writer released during test cleanup."));
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task CancelledOldImageDialogLeavesTheReplacementProjectUnchangedAsync()
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        var replacementPath = await CreateReplacementProjectAsync(files);
        var before = ReadPreferences(replacementPath);
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new TaskCompletionSource<LayerImageExportRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new List<LayerImageWriteRequest>();
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(files.PathFor("cancelled.png"));
        vm.OpenDxfLayerImageExportAsync = context =>
        {
            context.WriteLayerImagesAsync = write =>
            {
                writes.Add(write);
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            opened.SetResult();
            return dialog.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        var export = vm.ExportDxfLayerImageCommand.ExecuteAsync(null);
        try
        {
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await ReplaceProjectAsync(vm, replacementPath);
            var status = vm.StatusText;
            var canUndo = vm.CanUndo;

            dialog.SetResult(null);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            Assert.Empty(writes);
            Assert.Empty(vm.CadPads);
            Assert.False(vm.HasUnsavedChanges);
            Assert.Equal(canUndo, vm.CanUndo);
            // KNOWN DEFECT: Cancelling an image export provides no cancellation feedback in the status text.
            Assert.Equal(status, vm.StatusText);
            Assert.False(File.Exists(files.PathFor("cancelled.png")));
            Assert.True(await vm.SaveProjectAsync());
            Assert.Equal(before, ReadPreferences(replacementPath));
        }
        finally
        {
            dialog.TrySetResult(null);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaTheory]
    [InlineData(false, 1, "Layer image exported (1 file, PNG).")]
    [InlineData(true, 2, "Layer image exported (2 files, PNG).")]
    public async Task ImageExportWritesRequestedOutputModeAndRemembersTheFirstRequestedLayerAsync(
        bool oneFilePerLayer, int expectedFiles, string expectedStatus)
    {
        using var fixture = new WorkspaceFixture();
        using var files = new IoFailureScope();
        var vm = await fixture.OpenAsync();
        vm.DeselectAllLayers();
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        var request = CreateImageRequest(files.PathFor("layers.png"), oneFilePerLayer);
        var savedPath = files.PathFor("saved.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(savedPath);
        Assert.True(await vm.SaveProjectAsync());
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(request.SavePath);
        vm.OpenDxfLayerImageExportAsync = context =>
        {
            context.WriteLayerImagesAsync = LayerImageExportWindowHost.WriteAsync;
            return Task.FromResult<LayerImageExportRequest?>(request);
        };

        await vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.Equal(expectedStatus, vm.StatusText);
        var paths = Directory.GetFiles(files.Root, "*.png");
        Assert.Equal(expectedFiles, paths.Length);
        Assert.All(paths, path =>
        {
            using var bitmap = SKBitmap.Decode(path);
            Assert.NotNull(bitmap);
            Assert.Equal((36, 30), (bitmap.Width, bitmap.Height));
        });
        Assert.True(vm.HasUnsavedChanges);
        Assert.Empty(vm.CadPads);
        Assert.True(await vm.SaveProjectAsync());
        Assert.Equal(_requestedPreferences, ReadPreferences(savedPath));
    }

    [AvaloniaFact]
    public async Task RealProjectExportsOneSmallLayerImageThroughTheCapturedSourceAsync()
    {
        Assert.SkipWhen(!ExampleData.IsAvailable && !ExampleData.IsRequired, ExampleData.MissingMessage);
        using var project = ExampleProjectFixture.Create(TestPaths.FromRepo("example", "BOE36.35", "project_3635.json"));
        using var files = new IoFailureScope();
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(project.ProjectPath)).IsLoaded);
        await WaitForGridRebuildAsync(vm);
        var path = files.PathFor("sample.png");
        var writtenPadCounts = new List<int>();
        var selectedPadCount = 0;
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(path);
        vm.OpenDxfLayerImageExportAsync = context =>
        {
            var layer = context.Entries.OrderBy(entry => entry.PadCount).First();
            selectedPadCount = layer.PadCount;
            context.WriteLayerImagesAsync = write =>
            {
                var job = Assert.Single(write.Jobs);
                writtenPadCounts.Add(write.PadsOfLayer(Assert.Single(job.Layers)).Count);
                return LayerImageExportWindowHost.WriteAsync(write);
            };
            var size = context.CreateDefaultSize([layer.Name]).WithPixelsX(64).WithPixelsY(48);
            return Task.FromResult<LayerImageExportRequest?>(new(
                [layer.Name], size, 1m, false, DxfLayerImageFormat.Png, false, path));
        };

        await vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.True(selectedPadCount > 0);
        Assert.Equal(selectedPadCount, Assert.Single(writtenPadCounts));
        Assert.Equal("Layer image exported (1 file, PNG).", vm.StatusText);
        using var bitmap = SKBitmap.Decode(path);
        Assert.NotNull(bitmap);
        Assert.Equal((64, 48), (bitmap.Width, bitmap.Height));
    }

    private static async Task<FreeformHelperViewModel> OpenDuplicatePadsAsync(WorkspaceFixture fixture)
    {
        var path = CreateTempDxfFile(("alpha", 0, 0, 10, 10), ("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10));
        try
        {
            var vm = fixture.CreateViewModel();
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            return vm;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void ConfigureSingleCellBounds(FreeformHelperViewModel vm)
    {
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 1;
        cascade.YChannels = 1;
        vm.RegularSourceMode = RegularSourceMode.GeneratedGrid;
        vm.GridAlignmentMode = GridAlignmentMode.FromCadBounds;
        vm.GridPaddingPercent = 0;
    }

    private static async Task<CadPadSet> ExportDxfAsync(FreeformHelperViewModel vm, string path, bool visibleOnly)
    {
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(path);
        var command = visibleOnly ? vm.ExportDxfVisibleCommand : vm.ExportDxfAllCommand;
        await command.ExecuteAsync(null);
        return ReadDxf(path);
    }

    private static CadPadSet ReadDxf(string path) => new DxfImportService().ImportFromPath(path, new DxfImportOptions());

    private static LayerImageExportRequest CreateImageRequest(string path, bool oneFilePerLayer = false) => new(
        ["beta", "alpha"], LayerImageSize.CreateDefault(30, 10, 32, 24).WithPaddingX(2).WithPaddingY(3),
        3m, true, DxfLayerImageFormat.Png, oneFilePerLayer, path);

    private static void WireDelayedWriter(FreeformHelperViewModel vm, LayerImageExportRequest request,
        TaskCompletionSource<LayerImageWriteRequest> entered, TaskCompletionSource<IReadOnlyList<string>> writer)
    {
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(request.SavePath);
        vm.OpenDxfLayerImageExportAsync = context =>
        {
            context.WriteLayerImagesAsync = write =>
            {
                entered.SetResult(write);
                return writer.Task.WaitAsync(TestContext.Current.CancellationToken);
            };
            return Task.FromResult<LayerImageExportRequest?>(request);
        };
    }

    private static async Task<string> CreateReplacementProjectAsync(IoFailureScope files)
    {
        var path = files.PathFor("replacement.json");
        var project = new ProjectFile();
        project.Settings.View.AutoReplayStep2AfterProjectLoad = false;
        project.UiSnapshot.View.DxfLayerImagePreferredLayer = "replacement";
        project.UiSnapshot.View.DxfLayerImageWidthPixels = 44;
        project.UiSnapshot.View.DxfLayerImageHeightPixels = 22;
        project.UiSnapshot.View.DxfLayerImageLineWidthPixels = 2;
        project.UiSnapshot.View.DxfLayerImagePaddingXPixels = 5;
        project.UiSnapshot.View.DxfLayerImagePaddingYPixels = 7;
        project.UiSnapshot.View.DxfLayerImageFormat = "Bmp";
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        return path;
    }

    private static async Task ReplaceProjectAsync(FreeformHelperViewModel vm, string path)
    {
        vm.ApplyAppVisualPreferencesOnProjectLoad = false;
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        Assert.False(vm.HasUnsavedChanges);
    }

    private static DxfLayerImageExportRequest ReadPreferences(string path)
    {
        var view = JsonProjectStore.Load(path).UiSnapshot.View;
        return new DxfLayerImageExportRequest(view.DxfLayerImagePreferredLayer,
            (int)view.DxfLayerImageWidthPixels, (int)view.DxfLayerImageHeightPixels,
            (decimal)view.DxfLayerImageLineWidthPixels, (int)view.DxfLayerImagePaddingXPixels,
            (int)view.DxfLayerImagePaddingYPixels, Enum.Parse<DxfLayerImageFormat>(view.DxfLayerImageFormat),
            view.DxfLayerImageUseDarkTheme);
    }
}
