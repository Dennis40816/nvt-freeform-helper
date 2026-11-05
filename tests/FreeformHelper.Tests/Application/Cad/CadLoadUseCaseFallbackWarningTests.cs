// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadUseCaseFallbackWarningTests
{
    private static readonly byte[] _squareDxf = Encoding.UTF8.GetBytes(
        string.Join("\n", "0", "SECTION", "2", "ENTITIES", "0", "LWPOLYLINE", "8", "0", "70", "1",
            "10", "0", "20", "0", "10", "1", "20", "0", "10", "1", "20", "1", "10", "0", "20", "1",
            "0", "ENDSEC", "0", "EOF", string.Empty));

    [Fact]
    public void TryLoadFromProject_PathMissing_UsesEmbeddedAndWarnsWithThePath()
    {
        var useCase = new CadLoadUseCase(new DxfImportService());
        var missing = TestFiles.NewTempFilePath("missing-", ".dxf");

        var outcome = useCase.TryLoadFromProject(missing, _squareDxf, new DxfImportOptions());

        Assert.NotNull(outcome);
        Assert.Equal(CadLoadSource.Embedded, outcome.Source);
        Assert.NotEmpty(outcome.Cad.Pads);
        Assert.StartsWith("Warning: The DXF file was not found:", outcome.FallbackWarning, StringComparison.Ordinal);
        Assert.Contains(missing, outcome.FallbackWarning, StringComparison.Ordinal);
        Assert.EndsWith("Using the DXF copy stored in the project.", outcome.FallbackWarning, StringComparison.Ordinal);
    }

    [Fact]
    public void TryLoadFromProject_PathUnreadable_UsesEmbeddedAndWarnsWithTheReason()
    {
        var useCase = new CadLoadUseCase(new DxfImportService());
        var broken = TestFiles.NewTempFilePath("broken-", ".dxf");
        try
        {
            TestFiles.WriteText(broken, "0\nSECTION\n2\nENTITIES\n0\nLWPOLYLINE\n10\nnot-a-number\n", Encoding.UTF8);

            var outcome = useCase.TryLoadFromProject(broken, _squareDxf, new DxfImportOptions());

            Assert.NotNull(outcome);
            Assert.Equal(CadLoadSource.Embedded, outcome.Source);
            Assert.StartsWith("Warning: The DXF file could not be read (", outcome.FallbackWarning, StringComparison.Ordinal);
            Assert.Contains(broken, outcome.FallbackWarning, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(broken);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryLoadFromProject_NoPathInTheProject_UsesEmbeddedWithoutWarning(string? path)
    {
        var useCase = new CadLoadUseCase(new DxfImportService());

        var outcome = useCase.TryLoadFromProject(path, _squareDxf, new DxfImportOptions());

        Assert.NotNull(outcome);
        Assert.Equal(CadLoadSource.Embedded, outcome.Source);
        Assert.Null(outcome.FallbackWarning);
    }

    [Fact]
    public void TryLoadFromProject_PathReadable_UsesThePathWithoutWarning()
    {
        var useCase = new CadLoadUseCase(new DxfImportService());
        var path = TestFiles.NewTempFilePath("good-", ".dxf");
        try
        {
            TestFiles.WriteBytes(path, _squareDxf);

            var outcome = useCase.TryLoadFromProject(path, _squareDxf, new DxfImportOptions());

            Assert.NotNull(outcome);
            Assert.Equal(CadLoadSource.Path, outcome.Source);
            Assert.Null(outcome.FallbackWarning);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
