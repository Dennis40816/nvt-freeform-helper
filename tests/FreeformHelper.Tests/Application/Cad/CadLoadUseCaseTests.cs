using System.Text;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadUseCaseTests
{
    [Fact]
    public void TryLoadFromProject_PathInvalid_FallsBackToEmbedded()
    {
        var useCase = new CadLoadUseCase(new DxfImportService());
        var options = new DxfImportOptions();
        var brokenPath = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.dxf");

        try
        {
            File.WriteAllText(brokenPath, BrokenDxfContent, Encoding.UTF8);

            var outcome = useCase.TryLoadFromProject(brokenPath, ValidEmbeddedDxfBytes, options);

            Assert.NotNull(outcome);
            Assert.Equal(CadLoadSource.Embedded, outcome!.Source);
            Assert.True(outcome.Cad.Pads.Count > 0);
        }
        finally
        {
            if (File.Exists(brokenPath))
            {
                File.Delete(brokenPath);
            }
        }
    }

    [Fact]
    public void TryLoadFromProject_PathInvalid_AndNoEmbedded_Throws()
    {
        var useCase = new CadLoadUseCase(new DxfImportService());
        var options = new DxfImportOptions();
        var brokenPath = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.dxf");

        try
        {
            File.WriteAllText(brokenPath, BrokenDxfContent, Encoding.UTF8);

            Assert.Throws<FormatException>(() => useCase.TryLoadFromProject(brokenPath, null, options));
        }
        finally
        {
            if (File.Exists(brokenPath))
            {
                File.Delete(brokenPath);
            }
        }
    }

    [Fact]
    public void TryLoadFromEmbedded_DetectsSameLayerExactDuplicates_ButKeepsRawCadSet()
    {
        var useCase = new CadLoadUseCase(new DxfImportService());
        var options = new DxfImportOptions();
        var bytes = Encoding.UTF8.GetBytes(
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
            LWPOLYLINE
            8
            OTHER
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

        var outcome = useCase.TryLoadFromEmbedded(bytes, options);

        Assert.NotNull(outcome);
        Assert.Equal(3, outcome!.Cad.Pads.Count);
        Assert.Single(outcome.DuplicateSanitization.ExcludedPadIds);
        Assert.Equal(1, outcome.DuplicateSanitization.DuplicateSameLayerGroupCount);
        Assert.Contains(0, outcome.Cad.Pads.Select(static pad => pad.Id));
        Assert.Contains(1, outcome.Cad.Pads.Select(static pad => pad.Id));
        Assert.Contains(2, outcome.Cad.Pads.Select(static pad => pad.Id));
    }

    private static readonly byte[] ValidEmbeddedDxfBytes = Encoding.UTF8.GetBytes(
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

    private const string BrokenDxfContent =
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
        not-a-number
        20
        0
        0
        ENDSEC
        0
        EOF
        """;
}
