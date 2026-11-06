// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what a load by path returns and what it does to the open project (issue #64, long-method
/// split of the project load). The tests describe the current behavior: the result codes and
/// messages, the workflow replay flags, the custom pad values and the layer
/// toggle wiring. Timing values are not pinned.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadStateApplyCharacterizationTests
{
    private const decimal SavedThreshold = 0.27m;
    private const decimal UnsavedThreshold = 0.61m;
    private const string NoCadReplayStatus = "Step 1 auto replay skipped";

    [AvaloniaFact]
    public async Task LoadFromPath_PathWithSurroundingSpaces_ReturnsTheLoadedResultForTheFullPath()
    {
        var scope = new LoadScope();
        var path = await scope.SaveProjectAsync(vm => vm.MatchThreshold = SavedThreshold);
        var vm = scope.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync($"  {path}  ");

        var fullPath = Path.GetFullPath(path);
        Assert.True(result.IsLoaded);
        Assert.Equal("OK", result.Code);
        Assert.Equal(fullPath, result.Path);
        Assert.Equal($"Project loaded: {fullPath}", result.Message);
        Assert.Equal(SavedThreshold, vm.MatchThreshold);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task LoadFromPath_ProjectWithoutCad_SkipsBothReplaysAndSaysSo()
    {
        var scope = new LoadScope();
        var path = await scope.SaveProjectAsync(vm => vm.AutoReplayStep2AfterProjectLoad = true);
        var vm = scope.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.False(result.Step1Replayed);
        Assert.False(result.Step2Replayed);
        Assert.Empty(vm.CadPads);
        Assert.Contains(NoCadReplayStatus, vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task LoadFromPath_CadProjectWithStep2AutoReplay_ReplaysBothSteps()
    {
        var scope = new LoadScope();
        var path = await scope.SaveMatchedCadProjectAsync(autoReplayStep2: true);
        var vm = scope.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.NotEmpty(vm.CadPads);
        Assert.True(result.Step1Replayed);
        Assert.True(result.Step2Replayed);
        Assert.Contains("Step 2 freeform restored", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task LoadFromPath_CadProjectWithStep2AutoReplayOff_ReplaysOnlyStep1()
    {
        var scope = new LoadScope();
        var path = await scope.SaveMatchedCadProjectAsync(autoReplayStep2: false);
        var vm = scope.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.NotEmpty(vm.CadPads);
        Assert.True(result.Step1Replayed);
        Assert.False(result.Step2Replayed);
        Assert.DoesNotContain("Step 2 freeform restored", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task LoadFromPath_PendingEditsCannotProceed_ReturnsCancelledAndKeepsTheOpenProject()
    {
        var scope = new LoadScope();
        var path = await scope.SaveProjectAsync(vm => vm.MatchThreshold = SavedThreshold);
        var vm = scope.CreateViewModel();
        vm.MatchThreshold = UnsavedThreshold;
        vm.EnsureNoPendingEdits = () => false;

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.False(result.IsLoaded);
        Assert.Equal("CANCELLED", result.Code);
        Assert.Equal("Load cancelled (pending edits were not committed).", result.Message);
        Assert.Equal(Path.GetFullPath(path), result.Path);
        Assert.Equal(UnsavedThreshold, vm.MatchThreshold);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
    }

    [AvaloniaTheory]
    [InlineData(null, 0.0)]
    [InlineData(7.0, 7.0)]
    public async Task LoadFromPath_ReplacesTheCustomPadValuesOfTheOpenProject(double? savedValue, double expected)
    {
        var scope = new LoadScope();
        var path = await scope.SaveCadProjectAsync(savedValue is { } value ? (decimal)value : null);
        var vm = scope.CreateViewModel();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        var cadId = Assert.Single(vm.CadPads).Id;
        vm.SetCadPadCustomValues([cadId], 5m);
        Assert.Equal(5.0, vm.GetCadPadCustomValue(cadId));

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.Equal(expected, vm.GetCadPadCustomValue(cadId));
    }

    [AvaloniaFact]
    public async Task LoadFromPath_LayerTogglesOfThePreviousCad_NoLongerMarkTheProjectDirty()
    {
        var scope = new LoadScope();
        var path = await scope.SaveCadProjectAsync(customValue: null);
        var vm = scope.CreateViewModel();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        var stale = Assert.Single(vm.LayerToggles);

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.False(vm.HasUnsavedChanges);
        Assert.DoesNotContain(stale, vm.LayerToggles);
        stale.IsSelected = !stale.IsSelected;
        Assert.False(vm.HasUnsavedChanges);
    }

    /// <summary>One project file, one DXF file and view models that never touch the user's settings. The files sit in the shared temp folder that the test process removes at exit.</summary>
    private sealed class LoadScope
    {
        private readonly string _appSettingsPath = TestFiles.NewTempFilePath("freeform-helper-app-", ".json");
        private readonly string _dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(("signal", 0, 0, 30, 10));
        private readonly string _projectPath = TestFiles.NewTempFilePath("freeform-helper-load-", ".json");

        public FreeformHelperViewModel CreateViewModel() => new(new AppGeneralSettingsStore(_appSettingsPath))
        {
            ApplyAppVisualPreferencesOnProjectLoad = false,
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(_dxfPath),
            PickSaveProjectPathAsync = () => Task.FromResult<string?>(_projectPath),
            PickLoadProjectPathAsync = () => Task.FromResult<string?>(_projectPath),
            ConfirmEmbedDxfAsync = () => Task.FromResult(false),
        };

        public async Task<string> SaveProjectAsync(Action<FreeformHelperViewModel> configure)
        {
            var vm = CreateViewModel();
            configure(vm);
            Assert.True(await vm.SaveProjectAsync());
            return _projectPath;
        }

        public async Task<string> SaveCadProjectAsync(decimal? customValue)
        {
            var vm = CreateViewModel();
            await vm.OpenDxfCommand.ExecuteAsync(null);
            if (customValue is { } value)
            {
                vm.SetCadPadCustomValues([Assert.Single(vm.CadPads).Id], value);
            }

            Assert.True(await vm.SaveProjectAsync());
            return _projectPath;
        }

        public async Task<string> SaveMatchedCadProjectAsync(bool autoReplayStep2)
        {
            var vm = CreateViewModel();
            vm.XChannels = 3;
            vm.YChannels = 1;
            vm.ActiveAreaWidth = 30;
            vm.ActiveAreaHeight = 10;
            vm.GridPaddingPercent = 0;
            vm.AutoReplayStep2AfterProjectLoad = autoReplayStep2;
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await vm.MatchCommand.ExecuteAsync(null);
            await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
            Assert.True(await vm.SaveProjectAsync());
            return _projectPath;
        }
    }
}
