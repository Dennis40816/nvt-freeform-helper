using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ManualRangeErrorFlagBehaviorTests
{
    [Fact]
    public async Task ManualRangeInput_OutOfBounds_ShowsError()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();

        vm.ManualRowsRange = "999999";
        vm.ManualColsRange = "0";

        Assert.NotEmpty(vm.ManualRangeError);
        Assert.True(vm.HasManualRangeError);
        await WaitForGridRebuildAsync(vm);
    }

    [Fact]
    public async Task ManualRangeInput_OutOfBounds_RaisesHasManualRangeErrorAfterManualRangeError()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.ManualRowsRange = "999999";
        var changes = RecordNotifications(vm, nameof(vm.ManualRangeError), nameof(vm.HasManualRangeError));

        vm.ManualColsRange = "0";

        Assert.Equal([nameof(vm.ManualRangeError), nameof(vm.HasManualRangeError)], changes);
        await WaitForGridRebuildAsync(vm);
    }

    [Fact]
    public async Task ManualRangeInput_ClearedAfterError_HidesError()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.ManualRowsRange = "999999";
        vm.ManualColsRange = "0";

        vm.ManualRowsRange = string.Empty;
        vm.ManualColsRange = string.Empty;

        Assert.Equal(string.Empty, vm.ManualRangeError);
        Assert.False(vm.HasManualRangeError);
        await WaitForGridRebuildAsync(vm);
    }

    [Fact]
    public async Task ManualRangeInput_ClearedAfterError_RaisesHasManualRangeErrorAfterManualRangeError()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.ManualRowsRange = "999999";
        vm.ManualColsRange = "0";
        var changes = RecordNotifications(vm, nameof(vm.ManualRangeError), nameof(vm.HasManualRangeError));

        vm.ManualRowsRange = string.Empty;
        vm.ManualColsRange = string.Empty;

        Assert.Equal([nameof(vm.ManualRangeError), nameof(vm.HasManualRangeError)], changes);
        await WaitForGridRebuildAsync(vm);
    }
}
