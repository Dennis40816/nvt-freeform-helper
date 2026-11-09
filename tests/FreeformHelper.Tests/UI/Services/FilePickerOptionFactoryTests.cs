// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Platform.Storage;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class FilePickerOptionFactoryTests
{
    [Fact]
    public void OpenDxf_FiltersDxfFirstAndAllowsOneFile()
    {
        var options = FilePickerOptionFactory.OpenDxf();

        Assert.Equal("Open DXF", options.Title);
        Assert.False(options.AllowMultiple);
        AssertFilters(options.FileTypeFilter, ("DXF", "*.dxf"), ("All files", "*"));
    }

    [Fact]
    public void OpenProjectAndMask_UseJsonAndCsvFilters()
    {
        var project = FilePickerOptionFactory.OpenProject();
        var mask = FilePickerOptionFactory.OpenRegularVisibilityMask();

        Assert.Equal("Open Project", project.Title);
        AssertFilters(project.FileTypeFilter, ("FreeformHelper Project", "*.json"), ("All files", "*"));
        Assert.Equal("Import Regular Visibility Mask (SeeRegular.csv)", mask.Title);
        Assert.False(mask.AllowMultiple);
        AssertFilters(mask.FileTypeFilter, ("CSV", "*.csv"), ("All files", "*"));
    }

    [Fact]
    public void SaveProject_SuggestsTheDefaultProjectFileName()
    {
        var options = FilePickerOptionFactory.SaveProject();

        Assert.Equal("Save Project", options.Title);
        Assert.Equal("json", options.DefaultExtension);
        Assert.Equal("freeform_helper_project.json", options.SuggestedFileName);
        AssertFilters(options.FileTypeChoices, ("FreeformHelper Project", "*.json"), ("All files", "*"));
    }

    [Theory]
    [InlineData("csv", "csv", "CSV (*.csv)", "*.csv", "C source (*.c)", "*.c")]
    [InlineData("c", "c", "C source (*.c)", "*.c", "CSV (*.csv)", "*.csv")]
    [InlineData(".C", "c", "C source (*.c)", "*.c", "CSV (*.csv)", "*.csv")]
    [InlineData("txt", "csv", "CSV (*.csv)", "*.csv", "C source (*.c)", "*.c")]
    [InlineData("", "csv", "CSV (*.csv)", "*.csv", "C source (*.c)", "*.c")]
    public void SaveNotch_NormalizesTheExtensionAndOffersTheOtherNotchFormat(
        string requested, string expectedExt, string firstName, string firstPattern, string secondName, string secondPattern)
    {
        var options = FilePickerOptionFactory.SaveNotch("notch_table", requested);

        Assert.Equal("Save Notch Table", options.Title);
        Assert.Equal(expectedExt, options.DefaultExtension);
        Assert.Equal($"notch_table.{expectedExt}", options.SuggestedFileName);
        AssertFilters(options.FileTypeChoices, (firstName, firstPattern), (secondName, secondPattern), ("All files", "*"));
    }

    [Theory]
    [InlineData("png", "Save Image", "PNG image")]
    [InlineData("BMP", "Save Image", "BMP image")]
    [InlineData(".jpeg", "Save Image", "JPG image")]
    [InlineData("jpg", "Save Image", "JPG image")]
    [InlineData("dxf", "Save DXF", "DXF file")]
    [InlineData("json", "Save Export File", "JSON file")]
    [InlineData("", "Save Export File", "DAT file")]
    public void SaveExport_PicksTheTitleAndTypeNameFromTheExtension(string requested, string title, string typeName)
    {
        var options = FilePickerOptionFactory.SaveExport("layers", requested);

        var normalized = requested.Trim().TrimStart('.').ToLowerInvariant();
        var expectedExt = normalized.Length == 0 ? "dat" : normalized;
        Assert.Equal(title, options.Title);
        Assert.Equal(expectedExt, options.DefaultExtension);
        Assert.Equal($"layers.{expectedExt}", options.SuggestedFileName);
        AssertFilters(options.FileTypeChoices, (typeName, $"*.{expectedExt}"), ("All files", "*"));
    }

    private static void AssertFilters(
        IReadOnlyList<FilePickerFileType>? actual, params (string Name, string Pattern)[] expected)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, actual[i].Name);
            Assert.Equal(expected[i].Pattern, Assert.Single(actual[i].Patterns ?? []));
        }
    }
}
