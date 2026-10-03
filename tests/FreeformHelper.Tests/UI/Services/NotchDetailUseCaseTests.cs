using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDetailUseCaseTests
{
    [Fact]
    public void Build_CrossIcCompatibilityEntry_RemainsUnanchoredWhileResolvedProjectionUsesWorkflowIc()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(
            1,
            2,
            cellWidth: 10,
            cellHeight: 10,
            diffIndexSelector: static (_, col) => col == 0 ? 10 : 20,
            icIndexSelector: static (_, col) => col);
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = 7;
            regular.MatchScore = 1.0;
        }

        var cad = TestGeometryFactory.CreateCadPad(7, "signal", 0, 0, 20, 10);
        var settings = new ProjectSettings();
        var useCase = new NotchDetailUseCase();

        var compatibilityDetail = useCase.Build(cad, dxfIndex: 10, grid, settings);
        var resolved = useCase.BuildResolvedResult(
            cad,
            NotchDetailUseCase.BuildCompensation(cad, grid, settings),
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 10,
            allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage);
        var resolvedDetail = NotchDetailUseCase.BuildFromResolvedResult(cad, 10, grid, settings, resolved);

        Assert.Same(cad, compatibilityDetail.CadPad);
        Assert.Equal(10, compatibilityDetail.DxfIndex);
        Assert.Equal("Combined ratio: 200.00 %", compatibilityDetail.CombinedRatioText);

        var target = Assert.Single(resolved.TargetAllocation.Targets);
        Assert.Equal(0, target.IcIndex);
        Assert.Equal(10, target.DiffIndex);
        Assert.Equal("Combined ratio: 100.00 %", resolvedDetail.CombinedRatioText);
        Assert.Equal(compatibilityDetail.CombinedRatio, resolvedDetail.CombinedRatio);
    }
}
