using System.Reflection;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [ExampleDataFact]
    public async Task LoadProject_AppliesSavedRegularVisibilityMaskSnapshot_AndToggleCanRestorePureGeometrySeed()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(vm.IsRegularVisibilityMaskEnabled);
        Assert.Contains("enabled", vm.RegularVisibilityMaskSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(4848, out var maskedDiff4848));
        Assert.Equal(324, maskedDiff4848);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(4823, out var maskedDiff4823));
        Assert.Equal(323, maskedDiff4823);
        Assert.True(vm.TryGetCadDisplayIndex(4823, out var displayIndex4823));

        vm.IsRegularVisibilityMaskEnabled = false;

        Assert.False(vm.IsRegularVisibilityMaskEnabled);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(4848, out var pureGeometryDiff4848));
        Assert.Equal(325, pureGeometryDiff4848);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(4823, out var pureGeometryDiff4823));
        Assert.Equal(324, pureGeometryDiff4823);
        Assert.True(vm.TryGetCadDisplayIndex(4823, out var pureGeometryDisplayIndex4823));
        Assert.Equal(displayIndex4823, pureGeometryDisplayIndex4823);

        vm.IsRegularVisibilityMaskEnabled = true;

        Assert.True(vm.TryGetCadOutputFwDiffIndex(4848, out var restoredDiff4848));
        Assert.Equal(324, restoredDiff4848);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(4823, out var restoredDiff4823));
        Assert.Equal(323, restoredDiff4823);
        Assert.True(vm.TryGetCadDisplayIndex(4823, out var restoredDisplayIndex4823));
        Assert.Equal(displayIndex4823, restoredDisplayIndex4823);
    }

    [ExampleDataFact]
    public async Task SaveThenLoadProject_WithoutEmbeddedDxf_PersistsRegularVisibilityMaskSourcePathAndToggle_WithoutRuntimeReload()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        var csvPath = Path.Combine(repoRoot, "example", "BOE36.35", "SeeRegular.csv");
        var savePath = Path.Combine(Path.GetTempPath(), $"freeform-helper-mask-{Guid.NewGuid():N}.json");
        var missingDxfPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-missing-{Guid.NewGuid():N}.dxf");

        try
        {
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);
            vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(csvPath);
            await vm.LoadProjectCommand.ExecuteAsync(null);
            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

            Assert.True(vm.IsRegularVisibilityMaskEnabled);
            Assert.True(vm.TryGetCadOutputFwDiffIndex(4823, out var beforeSaveDiff));
            Assert.Equal(323, beforeSaveDiff);

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(savePath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            var savedProjectText = await File.ReadAllTextAsync(savePath);
            using (var doc = System.Text.Json.JsonDocument.Parse(savedProjectText))
            {
                var import = doc.RootElement.GetProperty("uiSnapshot").GetProperty("import");
                Assert.Equal(csvPath, import.GetProperty("regularVisibilityMaskSourcePath").GetString());
                Assert.True(import.GetProperty("useRegularVisibilityMask").GetBoolean());
            }

            var savedProjectNode = System.Text.Json.Nodes.JsonNode.Parse(savedProjectText);
            Assert.NotNull(savedProjectNode);
            savedProjectNode!["lastDxfPath"] = missingDxfPath;
            savedProjectNode["embeddedDxf"] = null;
            savedProjectNode["embeddedDxfName"] = null;
            await File.WriteAllTextAsync(savePath, savedProjectNode.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            var loadedVm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(savePath),
            };
            await loadedVm.LoadProjectCommand.ExecuteAsync(null);

            var sourcePathField = typeof(FreeformHelperViewModel).GetField(
                "_regularVisibilityMaskSourcePath",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(sourcePathField);

            Assert.True(loadedVm.IsRegularVisibilityMaskEnabled);
            Assert.Equal(csvPath, sourcePathField!.GetValue(loadedVm) as string);
            Assert.Contains("not loaded", loadedVm.RegularVisibilityMaskSummary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [ExampleDataFact]
    public async Task SaveThenLoadProject_WithEmbeddedDxf_RestoresEmbeddedRegularVisibilityMaskWhenSourcePathIsMissing()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        var csvPath = Path.Combine(repoRoot, "example", "BOE36.35", "SeeRegular.csv");
        var savePath = Path.Combine(Path.GetTempPath(), $"freeform-helper-mask-embed-{Guid.NewGuid():N}.json");
        var missingDxfPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-missing-{Guid.NewGuid():N}.dxf");
        var missingMaskPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-missing-{Guid.NewGuid():N}.csv");

        try
        {
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);
            vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(csvPath);
            await vm.LoadProjectCommand.ExecuteAsync(null);
            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

            Assert.True(vm.IsRegularVisibilityMaskEnabled);
            Assert.True(vm.TryGetCadOutputFwDiffIndex(4823, out var beforeSaveDiff));
            Assert.Equal(323, beforeSaveDiff);

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(savePath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
            Assert.True(await vm.SaveProjectAsync());

            var savedProjectText = await File.ReadAllTextAsync(savePath);
            using (var doc = System.Text.Json.JsonDocument.Parse(savedProjectText))
            {
                Assert.True(doc.RootElement.TryGetProperty("embeddedRegularVisibilityMask", out var embeddedMask));
                Assert.True(embeddedMask.GetBytesFromBase64().Length > 0);
                Assert.Equal("SeeRegular.csv", doc.RootElement.GetProperty("embeddedRegularVisibilityMaskName").GetString());
            }

            var savedProjectNode = System.Text.Json.Nodes.JsonNode.Parse(savedProjectText);
            Assert.NotNull(savedProjectNode);
            savedProjectNode!["lastDxfPath"] = missingDxfPath;
            savedProjectNode["uiSnapshot"]!["import"]!["regularVisibilityMaskSourcePath"] = missingMaskPath;
            await File.WriteAllTextAsync(savePath, savedProjectNode.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            var loadedVm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(savePath),
            };
            await loadedVm.LoadProjectCommand.ExecuteAsync(null);

            Assert.True(loadedVm.IsRegularVisibilityMaskEnabled);
            Assert.Contains("enabled", loadedVm.RegularVisibilityMaskSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not loaded", loadedVm.RegularVisibilityMaskSummary, StringComparison.OrdinalIgnoreCase);
            Assert.True(loadedVm.TryGetCadOutputFwDiffIndex(4823, out var loadedDiff));
            Assert.Equal(323, loadedDiff);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [ExampleDataFact]
    public async Task AnalyzeIndexMapping_WhenRegularMaskLoaded_OpensReportWithMaskAudit()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "TM8.1.json");
        var csvPath = Path.Combine(TestPaths.RepoRoot, "example", "TM 8.1", "SeeRegular.csv");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(csvPath);

        IndexMappingReportViewModel? openedReport = null;
        vm.OpenIndexMappingReportAsync = reportVm =>
        {
            openedReport = reportVm;
            return Task.CompletedTask;
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.NotNull(openedReport);
        Assert.True(openedReport!.HasMaskAudit);
        Assert.Contains("mask", vm.DxfRegularMappingSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.StatusText.Length <= 100, $"Workspace header status should be clamped, got {vm.StatusText.Length}: {vm.StatusText}");
        Assert.Contains("Diagnostics", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.True(openedReport.MaskAuditRows.Count > 0);
    }

    [ExampleDataFact]
    public async Task CreateSimulationWorkspaceSessionAsync_BuildsSimulationWorkspaceFor3635UsingSavedMaskState()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var session = await vm.CreateSimulationWorkspaceSessionAsync();

        Assert.NotNull(session);
        Assert.Contains("ready", vm.LastSimulationWorkspaceAttemptMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("none", vm.Step4DuplicateDiffSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.IsRegularVisibilityMaskEnabled);
        Assert.NotEqual(session!.Grid.Pads.Count, session.ActiveRegularPadIds.Count);
        Assert.True(session.ActiveRegularPadIds.Count > 0);
    }

    [ExampleDataFact]
    public async Task CreateSimulationWorkspaceSessionAsync_WhenRegularMaskEnabled_SplitsFwAndCadOutputFwDiffIdentity()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        var csvPath = Path.Combine(repoRoot, "example", "BOE36.35", "SeeRegular.csv");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(csvPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        var session = await vm.CreateSimulationWorkspaceSessionAsync();

        Assert.NotNull(session);
        var reg3843 = Assert.Single(session!.FwDiffGrid.Pads, static pad => pad.RegularPadId == 3843);
        var reg3844 = Assert.Single(session.FwDiffGrid.Pads, static pad => pad.RegularPadId == 3844);
        var reg3845 = Assert.Single(session.FwDiffGrid.Pads, static pad => pad.RegularPadId == 3845);
        var cadPadReg3843 = Assert.Single(session.CadOutputFwDiffGrid.Pads, static pad => pad.RegularPadId == 3843);
        var cadPadReg3844 = Assert.Single(session.CadOutputFwDiffGrid.Pads, static pad => pad.RegularPadId == 3844);
        var cadPadReg3845 = Assert.Single(session.CadOutputFwDiffGrid.Pads, static pad => pad.RegularPadId == 3845);
        var v22Cad4823 = Assert.Single(session.Table.Rows, static row =>
            row.Version == FreeformHelper.Domain.Notch.NotchAlgorithmVersion.V22 &&
            row.CadPadId == 4823);
        var v22Cad4848 = Assert.Single(session.Table.Rows, static row =>
            row.Version == FreeformHelper.Domain.Notch.NotchAlgorithmVersion.V22 &&
            row.CadPadId == 4848);
        Assert.Equal(323, reg3843.DiffIndex);
        Assert.Equal(324, reg3844.DiffIndex);
        Assert.Equal(325, reg3845.DiffIndex);
        Assert.Equal(323, cadPadReg3843.DiffIndex);
        Assert.Equal(323, cadPadReg3844.DiffIndex);
        Assert.Equal(324, cadPadReg3845.DiffIndex);
        Assert.Equal(323, v22Cad4823.DiffIndex);
        Assert.Equal(324, v22Cad4848.DiffIndex);
        Assert.Equal(324, v22Cad4823.V22Node!.TargetDiffIndex1);
        Assert.Equal(325, v22Cad4848.V22Node!.TargetDiffIndex1);
    }

    [ExampleDataFact]
    public async Task CreateSimulationWorkspaceSessionAsync_WhenRegularMaskEnabled_UsesMaskLimitedActiveSet()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        var csvPath = Path.Combine(repoRoot, "example", "BOE36.35", "SeeRegular.csv");
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(csvPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        var session = await vm.CreateSimulationWorkspaceSessionAsync();

        Assert.NotNull(session);
        Assert.NotEqual(session!.Grid.Pads.Count, session.ActiveRegularPadIds.Count);
        Assert.True(session.ActiveRegularPadIds.Count > 0);
        Assert.All(session.ActiveRegularPadIds, id => Assert.Contains(session.Grid.Pads, pad => pad.RegularPadId == id));
        Assert.Contains(session.Grid.Pads, pad => !session.ActiveRegularPadIds.Contains(pad.RegularPadId));
    }

    [ExampleDataFact]
    public async Task CreateSimulationWorkspaceSessionAsync_WhenRegularMaskEnabledButEmpty_KeepsEmptyActiveSet()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var maskField = typeof(FreeformHelperViewModel).GetField(
            "_loadedRegularVisibilityMaskPadIds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(maskField);
        maskField!.SetValue(vm, new HashSet<int>());
        vm.IsRegularVisibilityMaskEnabled = true;

        var session = await vm.CreateSimulationWorkspaceSessionAsync();

        Assert.NotNull(session);
        Assert.Empty(session!.ActiveRegularPadIds);
    }

    [ExampleDataFact]
    public async Task CreateCoordinatePlannerWorkspaceSessionAsync_IncludesHiddenLayerOptions()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var targetLayer = vm.LayerToggles.FirstOrDefault(static toggle => toggle.IsSelected);
        Assert.NotNull(targetLayer);
        var targetLayerName = targetLayer!.Name;

        targetLayer.IsSelected = false;

        var session = await vm.CreateCoordinatePlannerWorkspaceSessionAsync();

        Assert.NotNull(session);
        Assert.Contains(targetLayerName, session!.VisibleLayerNames, StringComparer.OrdinalIgnoreCase);
    }
}
