using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ActiveAreaPixelsSettingsTests
{
    [Fact]
    public void NewViewModel_HasNoPixelCounts()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal(0m, vm.ActiveAreaPixelsX);
        Assert.Equal(0m, vm.ActiveAreaPixelsY);
    }

    [Fact]
    public void SetPixelCounts_MarksTheProjectUnsavedAndNormalizesTheValue()
    {
        var vm = new FreeformHelperViewModel();
        vm.HasUnsavedChanges = false;

        vm.ActiveAreaPixelsX = 6480.4m;
        vm.ActiveAreaPixelsY = 848m;

        Assert.Equal(6480m, vm.ActiveAreaPixelsX);
        Assert.Equal(848m, vm.ActiveAreaPixelsY);
        Assert.True(vm.HasUnsavedChanges);

        vm.ActiveAreaPixelsX = -5m;
        vm.ActiveAreaPixelsY = 500_000m;

        Assert.Equal(0m, vm.ActiveAreaPixelsX);
        Assert.Equal(100_000m, vm.ActiveAreaPixelsY);
    }

    [Fact]
    public async Task SetPixelCounts_DoesNotRebuildTheGridOrChangeTheAreaSize()
    {
        var vm = new FreeformHelperViewModel();
        await vm.WaitForGridRebuildIdleAsync();
        var width = vm.ActiveAreaWidth;
        var height = vm.ActiveAreaHeight;
        const string sentinel = "AA pixels test sentinel";
        vm.StatusText = sentinel;

        vm.ActiveAreaPixelsX = 6480m;
        vm.ActiveAreaPixelsY = 848m;
        await vm.WaitForGridRebuildIdleAsync();

        // A grid rebuild always writes its own status text, so an untouched sentinel proves there was none.
        Assert.Equal(sentinel, vm.StatusText);
        Assert.Equal(width, vm.ActiveAreaWidth);
        Assert.Equal(height, vm.ActiveAreaHeight);
    }

    [Fact]
    public void SetPixelCounts_ToZeroAfterValuesReadsBackAsNotSet()
    {
        var vm = new FreeformHelperViewModel();
        vm.ActiveAreaPixelsX = 6480m;
        vm.ActiveAreaPixelsY = 848m;

        vm.ActiveAreaPixelsX = 0m;
        vm.ActiveAreaPixelsY = 0m;

        Assert.Equal(0m, vm.ActiveAreaPixelsX);
        Assert.Equal(0m, vm.ActiveAreaPixelsY);
    }

    [Fact]
    public void SettingsWindowDraft_OpensCleanWithoutAStatusLine()
    {
        var vm = new FreeformHelperViewModel();

        var settings = vm.CreateSettingsWindowViewModel();

        Assert.Equal(0m, settings.ActiveAreaPixels.PixelsX);
        Assert.Equal(0m, settings.ActiveAreaPixels.PixelsY);
        Assert.False(settings.ActiveAreaPixels.IsPixelsXEdited);
        Assert.False(settings.ActiveAreaPixels.IsPixelsYEdited);
        Assert.False(settings.ActiveAreaPixels.HasStatus);
    }

    [Fact]
    public void SettingsWindowDraft_SavingUntouchedKeepsCountsOfAProjectLoadedAfterItOpened()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();

        // The settings window is not modal. Loading project B replaces the counts while it stays open.
        vm.ActiveAreaPixelsX = 6480m;
        vm.ActiveAreaPixelsY = 848m;
        settings.SaveCommand.Execute(null);

        Assert.Equal(6480m, vm.ActiveAreaPixelsX);
        Assert.Equal(848m, vm.ActiveAreaPixelsY);
    }

    [Fact]
    public void SettingsWindowDraft_SavingWritesOnlyTheEditedAxis()
    {
        var vm = new FreeformHelperViewModel();
        vm.ActiveAreaPixelsX = 6480m;
        vm.ActiveAreaPixelsY = 848m;
        var settings = vm.CreateSettingsWindowViewModel();

        vm.ActiveAreaPixelsX = 3240m;
        settings.ActiveAreaPixels.PixelsY = 424m;
        settings.SaveCommand.Execute(null);

        Assert.Equal(3240m, vm.ActiveAreaPixelsX);
        Assert.Equal(424m, vm.ActiveAreaPixelsY);
    }

    [Fact]
    public void Undo_RestoresThePreviousPixelCount()
    {
        var vm = new FreeformHelperViewModel();
        vm.ActiveAreaPixelsX = 6480m;
        vm.ActiveAreaPixelsX = 3000m;

        vm.Undo();

        Assert.Equal(6480m, vm.ActiveAreaPixelsX);
    }

    [Fact]
    public void SettingsWindowDraft_StartsFromTheViewModelAndAppliesOnlyOnSave()
    {
        var vm = new FreeformHelperViewModel();
        vm.ActiveAreaPixelsX = 6480m;
        vm.ActiveAreaPixelsY = 848m;

        var settings = vm.CreateSettingsWindowViewModel();
        Assert.Equal(6480m, settings.ActiveAreaPixels.PixelsX);
        Assert.Equal(848m, settings.ActiveAreaPixels.PixelsY);

        settings.ActiveAreaPixels.PixelsX = 3240m;
        settings.ActiveAreaPixels.PixelsY = 424m;
        Assert.Equal(6480m, vm.ActiveAreaPixelsX);
        Assert.Equal(848m, vm.ActiveAreaPixelsY);

        settings.CancelCommand.Execute(null);
        Assert.Equal(6480m, vm.ActiveAreaPixelsX);

        var second = vm.CreateSettingsWindowViewModel();
        second.ActiveAreaPixels.PixelsX = 3240m;
        second.ActiveAreaPixels.PixelsY = 424m;
        second.SaveCommand.Execute(null);

        Assert.Equal(3240m, vm.ActiveAreaPixelsX);
        Assert.Equal(424m, vm.ActiveAreaPixelsY);
    }

    [Fact]
    public void SettingsWindowDraft_StatusFollowsTheDraftAreaSize()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();
        Assert.False(settings.ActiveAreaPixels.HasStatus);
        Assert.Equal(string.Empty, settings.ActiveAreaPixels.StatusText);

        settings.ActiveAreaWidth = 915.52m;
        settings.ActiveAreaHeight = 119.59m;
        settings.ActiveAreaPixels.PixelsX = 6480m;
        Assert.True(settings.ActiveAreaPixels.HasStatus);
        Assert.StartsWith("Check:", settings.ActiveAreaPixels.StatusText, StringComparison.Ordinal);

        settings.ActiveAreaPixels.PixelsY = 848m;
        Assert.StartsWith("Pixel size", settings.ActiveAreaPixels.StatusText, StringComparison.Ordinal);

        settings.ActiveAreaPixels.PixelsY = 600m;
        Assert.StartsWith("Check:", settings.ActiveAreaPixels.StatusText, StringComparison.Ordinal);

        settings.ActiveAreaHeight = 85m;
        Assert.StartsWith("Pixel size", settings.ActiveAreaPixels.StatusText, StringComparison.Ordinal);
    }
}
