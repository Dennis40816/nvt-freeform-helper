using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLayerUiSnapshotBuilderTests
{
    [Fact]
    public void Build_UsesSnapshotSelections_AndPreservesDesiredLayerOptions()
    {
        var cad = new CadPadSet(new[]
        {
            CreateRectCad(1, "signal", 0, 0, 10, 10),
            CreateRectCad(2, "regular", 12, 0, 22, 10),
            CreateRectCad(3, "aux", 24, 0, 34, 10),
        });
        var catalog = new DxfLayerCatalog(new[]
        {
            new DxfLayerCatalogItem("signal", isDefinedInLayerTable: true, DxfLayerContentKind.EntitySection, 1),
            new DxfLayerCatalogItem("regular", isDefinedInLayerTable: true, DxfLayerContentKind.EntitySection, 1),
            new DxfLayerCatalogItem("aux", isDefinedInLayerTable: true, DxfLayerContentKind.BlockSection, 1),
        });

        var snapshot = CadLayerUiSnapshotBuilder.Build(new CadLayerUiSnapshotRequest(
            Cad: cad,
            Catalog: catalog,
            LayerSelections: new[]
            {
                new LayerSelectionSnapshot { Name = "signal", IsSelected = false },
                new LayerSelectionSnapshot { Name = "regular", IsSelected = true },
            },
            PendingBoundLayerName: "aux",
            SelectedBoundLayerName: "signal",
            PendingRegularSourceLayerName: null,
            SelectedRegularSourceLayerName: "regular",
            SelectedDxfEditTargetLayerName: "signal",
            SelectedDxfEditRotationLayerName: "regular",
            MaxBoundLayerDropdownLayers: 2));

        Assert.Collection(
            snapshot.ToggleStates.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase),
            item =>
            {
                Assert.Equal("aux", item.Name);
                Assert.False(item.IsSelected);
            },
            item =>
            {
                Assert.Equal("regular", item.Name);
                Assert.True(item.IsSelected);
            },
            item =>
            {
                Assert.Equal("signal", item.Name);
                Assert.False(item.IsSelected);
            });

        Assert.Equal("aux", snapshot.SelectedBoundLayerName);
        Assert.Equal("regular", snapshot.SelectedRegularSourceLayerName);
        Assert.Equal("signal", snapshot.SelectedDxfEditTargetLayerName);
        Assert.Equal("regular", snapshot.SelectedDxfEditRotationLayerName);
        Assert.Equal("Hidden bounds layer is valid. Showing 2/3.", snapshot.BoundLayerOptionSummary);
        Assert.Contains(snapshot.BoundLayerOptions, option => string.Equals(option.Name, "aux", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_UsesCatalogDefaults_WhenSnapshotSelectionsAreMissing()
    {
        var cad = new CadPadSet(new[]
        {
            CreateRectCad(1, "entity", 0, 0, 10, 10),
            CreateRectCad(2, "blockOnly", 12, 0, 22, 10),
            CreateRectCad(3, "regular", 24, 0, 34, 10),
        });
        var catalog = new DxfLayerCatalog(new[]
        {
            new DxfLayerCatalogItem("entity", isDefinedInLayerTable: true, DxfLayerContentKind.EntitySection, 1),
            new DxfLayerCatalogItem("blockOnly", isDefinedInLayerTable: true, DxfLayerContentKind.BlockSection, 1),
            new DxfLayerCatalogItem("regular", isDefinedInLayerTable: true, DxfLayerContentKind.BlockSection, 1),
        });

        var snapshot = CadLayerUiSnapshotBuilder.Build(new CadLayerUiSnapshotRequest(
            Cad: cad,
            Catalog: catalog,
            LayerSelections: Array.Empty<LayerSelectionSnapshot>(),
            PendingBoundLayerName: null,
            SelectedBoundLayerName: null,
            PendingRegularSourceLayerName: null,
            SelectedRegularSourceLayerName: null,
            SelectedDxfEditTargetLayerName: null,
            SelectedDxfEditRotationLayerName: null,
            MaxBoundLayerDropdownLayers: 10));

        Assert.True(snapshot.ToggleStates.Single(static item => item.Name == "entity").IsSelected);
        Assert.False(snapshot.ToggleStates.Single(static item => item.Name == "blockOnly").IsSelected);
        Assert.True(snapshot.ToggleStates.Single(static item => item.Name == "regular").IsSelected);
        Assert.Equal("blockOnly", snapshot.SelectedDxfEditTargetLayerName);
        Assert.Equal("blockOnly", snapshot.SelectedDxfEditRotationLayerName);
    }

    private static CadPad CreateRectCad(int id, string layer, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
        return new CadPad(id, $"PAD_{id}", layer, polygon);
    }
}
