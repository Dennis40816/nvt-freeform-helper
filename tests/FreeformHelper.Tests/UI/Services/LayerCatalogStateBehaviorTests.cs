// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests.UI.Services;

[Collection("HeadlessUiSerial")]
public sealed class LayerCatalogStateBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryLoadFromPath_UnavailableSourceClearsVisibleLayersAndAllowsReload(bool missingFile)
    {
        var path = TestFiles.NewTempFilePath("layer-catalog-", ".dxf");
        TestFiles.WriteText(path, CreateDxf("SIGNAL_A"));
        var service = new LayerCatalogStateService(new DxfLayerCatalogReader());
        service.TryLoadFromPath(path);
        var original = service.Catalog;
        Assert.Equal("SIGNAL_A", Assert.Single(original.Layers).Name);
        var unavailablePath = missingFile ? TestFiles.NewTempFilePath("missing-layer-catalog-", ".dxf") : " ";

        service.TryLoadFromPath(unavailablePath);

        Assert.Empty(service.Catalog.Layers);
        service.TryLoadFromPath(path);
        Assert.Same(original, service.Catalog);
        Assert.Equal("SIGNAL_A", Assert.Single(service.Catalog.Layers).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryLoadFromEmbedded_ClearsAbsentDataAndRefreshesChangedDxfContent(bool useNullData)
    {
        var bytes = Encoding.UTF8.GetBytes(CreateDxf("SIGNAL_A"));
        var service = new LayerCatalogStateService(new DxfLayerCatalogReader());
        service.TryLoadFromEmbedded(bytes);
        var original = service.Catalog;
        Assert.Equal("SIGNAL_A", Assert.Single(original.Layers).Name);

        service.TryLoadFromEmbedded(useNullData ? null : Array.Empty<byte>());

        Assert.Empty(service.Catalog.Layers);
        service.TryLoadFromEmbedded(bytes.ToArray());
        Assert.Same(original, service.Catalog);

        service.TryLoadFromEmbedded(Encoding.UTF8.GetBytes(CreateDxf("GROUND_B")));

        var changed = Assert.Single(service.Catalog.Layers);
        Assert.Equal("GROUND_B", changed.Name);
        Assert.True(changed.HasEntitySectionContent);
        Assert.Equal(1, changed.EntityCount);

        service.TryLoadFromEmbedded(Encoding.UTF8.GetBytes(CreateDxf("LONGER_SIGNAL_LAYER")));

        Assert.Equal("LONGER_SIGNAL_LAYER", Assert.Single(service.Catalog.Layers).Name);
        var path = TestFiles.NewTempFilePath("layer-catalog-switch-", ".dxf");
        TestFiles.WriteText(path, CreateDxf("PATH_LAYER"));
        service.TryLoadFromPath(path);
        Assert.Equal("PATH_LAYER", Assert.Single(service.Catalog.Layers).Name);
        service.TryLoadFromEmbedded(bytes);
        Assert.Equal("SIGNAL_A", Assert.Single(service.Catalog.Layers).Name);
    }

    private static string CreateDxf(string layer) =>
        "0\r\nSECTION\r\n2\r\nENTITIES\r\n0\r\nLINE\r\n8\r\n" + layer +
        "\r\n10\r\n0\r\n20\r\n0\r\n11\r\n1\r\n21\r\n1\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n";
}
