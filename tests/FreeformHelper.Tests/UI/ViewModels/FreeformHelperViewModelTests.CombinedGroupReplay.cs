using System.Reflection;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void ApplyPersistedDxfEditProjectState_SkipsACombinedGroupWhoseOutputPadHasTooFewVertices()
    {
        var vm = CreateViewModelWithTwoSourcePads();

        ApplyPersistedCombinedGroups(vm, CreateCombinedGroup(1, outputPadId: 3, vertexCount: 2));

        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.False(vm.HasCombinedCadPads);
    }

    [Fact]
    public void ApplyPersistedDxfEditProjectState_ReplaysTheValidGroupsAroundAnInvalidOne()
    {
        var vm = CreateViewModelWithTwoSourcePads();

        ApplyPersistedCombinedGroups(
            vm,
            CreateCombinedGroup(1, outputPadId: 3, vertexCount: 2),
            CreateCombinedGroup(2, outputPadId: 4, vertexCount: 4));

        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.True(vm.HasCombinedCadPads);
    }

    private static FreeformHelperViewModel CreateViewModelWithTwoSourcePads()
    {
        var vm = new FreeformHelperViewModel();
        var source = new CadPadSet(new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 8, 0, 18, 10),
        });
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var resetBaselineMethod = typeof(FreeformHelperViewModel).GetMethod("ResetDxfEditBaseline", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(resetBaselineMethod);

        cadField!.SetValue(vm, source);
        resetBaselineMethod!.Invoke(vm, new object?[] { source });
        return vm;
    }

    private static void ApplyPersistedCombinedGroups(FreeformHelperViewModel vm, params ProjectDxfCombinedCadGroup[] groups)
    {
        var applyMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyPersistedDxfEditProjectState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyMethod);

        applyMethod!.Invoke(vm, new object?[] { new ProjectFile { DxfCombinedCadGroups = [.. groups] } });
    }

    private static ProjectDxfCombinedCadGroup CreateCombinedGroup(int groupId, int outputPadId, int vertexCount)
    {
        var vertices = new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        };

        return new ProjectDxfCombinedCadGroup
        {
            GroupId = groupId,
            SourceCadIds = [1, 2],
            OutputPads =
            [
                new ProjectCadPadSnapshot
                {
                    Id = outputPadId,
                    Name = $"COMBINED{outputPadId}",
                    Layer = "L1",
                    Vertices = [.. vertices.Take(vertexCount)],
                },
            ],
        };
    }
}
