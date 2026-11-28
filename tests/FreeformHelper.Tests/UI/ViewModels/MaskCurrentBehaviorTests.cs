using System.Collections.ObjectModel;
using System.Text;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MaskCurrentBehaviorTests
{
    [Fact]
    public async Task LoadProject_CurrentBehavior_MissingMaskStaysEnabledWithNoMaskAndFullSimulationGrid()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var missingMaskPath = Path.Combine(directory.FullName, "missing.csv");
            var projectPath = Path.Combine(directory.FullName, "project.json");
            await SaveMissingMaskProjectAsync(projectPath, missingMaskPath);
            Assert.False(File.Exists(missingMaskPath));
            Assert.Null(JsonProjectStore.Load(projectPath).EmbeddedRegularVisibilityMask);

            var vm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await vm.LoadProjectCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();

            Assert.True(vm.RegularMask.IsEnabled);
            var mask = vm.RegularMask.Workflow.Mask;
            Assert.Equal(RegularVisibilityMaskStatus.NotFound, mask.Status);
            Assert.False(mask.HasLoadedMask);
            Assert.Equal(missingMaskPath, mask.SourcePath);
            Assert.Equal(0, mask.ActiveRegularCount);
            Assert.Contains("not loaded", vm.RegularMask.Summary, StringComparison.Ordinal);

            var grid = Assert.IsType<RegularGrid>(FreeformHelperViewModelPrivateAccess.Grid(vm));
            Assert.Equal(1, grid.Rows);
            Assert.Equal(2, grid.Cols);
            var session = await vm.CreateSimulationWorkspaceSessionAsync(silent: false);

            Assert.NotNull(session);
            Assert.NotEmpty(session.Table.Rows);
            Assert.Equal(2, session.Grid.Pads.Count);
            Assert.Equal(2, session.ActiveRegularPadIds.Count);
            Assert.True(session.ActiveRegularPadIds.SetEquals(grid.Pads.Select(static pad => pad.RegularPadId)));
            Assert.True(vm.RegularMask.IsEnabled);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task SaveMissingMaskProjectAsync(string projectPath, string missingMaskPath)
    {
        await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile
        {
            EmbeddedDxfName = "synthetic.dxf",
            EmbeddedDxf = Encoding.UTF8.GetBytes("""
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
                """),
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
                Import = new UiImportSnapshot
                {
                    RegularVisibilityMaskSourcePath = missingMaskPath,
                    UseRegularVisibilityMask = true,
                },
            },
        }, CancellationToken.None);
    }

    [Fact]
    public async Task ImportMask_CurrentBehavior_AlreadyEnabledKeepsGeometryDiffUntilToggledOffAndOn()
    {
        var initialPath = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,10,0\n");
        var changedPath = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,0,10\n");
        try
        {
            var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 20]);
            // Unequal overlaps make the geometry seed independent of ordering or tie-breaking.
            var pad = TestGeometryFactory.CreateCadPad(101, "L1", 0.1, 0.1, 1.5, 0.9);
            var cad = new CadPadSet([pad]);
            var vm = new FreeformHelperViewModel
            {
                CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
            };
            await vm.WaitForGridRebuildIdleAsync();
            FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;
            FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
            vm.WorkflowDataForTests.LatestPadMatch = PadMatcher.Match(cad, grid);
            vm.CadPads = new ObservableCollection<CadPad>([pad]);
            vm.RegularPads = new ObservableCollection<RegularPad>(grid.Pads);
            vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(initialPath);

            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

            Assert.True(vm.RegularMask.IsEnabled);
            var initialMask = vm.RegularMask.Workflow.Mask;
            Assert.Equal(RegularVisibilityMaskStatus.Loaded, initialMask.Status);
            Assert.Equal(0, Assert.Single(initialMask.ActiveRegularPadIds));
            Assert.True(vm.TryGetCadOutputFwDiffIndex(pad.Id, out var geometryDiff));
            Assert.Equal(10, geometryDiff);

            vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(changedPath);
            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

            var changedMask = vm.RegularMask.Workflow.Mask;
            Assert.True(vm.RegularMask.IsEnabled);
            Assert.Equal(RegularVisibilityMaskStatus.Loaded, changedMask.Status);
            Assert.Equal(changedPath, changedMask.SourcePath);
            Assert.Equal(1, changedMask.ActiveRegularCount);
            Assert.Equal(1, Assert.Single(changedMask.ActiveRegularPadIds));
            Assert.True(vm.TryGetCadOutputFwDiffIndex(pad.Id, out var staleDiff));
            Assert.Equal(10, staleDiff);

            vm.RegularMask.IsEnabled = false;
            Assert.True(vm.TryGetCadOutputFwDiffIndex(pad.Id, out var disabledDiff));
            Assert.Equal(10, disabledDiff);

            vm.RegularMask.IsEnabled = true;
            Assert.True(vm.TryGetCadOutputFwDiffIndex(pad.Id, out var refreshedDiff));
            Assert.Equal(20, refreshedDiff);
        }
        finally
        {
            File.Delete(initialPath);
            File.Delete(changedPath);
        }
    }
}
