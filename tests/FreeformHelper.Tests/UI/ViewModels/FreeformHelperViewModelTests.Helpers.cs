using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    private static readonly double[] SinglePadGridXEdges = [0.0, 10.0];
    private static readonly double[] SinglePadGridYEdges = [0.0, 10.0];
    private static readonly double[] TwoPadGridXEdges = [0.0, 10.0, 20.0];
    private static readonly int[] SelectedCad101 = [101];
    private static readonly int[] SelectedCad101And102 = [101, 102];
    private static readonly int[] SelectedCad72 = [72];
    private static readonly int[] SelectedCad4809 = [4809];
    private static readonly int[] SelectedRegular200 = [200];
    private static readonly int[] SelectedRegular4616 = [4616];

    private static FreeformHelperViewModel CreateViewModelWithStep3PreviewReady()
    {
        var cad = CreateRectCad(101, 0, 3, 3, 7);
        var regular = CreateRectRegular(0, 0, 1001, 0, 0, 10, 10);
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>(new[] { cad }),
            RegularPads = new ObservableCollection<RegularPad>(new[] { regular }),
            EnableToFull = true,
            ShowNotchCanvasPreview = true,
            ShowNotchToRegularLabels = true,
            NotchPreviewVisualizationStep = 3m,
            NotchPreviewAutoPlayEnabled = false,
        };

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);

        cadField!.SetValue(vm, new CadPadSet(new[] { cad }));
        gridField!.SetValue(
            vm,
            new RegularGrid(
                rows: 1,
                cols: 1,
                xEdges: SinglePadGridXEdges,
                yEdges: SinglePadGridYEdges,
                pads: new[] { regular }));

        SetPadMatchResult(vm, cadPadId: 101, regularPadId: 1001);
        vm.ApplyCanvasSelection(SelectedCad101, Array.Empty<int>());
        return vm;
    }

    private static CadPad CreateSquareCad(int id, double area)
    {
        var side = Math.Sqrt(area);
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(side, 0),
            new Point2(side, side),
            new Point2(0, side),
        });

        return new CadPad(id, $"CAD{id}", "L1", polygon);
    }

    private static CadPad CreateRectCad(int id, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new CadPad(id, $"CAD{id}", "L1", polygon);
    }

    private static string CreateTempDxfFile(params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var path = Path.Combine(Path.GetTempPath(), $"freeformhelper-test-{Guid.NewGuid():N}.dxf");
        var builder = new StringBuilder();
        builder.AppendLine("0");
        builder.AppendLine("SECTION");
        builder.AppendLine("2");
        builder.AppendLine("ENTITIES");
        foreach (var pad in pads)
        {
            builder.AppendLine("0");
            builder.AppendLine("LWPOLYLINE");
            builder.AppendLine("8");
            builder.AppendLine(pad.Layer);
            builder.AppendLine("70");
            builder.AppendLine("1");
            builder.AppendLine("90");
            builder.AppendLine("4");
            AppendVertex(pad.MinX, pad.MinY);
            AppendVertex(pad.MaxX, pad.MinY);
            AppendVertex(pad.MaxX, pad.MaxY);
            AppendVertex(pad.MinX, pad.MaxY);
        }

        builder.AppendLine("0");
        builder.AppendLine("ENDSEC");
        builder.AppendLine("0");
        builder.AppendLine("EOF");
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        return path;

        void AppendVertex(double x, double y)
        {
            builder.AppendLine("10");
            builder.AppendLine(x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.AppendLine("20");
            builder.AppendLine(y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static RegularPad CreateRectRegular(int row, int col, int index, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new RegularPad(row, col, index, polygon);
    }

    private static void SetPadMatchResult(FreeformHelperViewModel vm, int cadPadId, int regularPadId)
    {
        SetPadMatchResults(vm, [(cadPadId, regularPadId)]);
    }

    private static void SetPadMatchResults(FreeformHelperViewModel vm, params (int CadPadId, int RegularPadId)[] matches)
    {
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        var regularToCad = new Dictionary<int, IReadOnlyList<PadMatchLink>>();
        foreach (var (cadPadId, regularPadId) in matches)
        {
            cadToRegular[cadPadId] = new[]
            {
                new PadMatchLink(cadPadId, regularPadId, 20.0, 0.8, 0.9)
            };
            regularToCad[regularPadId] = new[]
            {
                new PadMatchLink(cadPadId, regularPadId, 20.0, 0.8, 0.9)
            };
        }

        var field = typeof(FreeformHelperViewModel).GetField("_latestPadMatchResult", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, new PadMatchResult(cadToRegular, regularToCad));
    }

    private static void SetCadOutputFwDiff(FreeformHelperViewModel vm, int cadPadId, int diffIndex, int icIndex)
    {
        var outputDiffField = typeof(FreeformHelperViewModel).GetField("_cadOutputFwDiffIndexByCadId", BindingFlags.Instance | BindingFlags.NonPublic);
        var icField = typeof(FreeformHelperViewModel).GetField("_cadIcIndexByCadId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(outputDiffField);
        Assert.NotNull(icField);

        var outputDiffByCadId = Assert.IsType<Dictionary<int, int>>(outputDiffField!.GetValue(vm));
        var icByCadId = Assert.IsType<Dictionary<int, int>>(icField!.GetValue(vm));
        outputDiffByCadId[cadPadId] = diffIndex;
        icByCadId[cadPadId] = icIndex;
    }

    private static FreeformHelperViewModel.NotchValidationDisplayItem CreateValidationDisplayItem(
        string kindText,
        bool isDirect = false,
        bool isIncoming = false,
        bool isOutgoing = false)
    {
        return new FreeformHelperViewModel.NotchValidationDisplayItem(
            KindText: kindText,
            RowText: "#1",
            VersionText: "2.2",
            IcText: "IC 1",
            SourceText: "REG 1 / diff 10",
            TargetText: "REG 2 / diff 20",
            RatioText: "100%",
            AreaText: "1.0",
            ValuesText: "100,0,0",
            CommentText: string.Empty,
            NoteText: string.Empty,
            SourceRegularPadId: 1,
            TargetRegularPadId: 2,
            CadPadId: 1,
            IcIndex: 0,
            SourceDiffIndex: 10,
            TargetDiffIndex: 20,
            IsDirect: isDirect,
            IsIncoming: isIncoming,
            IsOutgoing: isOutgoing);
    }

    // DXF edit commands start a grid rebuild they do not await, and its completion clears the selection.
    // Wait for it first, so the selection made here is the one the next command sees.
    private static async Task SelectCadPadsAsync(FreeformHelperViewModel vm, IReadOnlyList<int> cadPadIds)
    {
        await WaitForGridRebuildAsync(vm);
        vm.ApplyCanvasSelection(cadPadIds, Array.Empty<int>());
    }

    // A rebuild that never finishes must fail here with its state, not stall the whole run.
    private static async Task WaitForGridRebuildAsync(FreeformHelperViewModel vm)
    {
        var rebuild = vm.WaitForGridRebuildIdleAsync();
        try
        {
            await rebuild.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException) when (!rebuild.IsCompleted)
        {
            throw new TimeoutException($"Grid rebuild did not finish within 30 s (status: '{vm.StatusText}').");
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, int timeoutMs = 1500)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var startedAt = Environment.TickCount64;
        while (!predicate())
        {
            if (Environment.TickCount64 - startedAt > timeoutMs)
            {
                throw new TimeoutException("Timed out waiting for test condition.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class TestCanvasHost : ICanvasHost
    {
        public IReadOnlyCollection<int> SelectedCadIds { get; private set; } = Array.Empty<int>();
        public IReadOnlyCollection<int> SelectedRegularIds { get; private set; } = Array.Empty<int>();
        public int FocusWithMinZoomCount { get; private set; }
        public int FitToContentCount { get; private set; }
        public int InvalidateCount { get; private set; }
        public double LastMinZoom { get; private set; }

        public void FitToContent()
        {
            FitToContentCount++;
        }

        public void ResetView()
        {
        }

        public void Invalidate()
        {
            InvalidateCount++;
        }

        public void ClearSelection()
        {
            SelectedCadIds = Array.Empty<int>();
            SelectedRegularIds = Array.Empty<int>();
        }

        public void SetSelection(IReadOnlyCollection<int> cadPadIds, IReadOnlyCollection<int> regularPadIndices)
        {
            SelectedCadIds = cadPadIds.ToArray();
            SelectedRegularIds = regularPadIndices.ToArray();
        }

        public void FocusSelection()
        {
        }

        public void FocusSelectionWithMinZoom(double minZoom)
        {
            FocusWithMinZoomCount++;
            LastMinZoom = minZoom;
        }

        public PadCanvasSelectionPerfSnapshot GetSelectionPerfSnapshot()
        {
            return default;
        }

        public PadCanvasVisibleDrawPerfSnapshot GetVisibleDrawPerfSnapshot()
        {
            return default;
        }

        public void ResetCounters()
        {
            FocusWithMinZoomCount = 0;
            FitToContentCount = 0;
            InvalidateCount = 0;
            LastMinZoom = 0;
        }
    }
}
