// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Sends the numeric Settings fields through the real draft view model, the Settings Save, the project save and a
/// project reload. Min and Max are the ranges that the NumberScrubber controls of the Settings window declare.
/// MinEnforced and MaxEnforced say whether the model clamps that side. Where it does not, only the control guards
/// the range, and the tests below pin the current pass-through. A fix that enforces a side changes the flag and must
/// say so in its pull request. See cloud-notes/settings-bounds.md for the findings.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class SettingsWindowNumericBoundsTests
{
    // Covered elsewhere or without a Settings control: CascadeNum and the per-IC channel totals (large values make
    // the grid rebuild very slow), the hidden LenScale and the CAD pad anchor.
    private static readonly string[] _notCovered =
    [
        nameof(SettingsWindowViewModel.CascadeNum),
        nameof(SettingsWindowViewModel.XChannels),
        nameof(SettingsWindowViewModel.YChannels),
        nameof(SettingsWindowViewModel.LenScale),
        nameof(SettingsWindowViewModel.CadOutputFwDiffIndexAnchorCadId),
    ];

    private static readonly Field[] _fields =
    [
        new(nameof(SettingsWindowViewModel.ActiveAreaWidth), 0.1m, 1000000m, false, false, false),
        new(nameof(SettingsWindowViewModel.ActiveAreaHeight), 0.1m, 1000000m, false, false, false),
        new(nameof(SettingsWindowViewModel.GridPaddingPercent), 0m, 100m, true, false, false) { PersistedMax = 50m },
        new(nameof(SettingsWindowViewModel.CoordinatePixelWidth), 1m, 1000000m, true, false, true),
        new(nameof(SettingsWindowViewModel.CoordinatePixelHeight), 1m, 1000000m, true, false, true),
        new(nameof(SettingsWindowViewModel.PanelBiasX), -1000000m, 1000000m, false, false, false),
        new(nameof(SettingsWindowViewModel.PanelBiasY), -1000000m, 1000000m, false, false, false),
        new(nameof(SettingsWindowViewModel.GlobalFontSizePercent), 80m, 140m, true, true, true),
        new(nameof(SettingsWindowViewModel.HighlightStrokeWidthAdjust), -2m, 4m, true, true, false),
        new(nameof(SettingsWindowViewModel.MatchThreshold), 0m, 1m, true, true, false),
        new(nameof(SettingsWindowViewModel.FreeformAxisThreshold), 0m, 1m, true, true, false),
        new(nameof(SettingsWindowViewModel.ToFullStrictOverlapPercent), 0m, 100m, true, true, false),
        new(nameof(SettingsWindowViewModel.BoundaryVirtualAreaCapPercent), 0m, 1000m, true, true, false),
        new(nameof(SettingsWindowViewModel.TargetCoverageCapPercent), 0m, 255m, true, true, true),
        new(nameof(SettingsWindowViewModel.NotchPreviewAutoPlayIntervalMs), 200m, 5000m, true, true, true),
        new(nameof(SettingsWindowViewModel.NotchPreviewVisualizationStep), 1m, 3m, true, true, true),
        new(nameof(SettingsWindowViewModel.MappingWeightIou), 0m, 10m, true, false, false),
        new(nameof(SettingsWindowViewModel.MappingWeightCentroidDistance), 0m, 10m, true, false, false),
        new(nameof(SettingsWindowViewModel.MappingWeightAreaRatio), 0m, 10m, true, false, false),
        new(nameof(SettingsWindowViewModel.MappingCandidateNumber), 1m, 400m, true, true, true),
        new(nameof(SettingsWindowViewModel.MappingLowConfidenceThreshold), 0m, 1m, true, true, false),
        new(nameof(SettingsWindowViewModel.MappingAmbiguousMargin), 0m, 1m, true, true, false),
        new(nameof(SettingsWindowViewModel.CadOutputFwDiffIndexStart), 0m, 1000000m, true, true, true),
        // Above Max the project save rejects NullValue with a message instead of clamping, so it has no AboveMax case.
        new(nameof(SettingsWindowViewModel.NullValue), 0m, 65535m, true, false, true) { SkipAboveMax = true },
        new(nameof(SettingsWindowViewModel.NotchThresholdQ7), 0m, 128m, true, true, true),
        new(nameof(SettingsWindowViewModel.NotchThresholdPercent), 0m, 100m, true, true, false),
    ];

    [Fact]
    public void EveryNumericSettingsProperty_IsCoveredOrListedAsNotCovered()
    {
        var numeric = typeof(SettingsWindowViewModel)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(decimal))
            .Select(property => property.Name)
            .ToList();

        var unknown = numeric.Except(_fields.Select(field => field.Name)).Except(_notCovered).ToList();

        Assert.Equal("", string.Join(",", unknown));
    }

    [AvaloniaFact]
    public async Task SaveAndReload_MinimumValues_KeepEveryField()
    {
        var mismatches = await FindMismatchesAsync(_fields, field => field.Min);

        Assert.Equal("", mismatches);
    }

    [AvaloniaFact]
    public async Task SaveAndReload_MiddleValues_KeepEveryField()
    {
        var mismatches = await FindMismatchesAsync(_fields, Middle);

        Assert.Equal("", mismatches);
    }

    [AvaloniaFact]
    public async Task SaveAndReload_MaximumValues_KeepEveryFieldExceptThePinnedGridPaddingCap()
    {
        // GridPaddingPercent documents 0-100 in the control, but the save path caps the ratio at 0.5 (50%).
        var mismatches = await FindMismatchesAsync(_fields, field => field.Max);

        Assert.Equal("", mismatches);
    }

    [AvaloniaFact]
    public async Task SaveAndReload_JustBelowMinimum_ClampsWhereTheModelEnforcesTheMinimum()
    {
        // ActiveAreaWidth and ActiveAreaHeight are left out: a value of 0 or less reloads as the default, not as Min.
        var fields = _fields.Where(field => !field.Name.StartsWith("ActiveArea", StringComparison.Ordinal)).ToList();

        var mismatches = await FindMismatchesAsync(fields, field => field.Min - field.Step);

        Assert.Equal("", mismatches);
    }

    [AvaloniaFact]
    public async Task SaveAndReload_JustAboveMaximum_ClampsWhereTheModelEnforcesTheMaximum()
    {
        var fields = _fields.Where(field => !field.SkipAboveMax).ToList();

        var mismatches = await FindMismatchesAsync(fields, field => field.Max + field.Step);

        Assert.Equal("", mismatches);
    }

    [AvaloniaFact]
    public async Task SaveAndReload_NullValueAboveMaximum_RejectsTheProjectSaveWithAMessage()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var draft = vm.CreateSettingsWindowViewModel();
        draft.NullValue = 65536m;
        draft.SaveCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        var saved = await vm.SaveProjectAsync();

        Assert.False(saved);
        Assert.Contains("NullValue must be in [0,65535]", vm.StatusText, StringComparison.Ordinal);
    }

    private static decimal Middle(Field field) =>
        field.Whole
            ? Math.Floor((field.Min + field.Max) / 2m)
            : Math.Round((field.Min + field.Max) / 2m, 2, MidpointRounding.AwayFromZero);

    private static async Task<string> FindMismatchesAsync(IReadOnlyList<Field> fields, Func<Field, decimal> probe)
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var draft = vm.CreateSettingsWindowViewModel();
        foreach (var field in fields)
        {
            typeof(SettingsWindowViewModel).GetProperty(field.Name)!.SetValue(draft, probe(field));
        }

        draft.SaveCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var reloaded = files.CreateViewModel();
        var result = await reloaded.LoadProjectFromPathAsync(files.PathFor("project.json"));
        Assert.True(result.IsLoaded);

        var mismatches = new List<string>();
        foreach (var field in fields)
        {
            var requested = probe(field);
            var expected = field.Expected(requested);
            var actual = (decimal)typeof(FreeformHelperViewModel).GetProperty(field.Name)!.GetValue(reloaded)!;
            if (actual != expected)
            {
                mismatches.Add($"{field.Name}: set {requested}, expected {expected}, reloaded {actual}");
            }
        }

        return string.Join("; ", mismatches);
    }

    private sealed record Field(string Name, decimal Min, decimal Max, bool MinEnforced, bool MaxEnforced, bool Whole)
    {
        public decimal? PersistedMax { get; init; }

        public bool SkipAboveMax { get; init; }

        // One unit for wide ranges, otherwise a small step, so that a value is just outside the range.
        public decimal Step => Max - Min > 50m ? 1m : 0.0001m;

        public decimal Expected(decimal requested)
        {
            if (PersistedMax is { } cap && requested > cap)
            {
                return cap;
            }

            if (requested < Min && MinEnforced)
            {
                return Min;
            }

            if (requested > Max && MaxEnforced)
            {
                return Max;
            }

            return requested;
        }
    }
}
