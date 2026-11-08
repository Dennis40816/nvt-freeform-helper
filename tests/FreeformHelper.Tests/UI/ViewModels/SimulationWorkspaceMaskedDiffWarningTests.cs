// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceMaskedDiffWarningTests
{
    [Fact]
    public void MaskedPadSharingAnEnabledDiff_IsWarnedWhileTheUniqueDiffPadIsNot()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 10, 11]);
        var table = new NotchTable(
        [
            new NotchTableRow(0, 10, 0, 313, new NotchV22Node(10, 50, 65535, 0, 65535, 0, 0), "half gain"),
        ]);
        var session = new SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1, ActiveRegularPadIds: new HashSet<int> { 0 });
        var vm = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session)
        {
            GlobalValue = 100d,
        };

        Assert.True(vm.HasDiffViolationPads);
        Assert.Equal(1, vm.DiffViolationPadCount);
        Assert.Equal("Potential mismatch pads: 1.", vm.DiffViolationSummaryText);
        Assert.Equal("REG 1", vm.DiffViolationPadListText);
        Assert.DoesNotContain("REG 2", vm.DiffViolationPadListText, StringComparison.Ordinal);
    }
}
