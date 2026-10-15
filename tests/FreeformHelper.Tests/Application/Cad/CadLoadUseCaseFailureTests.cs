using System.Text;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how <see cref="CadLoadUseCase.TryLoadFromProject"/> treats a failing path import.
/// </summary>
public sealed class CadLoadUseCaseFailureTests
{
    // An existing file, so the path branch is taken without creating anything.
    private static string ExistingPath => typeof(CadLoadUseCaseFailureTests).Assembly.Location;

    private static byte[] ValidEmbeddedDxfBytes => Encoding.UTF8.GetBytes(
        """
        0
        SECTION
        2
        ENTITIES
        0
        LWPOLYLINE
        8
        0
        70
        1
        10
        0
        20
        0
        10
        1
        20
        0
        10
        1
        20
        1
        10
        0
        20
        1
        0
        ENDSEC
        0
        EOF
        """);

    [Fact]
    public void TryLoadFromProject_WhenPathImportFailsWithOrdinaryException_FallsBackToEmbedded()
    {
        var service = new DxfImportService();
        var useCase = new CadLoadUseCase(ThrowInvalidOperation, service.ImportFromStream);

        var outcome = useCase.TryLoadFromProject(ExistingPath, ValidEmbeddedDxfBytes, new DxfImportOptions());

        Assert.NotNull(outcome);
        Assert.Equal(CadLoadSource.Embedded, outcome!.Source);
        Assert.True(outcome.Cad.Pads.Count > 0);
    }

    [Fact]
    public void TryLoadFromProject_WhenPathImportFailsAndNoEmbedded_ThrowsTheOriginalException()
    {
        var service = new DxfImportService();
        var failure = new InvalidOperationException("path import failed");
        var useCase = new CadLoadUseCase((_, _) => throw failure, service.ImportFromStream);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => useCase.TryLoadFromProject(ExistingPath, null, new DxfImportOptions()));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void TryLoadFromProject_WhenPathAndEmbeddedImportsFail_ThrowsAggregateWithBothExceptions()
    {
        var pathFailure = new InvalidOperationException("path import failed");
        var embeddedFailure = new FormatException("embedded import failed");
        var useCase = new CadLoadUseCase(
            (_, _) => throw pathFailure,
            (_, _) => throw embeddedFailure);

        var thrown = Assert.Throws<AggregateException>(
            () => useCase.TryLoadFromProject(ExistingPath, ValidEmbeddedDxfBytes, new DxfImportOptions()));

        Assert.Equal(new Exception[] { pathFailure, embeddedFailure }, thrown.InnerExceptions);
    }

    private static CadPadSet ThrowInvalidOperation(string path, DxfImportOptions options)
    {
        throw new InvalidOperationException("path import failed");
    }
}
