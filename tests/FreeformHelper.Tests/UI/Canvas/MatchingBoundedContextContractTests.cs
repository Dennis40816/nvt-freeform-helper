using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MatchingBoundedContextContractTests
{
    private static readonly double[] XEdges = { 0, 10, 20 };
    private static readonly double[] YEdges = { 0, 10 };

    [AvaloniaFact]
    public void SameInput_AuditSettingsAndOverridesLeaveOverlapAndHitTestingUnchanged()
    {
        var pads = new[]
        {
            new RegularPad(0, 0, 0, Rectangle(0, 0, 10, 10)),
            new RegularPad(0, 1, 1, Rectangle(10, 0, 20, 10)),
        };
        pads[0].AssignMapping(2, 40);
        pads[1].AssignMapping(2, 41);
        pads[1].Freeform = FreeformType.XWay;
        var grid = new RegularGrid(1, 2, XEdges, YEdges, pads);
        var cad = new CadPadSet(new[]
        {
            new CadPad(10, "Large", "L1", Rectangle(-4, 0, 16, 10)),
            new CadPad(20, "Small", "L1", Rectangle(6, 2, 12, 8)),
        });
        var canvas = CreateCanvas(cad.Pads, grid.Pads);

        var overlap = PadMatcher.Match(cad, grid);
        var expectedRelations = new[] { (10, 0), (10, 1), (20, 0), (20, 1) };
        Assert.Equal(expectedRelations, overlap.Links
            .OrderBy(link => link.CadPadId).ThenBy(link => link.RegularPadId)
            .Select(link => (link.CadPadId, link.RegularPadId)));
        Assert.All(overlap.CadToRegular.Values, links => Assert.Equal(2, links.Count));
        Assert.All(overlap.RegularToCad.Values, links => Assert.Equal(2, links.Count));
        Assert.All(overlap.Links, link => Assert.True(link.OverlapArea > 0));
        Assert.All(pads, pad => Assert.Equal(10, pad.MatchedCadPadId));
        canvas.PadMatchLinks = overlap.Links;
        var matchState = CapturePadState(pads);
        var linksBeforeAudit = overlap.Links.ToArray();

        void AssertBoundariesUnchanged()
        {
            Assert.Equal(matchState, CapturePadState(pads));
            Assert.Equal(linksBeforeAudit, overlap.Links);
            AssertHitPolicy(canvas);
            Assert.Equal(matchState, CapturePadState(pads));
        }

        AssertBoundariesUnchanged();
        var settings = new IndexMappingSettings
        {
            WeightIou = 1,
            WeightCentroidDistance = 0,
            WeightAreaRatio = 0,
            CandidatePaddingCells = 0,
            CandidateNumber = 1,
            LowConfidenceThreshold = 0,
            AmbiguousMargin = 0,
        };

        // Both CADs prefer the left regular; retaining one candidate leaves the smaller CAD unmapped.
        var limited = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings);
        var limitedPair = Assert.Single(limited.Pairs);
        Assert.Equal((10, 0), (limitedPair.CadPadId, limitedPair.RegularPadIndex));
        Assert.Equal(1, limited.Report.UnmappedCadCount);
        Assert.Equal(1, limited.Report.UnmappedRegularCount);
        AssertBoundariesUnchanged();

        settings.CandidateNumber = 2;
        var expanded = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings);
        var expectedSuggestions = new[] { (10, 0), (20, 1) };
        Assert.Equal(expectedSuggestions, expanded.Pairs.Select(pair => (pair.CadPadId, pair.RegularPadIndex)));
        Assert.Equal(2, expanded.Report.MappedCount);
        Assert.Equal(0, expanded.Report.UnmappedCadCount);
        Assert.Equal(0, expanded.Report.UnmappedRegularCount);
        Assert.Equal(0, expanded.Report.LowConfidenceCount);
        AssertBoundariesUnchanged();

        settings.LowConfidenceThreshold = 0.75;
        var strict = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings);
        Assert.Equal(expanded.Pairs, strict.Pairs);
        Assert.Equal(2, strict.Report.LowConfidenceCount);
        Assert.Equal(2, strict.Report.Issues.Count(issue => issue.Kind == DxfRegularMappingIssueKind.LowConfidence));
        AssertBoundariesUnchanged();

        var overridden = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, new Dictionary<int, int> { [10] = 1 });
        var expectedOverrides = new[] { (10, 1), (20, 0) };
        Assert.Equal(expectedOverrides, overridden.Pairs.Select(pair => (pair.CadPadId, pair.RegularPadIndex)));
        Assert.Equal(2, overridden.Report.MappedCount);
        AssertBoundariesUnchanged();
    }

    [AvaloniaFact]
    public void HitTest_UsesSmallestVisibleGeometryIncludingUnmatchedPadsWithoutChangingMatchState()
    {
        var pads = new[]
        {
            new RegularPad(0, 0, 0, Rectangle(0, 0, 10, 10)) { MatchedCadPadId = 10, MatchScore = 0.99 },
            new RegularPad(0, 1, 1, Rectangle(2, 2, 4, 4)) { MatchedCadPadId = null, MatchScore = 0 },
        };
        var cadPads = new[]
        {
            new CadPad(10, "Large", "L1", Rectangle(0, 0, 10, 10)),
            new CadPad(20, "Triangle", "L1", new Polygon2(new[]
            {
                new Point2(1, 1), new Point2(7, 1), new Point2(1, 7),
            })),
        };
        var canvas = CreateCanvas(cadPads, pads);
        var matchState = CapturePadState(pads);

        // All areas differ: CAD wins across kinds, then each kind chooses its smallest containing pad.
        Assert.Equal(("Cad", 20), HitTest(canvas, new Point2(3, 3)));
        Assert.Equal(("Regular", 1), HitTest(canvas, new Point2(3, 3), regularOnly: true));
        // Inside the triangle's bounds but outside its polygon, so the larger CAD is hit.
        Assert.Equal(("Cad", 10), HitTest(canvas, new Point2(6, 6)));
        canvas.ShowCad = false;
        Assert.Equal(("Regular", 1), HitTest(canvas, new Point2(3, 3)));
        canvas.ShowRegular = false;
        Assert.Equal(("None", 0), HitTest(canvas, new Point2(3, 3)));
        Assert.Equal(matchState, CapturePadState(pads));
    }

    private static void AssertHitPolicy(PadCanvas canvas)
    {
        canvas.ShowCad = true;
        canvas.ShowRegular = true;
        Assert.Equal(("Cad", 10), HitTest(canvas, new Point2(15, 5)));
        Assert.Equal(("Cad", 20), HitTest(canvas, new Point2(8, 5)));
        Assert.Equal(("Regular", 0), HitTest(canvas, new Point2(8, 5), regularOnly: true));
        Assert.Equal(("Cad", 10), HitTest(canvas, new Point2(-2, 5)));
        Assert.Equal(("None", 0), HitTest(canvas, new Point2(-2, 5), regularOnly: true));
        Assert.Equal(("Regular", 1), HitTest(canvas, new Point2(19, 5)));
        Assert.Equal(("None", 0), HitTest(canvas, new Point2(30, 5)));

        canvas.ShowCad = false;
        Assert.Equal(("Regular", 0), HitTest(canvas, new Point2(8, 5)));
        canvas.ShowCad = true;
        canvas.ShowRegular = false;
        Assert.Equal(("Cad", 20), HitTest(canvas, new Point2(8, 5)));
        Assert.Equal(("None", 0), HitTest(canvas, new Point2(8, 5), regularOnly: true));
        canvas.ShowCad = false;
        Assert.Equal(("None", 0), HitTest(canvas, new Point2(8, 5)));
        canvas.ShowCad = true;
        canvas.ShowRegular = true;
    }

    private static (int? CadPadId, double Score, int IcIndex, int DiffIndex, FreeformType Freeform)[] CapturePadState(
        IEnumerable<RegularPad> pads) =>
        pads.Select(pad => (pad.MatchedCadPadId, pad.MatchScore, pad.IcIndex, pad.DiffIndex, pad.Freeform)).ToArray();

    private static PadCanvas CreateCanvas(IReadOnlyList<CadPad> cadPads, IReadOnlyList<RegularPad> regularPads)
    {
        var canvas = new PadCanvas { CadPads = cadPads, RegularPads = regularPads, Width = 200, Height = 120 };
        // Avalonia 12 resolves pointer positions through a TopLevel.
        var window = new Window { Content = canvas, Width = 200, Height = 120 };
        window.Show();
        canvas.Measure(new Size(200, 120));
        canvas.Arrange(new Rect(0, 0, 200, 120));
        return canvas;
    }

    private static (string Kind, int IdOrIndex) HitTest(PadCanvas canvas, Point2 world, bool regularOnly = false)
    {
        var screen = canvas.WorldToScreen(world);
        if (regularOnly)
        {
            var index = canvas.TryGetRegularPadIndexAt(screen);
            return index.HasValue ? ("Regular", index.Value) : ("None", 0);
        }

        canvas.ClearSelection();
        (string Kind, int IdOrIndex) hit = ("None", 0);
        void OnCadContextRequested(object? sender, PadCanvas.CadPadContextRequestedEventArgs e) => hit = ("Cad", e.Pad.Id);
        void OnRegularContextRequested(object? sender, PadCanvas.RegularPadContextRequestedEventArgs e) => hit = ("Regular", e.Pad.Index);
        canvas.CadPadContextRequested += OnCadContextRequested;
        canvas.RegularPadContextRequested += OnRegularContextRequested;
        try
        {
            using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
            var root = TopLevel.GetTopLevel(canvas)!;
            var rootPosition = canvas.TranslatePoint(screen, root)!.Value;
            canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, root, rootPosition, 0,
                new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
                KeyModifiers.None, clickCount: 1));
            return hit;
        }
        finally
        {
            canvas.CadPadContextRequested -= OnCadContextRequested;
            canvas.RegularPadContextRequested -= OnRegularContextRequested;
        }
    }

    private static Polygon2 Rectangle(double minX, double minY, double maxX, double maxY) => new(new[]
    {
        new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY),
    });
}
