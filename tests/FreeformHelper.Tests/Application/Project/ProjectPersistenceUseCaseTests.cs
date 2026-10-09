using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ProjectPersistenceUseCaseTests
{
    private static readonly int[] ExpectedCombinedSourceCadIds = [1, 2];

    [Fact]
    public async Task SaveAsync_CancelledWhenNoPathSelected()
    {
        var useCase = new ProjectPersistenceUseCase(new JsonProjectStore(), new PadOverrideService());
        var file = new ProjectFile();

        var result = await useCase.SaveAsync(new ProjectSaveRequest(
            Project: file,
            Grid: null,
            CadPadCustomValues: new Dictionary<int, double>(),
            PickSaveProjectPathAsync: () => Task.FromResult<string?>(null),
            ConfirmEmbedDxfAsync: null,
            BuildUiSnapshot: () => new ProjectUiSnapshot(),
            ApplyUiToSettings: _ => { },
            HasCadLoaded: false));

        Assert.True(result.IsCancelled);
        Assert.False(result.IsSaved);
    }

    [Fact]
    public async Task SaveAsync_WritesFileAndCapturesOverrides()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonProjectStore();
            var useCase = new ProjectPersistenceUseCase(store, new PadOverrideService());
            var file = new ProjectFile();

            var grid = BuildTestGrid(rows: 1, cols: 2);
            grid.Pads[1].Freeform = FreeformType.XWay;

            var cadValues = new Dictionary<int, double> { [123] = 4.5 };

            var result = await useCase.SaveAsync(new ProjectSaveRequest(
                Project: file,
                Grid: grid,
                CadPadCustomValues: cadValues,
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(path),
                ConfirmEmbedDxfAsync: null,
                BuildUiSnapshot: () => new ProjectUiSnapshot(),
                ApplyUiToSettings: settings => settings.Grid.XChannels = 77,
                HasCadLoaded: false));

            Assert.True(result.IsSaved);
            Assert.False(result.IsCancelled);
            Assert.True(File.Exists(path));

            var loaded = JsonProjectStore.Load(path);
            Assert.Equal(77, loaded.Settings.Grid.XChannels);

            Assert.True(loaded.CadPadCustomValues.ContainsKey(123));
            Assert.Equal(4.5, loaded.CadPadCustomValues[123], 6);

            Assert.True(loaded.FreeformOverrides.ContainsKey(1));
            Assert.Equal(FreeformType.XWay, loaded.FreeformOverrides[1]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_EmbedDxf_EmbedsBytesAndName()
    {
        var dxfPath = Path.Combine(Path.GetTempPath(), $"cad_{Guid.NewGuid():N}.dxf");
        var maskPath = Path.Combine(Path.GetTempPath(), $"SeeRegular_{Guid.NewGuid():N}.csv");
        var projectPath = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var dxfBytes = new byte[] { 1, 2, 3, 4, 5 };
            var maskBytes = new byte[] { 6, 7, 8, 9 };
            File.WriteAllBytes(dxfPath, dxfBytes);
            File.WriteAllBytes(maskPath, maskBytes);

            var store = new JsonProjectStore();
            var useCase = new ProjectPersistenceUseCase(store, new PadOverrideService());
            var file = new ProjectFile { LastDxfPath = dxfPath };

            var result = await useCase.SaveAsync(new ProjectSaveRequest(
                Project: file,
                Grid: null,
                CadPadCustomValues: new Dictionary<int, double>(),
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync: () => Task.FromResult(true),
                BuildUiSnapshot: () => new ProjectUiSnapshot
                {
                    Import = new UiImportSnapshot
                    {
                        RegularVisibilityMaskSourcePath = maskPath,
                        UseRegularVisibilityMask = true,
                    },
                },
                ApplyUiToSettings: _ => { },
                HasCadLoaded: true));

            Assert.True(result.IsSaved);
            Assert.Null(result.EmbedWarning);

            var loaded = JsonProjectStore.Load(projectPath);
            Assert.NotNull(loaded.EmbeddedDxf);
            Assert.Equal(dxfBytes, loaded.EmbeddedDxf);
            Assert.Equal(Path.GetFileName(dxfPath), loaded.EmbeddedDxfName);
            Assert.NotNull(loaded.EmbeddedRegularVisibilityMask);
            Assert.Equal(maskBytes, loaded.EmbeddedRegularVisibilityMask);
            Assert.Equal(Path.GetFileName(maskPath), loaded.EmbeddedRegularVisibilityMaskName);
        }
        finally
        {
            if (File.Exists(dxfPath))
            {
                File.Delete(dxfPath);
            }
            if (File.Exists(maskPath))
            {
                File.Delete(maskPath);
            }
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_EmbedDeclined_ClearsEmbeddedFields()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonProjectStore();
            var useCase = new ProjectPersistenceUseCase(store, new PadOverrideService());
            var file = new ProjectFile
            {
                EmbeddedDxf = new byte[] { 9, 9, 9 },
                EmbeddedDxfName = "prev.dxf",
                EmbeddedRegularVisibilityMask = new byte[] { 8, 8, 8 },
                EmbeddedRegularVisibilityMaskName = "SeeRegular.csv",
            };

            var result = await useCase.SaveAsync(new ProjectSaveRequest(
                Project: file,
                Grid: null,
                CadPadCustomValues: new Dictionary<int, double>(),
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync: () => Task.FromResult(false),
                BuildUiSnapshot: () => new ProjectUiSnapshot(),
                ApplyUiToSettings: _ => { },
                HasCadLoaded: true));

            Assert.True(result.IsSaved);

            var loaded = JsonProjectStore.Load(projectPath);
            Assert.Null(loaded.EmbeddedDxf);
            Assert.Null(loaded.EmbeddedDxfName);
            Assert.Null(loaded.EmbeddedRegularVisibilityMask);
            Assert.Null(loaded.EmbeddedRegularVisibilityMaskName);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_WritesMatchingAndMappingSettingsAndUiSnapshot()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonProjectStore();
            var useCase = new ProjectPersistenceUseCase(store, new PadOverrideService());
            var file = new ProjectFile();

            var result = await useCase.SaveAsync(new ProjectSaveRequest(
                Project: file,
                Grid: null,
                CadPadCustomValues: new Dictionary<int, double>(),
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync: null,
                BuildUiSnapshot: () => new ProjectUiSnapshot
                {
                    Matching = new UiMatchingSnapshot
                    {
                        MatchMode = "Legacy overlap",
                        MatchThreshold = 0.44,
                        FreeformAxisThreshold = 0.66,
                        EnableAutoDetectXy = true,
                        EnableFreeformEdgeSpecialization = true,
                        EnableCentroidFallback = false,
                        NearestK = 7,
                    },
                },
                ApplyUiToSettings: settings =>
                {
                    settings.Matching.MatchThreshold = 0.44;
                    settings.Matching.FreeformAxisThreshold = 0.66;
                    settings.Matching.EnableAutoDetectXy = true;
                    settings.Matching.EnableFreeformEdgeSpecialization = true;
                    settings.Matching.EnableCentroidFallback = false;
                    settings.Matching.NearestK = 7;
                    settings.IndexMapping.CandidateNumber = 36;
                    settings.IndexMapping.CandidatePaddingCells = 3;
                },
                HasCadLoaded: false));

            Assert.True(result.IsSaved);

            var loaded = JsonProjectStore.Load(projectPath);
            Assert.Equal(0.44, loaded.Settings.Matching.MatchThreshold, 6);
            Assert.Equal(0.66, loaded.Settings.Matching.FreeformAxisThreshold, 6);
            Assert.True(loaded.Settings.Matching.EnableAutoDetectXy);
            Assert.True(loaded.Settings.Matching.EnableFreeformEdgeSpecialization);
            Assert.False(loaded.Settings.Matching.EnableCentroidFallback);
            Assert.Equal(7, loaded.Settings.Matching.NearestK);
            Assert.Equal(36, loaded.Settings.IndexMapping.CandidateNumber);
            Assert.Equal(3, loaded.Settings.IndexMapping.CandidatePaddingCells);

            Assert.Equal("Legacy overlap", loaded.UiSnapshot.Matching.MatchMode);
            Assert.Equal(0.44, loaded.UiSnapshot.Matching.MatchThreshold, 6);
            Assert.True(loaded.UiSnapshot.Matching.EnableAutoDetectXy);
            Assert.True(loaded.UiSnapshot.Matching.EnableFreeformEdgeSpecialization);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }

    [Fact]
    public async Task SaveThenLoadAsync_RoundTrip_PreservesCriticalProjectData()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonProjectStore();
            var useCase = new ProjectPersistenceUseCase(store, new PadOverrideService());
            var file = new ProjectFile
            {
                LastDxfPath = @"C:\temp\sample.dxf",
                EmbeddedDxf = new byte[] { 10, 20, 30 },
                EmbeddedDxfName = "sample.dxf",
            };

            var grid = BuildTestGrid(rows: 1, cols: 2);
            grid.Pads[1].Freeform = FreeformType.YWay;
            var cadValues = new Dictionary<int, double> { [42] = 1.25 };

            var save = await useCase.SaveAsync(new ProjectSaveRequest(
                Project: file,
                Grid: grid,
                CadPadCustomValues: cadValues,
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync: null,
                BuildUiSnapshot: () => new ProjectUiSnapshot
                {
                    View = new UiViewSnapshot
                    {
                        ShowCad = false,
                        ShowRegular = true,
                        CadLineWidth = 1.75,
                        CadLineColor = "#123456",
                    },
                    Matching = new UiMatchingSnapshot
                    {
                        MatchMode = "Legacy overlap",
                        MatchThreshold = 0.61,
                        FreeformAxisThreshold = 0.72,
                    },
                },
                ApplyUiToSettings: settings =>
                {
                    settings.Grid.XChannels = 88;
                    settings.Grid.YChannels = 33;
                    settings.Matching.MatchThreshold = 0.61;
                    settings.Matching.FreeformAxisThreshold = 0.72;
                },
                HasCadLoaded: false));

            Assert.True(save.IsSaved);

            var load = await useCase.LoadAsync(new ProjectLoadRequest(
                PickLoadProjectPathAsync: () => Task.FromResult<string?>(projectPath),
                CanProceedWithPendingEdits: () => true));

            Assert.True(load.IsLoaded);
            Assert.NotNull(load.Project);
            Assert.Equal(projectPath, load.Path);

            var loaded = load.Project!;
            Assert.Equal(88, loaded.Settings.Grid.XChannels);
            Assert.Equal(33, loaded.Settings.Grid.YChannels);
            Assert.Equal(0.61, loaded.Settings.Matching.MatchThreshold, 6);
            Assert.Equal(0.72, loaded.Settings.Matching.FreeformAxisThreshold, 6);
            Assert.Equal(1.25, loaded.CadPadCustomValues[42], 6);
            Assert.Equal(FreeformType.YWay, loaded.FreeformOverrides[1]);
            Assert.Equal("Legacy overlap", loaded.UiSnapshot.Matching.MatchMode);
            Assert.Equal(0.61, loaded.UiSnapshot.Matching.MatchThreshold, 6);
            Assert.Equal("#123456", loaded.UiSnapshot.View.CadLineColor);
            Assert.False(loaded.UiSnapshot.View.ShowCad);
            Assert.Equal("sample.dxf", loaded.EmbeddedDxfName);
            Assert.Equal(new byte[] { 10, 20, 30 }, loaded.EmbeddedDxf);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }

    [Fact]
    public async Task SaveThenLoadAsync_RoundTrip_PreservesDxfEditState()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonProjectStore();
            var useCase = new ProjectPersistenceUseCase(store, new PadOverrideService());
            var file = new ProjectFile
            {
                HiddenCadPadIds = new HashSet<int> { 2, 1500000000 },
                DxfCadLayerOverrides = new Dictionary<int, string>
                {
                    [3] = "MovedLayer",
                },
                DxfCadGeometryOverrides = new Dictionary<int, ProjectCadGeometrySnapshot>
                {
                    [3] = new ProjectCadGeometrySnapshot
                    {
                        Vertices =
                        [
                            new Point2(20, 0),
                            new Point2(30, 0),
                            new Point2(30, 10),
                            new Point2(20, 10),
                        ],
                    },
                },
                DxfCombinedCadGroups =
                [
                    new ProjectDxfCombinedCadGroup
                    {
                        GroupId = 4,
                        SourceCadIds = new List<int> { 1, 2 },
                        OutputPads =
                        [
                            new ProjectCadPadSnapshot
                            {
                                Id = 1_500_000_000,
                                Name = "COMB_1500000000",
                                Layer = "L1",
                                Vertices = new List<Point2>
                                {
                                    new(0, 0),
                                    new(10, 0),
                                    new(10, 10),
                                    new(0, 10),
                                },
                            },
                        ],
                    },
                ],
            };

            var save = await useCase.SaveAsync(new ProjectSaveRequest(
                Project: file,
                Grid: null,
                CadPadCustomValues: new Dictionary<int, double>(),
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync: null,
                BuildUiSnapshot: () => new ProjectUiSnapshot(),
                ApplyUiToSettings: _ => { },
                HasCadLoaded: false));

            Assert.True(save.IsSaved);

            var load = await useCase.LoadAsync(new ProjectLoadRequest(
                PickLoadProjectPathAsync: () => Task.FromResult<string?>(projectPath),
                CanProceedWithPendingEdits: () => true));

            Assert.True(load.IsLoaded);
            var loaded = Assert.IsType<ProjectFile>(load.Project);
            Assert.Equal(new HashSet<int> { 2, 1_500_000_000 }, loaded.HiddenCadPadIds);
            Assert.Equal("MovedLayer", loaded.DxfCadLayerOverrides[3]);
            Assert.Equal(4, loaded.DxfCadGeometryOverrides[3].Vertices.Count);
            Assert.Equal(20, loaded.DxfCadGeometryOverrides[3].Vertices[0].X, 6);
            Assert.Equal(0, loaded.DxfCadGeometryOverrides[3].Vertices[0].Y, 6);
            var group = Assert.Single(loaded.DxfCombinedCadGroups);
            Assert.Equal(4, group.GroupId);
            Assert.Equal(ExpectedCombinedSourceCadIds, group.SourceCadIds);
            var outputPad = Assert.Single(group.OutputPads);
            Assert.Equal(1_500_000_000, outputPad.Id);
            Assert.Equal("COMB_1500000000", outputPad.Name);
            Assert.Equal("L1", outputPad.Layer);
            Assert.Equal(4, outputPad.Vertices.Count);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_PublisherFailure_AwaitsPublicationAndPropagatesFailure()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            var publication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new JsonProjectStore
            {
                Publisher = (_, _, _) => publication.Task,
            };
            var useCase = new ProjectPersistenceUseCase(store, new PadOverrideService());

            var saving = useCase.SaveAsync(new ProjectSaveRequest(
                Project: new ProjectFile(),
                Grid: null,
                CadPadCustomValues: new Dictionary<int, double>(),
                PickSaveProjectPathAsync: () => Task.FromResult<string?>(path),
                ConfirmEmbedDxfAsync: null,
                BuildUiSnapshot: () => new ProjectUiSnapshot(),
                ApplyUiToSettings: _ => { },
                HasCadLoaded: false));

            var completedBeforePublication = saving.IsCompleted;
            var failure = new IOException("Publication failed.");
            publication.SetException(failure);

            var actual = await Assert.ThrowsAsync<IOException>(() => saving);
            Assert.Same(failure, actual);
            Assert.False(completedBeforePublication);
            Assert.Empty(Directory.GetFileSystemEntries(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static RegularGrid BuildTestGrid(int rows, int cols)
    {
        var xEdges = new double[cols + 1];
        for (var c = 0; c <= cols; c++)
        {
            xEdges[c] = c;
        }

        var yEdges = new double[rows + 1];
        for (var r = 0; r <= rows; r++)
        {
            yEdges[r] = r;
        }

        var pads = new List<RegularPad>(rows * cols);
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var idx = r * cols + c;
                var poly = new Polygon2(new[]
                {
                    new Point2(xEdges[c], yEdges[r]),
                    new Point2(xEdges[c + 1], yEdges[r]),
                    new Point2(xEdges[c + 1], yEdges[r + 1]),
                    new Point2(xEdges[c], yEdges[r + 1]),
                });
                pads.Add(new RegularPad(r, c, idx, poly));
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }
}

