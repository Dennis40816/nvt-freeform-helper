// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfLayerImageExportSettingsBehaviorTests
{
    [Theory]
    [InlineData("L2", 1)]
    [InlineData("Gone", 2)]
    public void PreferredLayerIsUsedWhenPresentOtherwiseAllLayersAreChecked(string preferred, int expectedCount)
    {
        var vm = new FreeformHelperViewModel();
        vm.RememberDxfLayerImageExportRequest(CreateRequest(layerName: preferred));

        var context = CaptureContext(vm);

        Assert.Equal(expectedCount, context.InitialCheckedLayers.Count);
        Assert.Contains("L2", context.InitialCheckedLayers);
    }

    [Fact]
    public void ContextUsesProjectPixelsAndIgnoresRememberedPixelsAndPadding()
    {
        var vm = new FreeformHelperViewModel { ActiveAreaPixelsX = 6480, ActiveAreaPixelsY = 848 };
        vm.RememberDxfLayerImageExportRequest(new DxfLayerImageExportRequest("L2", 640, 480, 2.5m, 30, 40, DxfLayerImageFormat.Jpg, true));

        var context = CaptureContext(vm);

        Assert.Equal(LayerImageSizeSource.Project, context.InitialSize.Source);
        Assert.Equal(6480, context.InitialSize.PixelsX);
        Assert.Equal(848, context.InitialSize.PixelsY);
        Assert.Equal(0, context.InitialSize.PaddingX);
        Assert.Equal(0, context.InitialSize.PaddingY);
        Assert.Equal(2.5m, context.LineWidthPixels);
        Assert.Equal(DxfLayerImageFormat.Jpg, context.Format);
        Assert.True(context.UseDarkTheme);
    }

    [Fact]
    public void ContextGroupsPadsOrdinallyAndOmitsBlankLayerNames()
    {
        var vm = new FreeformHelperViewModel();
        var pads = new List<CadPad>
        {
            LayerImageExportTestData.CreateSquareCad(1, "z", 0, 0, 10),
            LayerImageExportTestData.CreateSquareCad(2, "A", 20, 0, 10),
            LayerImageExportTestData.CreateSquareCad(3, "z", 40, 0, 10),
            LayerImageExportTestData.CreateSquareCad(4, " ", 0, 0, 10),
        };

        var context = vm.BuildLayerImageExportContext(pads, static (_, _) => Task.FromResult<string?>(null));

        Assert.Equal(new List<string> { "A", "z" }, context.Entries.Select(entry => entry.Name));
        Assert.Equal(2, context.Entries[1].PadCount);
        Assert.Equal(4096, context.InitialSize.PixelsX);
        Assert.Equal(819, context.InitialSize.PixelsY);
    }

    [Fact]
    public void InitialSizeUsesOnlyTheCheckedLayerBounds()
    {
        var vm = new FreeformHelperViewModel();
        vm.RememberDxfLayerImageExportRequest(CreateRequest(layerName: "L2"));
        var pads = new List<CadPad>
        {
            LayerImageExportTestData.CreateSquareCad(1, "L1", 0, 0, 10),
            LayerImageExportTestData.CreateSquareCad(2, "L2", 20, 0, 10),
        };

        var context = vm.BuildLayerImageExportContext(pads, static (_, _) => Task.FromResult<string?>(null));

        Assert.Equal(new List<string> { "L2" }, context.InitialCheckedLayers);
        Assert.Equal(4096, context.InitialSize.PixelsX);
        Assert.Equal(4096, context.InitialSize.PixelsY);
    }

    [Fact]
    public async Task RememberChangedValueMarksTheProjectUnsavedAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        Assert.False(project.Vm.HasUnsavedChanges);

        project.Vm.RememberDxfLayerImageExportRequest(CreateRequest(width: 640));

        Assert.True(project.Vm.HasUnsavedChanges);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task RememberEquivalentValuesKeepsTheProjectSavedAsync(string? layer)
    {
        using var project = await ProjectFixture.LoadAsync();

        project.Vm.RememberDxfLayerImageExportRequest(CreateRequest(layerName: layer!));

        Assert.False(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task RememberValueClampedToTheStoredOneKeepsTheProjectSavedAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        project.Vm.RememberDxfLayerImageExportRequest(CreateRequest(width: 100_000));
        await project.SaveAsync();

        project.Vm.RememberDxfLayerImageExportRequest(CreateRequest(width: 150_000));

        Assert.False(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task SuccessfulExportRemembersTheAdaptedClampedRequestAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        string path = TestFiles.NewTempFilePath("layer-export-", ".bmp");
        project.Vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(path);
        project.Vm.OpenDxfLayerImageExportAsync = SaveClampedRequestAsync;

        await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);
        await project.SaveAsync();

        Assert.True(File.Exists(path));
        var view = project.ReadSavedSnapshot().View;
        Assert.Equal("L1", view.DxfLayerImagePreferredLayer);
        Assert.Equal(32d, view.DxfLayerImageWidthPixels);
        Assert.Equal(24d, view.DxfLayerImageHeightPixels);
        Assert.Equal(100d, view.DxfLayerImageLineWidthPixels);
        Assert.Equal(0d, view.DxfLayerImagePaddingXPixels);
        Assert.Equal(3d, view.DxfLayerImagePaddingYPixels);
        Assert.Equal("Bmp", view.DxfLayerImageFormat);
        Assert.True(view.DxfLayerImageUseDarkTheme);
    }

    private static async Task<LayerImageExportRequest?> SaveClampedRequestAsync(LayerImageExportContext context)
    {
        context.WriteLayerImagesAsync = LayerImageExportWindowHost.WriteAsync;
        using var dialog = new LayerImageExportViewModel(context, LayerImageExportTestData.CreateClock());
        dialog.PreviewSettings.PixelsX = 32;
        dialog.PreviewSettings.PixelsY = 24;
        dialog.PreviewSettings.PaddingX = -1;
        dialog.PreviewSettings.PaddingY = 3;
        dialog.PreviewSettings.LineWidthPixels = 500m;
        dialog.PreviewSettings.UseDarkTheme = true;
        dialog.FileSettings.Format = DxfLayerImageFormat.Bmp;
        var settings = new LayerImageExportSettingsViewModel(dialog, context.PickSavePathAsync);
        await settings.SaveCommand.ExecuteAsync(null);
        return settings.Request;
    }

    [Fact]
    public async Task CancelledDialogKeepsTheStoredPreferencesAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        var path = Path.Combine(project.DirectoryPath, "cancelled.bmp");
        var statusBeforeCancel = project.Vm.StatusText;
        var pickerCalls = 0;
        var writerCalls = 0;
        bool? closed = null;
        project.Vm.OpenDxfLayerImageExportAsync = context =>
        {
            context.WriteLayerImagesAsync = request =>
            {
                writerCalls++;
                return LayerImageExportWindowHost.WriteAsync(request);
            };
            using var dialog = new LayerImageExportViewModel(context, LayerImageExportTestData.CreateClock());
            dialog.PreviewSettings.PixelsX = 32;
            dialog.PreviewSettings.PixelsY = 24;
            dialog.FileSettings.Format = DxfLayerImageFormat.Bmp;
            dialog.RequestClose += saved => closed = saved;
            dialog.CancelCommand.Execute(null);
            return Task.FromResult(dialog.CompletedRequest);
        };
        project.Vm.PickSaveExportPathAsync = (_, _) =>
        {
            pickerCalls++;
            return Task.FromResult<string?>(path);
        };

        await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal(0, pickerCalls);
        Assert.Equal(0, writerCalls);
        Assert.False(File.Exists(path));
        Assert.Equal(statusBeforeCancel, project.Vm.StatusText);
        Assert.Equal(DxfLayerImageFormat.Png, CaptureContext(project.Vm).Format);
        Assert.False(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task ExportDxfLayerImageCommand_ReopensWithChosenLayerAndFreshSizeDefaultsAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        var dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("L1", 0, 0, 20, 10), ("L2", 30, 0, 40, 10));
        try
        {
            project.Vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath);
            await project.Vm.OpenDxfCommand.ExecuteAsync(null);
            var outputPath = Path.Combine(project.DirectoryPath, "chosen-layer.bmp");
            project.Vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(outputPath);
            project.Vm.OpenDxfLayerImageExportAsync = SaveSecondLayerRequestAsync;

            await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

            Assert.True(File.Exists(outputPath), project.Vm.StatusText);
            LayerImageExportContext? reopened = null;
            project.Vm.OpenDxfLayerImageExportAsync = context =>
            {
                reopened = context;
                return Task.FromResult<LayerImageExportRequest?>(null);
            };

            await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

            Assert.NotNull(reopened);
            Assert.Equal("L2", Assert.Single(reopened.InitialCheckedLayers));
            Assert.Equal(LayerImageSizeSource.Auto, reopened.InitialSize.Source);
            Assert.Equal(4096, reopened.InitialSize.PixelsX);
            Assert.Equal(4096, reopened.InitialSize.PixelsY);
            Assert.Equal(0, reopened.InitialSize.PaddingX);
            Assert.Equal(0, reopened.InitialSize.PaddingY);
            Assert.Equal(2.5m, reopened.LineWidthPixels);
            Assert.Equal(DxfLayerImageFormat.Bmp, reopened.Format);
            Assert.True(reopened.UseDarkTheme);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    private static async Task<LayerImageExportRequest?> SaveSecondLayerRequestAsync(LayerImageExportContext context)
    {
        context.WriteLayerImagesAsync = LayerImageExportWindowHost.WriteAsync;
        using var dialog = new LayerImageExportViewModel(context, LayerImageExportTestData.CreateClock());
        foreach (var row in dialog.VisibleRows)
        {
            row.IsChecked = row.Name == "L2";
        }
        dialog.PreviewSettings.PixelsX = 32;
        dialog.PreviewSettings.PixelsY = 24;
        dialog.PreviewSettings.PaddingX = 3;
        dialog.PreviewSettings.PaddingY = 4;
        dialog.PreviewSettings.LineWidthPixels = 2.5m;
        dialog.PreviewSettings.UseDarkTheme = true;
        dialog.FileSettings.Format = DxfLayerImageFormat.Bmp;
        var settings = new LayerImageExportSettingsViewModel(dialog, context.PickSavePathAsync);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(settings.Request);
        Assert.Equal("L2", Assert.Single(settings.Request.Layers));
        Assert.Equal(32, settings.Request.Size.PixelsX);
        Assert.Equal(24, settings.Request.Size.PixelsY);
        return settings.Request;
    }

    [Fact]
    public async Task FailedWriteDoesNotRememberNewPreferencesAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        project.Vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(null);
        project.Vm.OpenDxfLayerImageExportAsync = context =>
        {
            context.WriteLayerImagesAsync = LayerImageExportWindowHost.WriteAsync;
            using var dialog = new LayerImageExportViewModel(context, LayerImageExportTestData.CreateClock());
            dialog.PreviewSettings.PixelsX = 32;
            dialog.PreviewSettings.PixelsY = 24;
            dialog.FileSettings.Format = DxfLayerImageFormat.Bmp;
            var request = dialog.BuildRequest(dialog.FileSettings) with { SavePath = project.DirectoryPath };
            return Task.FromResult<LayerImageExportRequest?>(request);
        };

        await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.Contains("Export DXF image failed", project.Vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(DxfLayerImageFormat.Png, CaptureContext(project.Vm).Format);
        Assert.False(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task MissingWriterReportsStatusWithoutRememberingPreferencesAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        project.Vm.PickSaveExportPathAsync = static (_, _) => Task.FromResult<string?>(null);
        project.Vm.OpenDxfLayerImageExportAsync = context => Task.FromResult<LayerImageExportRequest?>(new(
            context.InitialCheckedLayers, context.InitialSize, context.LineWidthPixels, context.UseDarkTheme,
            DxfLayerImageFormat.Bmp, false, Path.Combine(project.DirectoryPath, "unused.bmp")));

        await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.Equal("Export layer image: writer not wired.", project.Vm.StatusText);
        Assert.Equal(DxfLayerImageFormat.Png, CaptureContext(project.Vm).Format);
        Assert.False(project.Vm.HasUnsavedChanges);
        Assert.False(File.Exists(Path.Combine(project.DirectoryPath, "unused.bmp")));
    }

    [Fact]
    public async Task SaveProjectWritesTheExistingPreferenceSnapshotAsync()
    {
        using var project = await ProjectFixture.LoadAsync();
        project.Vm.RememberDxfLayerImageExportRequest(
            new DxfLayerImageExportRequest("L2", 640, 480, 2.5m, 30, 40, DxfLayerImageFormat.Jpg, true));

        await project.SaveAsync();

        var view = project.ReadSavedSnapshot().View;
        Assert.Equal("L2", view.DxfLayerImagePreferredLayer);
        Assert.Equal(640d, view.DxfLayerImageWidthPixels);
        Assert.Equal(480d, view.DxfLayerImageHeightPixels);
        Assert.Equal(2.5d, view.DxfLayerImageLineWidthPixels);
        Assert.Equal(30d, view.DxfLayerImagePaddingXPixels);
        Assert.Equal(40d, view.DxfLayerImagePaddingYPixels);
        Assert.Equal("Jpg", view.DxfLayerImageFormat);
        Assert.True(view.DxfLayerImageUseDarkTheme);
    }

    [Fact]
    public async Task LoadRestoresPreferencesButNeverPixelsOrPaddingAsync()
    {
        using var project = await ProjectFixture.LoadAsync(view =>
        {
            view.DxfLayerImagePreferredLayer = "L2";
            view.DxfLayerImageWidthPixels = 640.4;
            view.DxfLayerImageHeightPixels = 480.6;
            view.DxfLayerImageLineWidthPixels = 2.5;
            view.DxfLayerImagePaddingXPixels = 30;
            view.DxfLayerImagePaddingYPixels = 40;
            view.DxfLayerImageFormat = "bmp";
            view.DxfLayerImageUseDarkTheme = true;
        });

        var context = CaptureContext(project.Vm);

        Assert.Equal(new List<string> { "L2" }, context.InitialCheckedLayers);
        Assert.Equal(LayerImageSizeSource.Auto, context.InitialSize.Source);
        Assert.Equal(4096, context.InitialSize.PixelsX);
        Assert.Equal(4096, context.InitialSize.PixelsY);
        Assert.Equal(0, context.InitialSize.PaddingX);
        Assert.Equal(0, context.InitialSize.PaddingY);
        Assert.Equal(2.5m, context.LineWidthPixels);
        Assert.Equal(DxfLayerImageFormat.Bmp, context.Format);
        Assert.True(context.UseDarkTheme);
    }

    [Fact]
    public async Task AppVisualPreferencesStillOverrideProjectPreferencesAsync()
    {
        using var project = await ProjectFixture.LoadAsync(
            view => view.DxfLayerImageLineWidthPixels = 3, applyAppVisualPreferences: true);

        Assert.Equal(1m, CaptureContext(project.Vm).LineWidthPixels);
    }

    [Theory]
    [InlineData(0.001, 0.1)]
    [InlineData(500, 100)]
    public async Task InvalidSnapshotValuesRemainClampedAsync(double lineWidth, double expectedLineWidth)
    {
        using var project = await ProjectFixture.LoadAsync(view =>
        {
            view.DxfLayerImageWidthPixels = 0.2;
            view.DxfLayerImageHeightPixels = 500_000;
            view.DxfLayerImageLineWidthPixels = lineWidth;
            view.DxfLayerImagePaddingXPixels = -10;
            view.DxfLayerImagePaddingYPixels = 900_000;
        });

        await project.SaveAsync();

        var view = project.ReadSavedSnapshot().View;
        Assert.Equal(2d, view.DxfLayerImageWidthPixels);
        Assert.Equal(100_000d, view.DxfLayerImageHeightPixels);
        Assert.Equal(expectedLineWidth, view.DxfLayerImageLineWidthPixels);
        Assert.Equal(0d, view.DxfLayerImagePaddingXPixels);
        Assert.Equal(100_000d, view.DxfLayerImagePaddingYPixels);
        Assert.Equal((decimal)expectedLineWidth, CaptureContext(project.Vm).LineWidthPixels);
    }

    [Fact]
    public async Task UnknownSnapshotFormatFallsBackToPngAsync()
    {
        using var project = await ProjectFixture.LoadAsync(view => view.DxfLayerImageFormat = "tiff");

        Assert.Equal(DxfLayerImageFormat.Png, CaptureContext(project.Vm).Format);
    }

    private static LayerImageExportContext CaptureContext(FreeformHelperViewModel vm) => vm.BuildLayerImageExportContext(
        new List<CadPad>
        {
            LayerImageExportTestData.CreateSquareCad(1, "L1", 0, 0, 10),
            LayerImageExportTestData.CreateSquareCad(2, "L2", 20, 0, 10),
        }, static (_, _) => Task.FromResult<string?>(null));

    private static DxfLayerImageExportRequest CreateRequest(string layerName = "L1", int width = 1024) =>
        new(layerName, width, 1024, 1m, 200, 200, DxfLayerImageFormat.Png, false);

    private sealed class ProjectFixture : IDisposable
    {
        private readonly DirectoryInfo _directory;
        private readonly string _savePath;

        private ProjectFixture(DirectoryInfo directory, FreeformHelperViewModel vm)
        {
            _directory = directory;
            _savePath = Path.Combine(directory.FullName, "saved.json");
            Vm = vm;
        }

        public FreeformHelperViewModel Vm { get; }

        public string DirectoryPath => _directory.FullName;

        public static async Task<ProjectFixture> LoadAsync(
            Action<UiViewSnapshot>? configureView = null,
            bool applyAppVisualPreferences = false)
        {
            var directory = TestFiles.CreateTempDirectory();
            var projectPath = Path.Combine(directory.FullName, "project.json");
            var view = new UiViewSnapshot();
            configureView?.Invoke(view);
            await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile
            {
                EmbeddedDxfName = "synthetic.dxf",
                EmbeddedDxf = Encoding.UTF8.GetBytes(SyntheticDxf),
                Settings = new ProjectSettings
                {
                    Grid = new GridSettings { XChannels = 2, YChannels = 1 },
                },
                UiSnapshot = new ProjectUiSnapshot
                {
                    Grid = new UiGridSnapshot
                    {
                        CascadeCount = 1,
                        IcX = [2],
                        IcY = [1],
                        TotalX = 2,
                        TotalY = 1,
                        ActiveAreaWidthMm = 20,
                        ActiveAreaHeightMm = 10,
                    },
                    View = view,
                },
            }, CancellationToken.None);
            var vm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
                ApplyAppVisualPreferencesOnProjectLoad = applyAppVisualPreferences,
            };
            await vm.LoadProjectCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();
            return new ProjectFixture(directory, vm);
        }

        public async Task SaveAsync()
        {
            Vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(_savePath);
            Vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
            var saved = await Vm.SaveProjectAsync();
            Assert.True(saved);
        }

        public ProjectUiSnapshot ReadSavedSnapshot()
        {
            return JsonProjectStore.Load(_savePath).UiSnapshot;
        }

        public void Dispose()
        {
            _directory.Delete(recursive: true);
        }
    }

    private const string SyntheticDxf = """
        0
        SECTION
        2
        ENTITIES
        0
        LWPOLYLINE
        8
        L1
        70
        1
        90
        4
        10
        0
        20
        0
        10
        20
        20
        0
        10
        20
        20
        10
        10
        0
        20
        10
        0
        ENDSEC
        0
        EOF
        """;
}
