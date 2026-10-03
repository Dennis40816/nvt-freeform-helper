using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ProjectStoreTests
{
    private static readonly string[] ExpectedEnabledVersions = ["V21", "V22"];

    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonProjectStore();
            var file = new ProjectFile
            {
                Settings = new ProjectSettings
                {
                    Grid = new GridSettings
                    {
                        XChannels = 40,
                        YChannels = 20,
                        CascadeNum = 2,
                        BoundsPaddingRatio = 0.05,
                        ActiveAreaWidth = 300,
                        ActiveAreaHeight = 180
                    }
                }
            };

            JsonProjectStore.Save(path, file);
            var loaded = JsonProjectStore.Load(path);

            Assert.Equal(40, loaded.Settings.Grid.XChannels);
            Assert.Equal(20, loaded.Settings.Grid.YChannels);
            Assert.Equal(2, loaded.Settings.Grid.CascadeNum);
            Assert.Equal(0.05, loaded.Settings.Grid.BoundsPaddingRatio, 6);
            Assert.Equal(300, loaded.Settings.Grid.ActiveAreaWidth, 6);
            Assert.Equal(180, loaded.Settings.Grid.ActiveAreaHeight, 6);
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
    public void SaveAndLoad_RoundTripsMatchingAndIndexMappingSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonProjectStore();
            var file = new ProjectFile
            {
                EmbeddedRegularVisibilityMask = new byte[] { 5, 4, 3, 2, 1 },
                EmbeddedRegularVisibilityMaskName = "SeeRegular.csv",
                Settings = new ProjectSettings
                {
                    Matching = new MatchingSettings
                    {
                        Mode = MatchMode.LegacyOverlap,
                        MatchThreshold = 0.41,
                        FreeformAxisThreshold = 0.62,
                        EnableAutoDetectXy = true,
                        EnableFreeformEdgeSpecialization = true,
                        EnableCentroidFallback = false,
                        NearestK = 9,
                    },
                    IndexMapping = new IndexMappingSettings
                    {
                        WeightIou = 1.7,
                        WeightCentroidDistance = 0.4,
                        WeightAreaRatio = 0.2,
                        LowConfidenceThreshold = 0.61,
                        AmbiguousMargin = 0.07,
                        CandidateNumber = 25,
                        CandidatePaddingCells = 2,
                        DiagnosticTopK = 4,
                        MaxSampleIssues = 120,
                        MaxReportIssues = 4096,
                    },
                },
                UiSnapshot = new ProjectUiSnapshot
                {
                    Matching = new UiMatchingSnapshot
                    {
                        MatchMode = "CAD -> Regular",
                        MatchThreshold = 0.41,
                        FreeformAxisThreshold = 0.62,
                        EnableAutoDetectXy = true,
                        EnableFreeformEdgeSpecialization = true,
                        EnableCentroidFallback = false,
                        NearestK = 9,
                    },
                    View = new UiViewSnapshot
                    {
                        GlobalFontSizePercent = 112,
                        ShowNotchCanvasPreview = false,
                        NotchPreviewVisualizationStep = 2,
                        NotchPreviewAutoPlayEnabled = false,
                        NotchPreviewAutoPlayIntervalMs = 900,
                        NotchExportFileType = "Cv22",
                        DxfLayerImagePreferredLayer = "AA_LAYER",
                        DxfLayerImageWidthPixels = 2048,
                        DxfLayerImageHeightPixels = 1024,
                        DxfLayerImageLineWidthPixels = 1.6,
                        DxfLayerImagePaddingXPixels = 80,
                        DxfLayerImagePaddingYPixels = 120,
                        DxfLayerImageFormat = "Jpg",
                        DxfLayerImageUseDarkTheme = true,
                    },
                    Notch = new UiNotchSnapshot
                    {
                        EnableToFull = true,
                        EnabledVersions = new List<string> { "V21", "V22" },
                        LenScale = 16,
                        NullValue = 65535,
                        ThresholdQ7 = 32,
                        ThresholdPercentV22 = 25.0,
                        LinkThresholds = false,
                    },
                    Import = new UiImportSnapshot
                    {
                        OnlyClosedPolylines = true,
                        IncludeBlockPolylines = true,
                        LogLevel = "Debug",
                        RegularVisibilityMaskSourcePath = @"C:\mask\SeeRegular.csv",
                        UseRegularVisibilityMask = true,
                    },
                },
            };

            JsonProjectStore.Save(path, file);
            var loaded = JsonProjectStore.Load(path);

            Assert.Equal(new byte[] { 5, 4, 3, 2, 1 }, loaded.EmbeddedRegularVisibilityMask);
            Assert.Equal("SeeRegular.csv", loaded.EmbeddedRegularVisibilityMaskName);
            Assert.Equal(0.41, loaded.Settings.Matching.MatchThreshold, 6);
            Assert.Equal(0.62, loaded.Settings.Matching.FreeformAxisThreshold, 6);
            Assert.True(loaded.Settings.Matching.EnableAutoDetectXy);
            Assert.True(loaded.Settings.Matching.EnableFreeformEdgeSpecialization);
            Assert.False(loaded.Settings.Matching.EnableCentroidFallback);
            Assert.Equal(9, loaded.Settings.Matching.NearestK);

            Assert.Equal(1.7, loaded.Settings.IndexMapping.WeightIou, 6);
            Assert.Equal(0.4, loaded.Settings.IndexMapping.WeightCentroidDistance, 6);
            Assert.Equal(0.2, loaded.Settings.IndexMapping.WeightAreaRatio, 6);
            Assert.Equal(0.61, loaded.Settings.IndexMapping.LowConfidenceThreshold, 6);
            Assert.Equal(0.07, loaded.Settings.IndexMapping.AmbiguousMargin, 6);
            Assert.Equal(25, loaded.Settings.IndexMapping.CandidateNumber);
            Assert.Equal(2, loaded.Settings.IndexMapping.CandidatePaddingCells);
            Assert.Equal(4, loaded.Settings.IndexMapping.DiagnosticTopK);
            Assert.Equal(120, loaded.Settings.IndexMapping.MaxSampleIssues);
            Assert.Equal(4096, loaded.Settings.IndexMapping.MaxReportIssues);

            Assert.Equal("CAD -> Regular", loaded.UiSnapshot.Matching.MatchMode);
            Assert.Equal(0.41, loaded.UiSnapshot.Matching.MatchThreshold, 6);
            Assert.True(loaded.UiSnapshot.Matching.EnableAutoDetectXy);
            Assert.True(loaded.UiSnapshot.Matching.EnableFreeformEdgeSpecialization);
            Assert.Equal(112, loaded.UiSnapshot.View.GlobalFontSizePercent);
            Assert.False(loaded.UiSnapshot.View.ShowNotchCanvasPreview);
            Assert.Equal(2, loaded.UiSnapshot.View.NotchPreviewVisualizationStep, 6);
            Assert.False(loaded.UiSnapshot.View.NotchPreviewAutoPlayEnabled);
            Assert.Equal(900, loaded.UiSnapshot.View.NotchPreviewAutoPlayIntervalMs, 6);
            Assert.Equal("Cv22", loaded.UiSnapshot.View.NotchExportFileType);
            Assert.Equal("AA_LAYER", loaded.UiSnapshot.View.DxfLayerImagePreferredLayer);
            Assert.Equal(2048, loaded.UiSnapshot.View.DxfLayerImageWidthPixels, 6);
            Assert.Equal(1024, loaded.UiSnapshot.View.DxfLayerImageHeightPixels, 6);
            Assert.Equal(1.6, loaded.UiSnapshot.View.DxfLayerImageLineWidthPixels, 6);
            Assert.Equal(80, loaded.UiSnapshot.View.DxfLayerImagePaddingXPixels, 6);
            Assert.Equal(120, loaded.UiSnapshot.View.DxfLayerImagePaddingYPixels, 6);
            Assert.Equal("Jpg", loaded.UiSnapshot.View.DxfLayerImageFormat);
            Assert.True(loaded.UiSnapshot.View.DxfLayerImageUseDarkTheme);
            Assert.True(loaded.UiSnapshot.Notch.EnableToFull);
            Assert.Equal(ExpectedEnabledVersions, loaded.UiSnapshot.Notch.EnabledVersions);
            Assert.Equal(16, loaded.UiSnapshot.Notch.LenScale);
            Assert.Equal(65535, loaded.UiSnapshot.Notch.NullValue);
            Assert.Equal(32, loaded.UiSnapshot.Notch.ThresholdQ7);
            Assert.Equal(25.0, loaded.UiSnapshot.Notch.ThresholdPercentV22, 6);
            Assert.False(loaded.UiSnapshot.Notch.LinkThresholds);
            Assert.True(loaded.UiSnapshot.Import.OnlyClosedPolylines);
            Assert.True(loaded.UiSnapshot.Import.IncludeBlockPolylines);
            Assert.Equal("Debug", loaded.UiSnapshot.Import.LogLevel);
            Assert.Equal(@"C:\mask\SeeRegular.csv", loaded.UiSnapshot.Import.RegularVisibilityMaskSourcePath);
            Assert.True(loaded.UiSnapshot.Import.UseRegularVisibilityMask);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
