// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsSyncActiveAreaBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Load_ZeroActiveAreaDimension_UsesItsDefaultAndPreservesTheOtherDimensionAsync(bool zeroWidth)
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            var path = files.PathFor("zero-aa.json");
            var project = new ProjectFile
            {
                Settings = new ProjectSettings
                {
                    Grid = new GridSettings
                    {
                        XChannels = 2,
                        YChannels = 2,
                        ActiveAreaWidth = zeroWidth ? 0d : 73.25d,
                        ActiveAreaHeight = zeroWidth ? 41.5d : 0d,
                    },
                },
            };
            await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);

            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
            await vm.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(2m, vm.XChannels);
            Assert.Equal(2m, vm.YChannels);
            var defaults = new GridSettings();
            var expectedWidth = zeroWidth ? defaults.ActiveAreaWidth : 73.25d;
            var expectedHeight = zeroWidth ? 41.5d : defaults.ActiveAreaHeight;
            Assert.Equal((decimal)expectedWidth, vm.ActiveAreaWidth);
            Assert.Equal((decimal)expectedHeight, vm.ActiveAreaHeight);
            Assert.False(vm.HasUnsavedChanges);
            var draft = vm.CreateSettingsWindowViewModel();
            Assert.Equal(vm.ActiveAreaWidth, draft.ActiveAreaWidth);
            Assert.Equal(vm.ActiveAreaHeight, draft.ActiveAreaHeight);

            Assert.True(await vm.SaveProjectAsync(), vm.StatusText);

            var saved = JsonProjectStore.Load(path);
            Assert.Equal(expectedWidth, saved.Settings.Grid.ActiveAreaWidth);
            Assert.Equal(expectedHeight, saved.Settings.Grid.ActiveAreaHeight);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }
}
