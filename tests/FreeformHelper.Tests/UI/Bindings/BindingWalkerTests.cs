// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

[Collection("HeadlessUiSerial")]
public sealed class BindingWalkerTests
{
    [AvaloniaTheory]
    [InlineData("Child.Misspelled", "Misspelled")]
    [InlineData("Misspelled.Name", "Misspelled")]
    public void MissingSegmentReportsItsSourceAndCompletePath(string path, string segment)
    {
        var report = Walk($"<TextBlock Text=\"{{Binding {path}}}\"/>");

        var failure = Assert.Single(report.Findings);
        Assert.Equal(BindingCoverage.Failed, failure.Coverage);
        Assert.Equal("fixture.axaml", failure.Site.File);
        Assert.Equal(path, failure.Path);
        Assert.Equal(segment, failure.Segment);
        Assert.False(report.IsComplete);
    }

    [AvaloniaTheory]
    [InlineData("Child.ReadOnly", "TwoWay")]
    [InlineData("Child.InitOnly", "OneWayToSource")]
    [InlineData("Child.PrivateSetter", "TwoWay")]
    public void WriteModeRejectsNonWritableEndpoints(string path, string mode)
    {
        var report = Walk($"<TextBox Text=\"{{Binding {path}, Mode={mode}}}\"/>");

        var failure = Assert.Single(report.Findings);
        Assert.Equal(BindingCoverage.Failed, failure.Coverage);
        Assert.Contains("writable", failure.Reason, StringComparison.Ordinal);
        Assert.Equal(path.Split('.')[^1], failure.Segment);
    }

    [AvaloniaFact]
    public void DefaultModeUsesTheTargetPropertyMetadata()
    {
        var report = Walk("<TextBox Text=\"{Binding Child.ReadOnly}\"/>");

        Assert.Equal(BindingCoverage.Failed, Assert.Single(report.Findings).Coverage);
    }

    [AvaloniaFact]
    public void ReadOnlyChildReferenceAllowsWritingItsMutableEndpoint()
    {
        var report = Walk("<TextBox Text=\"{Binding Child.Name, Mode=TwoWay}\"/>");

        Assert.True(report.IsComplete, report.Format());
    }

    [AvaloniaTheory]
    [InlineData("Child?.Name")]
    [InlineData("Child.Name^")]
    public void UnsupportedSyntaxIsListedAsUncovered(string path)
    {
        var report = Walk($"<TextBlock Text=\"{{Binding {path}}}\"/>");

        Assert.Equal(BindingCoverage.Uncovered, Assert.Single(report.Findings).Coverage);
        Assert.Equal(1, report.UncoveredCount);
        Assert.Contains(path, report.Format(), StringComparison.Ordinal);
        Assert.False(report.IsComplete);
    }

    [AvaloniaFact]
    public void MissingNamedElementCannotFallBackToTheDataContext()
    {
        var report = Walk("<TextBlock Text=\"{Binding Name, ElementName=Absent}\"/>");

        var failure = Assert.Single(report.Findings);
        Assert.Equal("Absent", failure.Segment);
        Assert.Equal(BindingCoverage.Failed, failure.Coverage);
    }

