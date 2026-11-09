// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInspectorTraceGeneratedDiagnosticsBehaviorTests
{
    [AvaloniaFact]
    public async Task CadTrace_GeneratedDiagnostics_ShowsTheStructuredToFullExplanationAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        vm.EnableToFull = false;
        var cad = Assert.Single(vm.CadPads);

        var snapshot = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            cad.Id, includeNotchRowEligibilityDetails: false)?.Cad);

        var notch = Assert.IsType<PadInspectorNotchSnapshot>(snapshot.Notch);
        var diagnostics = Assert.IsType<string>(notch.Diagnostics);
        Assert.Contains(", ownerCnt=1,", diagnostics, StringComparison.Ordinal);
        var trace = Assert.Single(snapshot.RuleTrace, static entry => entry.Rule == "To Full");
        Assert.DoesNotContain("Raw:", trace.Detail, StringComparison.Ordinal);
        Assert.Contains("Target: IC 1 / Diff idx", trace.Detail, StringComparison.Ordinal);
        Assert.Contains("Owners: 1 (", trace.Detail, StringComparison.Ordinal);
        Assert.Contains("Applied: ", trace.Detail, StringComparison.Ordinal);
    }
}
