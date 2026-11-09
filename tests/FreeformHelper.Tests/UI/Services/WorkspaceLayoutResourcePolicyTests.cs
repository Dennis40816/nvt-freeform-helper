// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceLayoutResourcePolicyTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void NullRow_DoesNotLookUpResources(bool isExpanded)
    {
        // An unusable owner makes any resource lookup fail.
        var exception = Record.Exception(() => WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(null!, null, isExpanded));

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void Expanded_GridLengthResource_PreservesUnitAndValue()
    {
        var owner = new Border();
        var height = new GridLength(2.5, GridUnitType.Star);
        owner.Resources["ConsolePanelRowHeight"] = height;
        owner.Resources["ConsolePanelMinHeight"] = 0.0;
        var row = new RowDefinition { Height = GridLength.Auto };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: true);

        Assert.Equal(height, row.Height);
    }

    [AvaloniaFact]
    public void Expanded_DoubleResource_UsesPixels()
    {
        var owner = new Border();
        owner.Resources["ConsolePanelRowHeight"] = 175.0;
        owner.Resources["ConsolePanelMinHeight"] = 0.0;
        var row = new RowDefinition { Height = new GridLength(2, GridUnitType.Star) };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: true);

        Assert.Equal(new GridLength(175.0, GridUnitType.Pixel), row.Height);
    }

    [AvaloniaFact]
    public void Expanded_UnsupportedHeightResource_RetainsPreviousHeight()
    {
        var owner = new Border();
        owner.Resources["ConsolePanelRowHeight"] = "175";
        owner.Resources["ConsolePanelMinHeight"] = 0.0;
        var previousHeight = new GridLength(3, GridUnitType.Star);
        var row = new RowDefinition { Height = previousHeight };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: true);

        Assert.Equal(previousHeight, row.Height);
    }

    [AvaloniaFact]
    public void Expanded_MinHeightDouble_Applies()
    {
        var owner = new Border();
        owner.Resources["ConsolePanelRowHeight"] = GridLength.Auto;
        owner.Resources["ConsolePanelMinHeight"] = 42.5;
        var row = new RowDefinition { MinHeight = 12.0 };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: true);

        Assert.Equal(42.5, row.MinHeight);
    }

    [AvaloniaTheory]
    [InlineData(48.0f)]
    [InlineData(64)]
    public void Expanded_MinHeightFloatOrInt_RetainsPreviousMinHeight(object minHeight)
    {
        var owner = new Border();
        owner.Resources["ConsolePanelRowHeight"] = GridLength.Auto;
        owner.Resources["ConsolePanelMinHeight"] = minHeight;
        var row = new RowDefinition { MinHeight = 12.0 };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: true);

        Assert.Equal(12.0, row.MinHeight);
    }

    [AvaloniaTheory]
    [InlineData(42.5, 42.5)]
    [InlineData(38.25f, 38.25)]
    [InlineData(40, 40.0)]
    public void Collapsed_UsesAutoHeight_AndMinHeightFromResource(object minHeight, double expected)
    {
        var owner = new Border();
        owner.Resources["ConsoleCollapsedMinHeight"] = minHeight;
        var row = new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 12.0 };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: false);

        Assert.Equal(GridLength.Auto, row.Height);
        Assert.Equal(expected, row.MinHeight);
    }

    [AvaloniaTheory]
    [InlineData(0.0)]
    [InlineData(-4.0)]
    public void Collapsed_NonPositiveMinHeight_UsesZero(double minHeight)
    {
        var owner = new Border();
        owner.Resources["ConsoleCollapsedMinHeight"] = minHeight;
        var row = new RowDefinition { MinHeight = 12.0 };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: false);

        Assert.Equal(0.0, row.MinHeight);
    }

    [AvaloniaFact]
    public void Collapsed_MissingMinHeight_UsesZero()
    {
        var owner = new Border();
        var row = new RowDefinition { MinHeight = 12.0 };

        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(owner, row, isExpanded: false);

        Assert.Equal(0.0, row.MinHeight);
    }

    [AvaloniaTheory]
    [InlineData(42.5, 42.5)]
    [InlineData(7.25f, 7.25)]
    [InlineData(18, 18.0)]
    public void GetResourceDouble_DoubleFloatInt_ReturnsValue(object value, double expected)
    {
        var owner = new Border();
        owner.Resources["WorkspaceLayoutResourcePolicyTests.NumericValue"] = value;

        var actual = WorkspaceLayoutResourcePolicy.GetResourceDouble(owner, "WorkspaceLayoutResourcePolicyTests.NumericValue", -1.0);

        Assert.Equal(expected, actual);
    }

    [AvaloniaTheory]
    [InlineData("WorkspaceLayoutResourcePolicyTests.Missing")]
    [InlineData("WorkspaceLayoutResourcePolicyTests.Unsupported")]
    public void GetResourceDouble_MissingOrOtherType_ReturnsCallerFallback(string key)
    {
        var owner = new Border();
        owner.Resources["WorkspaceLayoutResourcePolicyTests.Unsupported"] = "42.5";

        var actual = WorkspaceLayoutResourcePolicy.GetResourceDouble(owner, key, 83.5);

        Assert.Equal(83.5, actual);
    }
}
