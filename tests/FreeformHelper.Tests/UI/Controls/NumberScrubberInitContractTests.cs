// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NumberScrubberInitContractTests
{
    [AvaloniaFact]
    public void ConstructionLeavesThePartsReadyBeforeTheControlIsShown()
    {
        var scrubber = Create(out var input, out var scrubArea);

        Assert.Equal(0m, scrubber.Value);
        Assert.Equal("0", input.Text);
        Assert.False(input.IsReadOnly);
        Assert.True(scrubArea.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void ValueAndFormatSetBeforeTheControlIsShownReachTheInput()
    {
        var scrubber = Create(out var input, out _);

        scrubber.Value = 2.5m;
        Assert.Equal("2.5", input.Text);

        scrubber.FormatString = "0.00";
        Assert.Equal("2.50", input.Text);
    }

    [AvaloniaFact]
    public void MixedStateShowsAMarkerAndRestoresTheValue()
    {
        var scrubber = Create(out var input, out _);
        scrubber.Value = 4m;

        scrubber.IsMixed = true;
        Assert.Equal("*", input.Text);

        scrubber.IsMixed = false;
        Assert.Equal("4", input.Text);
    }

    [AvaloniaFact]
    public void AccessibleNameIsForwardedToTheInput()
    {
        var scrubber = Create(out var input, out _);

        AutomationProperties.SetName(scrubber, "Gain");

        Assert.Equal("Gain", AutomationProperties.GetName(input));
    }

    private static NumberScrubber Create(out TextBox input, out Border scrubArea)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var scrubber = new NumberScrubber();
        input = Assert.IsType<TextBox>(scrubber.FindControl<TextBox>("InputBox"));
        scrubArea = Assert.IsType<Border>(scrubber.FindControl<Border>("ScrubArea"));
        return scrubber;
    }
}
