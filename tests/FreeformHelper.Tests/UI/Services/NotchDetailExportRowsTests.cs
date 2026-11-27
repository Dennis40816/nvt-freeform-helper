// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDetailExportRowsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetailMatchesExportIncludingTheFirmwareDiffProjection(bool projectDiff)
    {
        var panel = NotchHolePanelFixture.Build([NotchHolePanelFixture.Circle(27, 27, 4.2)],
            gridOrigin: NotchHolePanelFixture.StaggerForwardOrigin);
        var settings = NotchHolePanelFixture.CreateSettings(capsEnabled: true);
        var map = projectDiff ? new Dictionary<int, int> { [1] = 144 } : [];
        var table = new NotchExportService().Generate(panel.Cad, panel.Grid, settings,
            cadOutputFwDiffIndexByCadId: map);
        var projected = NotchCadOutputFwDiffProjectionService.Project(panel.Grid, table, map);
        var rows = NotchV22ExportRows.Project(projected.Table.Rows, settings.Notch.NullValue, settings.Notch.ExportProfile);

        var detail = NotchDetailTestBuilder.Build(panel.Cad.Pads[0], projectDiff ? 144 : 0,
            panel.Cad, panel.Grid, settings, null, map);

        Assert.Equal(rows.Where(static row => row.CadPadId == 1).OrderBy(static row => (row.Values[6] & 1) != 0)
                .Select(static row => string.Join(",", row.Values)),
            detail.Sources.SelectMany(static source => source.Rows).Select(static row => string.Join(",", row.Values.Select(static value => value.Value))));
        Assert.Equal(projectDiff ? 144 : 0, Assert.Single(detail.Sources).SourceDiffIndex);
        Assert.All(detail.Sources.SelectMany(static source => source.Rows), row =>
            Assert.Equal(projectDiff ? 144 : 0, row.Values[0].Value));
    }

    [Fact]
    public void DesignPanelProducesTheApprovedMainAndContinuationRows()
    {
        var panel = NotchHolePanelFixture.Build([NotchHolePanelFixture.Circle(27, 27, 4.2)],
            gridOrigin: NotchHolePanelFixture.StaggerForwardOrigin);
        var settings = NotchHolePanelFixture.CreateSettings(capsEnabled: true);

        var detail = NotchDetailTestBuilder.Build(panel.Cad.Pads[0], 0, panel.Cad, panel.Grid, settings, null,
            new Dictionary<int, int>());

        var source = Assert.Single(detail.Sources);
        int[] expected1 = [0, 136, 1, 38, 12, 38, 0];
        Assert.Equal(expected1, source.Rows[0].Values.Select(static value => value.Value));
        int[] expected2 = [0, 100, 13, 16, 65535, 0, 1];
        Assert.Equal(expected2, source.Rows[1].Values.Select(static value => value.Value));
        Assert.Equal(152, source.RowTotal);
        Assert.Equal(60, source.Cells[0].InRow);
        Assert.NotEmpty(source.PreviewPolygons);
    }

    [Fact]
    public void PreviewDrawsOnlyWhatToFullCanReach()
    {
        // The wall at x=4..6 splits the cell, so the CAD at x=0..2 reaches x=3 at most.
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = 1;
        var cad = TestGeometryFactory.CreateCadPad(1, "SENSOR", 0, 0, 2, 10);
        var wall = TestGeometryFactory.CreateCadPad(2, "SENSOR", 4, 0, 6, 10);
        var settings = NotchHolePanelFixture.CreateSettings();
        settings.Notch.ExportProfile = NotchExportProfile.Debug;

        var detail = NotchDetailTestBuilder.Build(cad, 0, new CadPadSet([cad, wall]), grid, settings, null,
            new Dictionary<int, int>());

        var reachable = new Rect2(0, 0, 3, 10);
        Assert.Equal(reachable, Assert.Single(detail.ToFullPolygons).Bounds);
        Assert.Equal(reachable, Assert.Single(Assert.Single(detail.Sources).PreviewPolygons).Bounds);
    }

    [Fact]
    public void LosingCadNamesTheWinnerAndKeepsItsAllocationsAndPreview()
    {
        var (cad, grid, settings) = NotchRowExplanationFixture.Create();
        var rival = TestGeometryFactory.CreateCadPad(2, "RIVAL", 0, 0, 12, 12);
        var allCad = new CadPadSet([.. cad.Pads, rival]);

        var detail = NotchDetailTestBuilder.Build(rival, 0, allCad, grid, settings, null,
            new Dictionary<int, int>());

        Assert.Empty(detail.Sources);
        Assert.Contains("CAD 1 wins IC 1 diff 0:", detail.ExplanationMessage, StringComparison.Ordinal);
        Assert.NotEmpty(detail.Allocations);
        Assert.NotEmpty(detail.PreviewRegularPads);
        Assert.Same(rival, detail.CadPad);
    }

    [Fact]
    public void CadWithoutOverlapShowsTheExistingEligibilityReason()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        var cad = TestGeometryFactory.CreateCadPad(1, "SENSOR", 20, 20, 21, 21);

        var detail = NotchDetailTestBuilder.Build(cad, 0, new CadPadSet([cad]), grid,
            NotchHolePanelFixture.CreateSettings(true), null, new Dictionary<int, int>());

        Assert.Empty(detail.Sources);
        Assert.Contains("No regular overlap.", detail.ExplanationMessage, StringComparison.Ordinal);
        Assert.Same(cad, detail.CadPad);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedModeKeepsTodaysContentAndSaysWhatIsNeeded(bool legacy)
    {
        var (cad, grid, settings) = NotchRowExplanationFixture.Create();
        settings.Notch.ComputationMode = legacy ? NotchComputationMode.LegacyRegularAnchor : NotchComputationMode.CadAllocation;
        settings.Notch.EnabledVersions.Clear();
        settings.Notch.EnabledVersions.Add(legacy ? NotchAlgorithmVersion.V22 : NotchAlgorithmVersion.V21);

        var detail = NotchDetailTestBuilder.Build(cad.Pads[0], 0, cad, grid, settings, null,
            new Dictionary<int, int>());

        Assert.Empty(detail.Sources);
        Assert.Equal("Row explanations require CadAllocation with V2.2 enabled.", detail.ExplanationMessage);
        Assert.NotEmpty(detail.Allocations);
        Assert.NotEmpty(detail.PreviewRegularPads);
    }

    [Theory]
    [InlineData(NotchExportProfile.Release, 0)]
    [InlineData(NotchExportProfile.Debug, 1)]
    public void NoOpRowsFollowTheSameProfilePolicyAsCExport(NotchExportProfile profile, int count)
    {
        var row = new NotchTableRow(0, 0, 0, 1, new NotchV22Node(0, 100, 65535, 0, 65535, 0, 0), "synthetic");
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        var settings = NotchHolePanelFixture.CreateSettings(true);
        var table = new NotchTable([row]);

        var selected = NotchV22ExportRows.Project(table.Rows, 65535, profile);
        var exported = NotchExportRangeCheck.ProjectAndValidate(table, grid, settings, 65535, profile);

        Assert.Equal(count, selected.Length);
        Assert.Equal(count, exported.V22Nodes.Sum(static nodes => nodes.Count));
    }

    [Theory]
    [InlineData(true, "No V2.2 output row: the export profile omits this no-op row.")]
    [InlineData(false, "")]
    public void DetailExplainsReleaseNoOpPruningAndKeepsCsvRows(bool firmwareOutput, string message)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = 1;
        var cad = TestGeometryFactory.CreateCadPad(1, "SENSOR", 0, 0, 10, 10);
        var settings = NotchHolePanelFixture.CreateSettings(true);
        settings.Notch.ExportProfile = NotchExportProfile.Release;

        var detail = NotchDetailTestBuilder.Build(cad, 0, new CadPadSet([cad]), grid, settings, null,
            new Dictionary<int, int>(), firmwareOutput);

        Assert.Equal(!firmwareOutput, detail.HasSources);
        Assert.NotEmpty(detail.Allocations);
        Assert.Equal(message, detail.ExplanationMessage);
    }

    [Fact]
    public void CrossIcCadCanWinMultipleSourcesAndSelectBetweenThem()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, 10, 10,
            diffIndexSelector: static (_, col) => col, icIndexSelector: static (_, col) => col);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[1].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = 1;
        grid.Pads[1].MatchedCadPadId = 1;
        var cad = TestGeometryFactory.CreateCadPad(1, "SENSOR", 0, 0, 20, 10);
        var settings = NotchHolePanelFixture.CreateSettings(true);
        settings.Notch.ExportProfile = NotchExportProfile.Debug;

        var detail = NotchDetailTestBuilder.Build(cad, 0, new CadPadSet([cad]), grid, settings, null,
            new Dictionary<int, int>());
        detail.SelectedSource = detail.Sources[1];

        Assert.Equal(2, detail.Sources.Length);
        Assert.True(detail.HasSourceSelector);
        var selected = detail.SelectedSource;
        Assert.NotNull(selected);
        Assert.Equal(1, selected.IcIndex);
        Assert.Equal(1, selected.SourceDiffIndex);
    }
}
