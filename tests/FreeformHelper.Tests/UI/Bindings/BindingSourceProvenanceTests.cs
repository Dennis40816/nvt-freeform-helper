// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

[Collection("HeadlessUiSerial")]
public sealed class BindingSourceProvenanceTests
{
    private const string SimulationFile = "Views/SimulationWorkspaceView.Integrated.cs";
    private const string SettingsFile = "Views/FreeformHelperView.SettingsWindow.cs";
    private const string ExportSettingsFile = "Views/LayerImageExportSettingsDialog.axaml";

    [AvaloniaTheory]
    [InlineData("ObservableRows")]
    [InlineData("ReadOnlyRows")]
    [InlineData("EnumerableRows")]
    public void ItemBindingRejectsAnUnrelatedDeclarationEvenWhenItsPathExists(string collection)
    {
        var report = WalkItems(collection, "UnrelatedItem");

        var failure = Assert.Single(report.Findings);
        Assert.Equal(BindingCoverage.Failed, failure.Coverage);
        Assert.Equal("SelectedValueBinding", failure.Site.Target);
        Assert.Equal("DataType", failure.Segment);
        Assert.Equal(typeof(Row).FullName, failure.Source);
        Assert.Equal("Caption", failure.Path);
    }

    [AvaloniaTheory]
    [InlineData("ObservableRows", "Row")]
    [InlineData("ReadOnlyRows", "Row")]
    [InlineData("EnumerableRows", "Row")]
    [InlineData("ObservableRows", "CaptionedItem")]
    public void ItemBindingAcceptsMatchingAndAssignableDeclarations(string collection, string declaration)
    {
        var report = WalkItems(collection, declaration);

        Assert.True(report.IsComplete, report.Format());
    }

    [AvaloniaTheory]
    [InlineData("ErasedRows")]
    [InlineData("ObjectRows")]
    public void UnknownCollectionItemsKeepDeclarationBasedChecking(string collection)
    {
        var report = WalkItems(collection, "Row");

        Assert.True(report.IsComplete, report.Format());
    }

    [AvaloniaFact]
    public void ExportFormatBindingUsesTheActualOptionType()
    {
        var combo = ReadFormatComboBox();

        var report = Walk(combo.ToString(), typeof(LayerImageExportSettingsViewModel));

        Assert.True(report.IsComplete, report.Format());
    }

    [AvaloniaFact]
    public void ExportFormatBindingRejectsTheSettingsTypeAsAnItemSource()
    {
        var combo = ReadFormatComboBox();
        combo.SetAttributeValue("SelectedValueBinding", "{Binding Format, DataType=vm:LayerImageFileSettings}");

        var report = Walk(combo.ToString(), typeof(LayerImageExportSettingsViewModel));

        var failure = Assert.Single(report.Findings);
        Assert.Equal(BindingCoverage.Failed, failure.Coverage);
        Assert.Equal("SelectedValueBinding", failure.Site.Target);
        Assert.Equal("DataType", failure.Segment);
        Assert.Equal(typeof(LayerImageFileSettings.ImageFormatOption).FullName, failure.Source);
        Assert.Equal("Format", failure.Path);
    }

    [AvaloniaFact]
    public void RegisteredCodeBindingsWithTheirActualSourcesStayChecked()
    {
        var report = CheckCode(ReadProduct(SimulationFile));

        Assert.True(report.IsComplete, report.Format());
    }

    [AvaloniaFact]
    public void CodeBindingChecksTheActualTargetSourceInsteadOfTheNameofOwner()
    {
        var source = ReadProduct(SimulationFile).Replace("Source = toggle", "Source = body", StringComparison.Ordinal);

        var report = CheckCode(source);

        var failure = Assert.Single(report.Findings);
        Assert.Equal(BindingCoverage.Failed, failure.Coverage);
        Assert.Equal(SimulationFile, failure.Site.File);
        Assert.Equal(typeof(Grid).FullName, failure.Source);
        Assert.Equal("IsChecked", failure.Segment);
    }

    [AvaloniaTheory]
    [InlineData("Source = unregistered")]
    [InlineData("")]
    public void CodeBindingDoesNotAssumeARegisteredSourceWhenItCannotBeResolved(string assignment)
    {
        var source = ReadProduct(SimulationFile).Replace("Source = toggle", assignment, StringComparison.Ordinal);

        var report = CheckCode(source);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(BindingCoverage.Uncovered, finding.Coverage);
        Assert.Equal(SimulationFile, finding.Site.File);
        Assert.Equal("C# source", finding.Segment);
    }

    private static BindingWalkReport WalkItems(string collection, string declaration) => Walk($$"""
        <ComboBox ItemsSource="{Binding {{collection}}}"
                  SelectedValueBinding="{Binding Caption, DataType=test:BindingSourceProvenanceTests+{{declaration}}}"/>
        """, typeof(Collections));

    private static BindingWalkReport Walk(string content, Type context)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var documents = new BindingDocuments(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fixture.axaml"] = """
                <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             xmlns:test="clr-namespace:FreeformHelper.Tests.UI.Bindings"
                             xmlns:vm="clr-namespace:FreeformHelper.UI.ViewModels">
                """ + content + "</UserControl>",
        });
        return new AxamlBindingWalker(documents).Walk("fixture.axaml", context);
    }

    private static BindingWalkReport CheckCode(string simulationSource)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SimulationFile] = simulationSource,
            [SettingsFile] = ReadProduct(SettingsFile),
        };
        var types = new BindingTypeResolver();
        var paths = new BindingPathVerifier(types, static (_, property) =>
            throw BindingWalkException.Uncovered(property, "Code fixtures do not have an AXAML frame."));
        var report = new BindingWalkReport();
        MainWindowBindingCatalog.CheckCodePaths(types, paths, report, sources);
        return report;
    }

    private static XElement ReadFormatComboBox() => Assert.Single(XElement.Parse(ReadProduct(ExportSettingsFile)).Descendants(),
        static element => element.Attribute("SelectedValueBinding") is not null);

    private static string ReadProduct(string file) => TestFiles.ReadContent(TestPaths.FromRepo("src", "FreeformHelper.UI", file));

    private sealed class Collections
    {
        public ObservableCollection<Row> ObservableRows { get; } = [];
        public IReadOnlyList<Row> ReadOnlyRows { get; } = [];
        public IEnumerable<Row> EnumerableRows { get; } = [];
        public System.Collections.IEnumerable ErasedRows { get; } = Array.Empty<Row>();
        public IEnumerable<object> ObjectRows { get; } = [];
    }

    private class CaptionedItem
    {
        public string Caption { get; } = string.Empty;
    }

    private sealed class Row : CaptionedItem
    {
    }

    private sealed class UnrelatedItem
    {
        public string Caption { get; } = string.Empty;
    }
}
