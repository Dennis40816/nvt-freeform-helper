// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowBindingWalkerTests(ITestOutputHelper output)
{
    // Sites that the walker cannot follow yet (secondary windows, key bindings, style setters that need a runtime ancestor).
    // The number only goes down: lower it in the same change that covers more sites. It must equal the measured count.
    private const int UncoveredCeiling = 18;

    [AvaloniaFact]
    public void MainWindowCompositionHasReadableAndWritableBindingPaths()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var documents = BindingDocuments.ReadRepository();

        var report = new AxamlBindingWalker(documents).WalkMainWindow();

        output.WriteLine(report.Format());
        Assert.NotEmpty(documents.Sites);
        var failed = report.Findings.Where(static finding => finding.Coverage == BindingCoverage.Failed)
            .Select(static finding => finding.Site.Location).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(BindingKnownIssues.Failures.Keys.Order(StringComparer.Ordinal), failed);
        Assert.True(report.UncoveredCount == UncoveredCeiling,
            $"Uncovered sites are {report.UncoveredCount}, the ceiling is {UncoveredCeiling}. Lower {nameof(UncoveredCeiling)} when the number went down.{Environment.NewLine}{report.Format()}");
    }
}
