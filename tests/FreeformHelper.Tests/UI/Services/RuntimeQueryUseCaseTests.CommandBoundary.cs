
using System.Runtime.CompilerServices;
using System.Text.Json;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    private static readonly string[] _expectedTraceRules = ["switch.enableToFull"];
    private static readonly bool[] _expectedTracePassed = [false];
    private static readonly string[] _expectedTraceDetails = ["global To Full switch is OFF"];

    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationSucceeds_ReturnsNullAndRunsTheOperationOnce()
    {
        var runs = 0;

        var failure = await RuntimeQueryCommandOperation.RunAsync(
            () =>
            {
                runs++;
                return Task.CompletedTask;
            },
            "TEST_FAILED",
            static ex => ex.Message);

        Assert.Null(failure);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationFailsLater_ReturnsTheFailureWithCodeAndMessage()
    {
        var failure = await RuntimeQueryCommandOperation.RunAsync(
            () => Task.FromException(new IOException("disk gone")),
            "TEST_FAILED",
            static ex => $"Failed: {ex.Message}");

        Assert.NotNull(failure);
        Assert.False(failure!.Ok);
        Assert.Equal("TEST_FAILED", failure.Error!.Code);
        Assert.Equal("Failed: disk gone", failure.Error.Message);
    }

    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationThrowsBeforeReturningTask_ReturnsTheFailure()
    {
        var failure = await RuntimeQueryCommandOperation.RunAsync(
            static () => throw new InvalidOperationException("not ready"),
            "TEST_FAILED",
            static ex => ex.Message);

        Assert.NotNull(failure);
        Assert.Equal("TEST_FAILED", failure!.Error!.Code);
        Assert.Equal("not ready", failure.Error.Message);
    }

    [Fact]
    public async Task RunCommandOperationAsync_WhenOperationRunsOutOfMemory_PropagatesTheException()
    {
        var describeCalls = 0;
        var memory = CreateOutOfMemory();

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => RuntimeQueryCommandOperation.RunAsync(
            () => Task.FromException(memory),
            "TEST_FAILED",
            ex =>
            {
                describeCalls++;
                return ex.Message;
            }));

        Assert.Same(memory, thrown);
        Assert.Equal(0, describeCalls);
    }

    [Fact]
    public async Task ExecuteAsync_QuerySimulation_WhenWorkspaceBuildFails_ReportsBuildFailedWithTheMessage()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        shell.Simulation.RequestBuildWorkspaceAsync = static () => throw new InvalidOperationException("build exploded");

        var response = await new RuntimeQueryUseCase(shell).ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "simulation",
            Args: null));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal("BUILD_FAILED", response.Error!.Code);
        Assert.Equal("Simulation workspace build failed: build exploded", response.Error.Message);
        Assert.False(shell.Simulation.IsBuildingWorkspace);
    }

    [Fact]
    public async Task ExecuteAsync_QuerySimulation_WhenWorkspaceBuildRunsOutOfMemory_PropagatesTheException()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var memory = CreateOutOfMemory();
        shell.Simulation.RequestBuildWorkspaceAsync = () => Task.FromException(memory);
        var useCase = new RuntimeQueryUseCase(shell);

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "simulation",
            Args: null)));

        Assert.Same(memory, thrown);
        Assert.False(shell.Simulation.IsBuildingWorkspace);
    }

    // The runtime reserves this type (CA2201), so the instance is created without running a constructor.
    private static OutOfMemoryException CreateOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_QueryMultiOwner_DisabledToFullStillOrdersAndLimitsSharedRegularsAsync(bool enableTrace)
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = BuildOrderedMultiOwnerQueryShell(enableTrace);

        var response = await ExecuteQueryAsync(shell, "multi-owner", ("cad-id", "31"), ("limit", "3"));

        Assert.True(response.Ok, response.Error?.Message);
        var root = SerializeToRootElement(response.Data);
        Assert.Equal("notch-v22-multi-owner", root.GetProperty("kind").GetString());
        var threshold = root.GetProperty("threshold");
        Assert.Equal("settings", threshold.GetProperty("source").GetString());
        Assert.Equal(0.1, threshold.GetProperty("overlapPercent").GetDouble());
        Assert.Equal(0.001, threshold.GetProperty("overlapRatio").GetDouble());
        Assert.True(threshold.GetProperty("ruleEngineEnabled").GetBoolean());
        Assert.Equal(enableTrace, threshold.GetProperty("ruleTraceEnabled").GetBoolean());
        var summary = root.GetProperty("summary");
        Assert.Equal(4, summary.GetProperty("totalRegulars").GetInt32());
        Assert.Equal(0, summary.GetProperty("gateMultiOwnerCount").GetInt32());
        Assert.Equal(4, summary.GetProperty("ownerCountGreaterThanOne").GetInt32());
        Assert.Equal(3, summary.GetProperty("returnedRegulars").GetInt32());
        Assert.Equal(1, summary.GetProperty("truncatedRegulars").GetInt32());
        var rows = root.GetProperty("regulars");
        Assert.Equal([204, 202, 203], rows.EnumerateArray()
            .Select(static row => row.GetProperty("regularPadId").GetInt32()));
        Assert.Equal([3, 2, 2], rows.EnumerateArray()
            .Select(static row => row.GetProperty("ownerCadPadCount").GetInt32()));
        Assert.Equal([50d, 100d, 100d], rows.EnumerateArray()
            .Select(static row => row.GetProperty("overlapArea").GetDouble()));
        Assert.Equal([31, 32, 33], ReadIntArray(rows[0].GetProperty("ownerCadPadIds")));
        Assert.Equal([31, 32], ReadIntArray(rows[1].GetProperty("ownerCadPadIds")));
        Assert.Equal([31, 32], ReadIntArray(rows[2].GetProperty("ownerCadPadIds")));
        Assert.All(rows.EnumerateArray(), row =>
        {
            Assert.Equal("GATE_TOFULL_DISABLED", row.GetProperty("toFullRuleCode").GetString());
            Assert.False(row.GetProperty("isToFullApplied").GetBoolean());
            var trace = row.GetProperty("ruleTrace").EnumerateArray().ToArray();
            Assert.Equal(enableTrace ? _expectedTraceRules : [],
                trace.Select(static step => step.GetProperty("rule").GetString()));
            Assert.Equal(enableTrace ? _expectedTracePassed : [],
                trace.Select(static step => step.GetProperty("passed").GetBoolean()));
            Assert.Equal(enableTrace ? _expectedTraceDetails : [],
                trace.Select(static step => step.GetProperty("detail").GetString()));
        });
    }

    [Fact]
    public async Task ExecuteAsync_QueryMultiOwner_ExactOverlapThresholdExcludesOwnerWithoutChangingSettingsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        var target = CreateCadPad(31, 0, 0, 5, 10);
        var minorOwner = CreateCadPad(32, 9.5, 0, 10, 10);
        var regular = CreateRegularPad(200, 0, 0, 0, 0, 10, 10, 0, 10);
        helper.EnableToFull = false;
        helper.ToFullStrictOverlapPercent = 1m;
        helper.CadPads.Add(target);
        helper.CadPads.Add(minorOwner);
        helper.RegularPads.Add(regular);
        FreeformHelperViewModelPrivateAccess.Cad(helper) = new CadPadSet([target, minorOwner]);
        FreeformHelperViewModelPrivateAccess.Grid(helper) = new RegularGrid(1, 1, [0, 10], [0, 10], [regular]);
        var useCase = new RuntimeQueryUseCase(shell);

        var equalThreshold = await ExecuteMultiOwnerAsync(useCase, 31, "5");

        Assert.True(equalThreshold.Ok, equalThreshold.Error?.Message);
        var equalRoot = SerializeToRootElement(equalThreshold.Data);
        Assert.Empty(equalRoot.GetProperty("regulars").EnumerateArray());
        Assert.Equal(1, equalRoot.GetProperty("summary").GetProperty("totalRegulars").GetInt32());
        Assert.Equal(0, equalRoot.GetProperty("summary").GetProperty("ownerCountGreaterThanOne").GetInt32());
        Assert.Equal(0, equalRoot.GetProperty("summary").GetProperty("truncatedRegulars").GetInt32());
        var threshold = equalRoot.GetProperty("threshold");
        Assert.Equal("override", threshold.GetProperty("source").GetString());
        Assert.Equal(5d, threshold.GetProperty("overlapPercent").GetDouble());
        Assert.Equal(0.05, threshold.GetProperty("overlapRatio").GetDouble());
        Assert.Equal(1d, threshold.GetProperty("currentSettingPercent").GetDouble());

        var belowThreshold = await ExecuteMultiOwnerAsync(useCase, 31, "4");

        Assert.True(belowThreshold.Ok, belowThreshold.Error?.Message);
        var belowRoot = SerializeToRootElement(belowThreshold.Data);
        var shared = Assert.Single(belowRoot.GetProperty("regulars").EnumerateArray());
        Assert.Equal(200, shared.GetProperty("regularPadId").GetInt32());
        Assert.Equal(2, shared.GetProperty("ownerCadPadCount").GetInt32());
        Assert.Equal([31, 32], ReadIntArray(shared.GetProperty("ownerCadPadIds")));

        var settings = await ExecuteMultiOwnerAsync(useCase, 31);

        Assert.True(settings.Ok, settings.Error?.Message);
        var settingsRoot = SerializeToRootElement(settings.Data);
        Assert.Equal("settings", settingsRoot.GetProperty("threshold").GetProperty("source").GetString());
        Assert.Single(settingsRoot.GetProperty("regulars").EnumerateArray());
        Assert.Equal(1m, helper.ToFullStrictOverlapPercent);
    }

    [Fact]
    public async Task ExecuteAsync_QueryNotchStage_VisibleCadWithoutGridReturnsCompensationNotReadyAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        shell.FreeformHelper.CadPads.Add(CreateCadPad(31, 0, 0, 5, 10));

        var response = await ExecuteQueryAsync(shell, "notch-stage", ("cad-id", "31"));

        Assert.False(response.Ok);
        Assert.Equal("NOT_READY", response.Error?.Code);
        Assert.Equal("Notch compensation is unavailable. Build grid and ensure CAD is visible.", response.Error?.Message);
        Assert.Null(response.Data);
    }

    private static ShellViewModel BuildOrderedMultiOwnerQueryShell(bool enableTrace)
    {
        var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        helper.EnableToRegular = true;
        helper.EnableToFull = false;
        helper.EnableToFullRuleEngine = true;
        helper.EnableToFullRuleTrace = enableTrace;
        helper.ToFullStrictOverlapPercent = 0.1m;
        var target = CreateCadPad(31, 5, 0, 35, 10);
        var secondOwner = CreateCadPad(32, 9.5, 0, 40, 10);
        var thirdOwner = CreateCadPad(33, 2, 0, 3, 10);
        helper.CadPads.Add(thirdOwner);
        helper.CadPads.Add(secondOwner);
        helper.CadPads.Add(target);
        var regulars = new[]
        {
            CreateRegularPad(204, 0, 0, 0, 0, 10, 10, 0, 10),
            CreateRegularPad(203, 0, 1, 10, 0, 20, 10, 0, 11),
            CreateRegularPad(202, 0, 2, 20, 0, 30, 10, 0, 12),
            CreateRegularPad(201, 0, 3, 30, 0, 40, 10, 0, 13)
        };
        foreach (var regular in regulars)
        {
            helper.RegularPads.Add(regular);
        }

        FreeformHelperViewModelPrivateAccess.Cad(helper) = new CadPadSet([thirdOwner, secondOwner, target]);
        FreeformHelperViewModelPrivateAccess.Grid(helper) = new RegularGrid(
            1, 4, [0, 10, 20, 30, 40], [0, 10], regulars);
        return shell;
    }

    [Fact]
    public async Task ExecuteAsync_QueryRegularPad_MultiCadMatchKeepsPrimaryAndAllOverlapEvidenceAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = BuildMultiMatchPadQueryShell();

        var response = await ExecuteQueryAsync(shell, "pad", ("regular-id", "104"));

        Assert.True(response.Ok, response.Error?.Message);
        var root = SerializeToRootElement(response.Data);
        Assert.Equal("regular", root.GetProperty("kind").GetString());
        var pad = root.GetProperty("regularPad");
        Assert.Equal(31, pad.GetProperty("matchedCadPadId").GetInt32());
        Assert.Equal(0.6, pad.GetProperty("matchScore").GetDouble(), 6);
        Assert.Equal([31, 32], ReadIntArray(root.GetProperty("matchedCadIds")));
        var snapshot = root.GetProperty("snapshot");
        Assert.Equal("regular", snapshot.GetProperty("kind").GetString());
        var regular = snapshot.GetProperty("regular");
        Assert.Equal([31, 32], ReadIntArray(regular.GetProperty("matchedCadPadIds")));
        Assert.Equal("Strict", regular.GetProperty("diffSource").GetString());
        Assert.Equal(1d, regular.GetProperty("matchConfidence").GetDouble());
        var details = regular.GetProperty("matchedCadDetails");
        Assert.Equal(2, details.GetArrayLength());
        AssertMatchedCadQueryDetail(details[0], 31, 17, 0.6);
        AssertMatchedCadQueryDetail(details[1], 32, 29, 0.4);
        Assert.Contains(root.GetProperty("ruleTrace").EnumerateArray(), static step =>
            step.GetProperty("rule").GetString() == "Match" &&
            step.GetProperty("outcome").GetString() == "Matched CAD pad count: 2");
        Assert.Contains(snapshot.GetProperty("ruleTrace").EnumerateArray(), static step =>
            step.GetProperty("rule").GetString() == "Diff source" &&
            step.GetProperty("outcome").GetString() == "Strict");
    }

    [Fact]
    public async Task ExecuteAsync_QueryRegularPad_UnmatchedYWayPreservesRowGeometryAndMappingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = BuildNonzeroRowPadQueryShell();
        shell.FreeformHelper.ApplyCanvasSelection([], [5]);

        var response = await ExecuteQueryAsync(shell, "pad", ("regular-id", "5"));

        Assert.True(response.Ok, response.Error?.Message);
        var root = SerializeToRootElement(response.Data);
        var pad = root.GetProperty("regularPad");
        var regular = root.GetProperty("snapshot").GetProperty("regular");
        AssertNonzeroRowQueryGeometry(pad);
        AssertNonzeroRowQueryGeometry(regular);
        Assert.Equal(0, regular.GetProperty("displayRow").GetInt32());
        Assert.Equal(15d, regular.GetProperty("centroid").GetProperty("x").GetDouble());
        Assert.Equal(20d, regular.GetProperty("centroid").GetProperty("y").GetDouble());
        Assert.True(pad.GetProperty("isSelected").GetBoolean());
        Assert.Equal(0d, pad.GetProperty("matchScore").GetDouble());
        Assert.Equal(JsonValueKind.Null, pad.GetProperty("matchedCadPadId").ValueKind);
        Assert.Empty(root.GetProperty("matchedCadIds").EnumerateArray());
        Assert.Empty(regular.GetProperty("matchedCadPadIds").EnumerateArray());
        Assert.Empty(regular.GetProperty("matchedCadDetails").EnumerateArray());
        Assert.Equal("Unmatched", regular.GetProperty("matchText").GetString());
        Assert.Equal("Unmatched", regular.GetProperty("diffSource").GetString());
        Assert.Equal(0d, regular.GetProperty("matchConfidence").GetDouble());
        Assert.Contains(root.GetProperty("ruleTrace").EnumerateArray(), static step =>
            step.GetProperty("rule").GetString() == "Identity" &&
            step.GetProperty("outcome").GetString() == "IC3/diff42" &&
            step.GetProperty("detail").GetString() == "R2  C1  REG5");
    }

    [Theory]
    [InlineData("cad", "31", "CAD pad 31 is not visible.")]
    [InlineData("regular", "104", "Regular pad 104 is not visible.")]
    public async Task ExecuteAsync_QueryPad_RemovedPadDoesNotReturnPreviouslyQueriedSnapshotAsync(
        string kind, string id, string message)
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = BuildMultiMatchPadQueryShell();
        var useCase = new RuntimeQueryUseCase(shell);
        var request = new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "pad",
            new Dictionary<string, string> { [kind + "-id"] = id });
        var visible = await useCase.ExecuteAsync(request);
        Assert.True(visible.Ok, visible.Error?.Message);
        Assert.Equal(kind, SerializeToRootElement(visible.Data).GetProperty("kind").GetString());

        if (kind == "cad")
        {
            shell.FreeformHelper.CadPads.Clear();
        }
        else
        {
            shell.FreeformHelper.RegularPads.Clear();
        }

        var removed = await useCase.ExecuteAsync(request);

        Assert.False(removed.Ok);
        Assert.Equal("PAD_NOT_FOUND", removed.Error?.Code);
        Assert.Equal(message, removed.Error?.Message);
        Assert.Null(removed.Data);
    }

    private static ShellViewModel BuildMultiMatchPadQueryShell()
    {
        var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        var primary = CreateCadPad(31, 0, 0, 6, 10);
        var secondary = CreateCadPad(32, 6, 0, 10, 10);
        var regular = CreateRegularPad(104, 0, 0, 0, 0, 10, 10, 2, 17);
        var cad = new CadPadSet([secondary, primary]);
        var grid = new RegularGrid(1, 1, [0, 10], [0, 10], [regular]);
        helper.CadPads.Add(secondary);
        helper.CadPads.Add(primary);
        helper.RegularPads.Add(regular);
        FreeformHelperViewModelPrivateAccess.Cad(helper) = cad;
        FreeformHelperViewModelPrivateAccess.Grid(helper) = grid;
        helper.WorkflowDataForTests.LatestPadMatch = PadMatchService.Match(cad, grid);
        helper.CadIndexMaps.OutputFwDiffIndexByCadId[31] = 17;
        helper.CadIndexMaps.OutputFwDiffIndexByCadId[32] = 29;
        helper.CadIndexMaps.IcIndexByCadId[31] = 2;
        helper.CadIndexMaps.IcIndexByCadId[32] = 2;
        helper.InvalidateWorkflowDataSnapshot();
        return shell;
    }

    private static ShellViewModel BuildNonzeroRowPadQueryShell()
    {
        var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        double[] xEdges = [-10, 5, 25];
        double[] yEdges = [-20, -5, 10, 30];
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                var id = (row * 2) + col;
                helper.RegularPads.Add(CreateRegularPad(id, row, col,
                    xEdges[col], yEdges[row], xEdges[col + 1], yEdges[row + 1], 2, 37 + id));
            }
        }

        helper.RegularPads[5].Freeform = FreeformType.YWay;
        FreeformHelperViewModelPrivateAccess.Grid(helper) = new RegularGrid(
            3, 2, xEdges, yEdges, helper.RegularPads.ToArray());
        return shell;
    }

    private static void AssertMatchedCadQueryDetail(JsonElement detail, int cadId, int diffIndex, double regularCoverage)
    {
        Assert.Equal(cadId, detail.GetProperty("cadPadId").GetInt32());
        Assert.Equal(diffIndex, detail.GetProperty("dxfIndex").GetInt32());
        Assert.Equal(1d, detail.GetProperty("cadCoverage").GetDouble());
        Assert.Equal(regularCoverage, detail.GetProperty("regularCoverage").GetDouble(), 6);
    }

    private static void AssertNonzeroRowQueryGeometry(JsonElement pad)
    {
        Assert.Equal(5, pad.GetProperty("regularPadId").GetInt32());
        Assert.Equal(5, pad.GetProperty("index").GetInt32());
        Assert.Equal(2, pad.GetProperty("row").GetInt32());
        Assert.Equal(1, pad.GetProperty("col").GetInt32());
        Assert.Equal(2, pad.GetProperty("icIndex").GetInt32());
        Assert.Equal(42, pad.GetProperty("diffIndex").GetInt32());
        Assert.Equal(400d, pad.GetProperty("area").GetDouble());
        Assert.Equal("YWay", pad.GetProperty("freeform").GetString());
        var bounds = pad.GetProperty("bounds");
        Assert.Equal(5d, bounds.GetProperty("minX").GetDouble());
        Assert.Equal(10d, bounds.GetProperty("minY").GetDouble());
        Assert.Equal(25d, bounds.GetProperty("maxX").GetDouble());
        Assert.Equal(30d, bounds.GetProperty("maxY").GetDouble());
    }
}