    [AvaloniaFact]
    public void NestedAndItemContextsUseTheirOwnTypesEvenForEmptyCollections()
    {
        var report = Walk("""
            <StackPanel DataContext="{Binding Child}">
              <TextBlock Text="{Binding Name}"/>
              <ItemsControl ItemsSource="{Binding Rows}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate><TextBlock Text="{Binding Misspelled}"/></DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """);

        var failure = Assert.Single(report.Findings);
        Assert.Equal("Misspelled", failure.Segment);
        Assert.Contains(nameof(Row), failure.Source, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void NamedAndAncestorContextsCrossTheTemplateBoundary()
    {
        var report = Walk("""
            <StackPanel x:Name="Outer">
              <ItemsControl ItemsSource="{Binding Child.Rows}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate>
                    <StackPanel>
                      <TextBlock Text="{Binding DataContext.Child.Name, ElementName=Outer}"/>
                      <TextBlock Text="{Binding $parent[UserControl].DataContext.Child.Name}"/>
                      <TextBlock Text="{Binding Caption}"/>
                    </StackPanel>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </StackPanel>
            """);

        Assert.True(report.IsComplete, report.Format());
        Assert.Equal(4, report.CheckedCount);
    }

    [AvaloniaFact]
    public void SharedResourceTemplateIsCheckedAtEveryConsumer()
    {
        var report = Walk("""
            <UserControl.Resources>
              <DataTemplate x:Key="Shared"><TextBlock Text="{Binding Caption}"/></DataTemplate>
            </UserControl.Resources>
            <StackPanel>
              <ItemsControl ItemsSource="{Binding Child.Rows}" ItemTemplate="{StaticResource Shared}"/>
              <ItemsControl ItemsSource="{Binding Child.WrongRows}" ItemTemplate="{StaticResource Shared}"/>
            </StackPanel>
            """);

        var failure = Assert.Single(report.Findings);
        Assert.Equal("Caption", failure.Segment);
        Assert.Contains(nameof(Child), failure.Source, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void DeclaredDataTypeDoesNotReplaceAnUnknownRuntimeContext()
    {
        var report = Walk("""
            <ContentControl Content="{Binding Unknown}">
              <ContentControl.ContentTemplate>
                <DataTemplate x:DataType="x:String"><TextBlock Text="{Binding Length}"/></DataTemplate>
              </ContentControl.ContentTemplate>
            </ContentControl>
            """);

        Assert.False(report.IsComplete);
        Assert.Contains(report.Findings, static finding => finding.Segment == "DataTemplate");
        Assert.Contains(report.Findings, static finding => finding.Coverage == BindingCoverage.Uncovered);
    }

    [AvaloniaFact]
    public void UnsupportedBindingOptionsDoNotDisappearFromCoverage()
    {
        var report = Walk("<TextBlock Text=\"{Binding Child.Name, FutureOption=True}\"/>");

        Assert.Equal("FutureOption", Assert.Single(report.Findings).Segment);
        Assert.Equal(1, report.UncoveredCount);
    }

    [Fact]
    public void MarkupGrammarPreservesNestedSourcesAndQuotedFormats()
    {
        var binding = BindingMarkup.Parse("{Binding Child.Name, RelativeSource={RelativeSource AncestorType=Window}, StringFormat='{}{0}, value={1}'}");

        Assert.Equal("Child.Name", binding.Path);
        Assert.Equal("{}{0}, value={1}", binding.Get("StringFormat"));
        Assert.Equal("Window", BindingMarkup.Parse(binding.Get("RelativeSource")!).Get("AncestorType"));
    }

    [Fact]
    public void OwningChildInitializationReportsTheNullSegment()
    {
        var error = Assert.Throws<BindingWalkException>(() => BindingPathVerifier.RequireInitialized(new Root(), nameof(Root.Absent)));

        Assert.Equal(nameof(Root.Absent), error.Segment);
        Assert.Equal(BindingCoverage.Failed, error.Coverage);
    }

    [AvaloniaFact]
    public void MultiBindingInputsAreIndependentlyChecked()
    {
        var report = Walk("""
            <TextBlock>
              <TextBlock.Text>
                <MultiBinding StringFormat="{0} {1}">
                  <Binding Path="Child.Name"/>
                  <Binding Path="Child.Missing"/>
                </MultiBinding>
              </TextBlock.Text>
            </TextBlock>
            """);

        var failure = Assert.Single(report.Findings);
        Assert.Equal("Missing", failure.Segment);
        Assert.Equal(3, report.SiteCount);
    }

    [AvaloniaFact]
    public void ErasedControlCollectionRetainsItsCallerItemTypeAcrossFiles()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var documents = new BindingDocuments(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fixture.axaml"] = """
                <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             xmlns:test="clr-namespace:FreeformHelper.Tests.UI.Bindings">
                  <test:BindingCardsFixture Cards="{Binding Child.Rows}"/>
                </UserControl>
                """,
            ["cards.axaml"] = """
                <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             x:Class="FreeformHelper.Tests.UI.Bindings.BindingCardsFixture" x:Name="Root">
                  <ItemsControl ItemsSource="{Binding Cards, ElementName=Root}">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate><TextBlock Text="{Binding Caption}"/></DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </UserControl>
                """,
        });

        var report = new AxamlBindingWalker(documents).Walk("fixture.axaml", typeof(Root));

        Assert.True(report.IsComplete, report.Format());
        Assert.Equal(3, report.CheckedCount);
    }

    [AvaloniaFact]
    public void CastAndIndexerGrammarPreserveTheActualSourceType()
    {
        var report = Walk("""
            <StackPanel xmlns:test="clr-namespace:FreeformHelper.Tests.UI.Bindings">
              <TextBlock Text="{Binding Child.Rows[0].Caption}"/>
              <TextBlock Text="{Binding $parent[UserControl].((test:BindingWalkerTests+Root)DataContext).Child.Name}"/>
              <TextBlock IsVisible="{Binding !Child.Enabled}"/>
            </StackPanel>
            """);

        Assert.True(report.IsComplete, report.Format());
    }

    [AvaloniaFact]
    public void ControlTemplateUsesTheTemplatedControlInsteadOfTheViewModel()
    {
        var report = Walk("""
            <Button Content="caption">
              <Button.Template>
                <ControlTemplate>
                  <ContentPresenter Content="{TemplateBinding Content}"
                                    IsVisible="{Binding IsEnabled, RelativeSource={RelativeSource TemplatedParent}}"/>
                </ControlTemplate>
              </Button.Template>
            </Button>
            """);

        Assert.True(report.IsComplete, report.Format());
    }

    [AvaloniaFact]
    public void CommandAndConverterContractsRejectIncompatibleTypes()
    {
        var report = Walk("""
            <UserControl.Resources><TextBlock x:Key="NotAConverter"/></UserControl.Resources>
            <StackPanel>
              <Button Command="{Binding Child.Name}"/>
              <TextBlock Text="{Binding Child.Name, Converter={StaticResource NotAConverter}}"/>
            </StackPanel>
            """);

        Assert.Collection(report.Findings,
            command => Assert.Contains("ICommand", command.Reason, StringComparison.Ordinal),
            converter => Assert.Equal("Converter", converter.Segment));
    }

    [AvaloniaFact]
    public void FallbackValueCannotHideAMissingPath()
    {
        var report = Walk("<TextBlock Text=\"{Binding Child.Missing, FallbackValue=available}\"/>");

        Assert.Equal("Missing", Assert.Single(report.Findings).Segment);
        Assert.False(report.IsComplete);
    }

    [AvaloniaFact]
    public void ResourceSourcesAndExplicitDataContextsDoNotUseTheInheritedModel()
    {
        var report = Walk("""
            <UserControl.Resources><x:String x:Key="TextSource">source</x:String></UserControl.Resources>
            <StackPanel DataContext="{StaticResource TextSource}">
              <TextBlock Text="{Binding Length}"/>
              <TextBlock>
                <TextBlock.Text><Binding Path="Length" Source="{StaticResource TextSource}"/></TextBlock.Text>
              </TextBlock>
            </StackPanel>
            """);

        Assert.True(report.IsComplete, report.Format());
        Assert.Equal(2, report.CheckedCount);
    }

    [AvaloniaFact]
    public void AncestorLevelsSelectTheOuterContext()
    {
        var report = Walk("""
            <UserControl DataContext="{Binding Child}">
              <StackPanel>
                <TextBlock Text="{Binding $parent[UserControl;1].DataContext.Child.Name}"/>
                <TextBlock Text="{Binding DataContext.Child.Name, RelativeSource={RelativeSource AncestorType=UserControl, AncestorLevel=2}}"/>
              </StackPanel>
            </UserControl>
            """);

        Assert.True(report.IsComplete, report.Format());
    }

    private static BindingWalkReport Walk(string content)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var documents = new BindingDocuments(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fixture.axaml"] = """
                <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                """ + content + "</UserControl>",
        });
        return new AxamlBindingWalker(documents).Walk("fixture.axaml", typeof(Root));
    }

    private sealed class Root
    {
        public Child Child { get; } = new();
        public Child? Absent { get; }
        public object? Unknown { get; }
    }

    private sealed class Child
    {
        public string Name { get; set; } = "child";
        public string ReadOnly { get; } = "read-only";
        public string InitOnly { get; init; } = "init";
        public string PrivateSetter { get; private set; } = "private";
        public bool Enabled { get; set; }
        public ObservableCollection<Row> Rows { get; } = [];
        public ObservableCollection<Child> WrongRows { get; } = [];
    }

    private sealed record Row(string Caption);
}

internal sealed class BindingCardsFixture : UserControl
{
    public System.Collections.IEnumerable Cards { get; set; } = Array.Empty<object>();
}
