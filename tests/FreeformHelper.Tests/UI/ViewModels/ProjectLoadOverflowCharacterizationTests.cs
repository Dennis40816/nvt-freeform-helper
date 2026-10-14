// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how a project load behaves when the file passes the schema checks but one value of the
/// UI snapshot cannot be converted (issue #64). The load replaces the active project and applies
/// the settings before it applies the view snapshot, so an exception in the view snapshot leaves
/// the new settings visible while the load reports a failure. These tests describe the current
/// result, not the wanted one. A fix that validates and stages the whole load before it commits
/// changes the expected values here and must say so in its pull request.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadOverflowCharacterizationTests
{
    private const decimal SavedThreshold = 0.27m;
    private const decimal BeforeLoadThreshold = 0.61m;

    [AvaloniaFact]
    public async Task LoadFromPath_ViewSnapshotValueOverflowsDecimal_ReportsFailureAfterApplyingTheFileSettings()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.MatchThreshold = SavedThreshold);
        var path = files.PathFor("project.json");
        // Control: the saved file loads before the value is changed.
        var control = await files.CreateViewModel().LoadProjectFromPathAsync(path);
        Assert.True(control.IsLoaded);
        StoreJsonDocument(path, OverflowCadLineWidth(LoadJsonDocument(path)));
        var vm = files.CreateViewModel();
        vm.MatchThreshold = BeforeLoadThreshold;

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.False(result.IsLoaded);
        Assert.Equal("LOAD_FAILED", result.Code);
        // The load failed, but the settings of the file are already in the view model.
        Assert.Equal(SavedThreshold, vm.MatchThreshold);
    }

    private static JsonNode OverflowCadLineWidth(JsonNode document)
    {
        var view = document["uiSnapshot"]!["view"]!.AsObject();
        view["cadLineWidth"] = 1e100;
        return document;
    }

    private static JsonNode LoadJsonDocument(string path)
    {
        using var reader = new StreamReader(path);
        return JsonNode.Parse(reader.ReadToEnd())!;
    }

    private static void StoreJsonDocument(string path, JsonNode document)
    {
        using var writer = new StreamWriter(path, append: false);
        writer.Write(document.ToJsonString());
    }
}
