using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [ExampleDataFact]
    public async Task GetCadV22StageOverlays_CAD4818_ReturnsFinalOutlineCoveringFullReg384()
    {
        var vm = new FreeformHelperViewModel();
        var projectPath = Path.Combine(TestPaths.RepoRoot, "example", "BOE36.35", "project_3635.json");
        using var fixture = ExampleProjectFixture.Create(projectPath);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var overlay = vm.GetCadV22StageOverlays(4818);

        Assert.NotNull(overlay);
        Assert.True(overlay.Value.IsToFullEnabled);
        var stage2 = Assert.Single(overlay.Value.Stage2Candidate);
        Assert.Equal(2.94776, stage2.Bounds.MinX, 5);
        Assert.Equal(15.82673, stage2.Bounds.MinY, 5);
        Assert.Equal(5.49116, stage2.Bounds.MaxX, 5);
        Assert.Equal(20.34833, stage2.Bounds.MaxY, 5);

        var stage3 = Assert.Single(overlay.Value.Stage3Final);
        Assert.True(stage3.Area() > 24.2782 && stage3.Area() < 24.2784);
        Assert.Equal(2.94776, stage3.Bounds.MinX, 5);
        Assert.Equal(15.82673, stage3.Bounds.MinY, 5);
        Assert.Equal(8.31716, stage3.Bounds.MaxX, 5);
        Assert.Equal(20.34833, stage3.Bounds.MaxY, 5);
    }

}
