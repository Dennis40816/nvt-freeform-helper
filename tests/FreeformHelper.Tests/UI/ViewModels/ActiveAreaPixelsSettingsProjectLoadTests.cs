using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ActiveAreaPixelsSettingsProjectLoadTests
{
    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(100)]
    public async Task StaleSettingsDraft_SavedAfterLoadingProjectB_KeepsProjectBPixelCounts(int? editedPixelsX)
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(project =>
        {
            project.ActiveAreaPixelsX = 6480m;
            project.ActiveAreaPixelsY = 848m;
        });
        var projectAPath = files.PathFor("project-a.json");
        TestFiles.CopyOverwriting(files.PathFor("project.json"), projectAPath);
        await files.SaveAsync(project =>
        {
            project.ActiveAreaPixelsX = 3240m;
            project.ActiveAreaPixelsY = 424m;
        });
        var projectBPath = files.PathFor("project-b.json");
        TestFiles.CopyOverwriting(files.PathFor("project.json"), projectBPath);

        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(projectAPath)).IsLoaded);
        var settings = vm.CreateSettingsWindowViewModel();
        Assert.Equal(6480m, settings.ActiveAreaPixels.PixelsX);
        if (editedPixelsX is { } edited)
        {
            settings.ActiveAreaPixels.PixelsX = edited;
        }

        // The settings window is not modal, so project B loads through the real load path while it stays open.
        Assert.True((await vm.LoadProjectFromPathAsync(projectBPath)).IsLoaded);
        Assert.Equal(3240m, vm.ActiveAreaPixelsX);
        Assert.Equal(424m, vm.ActiveAreaPixelsY);
        settings.SaveCommand.Execute(null);

        Assert.Equal(3240m, vm.ActiveAreaPixelsX);
        Assert.Equal(424m, vm.ActiveAreaPixelsY);
    }
}
