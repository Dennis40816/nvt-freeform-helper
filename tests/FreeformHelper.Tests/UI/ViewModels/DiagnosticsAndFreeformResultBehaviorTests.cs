using System.Text;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DiagnosticsAndFreeformResultBehaviorTests
{
    [Fact]
    public void NewViewModel_BeforeAnyRun_ShowsNotRunDiagnosticsAndFreeformStats()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal("Diagnostics: not run.", vm.DxfRegularMappingSummary);
        Assert.Empty(vm.DxfRegularMappingIssues);
        Assert.False(vm.HasDxfRegularMappingIssues);
        Assert.Equal("Freeform stats: not run.", vm.FreeformAutoDetectSummary);
        Assert.Empty(vm.FreeformAutoDetectRows);
        Assert.False(vm.HasFreeformAutoDetectRows);
    }

    [Fact]
    public async Task LoadProject_TwoRegularPads_ShowsFreeformStatsRowsAndSummary()
    {
        using var project = await ProjectFixture.LoadAsync();

        Assert.Equal("Freeform stats (project load auto-detect): total=2, tagged=2, none=0.", project.Vm.FreeformAutoDetectSummary);
        Assert.Equal(["None", "XWay", "YWay", "XYWay"], project.Vm.FreeformAutoDetectRows.Select(static row => row.Type));
        Assert.True(project.Vm.HasFreeformAutoDetectRows);
    }

    [Fact]
    public async Task ClearStep2Result_AfterProjectLoad_ResetsFreeformStats()
    {
        using var project = await ProjectFixture.LoadAsync();

        project.Vm.ClearStep2ResultCommand.Execute(null);

        Assert.Equal("Freeform stats: not run.", project.Vm.FreeformAutoDetectSummary);
        Assert.Empty(project.Vm.FreeformAutoDetectRows);
        Assert.False(project.Vm.HasFreeformAutoDetectRows);
    }

    [Fact]
    public async Task ClearStep2Result_AfterProjectLoad_RaisesTheSummaryTheRowsAndTheFlag()
    {
        using var project = await ProjectFixture.LoadAsync();
        var changes = RecordNotifications(
            project.Vm,
            nameof(FreeformHelperViewModel.FreeformAutoDetectSummary),
            nameof(FreeformHelperViewModel.FreeformAutoDetectRows),
            nameof(FreeformHelperViewModel.HasFreeformAutoDetectRows));

        project.Vm.ClearStep2ResultCommand.Execute(null);

        Assert.Contains(nameof(FreeformHelperViewModel.FreeformAutoDetectSummary), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.FreeformAutoDetectRows), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.HasFreeformAutoDetectRows), changes);
    }

    [Fact]
    public void ClearStep2Result_WithoutProject_RaisesOnlyTheRowsProperty()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(
            vm,
            nameof(FreeformHelperViewModel.FreeformAutoDetectSummary),
            nameof(FreeformHelperViewModel.FreeformAutoDetectRows),
            nameof(FreeformHelperViewModel.HasFreeformAutoDetectRows));

        vm.ClearStep2ResultCommand.Execute(null);

        Assert.Contains(nameof(FreeformHelperViewModel.FreeformAutoDetectRows), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.FreeformAutoDetectSummary), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.HasFreeformAutoDetectRows), changes);
    }

    [Fact]
    public async Task AnalyzeIndexMapping_NothingLoaded_KeepsNotRunDiagnostics()
    {
        var vm = new FreeformHelperViewModel();

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.Equal("Diagnostics: not run.", vm.DxfRegularMappingSummary);
        Assert.Empty(vm.DxfRegularMappingIssues);
        Assert.False(vm.HasDxfRegularMappingIssues);
    }

    [Fact]
    public async Task AnalyzeIndexMapping_LoadedProject_ShowsSummaryAndIssuesFromTheSameReport()
    {
        using var project = await ProjectFixture.LoadAsync();

        await project.Vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.StartsWith("Diagnostics", project.Vm.DxfRegularMappingSummary, StringComparison.Ordinal);
        Assert.NotEqual("Diagnostics: not run.", project.Vm.DxfRegularMappingSummary);
        Assert.Equal(project.Vm.DxfRegularMappingIssues.Count > 0, project.Vm.HasDxfRegularMappingIssues);
    }

    [Fact]
    public async Task ClearStep4Result_AfterAnalysis_ResetsDiagnostics()
    {
        using var project = await ProjectFixture.LoadAsync();
        await project.Vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        project.Vm.ClearStep4ResultCommand.Execute(null);

        Assert.Equal("Diagnostics: not run.", project.Vm.DxfRegularMappingSummary);
        Assert.Empty(project.Vm.DxfRegularMappingIssues);
        Assert.False(project.Vm.HasDxfRegularMappingIssues);
    }

    [Fact]
    public async Task ClearStep4Result_AfterAnalysis_RaisesTheSummary()
    {
        using var project = await ProjectFixture.LoadAsync();
        await project.Vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);
        var changes = RecordNotifications(
            project.Vm,
            nameof(FreeformHelperViewModel.DxfRegularMappingSummary));

        project.Vm.ClearStep4ResultCommand.Execute(null);

        Assert.Contains(nameof(FreeformHelperViewModel.DxfRegularMappingSummary), changes);
    }

    [Fact]
    public void ClearStep4Result_WithoutAnalysis_RaisesNoSummaryOrFlagNotification()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(
            vm,
            nameof(FreeformHelperViewModel.DxfRegularMappingSummary),
            nameof(FreeformHelperViewModel.HasDxfRegularMappingIssues));

        vm.ClearStep4ResultCommand.Execute(null);

        Assert.Empty(changes);
    }

    private sealed class ProjectFixture : IDisposable
    {
        private readonly DirectoryInfo _directory;

        private ProjectFixture(DirectoryInfo directory, FreeformHelperViewModel vm)
        {
            _directory = directory;
            Vm = vm;
        }

        public FreeformHelperViewModel Vm { get; }

        public static async Task<ProjectFixture> LoadAsync()
        {
            var directory = TestFiles.CreateTempDirectory();
            var projectPath = Path.Combine(directory.FullName, "project.json");
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
                },
            }, CancellationToken.None);
            var vm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await vm.LoadProjectCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();
            return new ProjectFixture(directory, vm);
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
