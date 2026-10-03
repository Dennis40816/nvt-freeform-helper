using System.Reflection;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class FreeformHelperViewModelTests
{
    private static readonly int[] SingleCadSelection = [1];
    private static readonly int[] PairCadSelection = [1, 2];
    private static readonly int[] SelectedCad23 = [2, 3];
    private static readonly double[] ShiftGridXEdges = [0d, 10d, 20d, 30d];
    private static readonly double[] ShiftGridYEdges = [0d, 10d];
    private static readonly (int CadPadId, int RegularPadId)[] ShiftVisibleCadMatches =
    [
        (1, 100),
        (2, 101),
        (3, 102),
    ];

    private static CadPad CreateCad(int id, string layer, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new CadPad(id, $"CAD{id}", layer, polygon);
    }

    private static void InitializeDxfEditCadState(FreeformHelperViewModel vm, IReadOnlyList<CadPad> source)
    {
        var cad = new CadPadSet(source);
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var updateLayerTogglesMethod = typeof(FreeformHelperViewModel).GetMethod("UpdateLayerToggles", BindingFlags.Instance | BindingFlags.NonPublic);
        var resetBaselineMethod = typeof(FreeformHelperViewModel).GetMethod("ResetDxfEditBaseline", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(updateLayerTogglesMethod);
        Assert.NotNull(resetBaselineMethod);

        cadField!.SetValue(vm, cad);
        resetBaselineMethod!.Invoke(vm, new object?[] { cad });
        updateLayerTogglesMethod!.Invoke(vm, new object?[] { cad });
    }

    private static CadPadSet GetCurrentCadPadSet(FreeformHelperViewModel vm)
    {
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        return Assert.IsType<CadPadSet>(cadField!.GetValue(vm));
    }

    private static int[] GetCombinedCadPadIds(FreeformHelperViewModel vm)
    {
        var cad = GetCurrentCadPadSet(vm);
        return cad.Pads
            .Where(static pad => pad.Name.StartsWith("COMB_", StringComparison.Ordinal))
            .Select(static pad => pad.Id)
            .ToArray();
    }

    private static void InitializeVisibleCadIndexing(FreeformHelperViewModel vm, IReadOnlyList<CadPad> source, RegularGrid grid)
    {
        var cad = new CadPadSet(source);
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        var projectFileField = typeof(FreeformHelperViewModel).GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);
        Assert.NotNull(projectFileField);

        cadField!.SetValue(vm, cad);
        gridField!.SetValue(vm, grid);
        vm.CadPads = new System.Collections.ObjectModel.ObservableCollection<CadPad>(source);

        var projectFile = Assert.IsType<ProjectFile>(projectFileField!.GetValue(vm));
        projectFile.Settings.Grid.XChannels = grid.Cols;
        projectFile.Settings.Grid.YChannels = grid.Rows;
        projectFile.Settings.Grid.ActiveAreaWidth = grid.XEdges[^1] - grid.XEdges[0];
        projectFile.Settings.Grid.ActiveAreaHeight = grid.YEdges[^1] - grid.YEdges[0];
    }

    private static RegularPad CreateRegularPad(int regularPadId, int row, int col, double minX, double minY, double maxX, double maxY, int diffIndex)
    {
        var pad = CreateRectRegular(row, col, regularPadId, minX, minY, maxX, maxY);
        pad.IcIndex = 0;
        pad.DiffIndex = diffIndex;
        return pad;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(target));
    }
}
