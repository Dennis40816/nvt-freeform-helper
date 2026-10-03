using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportServiceTests
{
    [Fact]
    public void GenerateCadAllocationResolvedBatch_RetainsPublicFacadeSignature()
    {
        var service = new NotchExportService();
        GenerateCadAllocationResolvedBatch facade = service.GenerateCadAllocationResolvedBatch;

        Assert.NotNull(facade);
    }

    private delegate (NotchTableGenerator.CadAllocationResolvedBatch Batch, NotchTable Table)
        GenerateCadAllocationResolvedBatch(
            CadPadSet cad,
            RegularGrid grid,
            ProjectSettings settings,
            IProgress<NotchGenerationProgress>? progress,
            IReadOnlySet<int>? activeRegularPadIds,
            IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId);
}
