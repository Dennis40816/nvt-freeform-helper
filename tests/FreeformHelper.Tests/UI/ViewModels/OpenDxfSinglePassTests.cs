using System.Reflection;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

// The tokenization counter is process-wide, so isolate these tests from other DXF readers.
[Collection("HeadlessUiSerial")]
public sealed class OpenDxfSinglePassTests
{
    [Theory]
    [InlineData(0, true, true)]
    [InlineData(0, true, false)]
    [InlineData(0, false, true)]
    [InlineData(0, false, false)]
    [InlineData(1, true, true)]
    [InlineData(1, true, false)]
    [InlineData(1, false, true)]
    [InlineData(1, false, false)]
    [InlineData(2, true, true)]
    [InlineData(2, true, false)]
    [InlineData(2, false, true)]
    [InlineData(2, false, false)]
    [InlineData(3, true, true)]
    [InlineData(3, true, false)]
    [InlineData(3, false, true)]
    [InlineData(3, false, false)]
    public async Task OpenDxfCommand_TokenizesOnce_AndPreservesTwoReadPadsAndCatalog(
        int fixture, bool onlyClosed, bool includeBlocks)
    {
        var path = DxfSinglePassImportTests.WriteTempDxf(fixture);
        try
        {
            var options = new DxfImportOptions
            {
                OnlyClosedPolylines = onlyClosed,
                IncludeBlockPolylines = includeBlocks,
            };
            var beforeBaseline = GetTokenizationCount();
            var expectedPads = DxfPadImporter.Import(path, options);
            var expectedCatalog = DxfLayerCatalogReader.ReadFromPath(path);
            Assert.Equal(2, GetTokenizationCount() - beforeBaseline);

            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
                ImportOnlyClosedPolylines = onlyClosed,
                ImportBlockPolylines = includeBlocks,
            };
            await vm.WaitForGridRebuildIdleAsync();

            var beforeOpen = GetTokenizationCount();
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();

            DxfSinglePassImportTests.AssertPadsEqual(expectedPads, GetField<CadPadSet>(vm, "_cad"));
            var state = GetField<LayerCatalogStateService>(vm, "_layerCatalogStateService");
            DxfSinglePassImportTests.AssertCatalogsEqual(expectedCatalog, state.Catalog);
            Assert.False(vm.IsCadLoadCanvasOverlayVisible);
            Assert.Equal(1, GetTokenizationCount() - beforeOpen);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenDxfCommand_AfterEmbeddedProjectLoad_ReplacesCatalogAndPreservesPathCache()
    {
        var path = DxfSinglePassImportTests.WriteTempDxf(0);
        var embeddedPath = DxfSinglePassImportTests.WriteTempDxf(1);
        var projectPath = Path.Combine(Path.GetTempPath(), $"dxf-single-pass-project-{Guid.NewGuid():N}.json");
        try
        {
            var options = new DxfImportOptions();
            var expectedOpenPads = DxfPadImporter.Import(path, options);
            var expectedOpenCatalog = DxfLayerCatalogReader.ReadFromPath(path);
            var expectedEmbeddedPads = DxfPadImporter.Import(embeddedPath, options);
            var expectedEmbeddedCatalog = DxfLayerCatalogReader.ReadFromPath(embeddedPath);
            await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile
            {
                EmbeddedDxf = File.ReadAllBytes(embeddedPath),
                EmbeddedDxfName = Path.GetFileName(embeddedPath),
            }, CancellationToken.None);
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
                ImportOnlyClosedPolylines = options.OnlyClosedPolylines,
                ImportBlockPolylines = options.IncludeBlockPolylines,
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();

            var beforeProjectLoad = GetTokenizationCount();
            await vm.LoadProjectCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();

            Assert.Equal(2, GetTokenizationCount() - beforeProjectLoad);
            var state = GetField<LayerCatalogStateService>(vm, "_layerCatalogStateService");
            DxfSinglePassImportTests.AssertPadsEqual(expectedEmbeddedPads, GetField<CadPadSet>(vm, "_cad"));
            DxfSinglePassImportTests.AssertCatalogsEqual(expectedEmbeddedCatalog, state.Catalog);

            var beforeReopen = GetTokenizationCount();
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();

            Assert.Equal(1, GetTokenizationCount() - beforeReopen);
            DxfSinglePassImportTests.AssertPadsEqual(expectedOpenPads, GetField<CadPadSet>(vm, "_cad"));
            DxfSinglePassImportTests.AssertCatalogsEqual(expectedOpenCatalog, state.Catalog);

            var openCatalog = state.Catalog;
            state.Clear();
            var beforeCacheLoad = GetTokenizationCount();
            state.TryLoadFromPath(path);
            Assert.Same(openCatalog, state.Catalog);
            Assert.Equal(beforeCacheLoad, GetTokenizationCount());
        }
        finally
        {
            File.Delete(path);
            File.Delete(embeddedPath);
            File.Delete(projectPath);
        }
    }

    private static long GetTokenizationCount()
    {
        var property = typeof(DxfPadImporter).GetProperty("TokenizationCount", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<long>(property.GetValue(null));
    }

    private static T GetField<T>(FreeformHelperViewModel vm, string name)
    {
        var field = typeof(FreeformHelperViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(vm));
    }
}
