using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{

    [Fact]
    public async Task LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields()
    {
        var sourcePath = TestFiles.NewTempFilePath("freeform-helper-notch-source-", ".json");
        var savedPath = TestFiles.NewTempFilePath("freeform-helper-notch-saved-", ".json");
        try
        {
            await new JsonProjectStore().SaveAsync(sourcePath, new ProjectFile
            {
                Settings = new ProjectSettings
                {
                    Notch = new NotchSettings
                    {
                        ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                        EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                    },
                },
            }, CancellationToken.None);

            var vm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(sourcePath),
            };
            await vm.LoadProjectCommand.ExecuteAsync(null);

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(savedPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            var saved = JsonProjectStore.Load(savedPath);
            Assert.Equal(NotchComputationMode.LegacyRegularAnchor, saved.Settings.Notch.ComputationMode);
            Assert.Collection(
                saved.Settings.Notch.EnabledVersions,
                version => Assert.Equal(NotchAlgorithmVersion.V22, version));
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(savedPath);
        }
    }

    [Fact]
    public async Task LoadThenSaveProject_PreservesIgnoredMatchingCompatibilityValuesIndependently()
    {
        var sourcePath = TestFiles.NewTempFilePath("freeform-helper-matching-source-", ".json");
        var savedPath = TestFiles.NewTempFilePath("freeform-helper-matching-saved-", ".json");
        try
        {
            await new JsonProjectStore().SaveAsync(sourcePath, new ProjectFile
            {
                Settings = new ProjectSettings
                {
                    Matching = new MatchingSettings
                    {
                        Mode = MatchMode.RegularToCad,
                        MatchThreshold = 0.27,
                        EnableCentroidFallback = false,
                        NearestK = 197,
                    },
                },
                UiSnapshot = new ProjectUiSnapshot
                {
                    Matching = new UiMatchingSnapshot
                    {
                        MatchMode = "compatibility-custom-mode",
                        MatchThreshold = 0.84,
                        EnableCentroidFallback = true,
                        NearestK = -41,
                    },
                },
            }, CancellationToken.None);

            var vm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(sourcePath),
            };
            await vm.LoadProjectCommand.ExecuteAsync(null);
            vm.MatchThreshold = 0.63m;

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(savedPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            var saved = JsonProjectStore.Load(savedPath);
            Assert.Equal(MatchMode.RegularToCad, saved.Settings.Matching.Mode);
            Assert.Equal(0.63, saved.Settings.Matching.MatchThreshold, 6);
            Assert.False(saved.Settings.Matching.EnableCentroidFallback);
            Assert.Equal(197, saved.Settings.Matching.NearestK);
            Assert.Equal("compatibility-custom-mode", saved.UiSnapshot.Matching.MatchMode);
            Assert.Equal(0.63, saved.UiSnapshot.Matching.MatchThreshold, 6);
            Assert.True(saved.UiSnapshot.Matching.EnableCentroidFallback);
            Assert.Equal(-41, saved.UiSnapshot.Matching.NearestK);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(savedPath);
        }
    }


    [Fact]
    public async Task SaveThenLoadProject_RestoresImportLogLevelFromProjectSnapshot()
    {
        using var logLevel = new ProcessLogLevelScope("Info");
        var vm = new FreeformHelperViewModel();
        Assert.True(vm.LogLevelOptions.Count >= 2);

        var original = vm.SelectedLogLevelOption;
        var persisted = vm.LogLevelOptions.First(option => !option.Equals(original));
        var other = vm.LogLevelOptions.First(option => !option.Equals(persisted));

        vm.SelectedLogLevelOption = persisted;

        var path = TestFiles.NewTempFilePath("freeform-helper-", ".json");
        try
        {
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            var saveResult = await vm.SaveProjectAsync();
            Assert.True(saveResult);

            vm.SelectedLogLevelOption = other;
            Assert.Equal(other.Value, vm.SelectedLogLevelOption.Value);

            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);
            await vm.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(persisted.Value, vm.SelectedLogLevelOption.Value);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }


    [Fact]
    public async Task SaveThenLoadProject_RestoresStepAndExportSettingsFromProjectSnapshot()
    {
        using var logLevel = new ProcessLogLevelScope("Info");
        var vm = new FreeformHelperViewModel
        {
            ApplyAppVisualPreferencesOnProjectLoad = false
        };

        ApplyDistinctSettingsThroughSettingsWindow(vm);

        var persisted = CaptureRestoredSettings(vm);

        var path = TestFiles.NewTempFilePath("freeform-helper-", ".json");
        try
        {
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            var saveResult = await vm.SaveProjectAsync();
            Assert.True(saveResult);

            ChangeSettingsAwayFromSavedValues(vm);

            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);
            await vm.LoadProjectCommand.ExecuteAsync(null);

            // One comparison for every restored setting, so a failure lists all values that differ.
            Assert.Equal(persisted, CaptureRestoredSettings(vm));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }


    private static void ApplyDistinctSettingsThroughSettingsWindow(FreeformHelperViewModel vm)
    {
        var targetLogLevel = vm.LogLevelOptions.FirstOrDefault(option =>
            !option.Equals(vm.SelectedLogLevelOption));
        if (string.IsNullOrWhiteSpace(targetLogLevel.Display))
        {
            targetLogLevel = vm.SelectedLogLevelOption;
        }

        var targetFileType = vm.NotchExportFileTypeOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedNotchExportFileTypeOption.Value);
        if (string.IsNullOrWhiteSpace(targetFileType.Display))
        {
            targetFileType = vm.SelectedNotchExportFileTypeOption;
        }

        var targetProfile = vm.NotchExportProfileOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedNotchExportProfileOption.Value);
        if (string.IsNullOrWhiteSpace(targetProfile.Display))
        {
            targetProfile = vm.SelectedNotchExportProfileOption;
        }

        var targetAlignment = vm.GridAlignmentOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedGridAlignmentOption.Value);
        if (string.IsNullOrWhiteSpace(targetAlignment.Display))
        {
            targetAlignment = vm.SelectedGridAlignmentOption;
        }

        var targetEnableToFullRuleEngine = !vm.EnableToFullRuleEngine;
        var targetEnableToFullRuleTrace = !vm.EnableToFullRuleTrace;
        var targetStrictOverlap = Math.Clamp(vm.ToFullStrictOverlapPercent + 1.25m, 0m, 100m);
        var targetAutoReplayStep2 = !vm.AutoReplayStep2AfterProjectLoad;
        var targetMappingWeightIou = vm.MappingWeightIou + 0.75m;
        var targetShowNotchPreview = !vm.ShowNotchCanvasPreview;
        var targetShowToRegularLabels = !vm.ShowNotchToRegularLabels;
        var targetPreviewStage = vm.NotchPreviewVisualizationStep >= 3m ? 2m : 3m;
        var targetAutoPlay = !vm.NotchPreviewAutoPlayEnabled;
        var targetAutoPlayInterval = vm.NotchPreviewAutoPlayIntervalMs >= 900m ? 700m : 900m;
        var targetGlobalFont = Math.Clamp(vm.GlobalFontSizePercent + 6m, 80m, 140m);
        var targetImportOnlyClosed = !vm.ImportOnlyClosedPolylines;

        var settings = vm.CreateSettingsWindowViewModel();
        settings.EnableToFullRuleEngine = targetEnableToFullRuleEngine;
        settings.EnableToFullRuleTrace = targetEnableToFullRuleTrace;
        settings.ToFullStrictOverlapPercent = targetStrictOverlap;
        settings.AutoReplayStep2AfterProjectLoad = targetAutoReplayStep2;
        settings.MappingWeightIou = targetMappingWeightIou;
        settings.ShowNotchCanvasPreview = targetShowNotchPreview;
        settings.ShowNotchToRegularLabels = targetShowToRegularLabels;
        settings.NotchPreviewVisualizationStep = targetPreviewStage;
        settings.NotchPreviewAutoPlayEnabled = targetAutoPlay;
        settings.NotchPreviewAutoPlayIntervalMs = targetAutoPlayInterval;
        settings.GlobalFontSizePercent = targetGlobalFont;
        settings.ImportOnlyClosedPolylines = targetImportOnlyClosed;
        settings.SelectedLogLevelOption = targetLogLevel;
        settings.SelectedNotchExportFileTypeOption = targetFileType;
        settings.SelectedNotchExportProfileOption = targetProfile;
        settings.SelectedGridAlignmentOption = targetAlignment;
        settings.SaveCommand.Execute(null);
    }

    private static void ChangeSettingsAwayFromSavedValues(FreeformHelperViewModel vm)
    {
        vm.EnableToFullRuleEngine = !vm.EnableToFullRuleEngine;
        vm.EnableToFullRuleTrace = !vm.EnableToFullRuleTrace;
        vm.ToFullStrictOverlapPercent = vm.ToFullStrictOverlapPercent <= 1m ? 2m : vm.ToFullStrictOverlapPercent - 1m;
        vm.AutoReplayStep2AfterProjectLoad = !vm.AutoReplayStep2AfterProjectLoad;
        vm.MappingWeightIou = vm.MappingWeightIou + 0.5m;
        vm.ShowNotchCanvasPreview = !vm.ShowNotchCanvasPreview;
        vm.ShowNotchToRegularLabels = !vm.ShowNotchToRegularLabels;
        vm.NotchPreviewVisualizationStep = vm.NotchPreviewVisualizationStep >= 3m ? 1m : 3m;
        vm.NotchPreviewAutoPlayEnabled = !vm.NotchPreviewAutoPlayEnabled;
        vm.NotchPreviewAutoPlayIntervalMs = vm.NotchPreviewAutoPlayIntervalMs >= 900m ? 700m : 900m;
        vm.GlobalFontSizePercent = vm.GlobalFontSizePercent >= 130m ? 120m : 130m;
        vm.ImportOnlyClosedPolylines = !vm.ImportOnlyClosedPolylines;
        vm.SelectedLogLevelOption = vm.LogLevelOptions.FirstOrDefault(option =>
            !option.Equals(vm.SelectedLogLevelOption));
        LoggingBootstrapper.ApplyMinimumLevel(vm.SelectedLogLevelOption.Value);
        vm.SelectedNotchExportFileTypeOption = vm.NotchExportFileTypeOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedNotchExportFileTypeOption.Value);
        vm.SelectedNotchExportProfileOption = vm.NotchExportProfileOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedNotchExportProfileOption.Value);
        vm.SelectedGridAlignmentOption = vm.GridAlignmentOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedGridAlignmentOption.Value);
    }

    private static (string Setting, object? Value)[] CaptureRestoredSettings(FreeformHelperViewModel vm) =>
    [
        (nameof(vm.EnableToFullRuleEngine), vm.EnableToFullRuleEngine),
        (nameof(vm.EnableToFullRuleTrace), vm.EnableToFullRuleTrace),
        (nameof(vm.ToFullStrictOverlapPercent), vm.ToFullStrictOverlapPercent),
        (nameof(vm.AutoReplayStep2AfterProjectLoad), vm.AutoReplayStep2AfterProjectLoad),
        (nameof(vm.MappingWeightIou), vm.MappingWeightIou),
        (nameof(vm.ShowNotchCanvasPreview), vm.ShowNotchCanvasPreview),
        (nameof(vm.ShowNotchToRegularLabels), vm.ShowNotchToRegularLabels),
        (nameof(vm.NotchPreviewVisualizationStep), vm.NotchPreviewVisualizationStep),
        (nameof(vm.NotchPreviewAutoPlayEnabled), vm.NotchPreviewAutoPlayEnabled),
        (nameof(vm.NotchPreviewAutoPlayIntervalMs), vm.NotchPreviewAutoPlayIntervalMs),
        (nameof(vm.GlobalFontSizePercent), vm.GlobalFontSizePercent),
        (nameof(vm.ImportOnlyClosedPolylines), vm.ImportOnlyClosedPolylines),
        (nameof(vm.SelectedLogLevelOption), vm.SelectedLogLevelOption.Value),
        (nameof(vm.SelectedNotchExportFileTypeOption), vm.SelectedNotchExportFileTypeOption.Value),
        (nameof(vm.SelectedNotchExportProfileOption), vm.SelectedNotchExportProfileOption.Value),
        (nameof(vm.SelectedGridAlignmentOption), vm.SelectedGridAlignmentOption.Value),
    ];

    [Fact]
    public async Task SaveThenLoadProject_PersistsStep5NotchVersionSelection()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();
        settings.EnableV21 = true;
        settings.EnableV22 = false;
        settings.SaveCommand.Execute(null);

        var path = TestFiles.NewTempFilePath("freeform-helper-step5-", ".json");
        try
        {
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            var saveResult = await vm.SaveProjectAsync();
            Assert.True(saveResult);

            using var doc = System.Text.Json.JsonDocument.Parse(await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken));
            var settingsVersions = doc.RootElement
                .GetProperty("settings")
                .GetProperty("notch")
                .GetProperty("enabledVersions")
                .EnumerateArray()
                .Select(static element => element.GetInt32())
                .ToArray();
            Assert.Contains(21, settingsVersions);
            Assert.DoesNotContain(30, settingsVersions);

            var uiNotch = doc.RootElement
                .GetProperty("uiSnapshot")
                .GetProperty("notch");
            Assert.False(uiNotch.TryGetProperty("enabledVersions", out _));

            vm.EnableV21 = false;
            vm.EnableV22 = true;

            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);
            await vm.LoadProjectCommand.ExecuteAsync(null);
            Assert.True(vm.EnableV21);
            Assert.False(vm.EnableV22);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }


    [Fact]
    public void ApplyViewSnapshot_Cv22ExportType_SelectsCv22()
    {
        var vm = new FreeformHelperViewModel();

        FreeformHelperViewModelPrivateAccess.ApplyViewSnapshot(vm, new UiViewSnapshot
        {
            NotchExportFileType = "Cv22"
        });

        Assert.Equal(
            FreeformHelperViewModel.NotchExportFileType.Cv22,
            vm.SelectedNotchExportFileTypeOption.Value);
    }


    [Fact]
    public async Task SaveThenLoadProject_RestoresDxfCombinedMovedAndRotatedEdits()
    {
        var dxfPath = CreateTempDxfFile(
            ("L1", 0, 0, 10, 10),
            ("L1", 8, 0, 18, 10),
            ("L1", 30, 0, 42, 10));
        var projectPath = TestFiles.NewTempFilePath("freeform-helper-dxf-edits-", ".json");
        try
        {
            var importService = new DxfImportService();
            var cad = importService.ImportFromPath(dxfPath, new DxfImportOptions());
            var orderedCadIds = cad.Pads
                .OrderBy(static pad => pad.Bounds.MinX)
                .Select(static pad => pad.Id)
                .ToArray();
            var vm = new FreeformHelperViewModel();

            FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
            vm.CadPads = new System.Collections.ObjectModel.ObservableCollection<CadPad>(cad.Pads);
            FreeformHelperViewModelPrivateAccess.ResetDxfEditBaseline(vm, cad);
            FreeformHelperViewModelPrivateAccess.UpdateLayerToggles(vm, cad);
            var projectFile = Assert.IsType<FreeformHelper.Infrastructure.Project.ProjectFile>(FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
            projectFile.LastDxfPath = dxfPath;

            await SelectCadPadsAsync(vm, [orderedCadIds[0], orderedCadIds[1]]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            Assert.Equal(1, vm.CombinedCadPadCount);
            await WaitForGridRebuildAsync(vm);

            vm.ApplyCanvasSelection([orderedCadIds[2]], Array.Empty<int>());
            await WaitForConditionAsync(() => vm.HasSelectedCadPadsForDxfEdit);
            vm.DxfEditNewLayerName = "MovedLayer";
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            Assert.Equal(1, vm.RelayeredCadPadCount);
            await WaitForGridRebuildAsync(vm);
            vm.DxfEditRotationDegrees = 90m;
            vm.ApplyCanvasSelection([orderedCadIds[2]], Array.Empty<int>());
            await WaitForConditionAsync(() => vm.HasSelectedCadPadsForDxfEdit);
            vm.RotateSelectedCadPadsCommand.Execute(null);
            Assert.Equal(1, vm.RotatedCadPadCount);
            await WaitForGridRebuildAsync(vm);

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            var loadedVm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await loadedVm.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(1, loadedVm.CombinedCadPadCount);
            Assert.Equal(2, loadedVm.DeletedCadPadCount);
            Assert.Equal(1, loadedVm.RelayeredCadPadCount);
            Assert.Equal(1, loadedVm.RotatedCadPadCount);

            var loadedCad = Assert.IsType<FreeformHelper.Domain.Pads.CadPadSet>(FreeformHelperViewModelPrivateAccess.Cad(loadedVm));
            var movedPad = Assert.Single(loadedCad.Pads, pad => pad.Id == orderedCadIds[2]);
            Assert.Equal("MovedLayer", movedPad.Layer);
            var baselinePad = Assert.Single(cad.Pads, pad => pad.Id == orderedCadIds[2]);
            Assert.NotEqual(baselinePad.Bounds.MinX, movedPad.Bounds.MinX, 6);
            Assert.NotEqual(baselinePad.Bounds.MaxY, movedPad.Bounds.MaxY, 6);
            _ = Assert.Single(loadedCad.Pads, static pad => pad.Name.StartsWith("COMB_", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(dxfPath))
            {
                File.Delete(dxfPath);
            }

            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }


    [Fact]
    public async Task SaveThenLoadProject_RestoresVisibleDuplicateCadPads()
    {
        var dxfPath = CreateTempDxfFile(
            ("L1", 0, 0, 10, 10),
            ("L1", 0, 0, 10, 10),
            ("L1", 20, 0, 30, 10));
        var projectPath = TestFiles.NewTempFilePath("freeform-helper-duplicates-", ".json");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.CadPads.Count);

            var duplicateEntry = Assert.Single(
                Assert.IsType<List<DxfEditChangeListEntry>>(FreeformHelperViewModelPrivateAccess.BuildDxfEditChangeEntries(vm)),
                entry => entry.Kind == DxfEditChangeKind.Duplicate);
            FreeformHelperViewModelPrivateAccess.ApplyDxfEditChangeEntry(vm, duplicateEntry);
            Assert.Equal(3, vm.CadPads.Count);

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            using (var doc = System.Text.Json.JsonDocument.Parse(await TestFiles.ReadContentAsync(projectPath, TestContext.Current.CancellationToken)))
            {
                var visibleDuplicates = doc.RootElement
                    .GetProperty("visibleDuplicateCadPadIds")
                    .EnumerateArray()
                    .Select(static element => element.GetInt32())
                    .ToArray();
                Assert.Single(visibleDuplicates);
            }

            var loadedVm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await loadedVm.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(3, loadedVm.CadPads.Count);
            Assert.True(loadedVm.HasDetectedDuplicateCadPads);
        }
        finally
        {
            if (File.Exists(dxfPath))
            {
                File.Delete(dxfPath);
            }

            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }


    [Fact]
    public async Task SaveThenLoadProject_RestoresLayerSelectionsAndDxfDependentOptions_AfterAsyncCadLoadApply()
    {
        var dxfPath = CreateTempDxfFile(
            ("signal", 0, 0, 10, 10),
            ("regular", 12, 0, 22, 10),
            ("aux", 24, 0, 34, 10));
        var projectPath = TestFiles.NewTempFilePath("freeform-helper-layer-ui-", ".json");
        try
        {
            var importService = new DxfImportService();
            var cad = importService.ImportFromPath(dxfPath, new DxfImportOptions());
            var vm = new FreeformHelperViewModel();

            FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
            FreeformHelperViewModelPrivateAccess.ResetDxfEditBaseline(vm, cad);
            FreeformHelperViewModelPrivateAccess.UpdateLayerToggles(vm, cad);

            var auxToggle = Assert.Single(vm.LayerToggles, static toggle => string.Equals(toggle.Name, "aux", StringComparison.OrdinalIgnoreCase));
            auxToggle.IsSelected = false;
            vm.RegularSourceMode = FreeformHelper.Application.Settings.RegularSourceMode.FromDxfLayer;
            vm.SelectedRegularSourceLayerOption = new RegularSourceLayerOption("regular", "regular");
            vm.SelectedBoundLayerOption = new BoundLayerOption("signal", "signal");
            vm.DxfEditTargetLayerName = "signal";

            var projectFile = Assert.IsType<FreeformHelper.Infrastructure.Project.ProjectFile>(FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
            projectFile.LastDxfPath = dxfPath;

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            var loadedVm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await loadedVm.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal("signal", loadedVm.SelectedBoundLayerOption.Name);
            Assert.Equal("regular", loadedVm.SelectedRegularSourceLayerOption.Name);
            Assert.False(Assert.Single(loadedVm.LayerToggles, static toggle => string.Equals(toggle.Name, "aux", StringComparison.OrdinalIgnoreCase)).IsSelected);
        }
        finally
        {
            if (File.Exists(dxfPath))
            {
                File.Delete(dxfPath);
            }

            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }


    [Fact]
    public async Task SaveThenLoadProject_ReplaysStep2FreeformAfterAsyncProjectLoad()
    {
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 30, 10));
        var projectPath = TestFiles.NewTempFilePath("freeform-helper-step2-replay-", ".json");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            };

            vm.XChannels = 3;
            vm.YChannels = 1;
            vm.ActiveAreaWidth = 30;
            vm.ActiveAreaHeight = 10;
            vm.GridPaddingPercent = 0;
            vm.AutoReplayStep2AfterProjectLoad = true;

            await vm.OpenDxfCommand.ExecuteAsync(null);
            await vm.MatchCommand.ExecuteAsync(null);
            await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);

            Assert.All(vm.RegularPads, static pad => Assert.Equal(FreeformType.XWay, pad.Freeform));

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            var loadedVm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };

            await loadedVm.LoadProjectCommand.ExecuteAsync(null);

            Assert.All(loadedVm.RegularPads, static pad => Assert.Equal(FreeformType.XWay, pad.Freeform));
            Assert.Contains("Step 2 freeform restored", loadedVm.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(dxfPath))
            {
                File.Delete(dxfPath);
            }

            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }
}
