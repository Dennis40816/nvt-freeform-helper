// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectSnapshotMappingCharacterizationTests
{
    [Fact]
    public async Task SaveCapturesEverySectionAndPreservesCompatibilityValuesAsync()
    {
        var vm = CreateViewModel();
        ConfigureGrid(vm);
        ConfigureView(vm);
        vm.MatchThreshold = 0.37m;
        vm.FreeformAxisThreshold = 0.19m;
        vm.EnableAutoDetectXy = false;
        vm.EnableFreeformEdgeSpecialization = true;
        vm.LinkNotchThresholds = false;
        vm.LenScale = 2.5m;
        vm.NullValue = 6.5m;
        vm.NotchThresholdQ7 = 12.5m;
        vm.NotchThresholdPercent = 21.25m;
        vm.EnableToFull = true;
        vm.ImportOnlyClosedPolylines = false;
        vm.ImportBlockPolylines = false;
        var matching = FreeformHelperViewModelPrivateAccess.ProjectFile(vm).UiSnapshot.Matching;
        matching.MatchMode = "legacy-mode";
        matching.EnableCentroidFallback = false;
        matching.NearestK = 17;

        var snapshot = await CaptureAsync(vm);

        AssertGrid(snapshot.Grid, vm);
        AssertView(snapshot.View, vm);
        Assert.Equivalent(new UiMatchingSnapshot
        {
            MatchMode = "legacy-mode",
            MatchThreshold = 0.37,
            FreeformAxisThreshold = 0.19,
            EnableAutoDetectXy = false,
            EnableFreeformEdgeSpecialization = true,
            EnableCentroidFallback = false,
            NearestK = 17,
        }, snapshot.Matching, strict: true);
        Assert.Equivalent(new UiNotchSnapshot
        {
            LenScale = 2,
            NullValue = 6,
            ThresholdQ7 = 12,
            ThresholdPercentV22 = 21.25,
            LinkThresholds = false,
            EnableToFull = true,
        }, snapshot.Notch, strict: true);
        Assert.Equivalent(new UiImportSnapshot
        {
            OnlyClosedPolylines = false,
            IncludeBlockPolylines = false,
            LogLevel = vm.SelectedLogLevelOption.Value,
        }, snapshot.Import, strict: true);
    }

    [Fact]
    public async Task CaptureRoundsIcValuesInOrderAndOwnsListsAndLayerRowsAsync()
    {
        var vm = CreateViewModel();
        vm.CascadeIcSettings.Clear();
        vm.CascadeIcSettings.Add(new FreeformHelperViewModel.CascadeIcSetting(0, 1, 1)
        {
            XChannels = 2.5m,
            YChannels = 0.4m,
        });
        vm.CascadeIcSettings.Add(new FreeformHelperViewModel.CascadeIcSetting(1, 1, 1)
        {
            XChannels = 3.5m,
            YChannels = 6.5m,
        });
        vm.LayerToggles.Add(new FreeformHelperViewModel.LayerToggle("z", false));
        vm.LayerToggles.Add(new FreeformHelperViewModel.LayerToggle("A"));

        var snapshot = await CaptureAsync(vm);
        vm.CascadeIcSettings[0].XChannels = 99;
        vm.CascadeIcSettings[1].YChannels = 88;
        vm.LayerToggles[0].Name = "changed";
        vm.LayerToggles[1].IsSelected = false;

        Assert.Equal<int>([2, 4], snapshot.Grid.IcX);
        Assert.Equal<int>([1, 6], snapshot.Grid.IcY);
        Assert.Equal<string>(["z", "A"], snapshot.View.LayerSelections.Select(row => row.Name));
        Assert.Equal<bool>([false, true], snapshot.View.LayerSelections.Select(row => row.IsSelected));
    }

    [Fact]
    public async Task MissingDisplayOptionsKeepEnumStringFallbacksAsync()
    {
        var vm = CreateViewModel();
        vm.SelectedScanOrder = (ScanOrder)12345;
        vm.GridAlignmentMode = (GridAlignmentMode)23456;

        var snapshot = await CaptureAsync(vm);

        Assert.Equal("12345", snapshot.Grid.ScanOrder);
        Assert.Equal("23456", snapshot.Grid.AlignmentMode);
    }

    [Fact]
    public async Task SaveCapturesImportedMaskSourceAndEnablementAsync()
    {
        using var files = new P1SnapshotProject();
        TestFiles.WriteText(files.MaskPath, "Xch:\"4\",Ych:\"2\"\ntimestamp(0:0:0),DiffData,1,0,1,0\n1,0,1,0\n");
        var vm = CreateViewModel();
        ConfigureGrid(vm);
        await vm.WaitForGridRebuildIdleAsync();
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(files.MaskPath);
        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        var snapshot = await CaptureAsync(vm);

        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.Equal(files.MaskPath, snapshot.Import.RegularVisibilityMaskSourcePath);
        Assert.True(snapshot.Import.UseRegularVisibilityMask);
    }

    [Fact]
    public async Task SavedPreferencesRoundTripThroughTheExistingProjectCommandsAsync()
    {
        using var files = new P1SnapshotProject();
        var saver = CreateViewModel();
        saver.RememberDxfLayerImageExportRequest(PreferenceRequest());
        saver.PickSaveProjectPathAsync = () => Task.FromResult<string?>(files.Path);
        await saver.SaveProjectCommand.ExecuteAsync(null);
        var loader = CreateViewModel();
        loader.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.Path);

        await loader.LoadProjectCommand.ExecuteAsync(null);
        var context = CaptureExportContext(loader);
        var view = (await CaptureAsync(loader)).View;

        Assert.Equal("L2", Assert.Single(context.InitialCheckedLayers));
        Assert.Equal(2.5m, context.LineWidthPixels);
        Assert.Equal(DxfLayerImageFormat.Jpg, context.Format);
        Assert.True(context.UseDarkTheme);
        Assert.Equal(640d, view.DxfLayerImageWidthPixels);
        Assert.Equal(480d, view.DxfLayerImageHeightPixels);
        Assert.Equal(30d, view.DxfLayerImagePaddingXPixels);
        Assert.Equal(40d, view.DxfLayerImagePaddingYPixels);
    }

    [Fact]
    public async Task EquivalentAndEquivalentlyClampedRequestsStayCleanAsync()
    {
        var vm = CreateViewModel();
        vm.RememberDxfLayerImageExportRequest(new DxfLayerImageExportRequest(
            null!, 150_000, 0, 500m, -1, 900_000, DxfLayerImageFormat.Bmp, true));
        vm.HasUnsavedChanges = false;
        vm.AppGeneralPersistenceForTests.DeferAfterProjectLoad = true;
        vm.AppGeneralPersistenceForTests.HasDeferredChanges = false;
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        vm.RememberDxfLayerImageExportRequest(new DxfLayerImageExportRequest(
            string.Empty, 100_000, 2, 100m, 0, 100_000, DxfLayerImageFormat.Bmp, true));
        vm.RememberDxfLayerImageExportRequest(new DxfLayerImageExportRequest(
            null!, 200_000, -1, 200m, -2, 200_000, DxfLayerImageFormat.Bmp, true));

        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.AppGeneralPersistenceForTests.HasDeferredChanges);
        Assert.Empty(notifications);
        var view = (await CaptureAsync(vm)).View;
        Assert.Equal(string.Empty, view.DxfLayerImagePreferredLayer);
        Assert.Equal(100_000d, view.DxfLayerImageWidthPixels);
        Assert.Equal(2d, view.DxfLayerImageHeightPixels);
        Assert.Equal(100d, view.DxfLayerImageLineWidthPixels);
        Assert.Equal(0d, view.DxfLayerImagePaddingXPixels);
        Assert.Equal(100_000d, view.DxfLayerImagePaddingYPixels);
    }

    [Fact]
    public void RememberPublishesNewPreferencesBeforeDirtyThenSchedulesPersistence()
    {
        var vm = CreateViewModel();
        vm.HasUnsavedChanges = false;
        vm.AppGeneralPersistenceForTests.DeferAfterProjectLoad = true;
        vm.AppGeneralPersistenceForTests.HasDeferredChanges = false;
        var observations = new List<(string Phase, object? Sender, bool Dirty, decimal Width, bool Deferred)>();
        vm.PropertyChanging += (sender, args) =>
        {
            if (args.PropertyName == nameof(vm.HasUnsavedChanges))
            {
                observations.Add(("changing", sender, vm.HasUnsavedChanges,
                    CaptureExportContext(vm).LineWidthPixels, vm.AppGeneralPersistenceForTests.HasDeferredChanges));
            }
        };
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(vm.HasUnsavedChanges))
            {
                observations.Add(("changed", sender, vm.HasUnsavedChanges,
                    CaptureExportContext(vm).LineWidthPixels, vm.AppGeneralPersistenceForTests.HasDeferredChanges));
            }
        };

        vm.RememberDxfLayerImageExportRequest(PreferenceRequest());

        Assert.Equal(new[]
        {
            ("changing", (object?)vm, false, 2.5m, false),
            ("changed", (object?)vm, true, 2.5m, false),
        }, observations);
        Assert.True(vm.AppGeneralPersistenceForTests.HasDeferredChanges);
    }

    [Fact]
    public async Task ApplyingOnlyImagePreferencesEmitsNoNotificationsOrPersistenceAsync()
    {
        var vm = CreateViewModel();
        var view = (await CaptureAsync(vm)).View;
        view.DxfLayerImageLineWidthPixels = 3.5;
        vm.HasUnsavedChanges = false;
        vm.AppGeneralPersistenceForTests.DeferAfterProjectLoad = true;
        vm.AppGeneralPersistenceForTests.HasDeferredChanges = false;
        var notifications = new List<string?>();
        vm.PropertyChanging += (_, args) => notifications.Add(args.PropertyName);
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        FreeformHelperViewModelPrivateAccess.ApplyViewSnapshot(vm, view);

        Assert.Empty(notifications);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.AppGeneralPersistenceForTests.HasDeferredChanges);
        Assert.Equal(3.5m, CaptureExportContext(vm).LineWidthPixels);
    }

    [Theory]
    [InlineData("tiff", DxfLayerImageFormat.Png)]
    [InlineData("bMp", DxfLayerImageFormat.Bmp)]
    [InlineData("12345", (DxfLayerImageFormat)12345)]
    public async Task LoadKeepsFormatParsingAndRememberedDimensionsAsync(string format, DxfLayerImageFormat expected)
    {
        using var files = new P1SnapshotProject();
        var path = files.Path;
        var file = CreateProject();
        file.UiSnapshot.View.DxfLayerImageFormat = format;
        await new JsonProjectStore().SaveAsync(path, file, CancellationToken.None);
        var vm = CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(path);
        vm.ActiveAreaPixelsX = 6480;
        vm.ActiveAreaPixelsY = 848;
        var context = CaptureExportContext(vm);
        var view = (await CaptureAsync(vm)).View;

        Assert.True(result.IsLoaded, result.Message);
        Assert.Equal(expected, context.Format);
        Assert.Equal(6480, context.InitialSize.PixelsX);
        Assert.Equal(848, context.InitialSize.PixelsY);
        Assert.Equal(0, context.InitialSize.PaddingX);
        Assert.Equal(0, context.InitialSize.PaddingY);
        Assert.Equal(640d, view.DxfLayerImageWidthPixels);
        Assert.Equal(482d, view.DxfLayerImageHeightPixels);
        Assert.Equal(30d, view.DxfLayerImagePaddingXPixels);
        Assert.Equal(42d, view.DxfLayerImagePaddingYPixels);
        Assert.Equal(expected.ToString(), view.DxfLayerImageFormat);
    }

    [Theory]
    [InlineData(false, 2.5)]
    [InlineData(true, 4.5)]
    public async Task LoadAppliesAppImagePreferencesOnlyWhenEnabledAsync(bool enabled, double expectedWidth)
    {
        using var files = new P1SnapshotProject();
        var path = files.Path;
        await new JsonProjectStore().SaveAsync(path, CreateProject(), CancellationToken.None);
        var vm = CreateViewModel();
        vm.ApplyAppVisualPreferencesOnProjectLoad = enabled;
        vm.AppGeneralPersistenceForTests.ViewSnapshot.DxfLayerImageLineWidthPixels = 4.5;

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded, result.Message);
        Assert.Equal((decimal)expectedWidth, CaptureExportContext(vm).LineWidthPixels);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task LoadConversionFailureKeepsEarlierSettingsAndPreviousPreferencesAsync()
    {
        using var files = new P1SnapshotProject();
        var path = files.Path;
        var file = CreateProject();
        file.Settings.Matching.MatchThreshold = 0.27;
        file.UiSnapshot.View.DxfLayerImageLineWidthPixels = 1e100;
        await new JsonProjectStore().SaveAsync(path, file, CancellationToken.None);
        var vm = CreateViewModel();
        vm.RememberDxfLayerImageExportRequest(PreferenceRequest() with { LineWidthPixels = 7m });

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.False(result.IsLoaded);
        Assert.Equal("LOAD_FAILED", result.Code);
        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.Equal(7m, CaptureExportContext(vm).LineWidthPixels);
    }

    [Fact]
    public async Task SnapshotIntegerConversionOverflowStillCancelsSaveBeforePickerAsync()
    {
        var vm = CreateViewModel();
        vm.MaxAreaBuckets = decimal.MaxValue;
        var pickerCalled = false;
        vm.PickSaveProjectPathAsync = () =>
        {
            pickerCalled = true;
            return Task.FromResult<string?>(null);
        };

        var saved = await vm.SaveProjectAsync();

        Assert.False(saved);
        Assert.False(pickerCalled);
        Assert.Contains("Save project failed", vm.StatusText, StringComparison.Ordinal);
    }

    private static FreeformHelperViewModel CreateViewModel()
    {
        // TestAppSettingsIsolation redirects the default store before any test runs.
        return new FreeformHelperViewModel { ApplyAppVisualPreferencesOnProjectLoad = false };
    }

    private sealed class P1SnapshotProject : IDisposable
    {
        private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();

        public string Path => System.IO.Path.Combine(_directory.FullName, "project.json");

        public string MaskPath => System.IO.Path.Combine(_directory.FullName, "mask.csv");

        public void Dispose() => _directory.Delete(recursive: true);
    }

    private static async Task<ProjectUiSnapshot> CaptureAsync(FreeformHelperViewModel vm)
    {
        ProjectUiSnapshot? captured = null;
        vm.PickSaveProjectPathAsync = () =>
        {
            captured = FreeformHelperViewModelPrivateAccess.ProjectFile(vm).UiSnapshot;
            return Task.FromResult<string?>(null);
        };
        Assert.False(await vm.SaveProjectAsync());
        return Assert.IsType<ProjectUiSnapshot>(captured);
    }

    private static DxfLayerImageExportRequest PreferenceRequest() =>
        new("L2", 640, 480, 2.5m, 30, 40, DxfLayerImageFormat.Jpg, true);

    private static LayerImageExportContext CaptureExportContext(FreeformHelperViewModel vm) =>
        vm.BuildLayerImageExportContext(new List<CadPad>
        {
            LayerImageExportTestData.CreateSquareCad(1, "L2", 0, 0, 10),
        }, static (_, _) => Task.FromResult<string?>(null));

    private static ProjectFile CreateProject() => new()
    {
        Settings = new ProjectSettings { Grid = new GridSettings { XChannels = 2, YChannels = 1 } },
        UiSnapshot = new ProjectUiSnapshot
        {
            View = new UiViewSnapshot
            {
                DxfLayerImagePreferredLayer = "L2",
                DxfLayerImageWidthPixels = 640.5,
                DxfLayerImageHeightPixels = 481.5,
                DxfLayerImageLineWidthPixels = 2.5,
                DxfLayerImagePaddingXPixels = 30.5,
                DxfLayerImagePaddingYPixels = 41.5,
            },
        },
    };

    private static void ConfigureGrid(FreeformHelperViewModel vm)
    {
        vm.CascadeNum = 1;
        vm.CascadeIcSettings[0].XChannels = 4;
        vm.CascadeIcSettings[0].YChannels = 2;
        vm.GridPaddingPercent = 3.25m;
        vm.ActiveAreaWidth = 40.5m;
        vm.ActiveAreaHeight = 20.25m;
    }

    private static void AssertGrid(UiGridSnapshot grid, FreeformHelperViewModel vm)
    {
        Assert.Equivalent(new UiGridSnapshot
        {
            CascadeCount = 1,
            IcX = [4],
            IcY = [2],
            TotalX = 4,
            TotalY = 2,
            ScanOrder = vm.SelectedScanOrderOption.Display,
            AlignmentMode = vm.SelectedGridAlignmentOption.Display,
            GridPaddingPercent = 3.25,
            ActiveAreaWidthMm = 40.5,
            ActiveAreaHeightMm = 20.25,
            PitchXmm = 10.125,
            PitchYmm = 10.125,
        }, grid, strict: true);
    }

    private static void ConfigureView(FreeformHelperViewModel vm)
    {
        vm.ShowCad = false;
        vm.ShowRegular = false;
        vm.HighlightUnmatched = false;
        vm.HighlightFreeform = false;
        vm.ColorCadByArea = true;
        vm.CadLineWidth = 1.25m;
        vm.CadLineColorHex = "#123456";
        vm.CadFillOpacity = 0.21m;
        vm.CadLineOpacity = 0.32m;
        vm.RegularLineWidth = 2.25m;
        vm.HighlightStrokeWidthAdjust = 0.75m;
        vm.RegularLineColorHex = "#654321";
        vm.RegularFillOpacity = 0.43m;
        vm.RegularLineOpacity = 0.54m;
        vm.RegularSelectedColorHex = "#ABCDEF";
        vm.RegularSelectedFillOpacity = 0.65m;
        vm.AreaBucketTolerance = 0.0125m;
        vm.MaxAreaBuckets = 20.5m;
        vm.ShowDiffIndexOverlay = false;
        vm.ShowNotchCanvasPreview = false;
        vm.ShowNotchToRegularLabels = false;
        vm.GlobalFontSizePercent = 110;
        vm.NotchPreviewVisualizationStep = 2;
        vm.NotchPreviewAutoPlayEnabled = false;
        vm.NotchPreviewAutoPlayIntervalMs = 900;
        vm.SelectedNotchExportFileTypeOption = vm.NotchExportFileTypeOptions[1];
        vm.CoordinatePixelWidth = 800;
        vm.CoordinatePixelHeight = 600;
        vm.CoordinatePreferredAaOutlineLayerName = "AA";
        vm.RememberDxfLayerImageExportRequest(PreferenceRequest());
    }

    private static void AssertView(UiViewSnapshot view, FreeformHelperViewModel vm)
    {
        Assert.Equivalent(new UiViewSnapshot
        {
            ShowCad = false,
            ShowRegular = false,
            HighlightUnmatched = false,
            HighlightFreeform = false,
            ColorCadByArea = true,
            CadLineWidth = 1.25,
            CadLineColor = "#123456",
            CadFillOpacity = 0.21,
            CadLineOpacity = 0.32,
            RegularLineWidth = 2.25,
            HighlightStrokeWidthAdjust = 0.75,
            RegularLineColor = "#654321",
            RegularFillOpacity = 0.43,
            RegularLineOpacity = 0.54,
            RegularSelectedColor = "#ABCDEF",
            RegularSelectedFillOpacity = 0.65,
            AreaBucketTolerance = 0.0125,
            MaxAreaBuckets = 20,
            ShowDiffIndexOverlay = false,
            ShowNotchCanvasPreview = false,
            ShowNotchToRegularLabels = false,
            GlobalFontSizePercent = 110,
            NotchPreviewVisualizationStep = 2,
            NotchPreviewAutoPlayEnabled = false,
            NotchPreviewAutoPlayIntervalMs = 900,
            NotchExportFileType = vm.SelectedNotchExportFileTypeOption.Value.ToString(),
            DxfLayerImagePreferredLayer = "L2",
            DxfLayerImageWidthPixels = 640,
            DxfLayerImageHeightPixels = 480,
            DxfLayerImageLineWidthPixels = 2.5,
            DxfLayerImagePaddingXPixels = 30,
            DxfLayerImagePaddingYPixels = 40,
            DxfLayerImageFormat = "Jpg",
            DxfLayerImageUseDarkTheme = true,
            CoordinatePixelWidth = 800,
            CoordinatePixelHeight = 600,
            CoordinatePreferredAaOutlineLayerName = "AA",
        }, view, strict: true);
    }
}
