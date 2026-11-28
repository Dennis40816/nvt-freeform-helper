// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests.UI.Services;

public sealed class LayerCatalogStateRefreshBehaviorTests
{
    [Fact]
    public void TryLoadFromPath_SameLengthEditWithNewWriteTime_RefreshesLayerNames()
    {
        var path = TestFiles.NewTempFilePath("cov77-layer-refresh-", ".dxf");
        TestFiles.WriteText(path, CreateDxf("SIGNAL_A"));
        var file = new FileInfo(path);
        var originalWriteTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        file.LastWriteTimeUtc = originalWriteTime;
        var originalLength = file.Length;
        var service = new LayerCatalogStateService(new DxfLayerCatalogReader());
        service.TryLoadFromPath(path);
        Assert.Equal("SIGNAL_A", Assert.Single(service.Catalog.Layers).Name);

        TestFiles.WriteText(path, CreateDxf("GROUND_B"));
        file.LastWriteTimeUtc = originalWriteTime.AddMinutes(1);
        file.Refresh();
        Assert.Equal(originalLength, file.Length);
        service.TryLoadFromPath(path);

        var layer = Assert.Single(service.Catalog.Layers);
        Assert.Equal("GROUND_B", layer.Name);
        Assert.Equal(1, layer.EntityCount);
        Assert.True(layer.HasEntitySectionContent);
    }

    [Fact]
    public void TryLoadFromEmbedded_OneByteTruncatedDxf_ReplacesOldLayersAndAllowsNextImport()
    {
        var service = new LayerCatalogStateService(new DxfLayerCatalogReader());
        service.TryLoadFromEmbedded(Encoding.UTF8.GetBytes(CreateDxf("SIGNAL_A")));
        Assert.Equal("SIGNAL_A", Assert.Single(service.Catalog.Layers).Name);

        service.TryLoadFromEmbedded([(byte)'0']);

        Assert.Empty(service.Catalog.Layers);

        service.TryLoadFromEmbedded(Encoding.UTF8.GetBytes(CreateDxf("GROUND_B")));

        Assert.Equal("GROUND_B", Assert.Single(service.Catalog.Layers).Name);
    }

    private static string CreateDxf(string layer) =>
        "0\r\nSECTION\r\n2\r\nENTITIES\r\n0\r\nLINE\r\n8\r\n" + layer +
        "\r\n10\r\n0\r\n20\r\n0\r\n11\r\n1\r\n21\r\n1\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n";
}
