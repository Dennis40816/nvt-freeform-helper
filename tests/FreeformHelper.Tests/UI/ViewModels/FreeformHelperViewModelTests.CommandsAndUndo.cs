using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class FreeformHelperViewModelTests
{
    private static readonly int[] _singleCadSelection = [1];
    private static readonly int[] _pairCadSelection = [1, 2];
    private static readonly int[] _selectedCad23 = [2, 3];
    private static readonly double[] _shiftGridXEdges = [0d, 10d, 20d, 30d];
    private static readonly double[] _shiftGridYEdges = [0d, 10d];
    private static readonly (int CadPadId, int RegularPadId)[] _shiftVisibleCadMatches =
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

        FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
        FreeformHelperViewModelPrivateAccess.ResetDxfEditBaseline(vm, cad);
        FreeformHelperViewModelPrivateAccess.UpdateLayerToggles(vm, cad);
    }

    private static CadPadSet GetCurrentCadPadSet(FreeformHelperViewModel vm)
    {
        return Assert.IsType<CadPadSet>(FreeformHelperViewModelPrivateAccess.Cad(vm));
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

        FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;
        vm.CadPads = new System.Collections.ObjectModel.ObservableCollection<CadPad>(source);

        var projectFile = Assert.IsType<ProjectFile>(FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
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
}
