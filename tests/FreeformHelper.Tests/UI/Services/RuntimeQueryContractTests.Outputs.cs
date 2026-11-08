// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryContractTests
{
    [AvaloniaFact]
    public async Task StatusAndSelectionProjectCountsWorkflowPreviewTimingsAndWarmCacheAsync()
    {
        using var shell = await CreateMatchedShellAsync();
        var helper = shell.FreeformHelper;
        var cad = Assert.Single(helper.CadPads);
        var regular = helper.RegularPads[0];
        helper.ApplyCanvasSelection([cad.Id], [regular.Index]);
        var useCase = new RuntimeQueryUseCase(shell);
        var notch = await QueryAsync(useCase, "notch", $"cad-id={cad.Id}");
        AssertNotchDebugAreas(notch, helper, cad.Id);
        await QueryAsync(useCase, "notch", $"cad-id={cad.Id}");

        var status = await QueryAsync(useCase, "status");
        Assert.Equal(1, status.GetProperty("cadCount").GetInt32());
        Assert.Equal(2, status.GetProperty("regularCount").GetInt32());
        Assert.Equal(1, status.GetProperty("selection").GetProperty("cadCount").GetInt32());
        Assert.Equal(1, status.GetProperty("selection").GetProperty("regularCount").GetInt32());
        AssertWorkflow(status.GetProperty("workflow"), helper);
        var preview = status.GetProperty("notchPreview");
        Assert.Equal(helper.ShowNotchCanvasPreview, preview.GetProperty("isVisible").GetBoolean());
        Assert.Equal(helper.ShowNotchToRegularLabels, preview.GetProperty("showToRegularLabels").GetBoolean());
        Assert.Equal((int)helper.NotchPreviewVisualizationStep, preview.GetProperty("stage").GetInt32());
        Assert.Equal(helper.NotchPreviewAutoPlayEnabled, preview.GetProperty("autoPlayEnabled").GetBoolean());
        Assert.Equal((double)helper.NotchPreviewAutoPlayIntervalMs, preview.GetProperty("autoPlayIntervalMs").GetDouble());
        AssertCache(status.GetProperty("cache"), helper);

        var selection = await QueryAsync(useCase, "selection");
        Assert.Equal([cad.Id], Integers(selection.GetProperty("selectedCadIds")));
        Assert.Equal([regular.Index], Integers(selection.GetProperty("selectedRegularIndices")));
        Assert.Equal([regular.RegularPadId], Integers(selection.GetProperty("selectedRegularPadIds")));
        var timing = helper.GetLastSelectionTimingSnapshot();
        var timings = selection.GetProperty("timings");
        Assert.Equal(timing.SummaryMs, timings.GetProperty("summaryMs").GetInt64());
        Assert.Equal(timing.InspectorMs, timings.GetProperty("inspectorMs").GetInt64());
        Assert.Equal(timing.NotchPreviewMs, timings.GetProperty("notchPreviewMs").GetInt64());
        Assert.Equal(timing.TotalMs, timings.GetProperty("totalMs").GetInt64());
        await AssertStageAndOwnerLimitsAsync(useCase, helper, cad.Id);
    }

    [AvaloniaTheory]
    [InlineData("cad")]
    [InlineData("regular")]
    public async Task PadProjectsGeometryIndicesMatchesAndRuleTraceAsync(string kind)
    {
        using var shell = await CreateMatchedShellAsync();
        var helper = shell.FreeformHelper;
        var cad = Assert.Single(helper.CadPads);
        var regular = helper.RegularPads[0];
        var id = kind == "cad" ? cad.Id : regular.RegularPadId;
        var root = await QueryAsync(new RuntimeQueryUseCase(shell), "pad", $"{kind}-id={id}");
        Assert.Equal(kind, root.GetProperty("kind").GetString());
        var pad = root.GetProperty(kind + "Pad");
        Assert.Equal(id, pad.GetProperty(kind == "cad" ? "id" : "regularPadId").GetInt32());
        Assert.Equal(kind == "cad" ? 200d : 100d, pad.GetProperty("area").GetDouble());
        Assert.Equal(0d, pad.GetProperty("bounds").GetProperty("minX").GetDouble());
        Assert.Equal(kind == "cad" ? 20d : 10d, pad.GetProperty("bounds").GetProperty("maxX").GetDouble());
        Assert.Equal(0d, pad.GetProperty("bounds").GetProperty("minY").GetDouble());
        Assert.Equal(10d, pad.GetProperty("bounds").GetProperty("maxY").GetDouble());
        var snapshot = root.GetProperty("snapshot");
        AssertPadSnapshot(kind, id, helper, pad, root, snapshot, cad.Id);
    }

    private static void AssertPadSnapshot(
        string kind, int id, FreeformHelperViewModel helper, JsonElement pad, JsonElement root, JsonElement snapshot, int cadId)
    {
        if (kind == "cad")
        {
            var expected = Assert.IsType<CadPadInspectorSnapshot>(helper.BuildCadPadInspectorSnapshot(id)?.Cad);
            Assert.Equal(expected.IcIndex, pad.GetProperty("icIndex").GetInt32());
            Assert.Equal(expected.DxfIndex, pad.GetProperty("diffIndex").GetInt32());
            Assert.Equal(helper.RegularPads.Select(static item => item.RegularPadId).Order(),
                Integers(root.GetProperty("matchedRegularIds")).Order());
            AssertTrace(root.GetProperty("ruleTrace"), expected.RuleTrace);
            AssertTrace(snapshot.GetProperty("ruleTrace"), expected.RuleTrace);
        }
        else
        {
            var regular = helper.RegularPads[0];
            var expected = Assert.IsType<RegularPadInspectorSnapshot>(helper.BuildRegularPadInspectorSnapshot(id)?.Regular);
            Assert.Equal(regular.IcIndex, pad.GetProperty("icIndex").GetInt32());
            Assert.Equal(regular.DiffIndex, pad.GetProperty("diffIndex").GetInt32());
            Assert.Equal("XWay", pad.GetProperty("freeform").GetString());
            Assert.Equal([cadId], Integers(root.GetProperty("matchedCadIds")));
            Assert.Equal(cadId, pad.GetProperty("matchedCadPadId").GetInt32());
            AssertTrace(root.GetProperty("ruleTrace"), expected.RuleTrace);
            AssertTrace(snapshot.GetProperty("ruleTrace"), expected.RuleTrace);
        }
    }

    [AvaloniaTheory]
    [InlineData("csv", false)]
    [InlineData("c-v21", true)]
    [InlineData("c-v22", true)]
    public async Task LoadRunExportAndClearWriteRequestedRowsAndReportReplayTimingAsync(string format, bool replayStep2)
    {
        using var original = await CreateMatchedShellAsync();
        var projectPath = TestFiles.NewTempFilePath("runtime-contract-", ".json");
        original.FreeformHelper.AutoReplayStep2AfterProjectLoad = replayStep2;
        original.FreeformHelper.EnableV21 = true;
        original.FreeformHelper.EnableV22 = true;
        original.FreeformHelper.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        Assert.True(await original.FreeformHelper.SaveProjectAsync());
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var loaded = await QueryAsync(useCase, "load-project", "path=" + projectPath);
        Assert.Equal(projectPath, loaded.GetProperty("path").GetString());
        Assert.Equal(1, loaded.GetProperty("cadCount").GetInt32());
        Assert.Equal(2, loaded.GetProperty("regularCount").GetInt32());
        AssertWorkflow(loaded.GetProperty("workflow"), shell.FreeformHelper);
        AssertLoadTimings(loaded.GetProperty("timings"), replayStep2);
        await RunAllStepsAsync(useCase, shell.FreeformHelper);

        var outputPath = TestFiles.NewTempFilePath("runtime-contract-export-", format == "csv" ? ".csv" : ".c");
        var path = format == "csv" ? Path.GetRelativePath(Directory.GetCurrentDirectory(), outputPath) : outputPath;
        var exported = await QueryAsync(useCase, "export-notch", $"format={format};path={path}");
        Assert.Equal(format, exported.GetProperty("format").GetString());
        Assert.Equal(outputPath, exported.GetProperty("path").GetString());
        Assert.True(exported.GetProperty("elapsedMs").GetInt64() >= 0);
        Assert.Equal(new FileInfo(path).Length, exported.GetProperty("fileSize").GetInt64());
        var generated = Assert.IsType<NotchTable>(shell.FreeformHelper.NotchCacheForTests.LastGeneratedTable);
        AssertExportedRows(await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken), format, generated);
        Assert.True(shell.FreeformHelper.EnableV21);
        Assert.True(shell.FreeformHelper.EnableV22);

        var cleared = await QueryAsync(useCase, "clear-step", "step=1");
        Assert.Equal(1, cleared.GetProperty("step").GetInt32());
        AssertWorkflow(cleared.GetProperty("workflow"), shell.FreeformHelper);
        Assert.False(cleared.GetProperty("workflow").GetProperty("hasStep1Result").GetBoolean());
        Assert.False(cleared.GetProperty("workflow").GetProperty("hasStep2Result").GetBoolean());
        Assert.False(cleared.GetProperty("workflow").GetProperty("hasStep4Result").GetBoolean());
        Assert.All(shell.FreeformHelper.RegularPads, static pad => Assert.Equal(FreeformType.None, pad.Freeform));
        Assert.Null(shell.FreeformHelper.NotchCacheForTests.LastGeneratedTable);
    }

    [AvaloniaFact]
    public async Task TerminalDefaultTailAndLinkLimitPreserveLevelAndAbsoluteLineNumbersAsync()
    {
        using var store = CreateTerminalStore();
        using var captured = await store.CaptureLatestAsync();
        using var shell = new ShellViewModel { IsDevPageAvailable = true, ConsoleStore = store };
        var useCase = new RuntimeQueryUseCase(shell);
        var terminal = await QueryAsync(useCase, "terminal");
        Assert.Equal(LoggingBootstrapper.GetCurrentMinimumLevelName(), terminal.GetProperty("minLevel").GetString());
        Assert.Equal(121, terminal.GetProperty("totalLines").GetInt32());
        Assert.Equal(120, terminal.GetProperty("returnedLines").GetInt32());
        var lines = terminal.GetProperty("lines");
        Assert.Contains("line2 https://a.test/2 https://b.test/2", lines[0].GetString(), StringComparison.Ordinal);
        Assert.Contains("line121 https://a.test/121 https://b.test/121", lines[119].GetString(), StringComparison.Ordinal);

        var links = await QueryAsync(useCase, "terminal-links");
        Assert.Equal(terminal.GetProperty("minLevel").GetString(), links.GetProperty("minLevel").GetString());
        Assert.Equal(121, links.GetProperty("totalLines").GetInt32());
        Assert.Equal(120, links.GetProperty("scannedLines").GetInt32());
        Assert.Equal(200, links.GetProperty("limit").GetInt32());
        Assert.Equal(200, links.GetProperty("returnedLinks").GetInt32());
        var entries = links.GetProperty("links");
        Assert.Equal(200, entries.GetArrayLength());
        Assert.Equal(2, entries[0].GetProperty("lineNumber").GetInt32());
        Assert.Equal("https://a.test/2", entries[0].GetProperty("target").GetString());
        Assert.Equal(101, entries[199].GetProperty("lineNumber").GetInt32());
        Assert.Equal("https://b.test/101", entries[199].GetProperty("target").GetString());
    }

    [AvaloniaFact]
    public async Task SimulationReturnsDeltaActiveSurfaceAndNotchImpactsFromCurrentWorkspaceAsync()
    {
        using var shell = await CreateMatchedShellAsync();
        var useCase = new RuntimeQueryUseCase(shell);
        var overview = await QueryAsync(useCase, "simulation");
        Assert.Equal(JsonValueKind.Null, overview.GetProperty("regular").ValueKind);
        var workspace = Assert.IsType<SimulationWorkspaceViewModel>(shell.Simulation.CurrentWorkspace);
        workspace.GlobalValue = 100d;
        var id = shell.FreeformHelper.RegularPads[1].RegularPadId;
        Assert.True(workspace.TryGetRegularQuerySnapshot(id, out var expected));
        Assert.NotEmpty(expected.ImpactItems);
        Assert.NotEqual(0d, expected.DeltaValue);
        Assert.True(expected.IsActiveSurface);

        var root = await QueryAsync(useCase, "simulation", $"regular-id={id}");
        var regular = root.GetProperty("regular");
        Assert.Equal(expected.BeforeValue, regular.GetProperty("beforeValue").GetDouble());
        Assert.Equal(expected.AfterValue, regular.GetProperty("afterValue").GetDouble());
        Assert.Equal(expected.DeltaValue, regular.GetProperty("deltaValue").GetDouble());
        Assert.Equal(expected.IsActiveSurface, regular.GetProperty("isActiveSurface").GetBoolean());
        AssertImpacts(regular.GetProperty("impacts"), expected.ImpactItems);

        var missing = await useCase.ExecuteAsync(new("1", "simulation", ParseArguments("regular-id=999")));
        Assert.False(missing.Ok);
        Assert.Equal("PAD_NOT_FOUND", missing.Error?.Code);
        Assert.Equal("Regular pad 999 is unavailable in the current Simulation workspace.", missing.Error?.Message);
    }

    private static async Task<ShellViewModel> CreateMatchedShellAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var helper = await FreeformHelperViewModelTests.CreateNotchExportCacheViewModelAsync(dxfPath);
        return new ShellViewModel(helper) { IsDevPageAvailable = true };
    }

    private static async Task<JsonElement> QueryAsync(RuntimeQueryUseCase useCase, string command, string arguments = "")
    {
        var result = await useCase.ExecuteAsync(new("1", command, ParseArguments(arguments)));
        Assert.True(result.Ok, $"{result.Error?.Code}: {result.Error?.Message}");
        Assert.Null(result.Error);
        return JsonSerializer.SerializeToElement(result.Data, RuntimeQueryProtocol.CompactJsonOptions);
    }

    private static int[] Integers(JsonElement array) => array.EnumerateArray().Select(static item => item.GetInt32()).ToArray();

    private static void AssertWorkflow(JsonElement actual, FreeformHelperViewModel helper)
    {
        var expected = helper.GetWorkflowStateSnapshot();
        Assert.Equal(expected.HasCad, actual.GetProperty("hasCad").GetBoolean());
        Assert.Equal(expected.HasGrid, actual.GetProperty("hasGrid").GetBoolean());
        Assert.Equal(expected.HasStep1Result, actual.GetProperty("hasStep1Result").GetBoolean());
        Assert.Equal(expected.HasStep2Result, actual.GetProperty("hasStep2Result").GetBoolean());
        Assert.Equal(expected.HasStep3Result, actual.GetProperty("hasStep3Result").GetBoolean());
        Assert.Equal(expected.HasStep4Result, actual.GetProperty("hasStep4Result").GetBoolean());
    }

    private static void AssertCache(JsonElement cache, FreeformHelperViewModel helper)
    {
        var expected = helper.GetNotchCompensationCacheMetrics();
        var compensation = cache.GetProperty("step3Compensation");
        Assert.Equal(expected.Revision, compensation.GetProperty("revision").GetInt32());
        Assert.Equal(expected.HitCount, compensation.GetProperty("hitCount").GetInt64());
        Assert.Equal(expected.MissCount, compensation.GetProperty("missCount").GetInt64());
        Assert.Equal(expected.HitRate, compensation.GetProperty("hitRate").GetDouble());
        var query = cache.GetProperty("queryCadCache");
        Assert.Equal(helper.GetNotchStep3Revision(), query.GetProperty("revision").GetInt32());
        Assert.Equal(1, query.GetProperty("entryCount").GetInt32());
        Assert.Equal(1, query.GetProperty("hitCount").GetInt64());
        Assert.Equal(1, query.GetProperty("missCount").GetInt64());
        Assert.Equal(0.5, query.GetProperty("hitRate").GetDouble());
        var export = helper.GetNotchExportGenerationCacheMetrics();
        var generation = cache.GetProperty("exportGenerationCache");
        Assert.Equal(export.HasEntry, generation.GetProperty("hasEntry").GetBoolean());
        Assert.Equal(export.EntryRowCount, generation.GetProperty("entryRowCount").GetInt32());
        Assert.Equal(export.Step3Revision, generation.GetProperty("step3Revision").GetInt32());
        Assert.Equal(export.HitCount, generation.GetProperty("hitCount").GetInt64());
        Assert.Equal(export.MissCount, generation.GetProperty("missCount").GetInt64());
    }

    private static void AssertTrace(JsonElement actual, IReadOnlyList<PadInspectorRuleTraceEntry> expected)
    {
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Count, actual.GetArrayLength());
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Rule, actual[i].GetProperty("rule").GetString());
            Assert.Equal(expected[i].Outcome, actual[i].GetProperty("outcome").GetString());
            Assert.Equal(expected[i].Detail, actual[i].GetProperty("detail").GetString());
        }
    }

    private static void AssertImpacts(JsonElement actual, IReadOnlyList<NotchApplySimulationImpactItemViewModel> expected)
    {
        Assert.Equal(expected.Count, actual.GetArrayLength());
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].RoleText, actual[i].GetProperty("roleText").GetString());
            Assert.Equal(expected[i].SummaryText, actual[i].GetProperty("summaryText").GetString());
            Assert.Equal(expected[i].DetailText, actual[i].GetProperty("detailText").GetString());
            Assert.Equal(expected[i].DeltaText, actual[i].GetProperty("deltaText").GetString());
            Assert.Equal(expected[i].SourceRowsText, actual[i].GetProperty("sourceRowsText").GetString());
        }
    }

    private static void AssertLoadTimings(JsonElement timings, bool replayStep2)
    {
        foreach (var key in new[] { "totalElapsedMs", "persistenceElapsedMs", "applyStateElapsedMs", "dxfElapsedMs", "rebuildElapsedMs" })
        {
            Assert.True(timings.GetProperty(key).GetInt64() >= 0, key);
        }

        var replay = timings.GetProperty("stepReplay");
        Assert.True(replay.GetProperty("step1Replayed").GetBoolean());
        Assert.Equal(replayStep2, replay.GetProperty("step2Replayed").GetBoolean());
        Assert.True(replay.GetProperty("step1ReplayElapsedMs").GetInt64() >= 0);
        Assert.True(replay.GetProperty("step2ReplayElapsedMs").GetInt64() >= 0);
    }

    private static async Task RunAllStepsAsync(RuntimeQueryUseCase useCase, FreeformHelperViewModel helper)
    {
        var cad = Assert.Single(helper.CadPads);
        await QueryAsync(useCase, "select-cad", $"cad-id={cad.Id}");
        helper.ShowNotchCanvasPreview = true;
        for (var step = 1; step <= 4; step++)
        {
            if (step == 3)
            {
                await QueryAsync(useCase, "select-cad", $"cad-id={cad.Id}");
                helper.ShowNotchCanvasPreview = true;
            }

            var root = await QueryAsync(useCase, "run-step", "step=" + step.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(step, root.GetProperty("step").GetInt32());
            AssertWorkflow(root.GetProperty("workflow"), helper);
            if (step == 3)
            {
                // Characterization: this fixture has no To Full result for the selected CAD, so Step 3 has no preview.
                Assert.Equal("Notch 2.2 preview: selected CAD has no effective To Full result.", root.GetProperty("statusText").GetString());
                Assert.False(root.GetProperty("workflow").GetProperty("hasStep3Result").GetBoolean());
                Assert.Equal(0, root.GetProperty("notchPreview").GetProperty("previewItemCount").GetInt32());
            }
            else
            {
                Assert.True(root.GetProperty("workflow").GetProperty($"hasStep{step}Result").GetBoolean(), root.ToString());
            }
        }
    }

    private static void AssertExportedRows(string content, string format, NotchTable generated)
    {
        if (format == "csv")
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("row_number,version,version_code,ic_index,ic_number,fw_diff_idx,", lines[0], StringComparison.Ordinal);
            var rows = lines.Skip(1).Select(static line => line.Split(',')).ToArray();
            Assert.NotEmpty(rows);
            AssertCsvRows(rows, generated);
            Assert.Contains(rows, static row => row[1] == "2.1");
            Assert.Contains(rows, static row => row[1] == "2.2");
            Assert.All(rows, static row => Assert.Equal("0", row[3]));
            return;
        }

        Assert.Contains(format == "c-v21" ? "Notch v2.1 compensation export" : "Notch v2.2 compensation export",
            content, StringComparison.Ordinal);
        var table = Regex.Match(content, @"castNHC_TABLE_IC1\[USER_NHC_NODE_NUM_IC1\] =\s*\{(?<rows>.*?)\};", RegexOptions.Singleline);
        Assert.True(table.Success, content);
        var nodes = Regex.Matches(table.Groups["rows"].Value, @"\{(?<values>[^{}]+)\}");
        Assert.NotEmpty(nodes);
        var version = format == "c-v21" ? NotchAlgorithmVersion.V21 : NotchAlgorithmVersion.V22;
        var expectedRows = generated.Rows.Where(row => row.Version == version).ToArray();
        Assert.Equal(expectedRows.Length, nodes.Count);
        Assert.All(nodes.Cast<Match>(), node =>
        {
            var values = node.Groups["values"].Value.Split(',', StringSplitOptions.TrimEntries);
            Assert.Equal(format == "c-v21" ? 9 : 7, values.Length);
            // The first value of a v2.1 node is not the FW diff index, so only v2.2 nodes are matched to rows.
            if (format == "c-v22")
            {
                Assert.Contains(expectedRows, row => row.DiffIndex == int.Parse(values[0], CultureInfo.InvariantCulture));
            }

            Assert.True(int.Parse(values[1], CultureInfo.InvariantCulture) > 0);
        });
    }

    private static void AssertCsvRows(string[][] actual, NotchTable generated)
    {
        var expected = generated.Rows.OrderBy(static row => row.Version).ThenBy(static row => row.IcIndex)
            .ThenBy(static row => row.DiffIndex).ThenBy(static row => row.CadPadId).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            var row = expected[i];
            Assert.Equal(row.DiffIndex.ToString(CultureInfo.InvariantCulture), actual[i][5]);
            Assert.Equal(row.RegularPadIndex.ToString(CultureInfo.InvariantCulture), actual[i][6]);
            Assert.Equal(row.CadPadId?.ToString(CultureInfo.InvariantCulture), actual[i][7]);
            Assert.Equal(row.Values.Length.ToString(CultureInfo.InvariantCulture), actual[i][8]);
            Assert.Equal(row.Values.Select(static value => value.ToString(CultureInfo.InvariantCulture)),
                actual[i].Skip(9).Take(row.Values.Length));
        }
    }

    private static void AssertNotchDebugAreas(JsonElement actual, FreeformHelperViewModel helper, int cadId)
    {
        var resolved = Assert.IsType<NotchV22ResolvedResult>(helper.GetCadV22ResolvedResult(cadId));
        var expected = resolved.Compensation.RegularDebugInfos.OrderByDescending(static info => info.OverlapArea)
            .ThenBy(static info => info.RegularPadId).ToArray();
        var regulars = actual.GetProperty("regulars");
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Length, regulars.GetArrayLength());
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].OverlapArea, regulars[i].GetProperty("overlapArea").GetDouble());
            Assert.Equal(expected[i].SourceArea, regulars[i].GetProperty("sourceArea").GetDouble());
            Assert.Equal(expected[i].BlockedArea, regulars[i].GetProperty("blockedArea").GetDouble());
            Assert.Equal(expected[i].ReachableArea, regulars[i].GetProperty("reachableArea").GetDouble());
        }
    }

    private static async Task AssertStageAndOwnerLimitsAsync(RuntimeQueryUseCase useCase, FreeformHelperViewModel helper, int cadId)
    {
        var stage = await QueryAsync(useCase, "notch-stage", $"cad-id={cadId};polygon-limit=1");
        var overlay = helper.GetCadV22StageOverlays(cadId);
        Assert.True(overlay.HasValue);
        AssertStageVertices(stage.GetProperty("stages").GetProperty("stage1"), overlay.Value.Stage1Seed);
        AssertStageVertices(stage.GetProperty("stages").GetProperty("stage2"), overlay.Value.Stage2Candidate);
        AssertStageVertices(stage.GetProperty("stages").GetProperty("stage3"), overlay.Value.Stage3Final);
        var before = helper.ToFullStrictOverlapPercent;
        var owners = await QueryAsync(useCase, "multi-owner", $"cad-id={cadId};limit=1;overlap-percent=100");
        Assert.Equal(0, owners.GetProperty("summary").GetProperty("gateMultiOwnerCount").GetInt32());
        Assert.Equal(0, owners.GetProperty("summary").GetProperty("ownerCountGreaterThanOne").GetInt32());
        Assert.Empty(owners.GetProperty("regulars").EnumerateArray());
        Assert.Equal(100d, owners.GetProperty("threshold").GetProperty("overlapPercent").GetDouble());
        Assert.Equal(before, helper.ToFullStrictOverlapPercent);
    }

    private static void AssertStageVertices(JsonElement actual, IReadOnlyList<Polygon2> expected)
    {
        Assert.Equal(expected.Count, actual.GetProperty("totalPolygons").GetInt32());
        Assert.Equal(Math.Min(1, expected.Count), actual.GetProperty("returnedPolygons").GetInt32());
        var polygons = actual.GetProperty("polygons");
        Assert.Equal(Math.Min(1, expected.Count), polygons.GetArrayLength());
        if (expected.Count == 0)
        {
            return;
        }

        var vertices = polygons[0].GetProperty("vertices");
        Assert.Equal(expected[0].Vertices.Length, vertices.GetArrayLength());
        for (var i = 0; i < expected[0].Vertices.Length; i++)
        {
            Assert.Equal(expected[0].Vertices[i].X, vertices[i].GetProperty("x").GetDouble());
            Assert.Equal(expected[0].Vertices[i].Y, vertices[i].GetProperty("y").GetDouble());
        }
    }

    private static LogStore CreateTerminalStore()
    {
        var store = new LogStore();
        for (var i = 1; i <= 121; i++)
        {
            store.Add(LogLevel.Info, "runtime-contract", $"line{i} https://a.test/{i} https://b.test/{i}", DateTimeOffset.UnixEpoch);
        }

        return store;
    }
}
