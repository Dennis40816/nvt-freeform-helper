using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how <see cref="LayerCatalogStateService"/> treats a failing DXF catalog read.
/// </summary>
public sealed class LayerCatalogStateServiceFailureTests
{
    // An existing file, so the path branch is taken without creating anything.
    private static string ExistingPath => typeof(LayerCatalogStateServiceFailureTests).Assembly.Location;

    private static byte[] SomeBytes => [1, 2, 3];

    [Fact]
    public void TryLoadFromPath_WhenReadFailsWithOrdinaryException_LeavesAnEmptyCatalogAndDoesNotThrow()
    {
        var service = new LayerCatalogStateService(ThrowInvalidOperation, ThrowInvalidOperation);

        service.TryLoadFromPath(ExistingPath);

        Assert.Empty(service.Catalog.Layers);
    }

    [Fact]
    public void TryLoadFromEmbedded_WhenReadFailsWithOrdinaryException_LeavesAnEmptyCatalogAndDoesNotThrow()
    {
        var service = new LayerCatalogStateService(ThrowInvalidOperation, ThrowInvalidOperation);

        service.TryLoadFromEmbedded(SomeBytes);

        Assert.Empty(service.Catalog.Layers);
    }

    [Fact]
    public void TryLoadFromPath_WhenReadSucceeds_ExposesTheCatalog()
    {
        var catalog = DxfLayerCatalog.Empty;
        var service = new LayerCatalogStateService(_ => catalog, ThrowInvalidOperation);

        service.TryLoadFromPath(ExistingPath);

        Assert.Same(catalog, service.Catalog);
    }

    private static DxfLayerCatalog ThrowInvalidOperation(string path)
    {
        throw new InvalidOperationException("read failed");
    }

    private static DxfLayerCatalog ThrowInvalidOperation(Stream stream)
    {
        throw new InvalidOperationException("read failed");
    }
}
