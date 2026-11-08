// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLayerUiSnapshotOptionsBehaviorTests
{
    private static readonly string?[] _expectedBoundLayerNames = [null, "alpha", "zBoundary"];

    [Fact]
    public void Build_DesiredHiddenBoundsLayerBeyondLimit_ReplacesLastOptionAndKeepsCanonicalSelection()
    {
        using var culture = TestCulture.UseEnUs();
        var cad = new CadPadSet(
        [
            TestGeometryFactory.CreateCadPad(1, "alpha", 0, 0, 10, 10),
            TestGeometryFactory.CreateCadPad(2, "middle", 12, 0, 22, 10),
        ]);
        var catalog = new DxfLayerCatalog(
        [
            new DxfLayerCatalogItem("alpha", true, DxfLayerContentKind.EntitySection, 1),
            new DxfLayerCatalogItem("middle", true, DxfLayerContentKind.EntitySection, 1),
            new DxfLayerCatalogItem("zBoundary", true, DxfLayerContentKind.None, 0),
        ]);

        var snapshot = CadLayerUiSnapshotBuilder.Build(new CadLayerUiSnapshotRequest(
            Cad: cad,
            Catalog: catalog,
            LayerSelections: null,
            PendingBoundLayerName: "ZBOUNDARY",
            SelectedBoundLayerName: "alpha",
            PendingRegularSourceLayerName: "MIDDLE",
            SelectedRegularSourceLayerName: "alpha",
            SelectedDxfEditTargetLayerName: "ZBOUNDARY",
            SelectedDxfEditRotationLayerName: "removed-layer",
            MaxBoundLayerDropdownLayers: 2));

        Assert.True(snapshot.HasCadLayers);
        Assert.False(snapshot.ToggleStates.Single(static layer => layer.Name == "zBoundary").IsSelected);
        Assert.Equal(_expectedBoundLayerNames, snapshot.BoundLayerOptions.Select(static option => option.Name));
        Assert.Equal("zBoundary", snapshot.SelectedBoundLayerName);
        Assert.Equal("Hidden bounds layer is valid. Showing 2/3.", snapshot.BoundLayerOptionSummary);
        Assert.Equal("middle", snapshot.SelectedRegularSourceLayerName);
        Assert.Equal("zBoundary", snapshot.SelectedDxfEditTargetLayerName);
        Assert.Equal("alpha", snapshot.SelectedDxfEditRotationLayerName);
    }

    [Fact]
    public void Build_NoLayersAfterReload_DropsStaleSelectionsAndKeepsOnlyPlaceholderOptions()
    {
        var snapshot = CadLayerUiSnapshotBuilder.Build(new CadLayerUiSnapshotRequest(
            Cad: new CadPadSet([]),
            Catalog: DxfLayerCatalog.Empty,
            LayerSelections: null,
            PendingBoundLayerName: null,
            SelectedBoundLayerName: "old-boundary",
            PendingRegularSourceLayerName: null,
            SelectedRegularSourceLayerName: "old-regular",
            SelectedDxfEditTargetLayerName: "old-target",
            SelectedDxfEditRotationLayerName: "old-rotation",
            MaxBoundLayerDropdownLayers: 2));

        Assert.False(snapshot.HasCadLayers);
        Assert.Empty(snapshot.ToggleStates);
        var boundOption = Assert.Single(snapshot.BoundLayerOptions);
        Assert.Null(boundOption.Name);
        Assert.Equal("Auto (visible layers)", boundOption.Display);
        Assert.Null(snapshot.SelectedBoundLayerName);
        Assert.Equal("Hidden bounds layer is still valid.", snapshot.BoundLayerOptionSummary);
        var regularOption = Assert.Single(snapshot.RegularSourceLayerOptions);
        Assert.Null(regularOption.Name);
        Assert.Equal("Select layer", regularOption.Display);
        Assert.Null(snapshot.SelectedRegularSourceLayerName);
        Assert.Empty(snapshot.DxfEditLayerOptions);
        Assert.Empty(snapshot.SelectedDxfEditTargetLayerName);
        Assert.Empty(snapshot.SelectedDxfEditRotationLayerName);
    }
}
