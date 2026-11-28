// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Platform.Storage;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerFilePickerOptionsTests
{
    [Fact]
    public void SaveTextExport_UsesTheCallerNamesAndAppendsAllFiles()
    {
        var options = CoordinatePlannerFilePickerOptions.SaveTextExport(
            "Export Coordinates CSV",
            "csv",
            "coordinates.csv",
            "CSV",
            ["*.csv"]);

        Assert.Equal("Export Coordinates CSV", options.Title);
        Assert.Equal("csv", options.DefaultExtension);
        Assert.Equal("coordinates.csv", options.SuggestedFileName);
        AssertChoices(options.FileTypeChoices, ("CSV", "*.csv"), ("All files", "*"));
    }

    [Fact]
    public void SavePreviewImage_OffersPngThenBmpThenAllFiles()
    {
        var options = CoordinatePlannerFilePickerOptions.SavePreviewImage();

        Assert.Equal("Export Coordinate Preview", options.Title);
        Assert.Equal("png", options.DefaultExtension);
        Assert.Equal("coordinate_planner.png", options.SuggestedFileName);
        AssertChoices(options.FileTypeChoices, ("PNG", "*.png"), ("BMP", "*.bmp"), ("All files", "*"));
    }

    private static void AssertChoices(
        IReadOnlyList<FilePickerFileType>? choices,
        params (string Name, string Pattern)[] expected)
    {
        Assert.NotNull(choices);
        Assert.Equal(expected.Length, choices.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, choices[i].Name);
            Assert.Equal([expected[i].Pattern], choices[i].Patterns);
        }
    }
}
