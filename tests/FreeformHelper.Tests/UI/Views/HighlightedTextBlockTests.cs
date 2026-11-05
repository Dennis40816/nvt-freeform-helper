// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class HighlightedTextBlockTests
{
    [AvaloniaFact]
    public void SeveralMatchesSplitOnlyTheMatchedSpans()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var control = new HighlightedTextBlock
        {
            Text = "NC.Ref.drawing",
            Ranges = new List<LayerTextRange> { new(0, 2), new(3, 3) },
        };
        var runs = GetRuns(control);

        Assert.Collection(runs,
            run => AssertMatch(run, "NC"),
            run => AssertPlain(run, "."),
            run => AssertMatch(run, "Ref"),
            run => AssertPlain(run, ".drawing"));
    }

    [AvaloniaFact]
    public void NoMatchesKeepOnePlainRun()
    {
        var control = new HighlightedTextBlock { Text = "NC.drawing" };

        AssertPlain(Assert.Single(GetRuns(control)), "NC.drawing");
    }

    [AvaloniaFact]
    public void MatchesAtBothEndsDoNotCreateEmptyRuns()
    {
        var control = new HighlightedTextBlock
        {
            Text = "Ref1.drawing",
            Ranges = new List<LayerTextRange> { new(0, 3), new(5, 7) },
        };

        Assert.Collection(GetRuns(control),
            run => AssertMatch(run, "Ref"),
            run => AssertPlain(run, "1."),
            run => AssertMatch(run, "drawing"));
    }

    [AvaloniaFact]
    public void LayoutChangesKeepRunsAndTextChangesRebuildThem()
    {
        var control = new HighlightedTextBlock { Text = "NC.drawing" };
        var original = Assert.Single(GetRuns(control));

        control.IsVisible = false;
        Assert.Same(original, Assert.Single(GetRuns(control)));
        control.Text = "PGT.drawing";

        var replacement = Assert.Single(GetRuns(control));
        Assert.NotSame(original, replacement);
        AssertPlain(replacement, "PGT.drawing");
    }

    private static Run[] GetRuns(HighlightedTextBlock control) =>
        Assert.IsType<TextBlock>(control.Child).Inlines!.OfType<Run>().ToArray();

    private static void AssertMatch(Run run, string text)
    {
        Assert.Equal(text, run.Text);
        Assert.Equal(FontWeight.SemiBold, run.FontWeight);
    }

    private static void AssertPlain(Run run, string text)
    {
        Assert.Equal(text, run.Text);
        Assert.Equal(FontWeight.Normal, run.FontWeight);
    }
}
