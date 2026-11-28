// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShellInitializationOrderCharacterizationTests
{
    [AvaloniaFact]
    public async Task Constructor_InitializesOptionsAndCommandsBeforeProjectOverridesAppSettingsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var logLevel = new ProcessLogLevelScope("Info");
        using var files = new SyntheticWorkflowProjectScope();
        var store = new AppGeneralSettingsStore(files.PathFor("app.json"));
        store.Save(new AppGeneralSettingsDocument
        {
            Behavior = new AppGeneralBehaviorSettings { ApplyVisualPreferencesOnProjectLoad = false },
            Import = new UiImportSnapshot { OnlyClosedPolylines = false, LogLevel = "Warn" },
            View = new UiViewSnapshot { CadLineWidth = 0.75, NotchPreviewAutoPlayEnabled = false },
        });

        var vm = new FreeformHelperViewModel(store);

        AssertInitializedOptions(vm);
        AssertInitializedCommands(vm);
        Assert.Equal("Ready.", vm.StatusText);
        Assert.False(vm.ImportOnlyClosedPolylines);
        Assert.Equal(0.75m, vm.CadLineWidth);
        Assert.Equal("Warn", vm.SelectedLogLevelOption.Value);
        var project = new ProjectFile();
        project.Settings.Grid.XChannels = 2;
        project.Settings.Grid.YChannels = 2;
        project.Settings.View.AutoReplayStep2AfterProjectLoad = false;
        project.UiSnapshot.Import = new UiImportSnapshot { OnlyClosedPolylines = true, LogLevel = "Info" };
        project.UiSnapshot.View = new UiViewSnapshot { CadLineWidth = 0.25, NotchPreviewAutoPlayEnabled = false };
        var path = files.PathFor("project.json");
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(4, vm.RegularPads.Count);
        Assert.True(vm.ImportOnlyClosedPolylines);
        Assert.Equal(0.25m, vm.CadLineWidth);
        Assert.Equal("Info", vm.SelectedLogLevelOption.Value);
    }

    [AvaloniaFact]
    public void InteractionSelection_AfterConstruction_UpdatesAndClearsViewModelSelection()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new SyntheticWorkflowProjectScope();
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(files.PathFor("app.json")));
        var pad = TestGeometryFactory.CreateCadPad(17, "signal", 0, 0, 10, 10);
        vm.CadPads.Add(pad);
        Assert.Empty(vm.SelectedCadPadIds);

        vm.InteractionState.SetSelection([pad.Id], []);

        Assert.Equal(pad.Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal("CAD: 1 pads.", vm.CadSelectionText);
        Assert.Empty(vm.SelectedRegularPadIndices);

        vm.InteractionState.SetSelection([], []);

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal("CAD: 0 pads.", vm.CadSelectionText);
    }

    private static void AssertInitializedOptions(FreeformHelperViewModel vm)
    {
        AssertFirstOption(vm.ScanOrderOptions, vm.SelectedScanOrderOption);
        Assert.NotEmpty(vm.GridAlignmentOptions);
        Assert.Contains(vm.SelectedGridAlignmentOption, vm.GridAlignmentOptions);
        AssertFirstOption(vm.RegularSourceModeOptions, vm.SelectedRegularSourceModeOption);
        AssertFirstOption(vm.DxfEditRotationScopeOptions, vm.SelectedDxfEditRotationScopeOption);
        Assert.NotEmpty(vm.LogLevelOptions);
        Assert.Contains(vm.SelectedLogLevelOption, vm.LogLevelOptions);
        AssertFirstOption(vm.CadOutputFwDiffAutoModeOptions, vm.SelectedCadOutputFwDiffAutoModeOption);
        AssertFirstOption(vm.NotchExportFileTypeOptions, vm.SelectedNotchExportFileTypeOption);
        AssertFirstOption(vm.NotchExportProfileOptions, vm.SelectedNotchExportProfileOption);
        AssertFirstOption(vm.NotchCompensationModelOptions, vm.SelectedNotchCompensationModelOption);
        AssertFirstOption(vm.BoundLayerOptions, vm.SelectedBoundLayerOption);
        AssertFirstOption(vm.RegularSourceLayerOptions, vm.SelectedRegularSourceLayerOption);
    }

    private static void AssertFirstOption<T>(IReadOnlyList<T> options, T selected)
    {
        Assert.NotEmpty(options);
        Assert.Equal(options[0], selected);
    }

    private static void AssertInitializedCommands(FreeformHelperViewModel vm)
    {
        object?[] commands =
        [
            vm.OpenDxfCommand, vm.MatchCommand, vm.ImportRegularVisibilityMaskCommand,
            vm.ClearRegularVisibilityMaskCommand, vm.AutoDetectFreeformsCommand,
            vm.SaveProjectCommand, vm.LoadProjectCommand, vm.ExportNotchCommand,
            vm.ExportDxfVisibleCommand, vm.ExportDxfAllCommand, vm.ExportDxfLayerImageCommand,
            vm.ShowNotchDetailCommand, vm.ClearStep1ResultCommand, vm.ClearStep2ResultCommand,
            vm.ClearStep3ResultCommand, vm.ClearStep4ResultCommand, vm.ClearStep5ResultCommand,
            vm.UseSelectedRegularForNotchValidationCommand, vm.AnalyzeNotchValidationCommand,
            vm.ClearNotchValidationCommand, vm.FocusNotchValidationItemCommand,
            vm.PreviousNotchPreviewStageCommand, vm.NextNotchPreviewStageCommand,
            vm.FitCommand, vm.ClearSelectionCommand, vm.QuickFocusCommand, vm.UndoCommand,
            vm.SetFreeformNoneCommand, vm.SetFreeformXCommand, vm.SetFreeformYCommand, vm.SetFreeformXYCommand,
            vm.CheckDxfQualityCommand, vm.SelectDxfOverlapPadsCommand, vm.ClearDxfOverlapHighlightsCommand,
            vm.ShowDxfOverlapDetailsCommand, vm.AnalyzeIndexMappingCommand,
            vm.OffsetSelectedCadOutputFwDiffIndicesCommand, vm.SelectStep4DuplicateDiffGroupCommand,
            vm.DeleteSelectedCadPadsCommand, vm.RestoreLastDeletedCadPadsCommand,
            vm.RestoreAllHiddenCadPadsCommand, vm.CombineSelectedCadPadsCommand, vm.ClearCombinedCadPadsCommand,
            vm.ResetAllDxfEditsCommand, vm.OpenHiddenDxfEditChangeListCommand,
            vm.OpenDuplicateDxfEditChangeListCommand, vm.OpenCombinedDxfEditChangeListCommand,
            vm.OpenMovedDxfEditChangeListCommand, vm.OpenRotatedDxfEditChangeListCommand,
            vm.MoveSelectedCadPadsToLayerCommand, vm.CreateLayerAndMoveSelectedCadPadsCommand,
            vm.RotateSelectedCadPadsCommand,
        ];
        Assert.All(commands, command => Assert.NotNull(command));
    }
}
