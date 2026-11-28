// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerRenderStateCharacterizationTests
{
    [Fact]
    public void HostCallbacks_KeepPublicNamesTypesAndNullDefaults()
    {
        var viewModel = CreateViewModel();

        AssertCallbackType<Func<CoordinateArtifactRow, Task>>(nameof(viewModel.RequestArtifactDetailAsync));
        AssertCallbackType<Func<Task<bool>>>(nameof(viewModel.RequestExportPreviewPngAsync));
        AssertCallbackType<Func<string, Task<bool>>>(nameof(viewModel.RequestSetClipboardTextAsync));
        AssertCallbackType<Func<CoordinatePlannerTextExportRequest, Task<bool>>>(nameof(viewModel.RequestSaveTextFileAsync));
        Assert.Null(viewModel.RequestArtifactDetailAsync);
        Assert.Null(viewModel.RequestExportPreviewPngAsync);
        Assert.Null(viewModel.RequestSetClipboardTextAsync);
        Assert.Null(viewModel.RequestSaveTextFileAsync);
    }

    [Theory]
    [InlineData("RequestArtifactDetailAsync")]
    [InlineData("RequestExportPreviewPngAsync")]
    [InlineData("RequestSetClipboardTextAsync")]
    [InlineData("RequestSaveTextFileAsync")]
    public void HostCallbacks_RoundTripAndClearIndependentlyAcrossViewModels(string clearedName)
    {
        var first = CreateViewModel();
        var second = CreateViewModel();
        Func<CoordinateArtifactRow, Task> detail = _ => Task.CompletedTask;
        Func<Task<bool>> preview = () => Task.FromResult(true);
        Func<string, Task<bool>> clipboard = _ => Task.FromResult(true);
        Func<CoordinatePlannerTextExportRequest, Task<bool>> save = _ => Task.FromResult(true);
        first.RequestArtifactDetailAsync = detail;
        first.RequestExportPreviewPngAsync = preview;
        first.RequestSetClipboardTextAsync = clipboard;
        first.RequestSaveTextFileAsync = save;
        AssertCallbacks(first, detail, preview, clipboard, save);
        AssertCallbacks(second, null, null, null, null);
        second.RequestArtifactDetailAsync = detail;
        second.RequestExportPreviewPngAsync = preview;
        second.RequestSetClipboardTextAsync = clipboard;
        second.RequestSaveTextFileAsync = save;

        switch (clearedName)
        {
            case "RequestArtifactDetailAsync": first.RequestArtifactDetailAsync = null; break;
            case "RequestExportPreviewPngAsync": first.RequestExportPreviewPngAsync = null; break;
            case "RequestSetClipboardTextAsync": first.RequestSetClipboardTextAsync = null; break;
            case "RequestSaveTextFileAsync": first.RequestSaveTextFileAsync = null; break;
        }

        AssertCallbacks(first,
            clearedName == "RequestArtifactDetailAsync" ? null : detail,
            clearedName == "RequestExportPreviewPngAsync" ? null : preview,
            clearedName == "RequestSetClipboardTextAsync" ? null : clipboard,
            clearedName == "RequestSaveTextFileAsync" ? null : save);
        AssertCallbacks(second, detail, preview, clipboard, save);
    }

    [Fact]
    public void ConstructionAndThreeInputChanges_KeepCalculatedRenderResults()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        AssertCalculatedRender(viewModel, bist: true, wide: false, threeHorizontal: false);

        viewModel.MachineWidth = 80m;
        AssertCalculatedRender(viewModel, bist: true, wide: true, threeHorizontal: false);

        viewModel.ShowBistRectangle = false;
        AssertCalculatedRender(viewModel, bist: false, wide: true, threeHorizontal: false);

        viewModel.HorizontalGuideCount = 3m;
        AssertCalculatedRender(viewModel, bist: false, wide: true, threeHorizontal: true);
    }

    [Fact]
    public void CustomArrayInputChange_PublishesCalculatedCornerAndDotRows()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        viewModel.CustomArrayColumnCount = 2m;
        viewModel.CustomArrayRowCount = 2m;

        viewModel.ShowCustomArray = true;

        CoordinatePlannerPointRow[] corners =
        [
            new("Array TL", "4-point corner", "(0, 0)", "(0, 0)", "(0, 0)"),
            new("Array TR", "4-point corner", "(400, 0)", "(40, 0)", "(40, 0)"),
            new("Array BR", "4-point corner", "(400, 200)", "(40, 20)", "(40, 20)"),
            new("Array BL", "4-point corner", "(0, 200)", "(0, 20)", "(0, 20)"),
        ];
        CoordinatePlannerPointRow[] dots =
        [
            new("P1-1", "Array dot", "(0, 0)", "(0, 0)", "(0, 0)"),
            new("P1-2", "Array dot", "(400, 0)", "(40, 0)", "(40, 0)"),
            new("P2-1", "Array dot", "(0, 200)", "(0, 20)", "(0, 20)"),
            new("P2-2", "Array dot", "(400, 200)", "(40, 20)", "(40, 20)"),
        ];
        Assert.Equal(corners, viewModel.CustomArrayCornerRows);
        Assert.Equal(dots, viewModel.CustomArrayPointRows);
        Assert.Equal("Coordinate planner ready. Corners 4, guides 10, BIST on, array dots 4.", viewModel.StatusText);
    }

    [Fact]
    public void OutlineAndGuideBasisChanges_PublishIndependentBoundsAndSourceTexts()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedGuideBasisOption = viewModel.GuideBasisOptions.Single(static option =>
            option.Mode == CoordinatePlannerGuideBasisMode.RegularGrid);

        viewModel.SelectedAaBoundsOption = viewModel.AaBoundsOptions.Single(static option => option.LayerName == "SIG");

        Assert.Equal(new Rect2(2, 0, 3, 1), viewModel.Snapshot.ActiveAreaBounds);
        Assert.Equal(new Rect2(0, 0, 4, 2), viewModel.PreviewBounds);
        Assert.Equal("AA outline: layer SIG", viewModel.ActiveAreaSourceSummaryText);
        Assert.Equal("Guide reference: regular grid bounds", viewModel.GuideSourceSummaryText);
        Assert.Equal(new Point2(2, 1), viewModel.Snapshot.Points[0].World);
        Assert.Equal(new Point2(0, 2), viewModel.Snapshot.Lines[0].StartWorld);
        Assert.Equal(new Point2(4, 2), viewModel.Snapshot.Lines[0].EndWorld);
    }

    [Fact]
    public void RebuildNotifications_PublishNewRenderBeforeSelectionStatusAndInputEvents()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        viewModel.SelectedArtifactRow = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "bist-center");
        var before = CaptureRender(viewModel);
        var changes = RecordNotifications(viewModel);

        viewModel.ShowBistRectangle = false;

        string[] expectedNames =
        [
            "HasSelectedArtifact", "SelectedArtifactRow", "BistGenerationStatusText", "StatusText",
            "Snapshot", "PreviewBounds", "AaCornerRows", "CustomArrayCornerRows", "CustomArrayPointRows",
            "HorizontalGuideRows", "VerticalGuideRows", "ArtifactRows", "HasBistRectangle", "MachineSummaryText",
            "PixelSummaryText", "CopperSummaryText", "GuideSummaryText", "ArtifactSummaryText", "ArtifactSortStatusText",
            "SafeCoordinatePolicyText", "BistRectangleSummaryText", "CustomArraySummaryText",
            "SelectedCustomArrayCornerMachineX", "SelectedCustomArrayCornerMachineY", "SelectedCustomArrayCornerSummaryText",
            "HasCustomArtifacts", "CustomArtifactSummaryText", "ActiveAreaSourceSummaryText", "GuideSourceSummaryText",
            "ShowBistRectangle",
        ];
        Assert.Equal(expectedNames, changes.Select(static change => change.Name));
        AssertCalculatedRender(viewModel, bist: false, wide: false, threeHorizontal: false);
        var after = CaptureRender(viewModel);
        Assert.NotSame(before[0], after[0]);
        Assert.All(changes, change => AssertRenderSame(after, change.Render));
        Assert.All(changes, static change => Assert.Null(change.Selection));
        Assert.Equal("Coordinate planner ready. Corners 4, guides 10, BIST on, array dots 0.", changes[0].Status);
        Assert.Equal("Coordinate planner ready. Corners 4, guides 10, BIST on, array dots 0.", changes[1].Status);
        Assert.All(changes.Skip(2), static change => Assert.Equal(
            "Coordinate planner ready. Corners 4, guides 10, BIST off, array dots 0.", change.Status));
    }

    [Fact]
    public void LayerNotifications_UpdateCadBeforeAllEventsAndKeepRenderInstances()
    {
        var viewModel = CreateViewModel();
        var before = CaptureRender(viewModel);
        var changes = RecordNotifications(viewModel);

        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option => option.LayerName == "SIG");

        string[] expectedNames = ["CadPadsForCanvas", "ShowCad", "LayerSummaryText", "SelectedLayerOption"];
        Assert.Equal(expectedNames, changes.Select(static change => change.Name));
        Assert.All(changes, change => AssertRenderSame(before, change.Render));
        Assert.All(changes, static change => Assert.Equal(2, Assert.Single(change.CadPads).Id));
        Assert.All(changes, static change => Assert.True(change.ShowCad));
        Assert.All(changes, static change => Assert.Equal(
            "Coordinate planner ready. Corners 4, guides 10, BIST on, array dots 0.", change.Status));
        Assert.All(changes, static change => Assert.Null(change.Selection));
    }

    [Fact]
    public void SortNotifications_KeepOldRowsForKeyEventThenPublishNewRowsAndReverse()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        viewModel.ShowBistRectangle = false;
        viewModel.HorizontalGuideCount = 0m;
        viewModel.VerticalGuideCount = 0m;
        var before = CaptureRender(viewModel);
        var originalRows = viewModel.ArtifactRows;
        var changes = RecordNotifications(viewModel);

        viewModel.SortArtifactRowsCommand.Execute("Label");

        string[] expectedNames = ["ArtifactSortKey", "ArtifactRows", "ArtifactSortStatusText"];
        Assert.Equal(expectedNames, changes.Select(static change => change.Name));
        AssertRenderSame(before, changes[0].Render);
        var ascending = CaptureRender(viewModel);
        AssertRenderSame(ascending, changes[1].Render);
        AssertRenderSame(ascending, changes[2].Render);
        AssertOtherRenderSame(before, ascending);
        string[] ascendingKeys = ["aa-bl", "aa-br", "aa-tl", "aa-tr"];
        Assert.Equal(ascendingKeys, viewModel.ArtifactRows.Select(static row => row.Key));
        Assert.Equal("Sort: Label asc", viewModel.ArtifactSortStatusText);
        Assert.All(originalRows, row => Assert.Same(row, Assert.Single(viewModel.ArtifactRows, item => item.Key == row.Key)));
        changes.Clear();

        viewModel.SortArtifactRowsCommand.Execute("Label");

        string[] reverseNames = ["ArtifactSortDescending", "ArtifactRows", "ArtifactSortStatusText"];
        Assert.Equal(reverseNames, changes.Select(static change => change.Name));
        AssertRenderSame(ascending, changes[0].Render);
        var descending = CaptureRender(viewModel);
        AssertRenderSame(descending, changes[1].Render);
        AssertRenderSame(descending, changes[2].Render);
        AssertOtherRenderSame(before, descending);
        string[] descendingKeys = ["aa-tr", "aa-tl", "aa-br", "aa-bl"];
        Assert.Equal(descendingKeys, viewModel.ArtifactRows.Select(static row => row.Key));
        Assert.Equal("Sort: Label desc", viewModel.ArtifactSortStatusText);
        Assert.All(originalRows, row => Assert.Same(row, Assert.Single(viewModel.ArtifactRows, item => item.Key == row.Key)));
        Assert.All(changes, static change => Assert.Equal(
            "Coordinate planner ready. Corners 4, guides 0, BIST off, array dots 0.", change.Status));
        Assert.All(changes, static change => Assert.Null(change.Selection));
    }

    [Fact]
    public void SelectedLayerOption_TracksNoneAllVisibleAndEachSingleLayer()
    {
        var viewModel = CreateViewModel();
        int[] bothIds = [1, 2];
        Assert.Equal(bothIds, viewModel.CadPadsForCanvas.Select(static pad => pad.Id));
        Assert.True(viewModel.ShowCad);

        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option => option.Mode == CoordinatePlannerCadLayerMode.None);
        Assert.Empty(viewModel.CadPadsForCanvas);
        Assert.False(viewModel.ShowCad);

        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option => option.LayerName == "SIG");
        Assert.Equal(2, Assert.Single(viewModel.CadPadsForCanvas).Id);
        Assert.True(viewModel.ShowCad);
        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option => option.LayerName == "AUX");
        Assert.Equal(1, Assert.Single(viewModel.CadPadsForCanvas).Id);
        Assert.True(viewModel.ShowCad);

        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option =>
            option.Mode == CoordinatePlannerCadLayerMode.AllVisible);
        Assert.Equal(bothIds, viewModel.CadPadsForCanvas.Select(static pad => pad.Id));
        Assert.True(viewModel.ShowCad);
    }

    [Fact]
    public void SelectedArtifactRow_SurvivesRebuildByKeyWithUpdatedCoordinates()
    {
        var viewModel = CreateViewModel();
        var selected = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-tr");
        viewModel.SelectedArtifactRow = selected;

        viewModel.MachineWidth = 80m;

        var rebuilt = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-tr");
        Assert.Same(rebuilt, viewModel.SelectedArtifactRow);
        Assert.NotSame(selected, rebuilt);
        Assert.Equal(80d, rebuilt.MachineX);
        Assert.True(viewModel.HasSelectedArtifact);
    }

    private static void AssertCallbackType<T>(string name)
    {
        var property = typeof(CoordinatePlannerWorkspaceViewModel).GetProperty(name);
        Assert.NotNull(property);
        Assert.Equal(typeof(T), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
    }

    private static void AssertCallbacks(
        CoordinatePlannerWorkspaceViewModel viewModel,
        Func<CoordinateArtifactRow, Task>? detail,
        Func<Task<bool>>? preview,
        Func<string, Task<bool>>? clipboard,
        Func<CoordinatePlannerTextExportRequest, Task<bool>>? save)
    {
        Assert.Same(detail, viewModel.RequestArtifactDetailAsync);
        Assert.Same(preview, viewModel.RequestExportPreviewPngAsync);
        Assert.Same(clipboard, viewModel.RequestSetClipboardTextAsync);
        Assert.Same(save, viewModel.RequestSaveTextFileAsync);
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel()
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 4d, cellHeight: 2d),
                [TestGeometryFactory.CreateCadPad(1, "AUX", 0d, 0d, 1d, 1d),
                 TestGeometryFactory.CreateCadPad(2, "SIG", 2d, 0d, 3d, 1d)],
                ["AUX", "SIG"], SourceRevision: 0,
                DefaultMachineWidth: 40d, DefaultMachineHeight: 20d, DefaultPixelWidth: 400, DefaultPixelHeight: 200));
    }

    private static object[] CaptureRender(CoordinatePlannerWorkspaceViewModel viewModel) =>
    [
        viewModel.Snapshot, viewModel.PreviewBounds, viewModel.AaCornerRows,
        viewModel.CustomArrayCornerRows, viewModel.CustomArrayPointRows,
        viewModel.HorizontalGuideRows, viewModel.VerticalGuideRows, viewModel.ArtifactRows,
        viewModel.ActiveAreaSourceSummaryText, viewModel.GuideSourceSummaryText,
    ];

    private static List<(string? Name, object[] Render, string Status, CoordinateArtifactRow? Selection,
        IReadOnlyList<CadPad> CadPads, bool ShowCad)> RecordNotifications(
        CoordinatePlannerWorkspaceViewModel viewModel)
    {
        var changes = new List<(string?, object[], string, CoordinateArtifactRow?,
            IReadOnlyList<CadPad>, bool)>();
        viewModel.PropertyChanged += (_, args) => changes.Add((args.PropertyName, CaptureRender(viewModel),
            viewModel.StatusText, viewModel.SelectedArtifactRow, viewModel.CadPadsForCanvas, viewModel.ShowCad));
        return changes;
    }

    private static void AssertOtherRenderSame(object[] expected, object[] actual)
    {
        Assert.Same(expected[0], actual[0]);
        Assert.Equal(expected[1], actual[1]);
        for (var index = 2; index <= 6; index++)
        {
            Assert.Same(expected[index], actual[index]);
        }
        Assert.Equal(expected[8], actual[8]);
        Assert.Equal(expected[9], actual[9]);
    }

    private static void AssertRenderSame(object[] expected, object[] actual)
    {
        AssertOtherRenderSame(expected, actual);
        Assert.Same(expected[7], actual[7]);
    }

    private static void AssertCalculatedRender(
        CoordinatePlannerWorkspaceViewModel viewModel, bool bist, bool wide, bool threeHorizontal)
    {
        var expected = ExpectedSnapshot(bist, wide, threeHorizontal);
        Assert.Equal(expected.ActiveAreaBounds, viewModel.Snapshot.ActiveAreaBounds);
        Assert.Equal(0d, viewModel.Snapshot.CopperPillarRadiusMachine);
        Assert.Equal(0d, viewModel.Snapshot.WorldPillarRadiusX);
        Assert.Equal(0d, viewModel.Snapshot.WorldPillarRadiusY);
        Assert.Equal(expected.Points, viewModel.Snapshot.Points);
        Assert.Equal(expected.Lines, viewModel.Snapshot.Lines);
        Assert.Equal(expected.Rectangles, viewModel.Snapshot.Rectangles);
        Assert.Equal(new Rect2(0d, 0d, 4d, 2d), viewModel.PreviewBounds);
        CoordinatePlannerPointRow[] corners =
        [
            new("AA TL", "AA corner", "(0, 0)", "(0, 0)", "(0, 0)"),
            new("AA TR", "AA corner", "(400, 0)", wide ? "(80, 0)" : "(40, 0)", wide ? "(80, 0)" : "(40, 0)"),
            new("AA BR", "AA corner", "(400, 200)", wide ? "(80, 20)" : "(40, 20)", wide ? "(80, 20)" : "(40, 20)"),
            new("AA BL", "AA corner", "(0, 200)", "(0, 20)", "(0, 20)"),
        ];
        Assert.Equal(corners, viewModel.AaCornerRows);
        Assert.Empty(viewModel.CustomArrayCornerRows);
        Assert.Empty(viewModel.CustomArrayPointRows);
        AssertGuideRows(viewModel, wide, threeHorizontal);
        AssertArtifactRows(viewModel.ArtifactRows, expected, bist, threeHorizontal);
        Assert.Equal("AA outline: regular grid bounds", viewModel.ActiveAreaSourceSummaryText);
        Assert.Equal("Guide reference: AA outline: regular grid bounds", viewModel.GuideSourceSummaryText);
        Assert.Equal(bist, viewModel.HasBistRectangle);
        var expectedStatus = (bist, threeHorizontal) switch
        {
            (true, false) => "Coordinate planner ready. Corners 4, guides 10, BIST on, array dots 0.",
            (false, false) => "Coordinate planner ready. Corners 4, guides 10, BIST off, array dots 0.",
            _ => "Coordinate planner ready. Corners 4, guides 8, BIST off, array dots 0.",
        };
        Assert.Equal(expectedStatus, viewModel.StatusText);
    }

    private static CoordinatePlannerSnapshot ExpectedSnapshot(bool bist, bool wide, bool threeHorizontal)
    {
        var right = wide ? 80d : 40d;
        var quarter = wide ? 20d : 10d;
        var half = wide ? 40d : 20d;
        var threeQuarters = wide ? 60d : 30d;
        var points = new List<CoordinatePlannerPoint>
        {
            ExpectedPoint("aa-tl", "AA TL", CoordinatePlannerPointKind.AaCorner, (0, 2), (0, 0), (0, 0)),
            ExpectedPoint("aa-tr", "AA TR", CoordinatePlannerPointKind.AaCorner, (4, 2), (400, 0), (right, 0)),
            ExpectedPoint("aa-br", "AA BR", CoordinatePlannerPointKind.AaCorner, (4, 0), (400, 200), (right, 20)),
            ExpectedPoint("aa-bl", "AA BL", CoordinatePlannerPointKind.AaCorner, (0, 0), (0, 200), (0, 20)),
        };
        if (bist)
        {
            points.Add(ExpectedPoint("bist-tl", "BIST TL", CoordinatePlannerPointKind.BistCorner, (1, 1.5), (100, 50), (quarter, 5)));
            points.Add(ExpectedPoint("bist-tr", "BIST TR", CoordinatePlannerPointKind.BistCorner, (3, 1.5), (300, 50), (threeQuarters, 5)));
            points.Add(ExpectedPoint("bist-br", "BIST BR", CoordinatePlannerPointKind.BistCorner,
                (3, 0.5), (300, 150), (threeQuarters, 15)));
            points.Add(ExpectedPoint("bist-bl", "BIST BL", CoordinatePlannerPointKind.BistCorner, (1, 0.5), (100, 150), (quarter, 15)));
        }
        CoordinatePlannerLine[] horizontal = threeHorizontal ?
        [
            ExpectedLine("h-1", "H1", CoordinatePlannerLineKind.HorizontalGuide, (0, 2), (4, 2), (0, 0), (400, 0), (0, 0), (right, 0)),
            ExpectedLine("h-2", "H2", CoordinatePlannerLineKind.HorizontalGuide,
                (0, 1), (4, 1), (0, 100), (400, 100), (0, 10), (right, 10)),
            ExpectedLine("h-3", "H3", CoordinatePlannerLineKind.HorizontalGuide,
                (0, 0), (4, 0), (0, 200), (400, 200), (0, 20), (right, 20)),
        ] :
        [
            ExpectedLine("h-1", "H1", CoordinatePlannerLineKind.HorizontalGuide, (0, 2), (4, 2), (0, 0), (400, 0), (0, 0), (right, 0)),
            ExpectedLine("h-2", "H2", CoordinatePlannerLineKind.HorizontalGuide,
                (0, 1.5), (4, 1.5), (0, 50), (400, 50), (0, 5), (right, 5)),
            ExpectedLine("h-3", "H3", CoordinatePlannerLineKind.HorizontalGuide,
                (0, 1), (4, 1), (0, 100), (400, 100), (0, 10), (right, 10)),
            ExpectedLine("h-4", "H4", CoordinatePlannerLineKind.HorizontalGuide,
                (0, 0.5), (4, 0.5), (0, 150), (400, 150), (0, 15), (right, 15)),
            ExpectedLine("h-5", "H5", CoordinatePlannerLineKind.HorizontalGuide,
                (0, 0), (4, 0), (0, 200), (400, 200), (0, 20), (right, 20)),
        ];
        CoordinatePlannerLine[] vertical =
        [
            ExpectedLine("v-1", "V1", CoordinatePlannerLineKind.VerticalGuide, (0, 2), (0, 0), (0, 0), (0, 200), (0, 0), (0, 20)),
            ExpectedLine("v-2", "V2", CoordinatePlannerLineKind.VerticalGuide,
                (1, 2), (1, 0), (100, 0), (100, 200), (quarter, 0), (quarter, 20)),
            ExpectedLine("v-3", "V3", CoordinatePlannerLineKind.VerticalGuide, (2, 2), (2, 0), (200, 0), (200, 200), (half, 0), (half, 20)),
            ExpectedLine("v-4", "V4", CoordinatePlannerLineKind.VerticalGuide,
                (3, 2), (3, 0), (300, 0), (300, 200), (threeQuarters, 0), (threeQuarters, 20)),
            ExpectedLine("v-5", "V5", CoordinatePlannerLineKind.VerticalGuide,
                (4, 2), (4, 0), (400, 0), (400, 200), (right, 0), (right, 20)),
        ];
        CoordinatePlannerRectangle[] rectangles = bist ?
        [
            new("bist-center", "BIST center blank rect", CoordinatePlannerRectangleKind.BistCenter,
                new Rect2(1, 0.5, 3, 1.5), new Rect2(1, 0.5, 3, 1.5),
                100, 50, 300, 150, quarter, 5, threeQuarters, 15, quarter, 5, threeQuarters, 15),
        ] : [];
        return new CoordinatePlannerSnapshot(new Rect2(0, 0, 4, 2), 0, 0, 0, points, [.. horizontal, .. vertical], rectangles);
    }

    private static CoordinatePlannerPoint ExpectedPoint(
        string key, string label, CoordinatePlannerPointKind kind,
        (double X, double Y) world, (double X, double Y) pixel, (double X, double Y) machine) =>
        new(key, label, kind, new Point2(world.X, world.Y), new Point2(world.X, world.Y),
            pixel.X, pixel.Y, machine.X, machine.Y, machine.X, machine.Y);

    private static CoordinatePlannerLine ExpectedLine(
        string key, string label, CoordinatePlannerLineKind kind,
        (double X, double Y) startWorld, (double X, double Y) endWorld,
        (double X, double Y) startPixel, (double X, double Y) endPixel,
        (double X, double Y) startMachine, (double X, double Y) endMachine) =>
        new(key, label, kind, new Point2(startWorld.X, startWorld.Y), new Point2(endWorld.X, endWorld.Y),
            new Point2(startWorld.X, startWorld.Y), new Point2(endWorld.X, endWorld.Y),
            startPixel.X, startPixel.Y, endPixel.X, endPixel.Y, startMachine.X, startMachine.Y, endMachine.X, endMachine.Y,
            startMachine.X, startMachine.Y, endMachine.X, endMachine.Y);

    private static void AssertGuideRows(CoordinatePlannerWorkspaceViewModel viewModel, bool wide, bool threeHorizontal)
    {
        var right = wide ? "80" : "40";
        CoordinatePlannerLineRow[] horizontal = threeHorizontal ?
        [
            new("H1", "(0, 0) -> (400, 0)", $"(0, 0) -> ({right}, 0)", $"(0, 0) -> ({right}, 0)"),
            new("H2", "(0, 100) -> (400, 100)", $"(0, 10) -> ({right}, 10)", $"(0, 10) -> ({right}, 10)"),
            new("H3", "(0, 200) -> (400, 200)", $"(0, 20) -> ({right}, 20)", $"(0, 20) -> ({right}, 20)"),
        ] :
        [
            new("H1", "(0, 0) -> (400, 0)", $"(0, 0) -> ({right}, 0)", $"(0, 0) -> ({right}, 0)"),
            new("H2", "(0, 50) -> (400, 50)", $"(0, 5) -> ({right}, 5)", $"(0, 5) -> ({right}, 5)"),
            new("H3", "(0, 100) -> (400, 100)", $"(0, 10) -> ({right}, 10)", $"(0, 10) -> ({right}, 10)"),
            new("H4", "(0, 150) -> (400, 150)", $"(0, 15) -> ({right}, 15)", $"(0, 15) -> ({right}, 15)"),
            new("H5", "(0, 200) -> (400, 200)", $"(0, 20) -> ({right}, 20)", $"(0, 20) -> ({right}, 20)"),
        ];
        var quarter = wide ? "20" : "10";
        var half = wide ? "40" : "20";
        var threeQuarters = wide ? "60" : "30";
        CoordinatePlannerLineRow[] vertical =
        [
            new("V1", "(0, 0) -> (0, 200)", "(0, 0) -> (0, 20)", "(0, 0) -> (0, 20)"),
            new("V2", "(100, 0) -> (100, 200)", $"({quarter}, 0) -> ({quarter}, 20)", $"({quarter}, 0) -> ({quarter}, 20)"),
            new("V3", "(200, 0) -> (200, 200)", $"({half}, 0) -> ({half}, 20)", $"({half}, 0) -> ({half}, 20)"),
            new("V4", "(300, 0) -> (300, 200)",
                $"({threeQuarters}, 0) -> ({threeQuarters}, 20)", $"({threeQuarters}, 0) -> ({threeQuarters}, 20)"),
            new("V5", "(400, 0) -> (400, 200)", $"({right}, 0) -> ({right}, 20)", $"({right}, 0) -> ({right}, 20)"),
        ];
        Assert.Equal(horizontal, viewModel.HorizontalGuideRows);
        Assert.Equal(vertical, viewModel.VerticalGuideRows);
    }

    private static void AssertArtifactRows(
        IReadOnlyList<CoordinateArtifactRow> rows, CoordinatePlannerSnapshot expected, bool bist, bool threeHorizontal)
    {
        string[] expectedKeys =
        [
            "aa-tl", "aa-tr", "aa-br", "aa-bl",
            .. (bist ? new[] { "bist-center", "bist-tl", "bist-tr", "bist-br", "bist-bl" } : Array.Empty<string>()),
            "h-1", "h-2", "h-3", .. (threeHorizontal ? Array.Empty<string>() : new[] { "h-4", "h-5" }),
            "v-1", "v-2", "v-3", "v-4", "v-5",
        ];
        Assert.Equal(expectedKeys, rows.Select(static row => row.Key));
        foreach (var point in expected.Points)
        {
            var row = Assert.Single(rows, row => row.Key == point.Key);
            Assert.Equal(point.Label, row.Label);
            Assert.Equal("Point", row.Kind);
            Assert.Equal(CoordinateArtifactGeometryKind.Point, row.GeometryKind);
            Assert.Equal((point.PixelX, point.PixelY, point.MachineX, point.MachineY, point.SafeMachineX, point.SafeMachineY,
                point.World.X, point.World.Y),
                (row.PixelX, row.PixelY, row.MachineX, row.MachineY, row.SafeMachineX, row.SafeMachineY, row.WorldX, row.WorldY));
            Assert.Null(row.PixelEndX);
            Assert.Null(row.PixelEndY);
        }
        foreach (var line in expected.Lines)
        {
            var row = Assert.Single(rows, row => row.Key == line.Key);
            Assert.Equal(line.Label, row.Label);
            Assert.Equal("Line", row.Kind);
            Assert.Equal(CoordinateArtifactGeometryKind.Line, row.GeometryKind);
            Assert.Equal((line.StartPixelX, line.StartPixelY, (double?)line.EndPixelX, (double?)line.EndPixelY),
                (row.PixelX, row.PixelY, row.PixelEndX, row.PixelEndY));
            Assert.Equal((line.StartMachineX, line.StartMachineY, (double?)line.EndMachineX, (double?)line.EndMachineY),
                (row.MachineX, row.MachineY, row.MachineEndX, row.MachineEndY));
            Assert.Equal((line.SafeStartMachineX, line.SafeStartMachineY, (double?)line.SafeEndMachineX, (double?)line.SafeEndMachineY),
                (row.SafeMachineX, row.SafeMachineY, row.SafeMachineEndX, row.SafeMachineEndY));
            Assert.Equal((line.StartWorld.X, line.StartWorld.Y, (double?)line.EndWorld.X, (double?)line.EndWorld.Y),
                (row.WorldX, row.WorldY, row.WorldEndX, row.WorldEndY));
        }
        if (bist)
        {
            var row = Assert.Single(rows, static row => row.Key == "bist-center");
            var rect = Assert.Single(expected.Rectangles);
            Assert.Equal("Rect", row.Kind);
            Assert.Equal(CoordinateArtifactGeometryKind.Rectangle, row.GeometryKind);
            Assert.Equal((100d, 50d, (double?)300d, (double?)150d), (row.PixelX, row.PixelY, row.PixelEndX, row.PixelEndY));
            Assert.Equal((rect.MachineLeft, 5d, (double?)rect.MachineRight, (double?)15d),
                (row.MachineX, row.MachineY, row.MachineEndX, row.MachineEndY));
        }
        Assert.All(rows, static row => Assert.Equal("pixel/mm/world", row.Unit));
    }
}
