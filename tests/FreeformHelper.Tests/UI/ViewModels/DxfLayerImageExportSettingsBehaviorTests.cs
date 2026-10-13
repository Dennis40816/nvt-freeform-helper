using System.Text;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfLayerImageExportSettingsBehaviorTests
{
    private static readonly string[] _layers = ["L1", "L2"];

    [Fact]
    public void BuildDxfLayerImageExportInitialRequest_NewViewModel_ReturnsTheDefaultRequestForTheFirstLayer()
    {
        var vm = new FreeformHelperViewModel();

        var request = vm.BuildDxfLayerImageExportInitialRequest(_layers);

        Assert.Equal(
            new DxfLayerImageExportRequest("L1", 1024, 1024, 1m, 200, 200, DxfLayerImageFormat.Png, false),
            request);
    }

    [Fact]
    public void BuildDxfLayerImageExportInitialRequest_NoLayers_FallsBackToLayerZero()
    {
        var vm = new FreeformHelperViewModel();

        var request = vm.BuildDxfLayerImageExportInitialRequest([]);

        Assert.Equal("0", request.LayerName);
    }

    [Fact]
    public void BuildDxfLayerImageExportInitialRequest_NullLayers_Throws()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Throws<ArgumentNullException>(() => vm.BuildDxfLayerImageExportInitialRequest(null!));
    }

    [Fact]
    public void BuildDxfLayerImageExportInitialRequest_RememberedLayerIsListed_UsesTheRememberedLayer()
    {
        var vm = new FreeformHelperViewModel();
        vm.RememberDxfLayerImageExportRequest(CreateRequest(layerName: "L2"));

        var request = vm.BuildDxfLayerImageExportInitialRequest(_layers);

        Assert.Equal("L2", request.LayerName);
    }

    [Fact]
    public void BuildDxfLayerImageExportInitialRequest_RememberedLayerIsNotListed_UsesTheFirstLayer()
    {
        var vm = new FreeformHelperViewModel();
        vm.RememberDxfLayerImageExportRequest(CreateRequest(layerName: "Gone"));

        var request = vm.BuildDxfLayerImageExportInitialRequest(_layers);

        Assert.Equal("L1", request.LayerName);
    }

    [Fact]
    public void RememberDxfLayerImageExportRequest_AllValuesChanged_ReturnsEveryValueFromTheInitialRequest()
    {
        var vm = new FreeformHelperViewModel();
        var remembered = new DxfLayerImageExportRequest("L2", 640, 480, 2.5m, 30, 40, DxfLayerImageFormat.Jpg, true);

        vm.RememberDxfLayerImageExportRequest(remembered);

        Assert.Equal(remembered, vm.BuildDxfLayerImageExportInitialRequest(_layers));
    }

    [Fact]
    public void RememberDxfLayerImageExportRequest_ValuesBelowTheRange_ClampsToTheMinimum()
    {
        var vm = new FreeformHelperViewModel();

        vm.RememberDxfLayerImageExportRequest(
            new DxfLayerImageExportRequest("L1", 1, -5, 0.01m, -1, -2, DxfLayerImageFormat.Png, false));

        var request = vm.BuildDxfLayerImageExportInitialRequest(_layers);
        Assert.Equal(2, request.WidthPixels);
        Assert.Equal(2, request.HeightPixels);
        Assert.Equal(0.1m, request.LineWidthPixels);
        Assert.Equal(0, request.PaddingXPixels);
        Assert.Equal(0, request.PaddingYPixels);
    }

    [Fact]
    public void RememberDxfLayerImageExportRequest_ValuesAboveTheRange_ClampsToTheMaximum()
    {
        var vm = new FreeformHelperViewModel();

        vm.RememberDxfLayerImageExportRequest(
            new DxfLayerImageExportRequest("L1", 200_000, 100_001, 500m, 100_001, 999_999, DxfLayerImageFormat.Png, false));

        var request = vm.BuildDxfLayerImageExportInitialRequest(_layers);
        Assert.Equal(100_000, request.WidthPixels);
        Assert.Equal(100_000, request.HeightPixels);
        Assert.Equal(100m, request.LineWidthPixels);
        Assert.Equal(100_000, request.PaddingXPixels);
        Assert.Equal(100_000, request.PaddingYPixels);
    }

    [Fact]
    public async Task RememberDxfLayerImageExportRequest_ChangedValue_MarksTheProjectUnsaved()
    {
        using var project = await ProjectFixture.LoadAsync();
        Assert.False(project.Vm.HasUnsavedChanges);

        project.Vm.RememberDxfLayerImageExportRequest(CreateRequest(width: 640));

        Assert.True(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task RememberDxfLayerImageExportRequest_SameValues_KeepsTheProjectSaved()
    {
        using var project = await ProjectFixture.LoadAsync();
        var current = project.Vm.BuildDxfLayerImageExportInitialRequest(_layers);

        project.Vm.RememberDxfLayerImageExportRequest(current with { LayerName = string.Empty });

        Assert.False(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task RememberDxfLayerImageExportRequest_ValueClampedToTheStoredOne_KeepsTheProjectSaved()
    {
        using var project = await ProjectFixture.LoadAsync();
        project.Vm.RememberDxfLayerImageExportRequest(CreateRequest(width: 100_000));
        await project.SaveAsync();

        project.Vm.RememberDxfLayerImageExportRequest(CreateRequest(width: 150_000));

        Assert.False(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task ExportDxfLayerImageCommand_ConfirmedRequest_RemembersItWithoutAPath()
    {
        using var project = await ProjectFixture.LoadAsync();
        project.Vm.OpenDxfLayerImageExportAsync = _ =>
            Task.FromResult<DxfLayerImageExportRequest?>(
                new DxfLayerImageExportRequest("L1", 320, 240, 3m, 10, 20, DxfLayerImageFormat.Bmp, true));
        project.Vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(null);

        await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.Equal(
            new DxfLayerImageExportRequest("L1", 320, 240, 3m, 10, 20, DxfLayerImageFormat.Bmp, true),
            project.Vm.BuildDxfLayerImageExportInitialRequest(_layers));
        Assert.True(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task ExportDxfLayerImageCommand_CancelledDialog_KeepsTheStoredRequest()
    {
        using var project = await ProjectFixture.LoadAsync();
        project.Vm.OpenDxfLayerImageExportAsync = _ => Task.FromResult<DxfLayerImageExportRequest?>(null);
        project.Vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(null);

        await project.Vm.ExportDxfLayerImageCommand.ExecuteAsync(null);

        Assert.Equal(
            new DxfLayerImageExportRequest("L1", 1024, 1024, 1m, 200, 200, DxfLayerImageFormat.Png, false),
            project.Vm.BuildDxfLayerImageExportInitialRequest(_layers));
        Assert.False(project.Vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task SaveProject_AfterRemembering_WritesTheRememberedValuesToTheUiSnapshot()
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
    public async Task LoadProject_SnapshotValues_RestoresThemInTheInitialRequest()
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

        var request = project.Vm.BuildDxfLayerImageExportInitialRequest(_layers);

        Assert.Equal(
            new DxfLayerImageExportRequest("L2", 640, 481, 2.5m, 30, 40, DxfLayerImageFormat.Bmp, true),
            request);
    }

    [Fact]
    public async Task LoadProject_AppVisualPreferencesLayerOn_ReplacesTheProjectValuesWithTheAppValues()
    {
        using var project = await ProjectFixture.LoadAsync(
            view => view.DxfLayerImageWidthPixels = 640,
            applyAppVisualPreferences: true);

        var request = project.Vm.BuildDxfLayerImageExportInitialRequest(_layers);

        Assert.Equal(1024, request.WidthPixels);
    }

    [Fact]
    public async Task LoadProject_OutOfRangeSnapshotValues_ClampsThem()
    {
        using var project = await ProjectFixture.LoadAsync(view =>
        {
            view.DxfLayerImageWidthPixels = 0.2;
            view.DxfLayerImageHeightPixels = 500_000;
            view.DxfLayerImageLineWidthPixels = 0.001;
            view.DxfLayerImagePaddingXPixels = -10;
            view.DxfLayerImagePaddingYPixels = 900_000;
        });

        var request = project.Vm.BuildDxfLayerImageExportInitialRequest(_layers);

        Assert.Equal(2, request.WidthPixels);
        Assert.Equal(100_000, request.HeightPixels);
        Assert.Equal(0.1m, request.LineWidthPixels);
        Assert.Equal(0, request.PaddingXPixels);
        Assert.Equal(100_000, request.PaddingYPixels);
    }

    [Fact]
    public async Task LoadProject_UnknownFormatName_FallsBackToPng()
    {
        using var project = await ProjectFixture.LoadAsync(view => view.DxfLayerImageFormat = "tiff");

        var request = project.Vm.BuildDxfLayerImageExportInitialRequest(_layers);

        Assert.Equal(DxfLayerImageFormat.Png, request.Format);
    }

    private static DxfLayerImageExportRequest CreateRequest(string layerName = "L1", int width = 1024)
    {
        return new DxfLayerImageExportRequest(layerName, width, 1024, 1m, 200, 200, DxfLayerImageFormat.Png, false);
    }

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
