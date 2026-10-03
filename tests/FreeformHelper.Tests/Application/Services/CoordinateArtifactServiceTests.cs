using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinateArtifactServiceTests
{
    [Fact]
    public void BuildSnapshot_ProjectsCoordinateRowsInStableOrder()
    {
        var snapshot = BuildPlannerSnapshot(horizontalCount: 2, verticalCount: 2);

        var artifactSnapshot = CoordinateArtifactService.BuildSnapshot(
            snapshot,
            new Rect2(1d, 1d, 9d, 4d),
            "AA outline: unit test",
            "Guide reference: unit test");

        Assert.Equal(13, artifactSnapshot.Rows.Count);
        Assert.Equal("aa-tl", artifactSnapshot.Rows[0].Key);
        Assert.Equal("bist-center", artifactSnapshot.Rows[4].Key);
        Assert.Equal("h-1", artifactSnapshot.Rows[9].Key);
        Assert.Equal("v-1", artifactSnapshot.Rows[11].Key);
        Assert.All(artifactSnapshot.Rows, static row => Assert.Equal("pixel/mm/world", row.Unit));
        Assert.Contains(
            artifactSnapshot.Rows,
            static row => row.Key == "h-1" &&
                          row.SourceText == "Guide reference: unit test" &&
                          row.SourceBoundsText == "(1, 1) -> (9, 4)");
    }

    [Fact]
    public void ExportCsv_UsesStableColumnsAndNumericFields()
    {
        var artifactSnapshot = BuildArtifactSnapshot();

        var csv = CoordinateArtifactService.ExportCsv(artifactSnapshot);

        Assert.StartsWith(
            "key,label,kind,recipe,geometry,pixel_x,pixel_y,pixel_end_x,pixel_end_y,machine_x,machine_y,machine_end_x,machine_end_y,safe_machine_x,safe_machine_y,safe_machine_end_x,safe_machine_end_y,world_x,world_y,world_end_x,world_end_y,unit,source_bounds,source,detail",
            csv,
            StringComparison.Ordinal);
        Assert.Contains("\"aa-tl\",\"AA TL\",\"Point\",\"AA corners\",\"Point\",\"0\",\"0\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"bist-center\",\"BIST center blank rect\",\"Rect\",\"BIST rectangle\",\"Rectangle\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportJson_SerializesSnapshotMetadataAndRows()
    {
        var artifactSnapshot = BuildArtifactSnapshot();

        var json = CoordinateArtifactService.ExportJson(artifactSnapshot);

        Assert.Contains("\"activeAreaSourceText\": \"AA outline: unit test\"", json, StringComparison.Ordinal);
        Assert.Contains("\"guideSourceText\": \"Guide reference: unit test\"", json, StringComparison.Ordinal);
        Assert.Contains("\"rows\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"geometryKind\": \"Point\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportClipboardTable_UsesReadableTabularText()
    {
        var artifactSnapshot = BuildArtifactSnapshot();

        var text = CoordinateArtifactService.ExportClipboardTable(artifactSnapshot.Rows.Take(1));

        Assert.StartsWith("Label\tKind\tRecipe\tPixel\tMachine\tSafe machine\tWorld\tSource bounds\tSource", text, StringComparison.Ordinal);
        Assert.Contains("AA TL\tPoint\tAA corners\t(0, 0)\t(0, 0)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSnapshot_IncludesCustomPointAndPathRows()
    {
        var plannerSnapshot = CoordinatePlannerComputationService.BuildSnapshot(
            new Rect2(0d, 0d, 100d, 100d),
            new CoordinatePlannerRequest(
                MachineOriginX: 0d,
                MachineOriginY: 0d,
                MachineWidth: 100d,
                MachineHeight: 100d,
                PixelWidth: 1000,
                PixelHeight: 1000,
                HorizontalGuideCount: 0,
                VerticalGuideCount: 0,
                CopperPillarDiameter: 0d,
                ShowBistRectangle: false,
                ShowCustomArray: false,
                CustomArrayColumnCount: 0,
                CustomArrayRowCount: 0,
                CustomArrayTopLeftMachineX: 0d,
                CustomArrayTopLeftMachineY: 0d,
                CustomArrayTopRightMachineX: 100d,
                CustomArrayTopRightMachineY: 0d,
                CustomArrayBottomRightMachineX: 100d,
                CustomArrayBottomRightMachineY: 100d,
                CustomArrayBottomLeftMachineX: 0d,
                CustomArrayBottomLeftMachineY: 100d,
                CustomPoints: [new CoordinateCustomPointRequest("1", "Probe A", 25d, 30d)],
                CustomPaths: [new CoordinateCustomPathRequest("1", "Edge sweep", 10d, 20d, 90d, 20d, 3)]));

        var artifactSnapshot = CoordinateArtifactService.BuildSnapshot(
            plannerSnapshot,
            plannerSnapshot.ActiveAreaBounds,
            "AA outline: unit test",
            "Guide reference: unit test");

        Assert.Contains(
            artifactSnapshot.Rows,
            static row => row.Key == "custom-point-1" &&
                          row.Recipe == "Custom point" &&
                          row.Kind == "Point");
        Assert.Contains(
            artifactSnapshot.Rows,
            static row => row.Key == "custom-path-1" &&
                          row.Recipe == "Custom path" &&
                          row.Kind == "Path");
        Assert.Equal(3, artifactSnapshot.Rows.Count(static row => row.Kind == "Step"));
    }

    private static CoordinateArtifactSnapshot BuildArtifactSnapshot()
    {
        var snapshot = BuildPlannerSnapshot(horizontalCount: 2, verticalCount: 2);
        return CoordinateArtifactService.BuildSnapshot(
            snapshot,
            new Rect2(1d, 1d, 9d, 4d),
            "AA outline: unit test",
            "Guide reference: unit test");
    }

    private static CoordinatePlannerSnapshot BuildPlannerSnapshot(int horizontalCount, int verticalCount)
    {
        return CoordinatePlannerComputationService.BuildSnapshot(
            new Rect2(0d, 0d, 10d, 5d),
            new Rect2(1d, 1d, 9d, 4d),
            new CoordinatePlannerRequest(
                MachineOriginX: 0d,
                MachineOriginY: 0d,
                MachineWidth: 100d,
                MachineHeight: 50d,
                PixelWidth: 1000,
                PixelHeight: 500,
                HorizontalGuideCount: horizontalCount,
                VerticalGuideCount: verticalCount,
                CopperPillarDiameter: 0d,
                ShowBistRectangle: true,
                ShowCustomArray: false,
                CustomArrayColumnCount: 0,
                CustomArrayRowCount: 0,
                CustomArrayTopLeftMachineX: 0d,
                CustomArrayTopLeftMachineY: 0d,
                CustomArrayTopRightMachineX: 100d,
                CustomArrayTopRightMachineY: 0d,
                CustomArrayBottomRightMachineX: 100d,
                CustomArrayBottomRightMachineY: 50d,
                CustomArrayBottomLeftMachineX: 0d,
                CustomArrayBottomLeftMachineY: 50d));
    }
}
