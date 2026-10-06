using System.Reflection;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
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
        var sourcePath = Path.Combine(Path.GetTempPath(), $"freeform-helper-notch-source-{Guid.NewGuid():N}.json");
        var savedPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-notch-saved-{Guid.NewGuid():N}.json");
        try
        {
            JsonProjectStore.Save(sourcePath, new ProjectFile
            {
                Settings = new ProjectSettings
                {
                    Notch = new NotchSettings
                    {
                        ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                        EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                    },
                },
            });

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
        var sourcePath = Path.Combine(Path.GetTempPath(), $"freeform-helper-matching-source-{Guid.NewGuid():N}.json");
        var savedPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-matching-saved-{Guid.NewGuid():N}.json");
        try
        {
            JsonProjectStore.Save(sourcePath, new ProjectFile
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
            });

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
        var vm = new FreeformHelperViewModel();
        Assert.True(vm.LogLevelOptions.Count >= 2);

        var original = vm.SelectedLogLevelOption;
        var persisted = vm.LogLevelOptions.First(option => !option.Equals(original));
        var other = vm.LogLevelOptions.First(option => !option.Equals(persisted));

        vm.SelectedLogLevelOption = persisted;

        var path = Path.Combine(Path.GetTempPath(), $"freeform-helper-{Guid.NewGuid():N}.json");
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
        var vm = new FreeformHelperViewModel
        {
            ApplyAppVisualPreferencesOnProjectLoad = false
        };

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

        var persistedEnableToFullRuleEngine = vm.EnableToFullRuleEngine;
        var persistedEnableToFullRuleTrace = vm.EnableToFullRuleTrace;
        var persistedStrictOverlap = vm.ToFullStrictOverlapPercent;
        var persistedAutoReplayStep2 = vm.AutoReplayStep2AfterProjectLoad;
        var persistedMappingWeightIou = vm.MappingWeightIou;
        var persistedShowNotchPreview = vm.ShowNotchCanvasPreview;
        var persistedShowToRegularLabels = vm.ShowNotchToRegularLabels;
        var persistedPreviewStage = vm.NotchPreviewVisualizationStep;
        var persistedAutoPlay = vm.NotchPreviewAutoPlayEnabled;
        var persistedAutoPlayInterval = vm.NotchPreviewAutoPlayIntervalMs;
        var persistedGlobalFont = vm.GlobalFontSizePercent;
        var persistedImportOnlyClosed = vm.ImportOnlyClosedPolylines;
        var persistedLogLevel = vm.SelectedLogLevelOption;
        var persistedFileType = vm.SelectedNotchExportFileTypeOption;
        var persistedProfile = vm.SelectedNotchExportProfileOption;
        var persistedAlignment = vm.SelectedGridAlignmentOption;

        var path = Path.Combine(Path.GetTempPath(), $"freeform-helper-{Guid.NewGuid():N}.json");
        try
        {
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            var saveResult = await vm.SaveProjectAsync();
            Assert.True(saveResult);

            vm.EnableToFullRuleEngine = !persistedEnableToFullRuleEngine;
            vm.EnableToFullRuleTrace = !persistedEnableToFullRuleTrace;
            vm.ToFullStrictOverlapPercent = persistedStrictOverlap <= 1m ? 2m : persistedStrictOverlap - 1m;
            vm.AutoReplayStep2AfterProjectLoad = !persistedAutoReplayStep2;
            vm.MappingWeightIou = persistedMappingWeightIou + 0.5m;
            vm.ShowNotchCanvasPreview = !persistedShowNotchPreview;
            vm.ShowNotchToRegularLabels = !persistedShowToRegularLabels;
            vm.NotchPreviewVisualizationStep = persistedPreviewStage >= 3m ? 1m : 3m;
            vm.NotchPreviewAutoPlayEnabled = !persistedAutoPlay;
            vm.NotchPreviewAutoPlayIntervalMs = persistedAutoPlayInterval >= 900m ? 700m : 900m;
            vm.GlobalFontSizePercent = persistedGlobalFont >= 130m ? 120m : 130m;
            vm.ImportOnlyClosedPolylines = !persistedImportOnlyClosed;
            vm.SelectedLogLevelOption = vm.LogLevelOptions.FirstOrDefault(option =>
                !option.Equals(persistedLogLevel));
            LoggingBootstrapper.ApplyMinimumLevel(vm.SelectedLogLevelOption.Value);
            vm.SelectedNotchExportFileTypeOption = vm.NotchExportFileTypeOptions.FirstOrDefault(option =>
                option.Value != persistedFileType.Value);
            vm.SelectedNotchExportProfileOption = vm.NotchExportProfileOptions.FirstOrDefault(option =>
                option.Value != persistedProfile.Value);
            vm.SelectedGridAlignmentOption = vm.GridAlignmentOptions.FirstOrDefault(option =>
                option.Value != persistedAlignment.Value);

            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);
            await vm.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(persistedEnableToFullRuleEngine, vm.EnableToFullRuleEngine);
            Assert.Equal(persistedEnableToFullRuleTrace, vm.EnableToFullRuleTrace);
            Assert.Equal(persistedStrictOverlap, vm.ToFullStrictOverlapPercent);
            Assert.Equal(persistedAutoReplayStep2, vm.AutoReplayStep2AfterProjectLoad);
            Assert.Equal(persistedMappingWeightIou, vm.MappingWeightIou);
            Assert.Equal(persistedShowNotchPreview, vm.ShowNotchCanvasPreview);
            Assert.Equal(persistedShowToRegularLabels, vm.ShowNotchToRegularLabels);
            Assert.Equal(persistedPreviewStage, vm.NotchPreviewVisualizationStep);
            Assert.Equal(persistedAutoPlay, vm.NotchPreviewAutoPlayEnabled);
            Assert.Equal(persistedAutoPlayInterval, vm.NotchPreviewAutoPlayIntervalMs);
            Assert.Equal(persistedGlobalFont, vm.GlobalFontSizePercent);
            Assert.Equal(persistedImportOnlyClosed, vm.ImportOnlyClosedPolylines);
            Assert.Equal(persistedLogLevel.Value, vm.SelectedLogLevelOption.Value);
            Assert.Equal(persistedFileType.Value, vm.SelectedNotchExportFileTypeOption.Value);
            Assert.Equal(persistedProfile.Value, vm.SelectedNotchExportProfileOption.Value);
            Assert.Equal(persistedAlignment.Value, vm.SelectedGridAlignmentOption.Value);
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
    public async Task SaveThenLoadProject_PersistsStep5NotchVersionSelection()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();
        settings.EnableV21 = true;
        settings.EnableV22 = false;
        settings.SaveCommand.Execute(null);

        var path = Path.Combine(Path.GetTempPath(), $"freeform-helper-step5-{Guid.NewGuid():N}.json");
        try
        {
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            var saveResult = await vm.SaveProjectAsync();
            Assert.True(saveResult);

            using var doc = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
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
        var method = typeof(FreeformHelperViewModel).GetMethod(
            "ApplyViewSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(vm, new object?[]
        {
            new UiViewSnapshot
            {
                NotchExportFileType = "Cv22"
            }
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
        var projectPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-dxf-edits-{Guid.NewGuid():N}.json");
        try
        {
            var importService = new DxfImportService();
            var cad = importService.ImportFromPath(dxfPath, new DxfImportOptions());
            var orderedCadIds = cad.Pads
                .OrderBy(static pad => pad.Bounds.MinX)
                .Select(static pad => pad.Id)
                .ToArray();
            var vm = new FreeformHelperViewModel();
            var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
            var projectField = typeof(FreeformHelperViewModel).GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
            var resetBaselineMethod = typeof(FreeformHelperViewModel).GetMethod("ResetDxfEditBaseline", BindingFlags.Instance | BindingFlags.NonPublic);
            var updateLayerTogglesMethod = typeof(FreeformHelperViewModel).GetMethod("UpdateLayerToggles", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(cadField);
            Assert.NotNull(projectField);
            Assert.NotNull(resetBaselineMethod);
            Assert.NotNull(updateLayerTogglesMethod);

            cadField!.SetValue(vm, cad);
            vm.CadPads = new System.Collections.ObjectModel.ObservableCollection<CadPad>(cad.Pads);
            resetBaselineMethod!.Invoke(vm, new object?[] { cad });
            updateLayerTogglesMethod!.Invoke(vm, new object?[] { cad });
            var projectFile = Assert.IsType<FreeformHelper.Infrastructure.Project.ProjectFile>(projectField!.GetValue(vm));
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

            var loadedCad = Assert.IsType<FreeformHelper.Domain.Pads.CadPadSet>(cadField.GetValue(loadedVm));
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
        var projectPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-duplicates-{Guid.NewGuid():N}.json");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.CadPads.Count);

            var buildEntriesMethod = typeof(FreeformHelperViewModel).GetMethod("BuildDxfEditChangeEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            var applyEntryMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyDxfEditChangeEntry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(buildEntriesMethod);
            Assert.NotNull(applyEntryMethod);

            var duplicateEntry = Assert.Single(
                Assert.IsType<List<DxfEditChangeListEntry>>(buildEntriesMethod!.Invoke(vm, null)),
                entry => entry.Kind == DxfEditChangeKind.Duplicate);
            applyEntryMethod!.Invoke(vm, [duplicateEntry]);
            Assert.Equal(3, vm.CadPads.Count);

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vm.SaveProjectAsync());

            using (var doc = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(projectPath, TestContext.Current.CancellationToken)))
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
        var projectPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-layer-ui-{Guid.NewGuid():N}.json");
        try
        {
            var importService = new DxfImportService();
            var cad = importService.ImportFromPath(dxfPath, new DxfImportOptions());
            var vm = new FreeformHelperViewModel();
            var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
            var projectField = typeof(FreeformHelperViewModel).GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
            var resetBaselineMethod = typeof(FreeformHelperViewModel).GetMethod("ResetDxfEditBaseline", BindingFlags.Instance | BindingFlags.NonPublic);
            var updateLayerTogglesMethod = typeof(FreeformHelperViewModel).GetMethod("UpdateLayerToggles", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(cadField);
            Assert.NotNull(projectField);
            Assert.NotNull(resetBaselineMethod);
            Assert.NotNull(updateLayerTogglesMethod);

            cadField!.SetValue(vm, cad);
            resetBaselineMethod!.Invoke(vm, new object?[] { cad });
            updateLayerTogglesMethod!.Invoke(vm, new object?[] { cad });

            var auxToggle = Assert.Single(vm.LayerToggles, static toggle => string.Equals(toggle.Name, "aux", StringComparison.OrdinalIgnoreCase));
            auxToggle.IsSelected = false;
            vm.RegularSourceMode = FreeformHelper.Application.Settings.RegularSourceMode.FromDxfLayer;
            vm.SelectedRegularSourceLayerOption = new FreeformHelperViewModel.RegularSourceLayerOption("regular", "regular");
            vm.SelectedBoundLayerOption = new FreeformHelperViewModel.BoundLayerOption("signal", "signal");
            vm.DxfEditTargetLayerName = "signal";

            var projectFile = Assert.IsType<FreeformHelper.Infrastructure.Project.ProjectFile>(projectField!.GetValue(vm));
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
        var projectPath = Path.Combine(Path.GetTempPath(), $"freeform-helper-step2-replay-{Guid.NewGuid():N}.json");
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
