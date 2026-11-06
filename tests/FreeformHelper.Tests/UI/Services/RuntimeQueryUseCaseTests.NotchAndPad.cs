using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_NotchPadAndInspector_SameResolvedDisplayWithRoundedZeroTarget()
    {
        const double cadArea = 4570.0;
        const double cadHeight = 5.4;
        const double emittedArea = 18.09;
        const double roundedZeroArea = 18.0; // Q7-positive CAD allocation with rounded-zero regular coverage.
        var anchorMaxX = (cadArea - (2 * emittedArea) - roundedZeroArea) / cadHeight;
        var emittedMaxX = anchorMaxX + (emittedArea / cadHeight);
        var secondEmittedMaxX = emittedMaxX + (emittedArea / cadHeight);
        var cadMaxX = secondEmittedMaxX + (roundedZeroArea / cadHeight);
        var gridMaxX = secondEmittedMaxX + 800.0;
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        var cad = CreateCadPad(274, 0, 0, cadMaxX, cadHeight);
        var regulars = new[]
        {
            CreateRegularPad(4808, 0, 0, 0, 0, anchorMaxX, 10, 0, 83),
            CreateRegularPad(4809, 0, 1, anchorMaxX, 0, emittedMaxX, 10, 0, 84),
            CreateRegularPad(4810, 0, 2, emittedMaxX, 0, secondEmittedMaxX, 10, 0, 85),
            CreateRegularPad(4811, 0, 3, secondEmittedMaxX, 0, gridMaxX, 10, 0, 86),
        };
        foreach (var regular in regulars)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Id;
            regular.MatchScore = 1.0;
            helper.RegularPads.Add(regular);
        }

        helper.CadPads.Add(cad);
        helper.EnableToRegular = true;
        helper.EnableToFull = true;
        helper.ToFullStrictOverlapPercent = 0.1m;
        helper.EnableBoundaryVirtualAreaCap = true;
        FreeformHelperViewModelPrivateAccess.Cad(helper) = new CadPadSet([cad]);
        var grid = new RegularGrid(
            rows: 1,
            cols: 4,
            xEdges: [0, anchorMaxX, emittedMaxX, secondEmittedMaxX, gridMaxX],
            yEdges: [0, 10],
            pads: regulars);
        FreeformHelperViewModelPrivateAccess.Grid(helper) = grid;
        helper.CadIndexMaps.OutputFwDiffIndexByCadId[cad.Id] = 83;
        helper.CadIndexMaps.IcIndexByCadId[cad.Id] = 0;
        helper.InvalidateWorkflowDataSnapshot();

        Assert.Equal(1, Assert.Single(NotchAllocationService.BuildAllocations(cad, grid),
            allocation => allocation.Pad.DiffIndex == 86).Q7);
        var resolved = Assert.IsType<NotchV22ResolvedResult>(helper.GetCadV22ResolvedResult(cad.Id));
        var snapshot = Assert.IsType<PadInspectorSnapshot>(helper.BuildCadPadInspectorSnapshot(cad.Id));
        var inspector = Assert.IsType<PadInspectorNotchSnapshot>(snapshot.Cad?.Notch);
        var padInfo = new CadPadInfoViewModel(
            pads: [cad],
            getDxfIndex: _ => 83,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null,
            inspectorSnapshot: snapshot);
        var useCase = new RuntimeQueryUseCase(shell);
        var args = new Dictionary<string, string> { ["cad-id"] = cad.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        var notchResponse = await useCase.ExecuteAsync(new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "notch", args));
        var padResponse = await useCase.ExecuteAsync(new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "pad", args));
        Assert.True(notchResponse.Ok, notchResponse.Error?.Message);
        Assert.True(padResponse.Ok, padResponse.Error?.Message);
        var notch = SerializeToRootElement(notchResponse.Data);
        var pad = SerializeToRootElement(padResponse.Data).GetProperty("snapshot").GetProperty("cad").GetProperty("notch");
        var notchAllocation = notch.GetProperty("stage3Allocation");
        var notchDisplay = notch.GetProperty("ratios").GetProperty("display");
        var padDisplay = pad.GetProperty("display");
        var expected = NotchDisplayProjector.Build(inspector);

        Assert.Same(resolved, helper.GetCadV22ResolvedResult(cad.Id));
        Assert.Same(resolved.TargetAllocation.TargetCoverageProjection, inspector.TargetCoverageProjection);
        Assert.Equal($"r{helper.GetNotchStep3Revision()}:cad{cad.Id}", notch.GetProperty("cache").GetProperty("cacheKey").GetString());
        Assert.Equal(resolved.Compensation.Stage3Area, inspector.Stage3Area);
        Assert.Equal(inspector.Stage3Area, notch.GetProperty("ratios").GetProperty("stage3Area").GetDouble());
        Assert.Equal(inspector.Stage3Area, pad.GetProperty("stage3Area").GetDouble());
        Assert.Equal(resolved.TargetAllocation.Stage3Area, notchAllocation.GetProperty("stage3Area").GetDouble());
        Assert.True(resolved.Compensation.IsToFullEnabled);
        Assert.Equal(resolved.Compensation.IsToFullEnabled, inspector.IsToFullEnabled);
        Assert.Equal(inspector.IsToFullEnabled, notch.GetProperty("summary").GetProperty("isToFullEnabled").GetBoolean());
        Assert.Equal(inspector.IsToFullEnabled, pad.GetProperty("isToFullEnabled").GetBoolean());
        Assert.Equal(expected.Stage3AreaText, padInfo.Stage3AreaText);
        Assert.Equal(expected.Stage3AreaText, notchDisplay.GetProperty("stage3AreaText").GetString());
        Assert.Equal(expected.Stage3AreaText, padDisplay.GetProperty("stage3AreaText").GetString());
        Assert.Equal(expected.ToFullRatioText, padInfo.ToFullRatioText);
        Assert.Equal(expected.ToFullRatioText, notchDisplay.GetProperty("toFullRatioText").GetString());
        Assert.Equal(expected.ToFullRatioText, padDisplay.GetProperty("toFullRatioText").GetString());

        var coverage = resolved.TargetAllocation.TargetCoverageProjection;
        Assert.Equal([84, 85], coverage.EmittedTargets.Select(static target => target.DiffIndex));
        Assert.Equal(0, Assert.Single(inspector.Targets, target => target.DiffIndex == 86).RatioPercentRounded);
        Assert.Equal(
            resolved.TargetAllocation.Targets.Select(static target => target.DiffIndex),
            inspector.Targets.Select(static target => target.DiffIndex));
        Assert.Equal(
            inspector.Targets.Select(static target => target.DiffIndex),
            notchAllocation.GetProperty("targets").EnumerateArray().Select(static target => target.GetProperty("diffIndex").GetInt32()));
        Assert.Equal(
            inspector.Targets.Select(static target => target.DiffIndex),
            pad.GetProperty("targets").EnumerateArray().Select(static target => target.GetProperty("diffIndex").GetInt32()));
        Assert.Equal(expected.TargetAllocationSummaryText, padInfo.TargetAllocationSummaryText);
        Assert.Equal(expected.TargetAllocationSummaryText, notchAllocation.GetProperty("display").GetProperty("targetAllocationSummaryText").GetString());
        Assert.Equal(expected.TargetAllocationSummaryText, padDisplay.GetProperty("targetAllocationSummaryText").GetString());
        Assert.Equal(expected.TargetAllocationLines, padInfo.TargetAllocationLines);
        Assert.Equal(
            expected.TargetAllocationLines,
            notchAllocation.GetProperty("display").GetProperty("targetAllocationLines").EnumerateArray().Select(static line => line.GetString()));
        Assert.Equal(coverage.DisplayCombinedRatio, inspector.TargetCoverageProjection?.DisplayCombinedRatio);
        Assert.Equal(expected.CombinedValueText, padInfo.CombinedValueText);
        Assert.Equal(expected.CombinedRatioText, notchDisplay.GetProperty("combinedRatioText").GetString());
        Assert.Equal(expected.CombinedRatioText, padDisplay.GetProperty("combinedRatioText").GetString());
        Assert.Equal(["IC1/diff83", "IC1/diff84", "IC1/diff85", "IC1/diff86"], padInfo.TargetAllocationItems.Select(static item => item.DiffText));
        Assert.Equal("Below gate", padInfo.TargetAllocationItems[3].RoleText);
    }


    [Fact]
    public async Task ExecuteAsync_QuerySelectCad_ReturnsSelectionTimings()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var vm = shell.FreeformHelper;
        vm.CadPads.Add(CreateCadPad(id: 101, minX: 0, minY: 0, maxX: 10, maxY: 10));
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "select-cad",
            Args: new Dictionary<string, string>
            {
                ["cad-id"] = "101"
            }));

        Assert.True(response.Ok);
        Assert.Null(response.Error);

        var root = SerializeToRootElement(response.Data);
        var selection = root.GetProperty("selection");
        Assert.Equal(101, selection.GetProperty("selectedCadIds")[0].GetInt32());

        var timings = selection.GetProperty("timings");
        Assert.True(timings.GetProperty("summaryMs").GetInt64() >= 0);
        Assert.True(timings.GetProperty("inspectorMs").GetInt64() >= 0);
        Assert.True(timings.GetProperty("notchPreviewMs").GetInt64() >= 0);
        Assert.True(timings.GetProperty("totalMs").GetInt64() >= 0);
    }


    [Fact]
    public async Task ExecuteAsync_QueryNotchValidation_UsesTypedPayloadWhenAvailable()
    {
        using var shell = BuildShellWithNotchValidationTable(BuildTypedValidationTable());
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "notch-validation",
            Args: new Dictionary<string, string>
            {
                ["regular-id"] = "100"
            }));

        Assert.True(response.Ok);
        Assert.Null(response.Error);

        var root = SerializeToRootElement(response.Data);
        Assert.Equal("notch-validation", root.GetProperty("kind").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("directRowCount").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("incomingRowCount").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("outgoingRowCount").GetInt32());

        var directRows = root.GetProperty("rows").GetProperty("direct");
        Assert.Equal("typed", directRows[0].GetProperty("v22").GetProperty("source").GetString());

        var incomingRows = root.GetProperty("rows").GetProperty("incoming");
        Assert.Equal("typed", incomingRows[0].GetProperty("v22").GetProperty("source").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_QueryNotchValidation_RejectsInvalidFirmwareNullSentinel()
    {
        using var shell = BuildShellWithNotchValidationTable(BuildTypedValidationTable());
        var projectFile = new ProjectFile();
        projectFile.Settings.Notch.NullValue = ushort.MaxValue + 1;
        FreeformHelperViewModelPrivateAccess.ProjectFile(shell.FreeformHelper) = projectFile;
        var useCase = new RuntimeQueryUseCase(shell);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "notch-validation",
            Args: new Dictionary<string, string>
            {
                ["regular-id"] = "100"
            })));

        Assert.Equal("NullValue must be in [0,65535].", error.Message);
    }


    [Fact]
    public async Task ExecuteAsync_QueryNotchValidation_FallsBackToLegacyValuesPayload()
    {
        using var shell = BuildShellWithNotchValidationTable(BuildLegacyValidationTable());
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "notch-validation",
            Args: new Dictionary<string, string>
            {
                ["regular-id"] = "100"
            }));

        Assert.True(response.Ok);
        Assert.Null(response.Error);

        var root = SerializeToRootElement(response.Data);
        var directRows = root.GetProperty("rows").GetProperty("direct");
        Assert.Equal("legacy-values", directRows[0].GetProperty("v22").GetProperty("source").GetString());
        Assert.Equal(10, directRows[0].GetProperty("v22").GetProperty("anchorDiffIndex").GetInt32());
        Assert.Equal(120, directRows[0].GetProperty("v22").GetProperty("combinePercent").GetInt32());
    }


    [Fact]
    public async Task ExecuteAsync_QueryNotchValidation_ProducesStableRowPayloadShape()
    {
        using var shell = BuildShellWithNotchValidationTable(BuildTypedValidationTable());
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "notch-validation",
            Args: new Dictionary<string, string>
            {
                ["regular-id"] = "100"
            }));

        Assert.True(response.Ok);
        Assert.Null(response.Error);

        var root = SerializeToRootElement(response.Data);
        var row = root.GetProperty("rows").GetProperty("direct")[0];
        var v22 = row.GetProperty("v22");

        Assert.Equal("v2.2", row.GetProperty("version").GetString());
        Assert.Equal(0, row.GetProperty("icIndex").GetInt32());
        Assert.Equal(10, row.GetProperty("sourceDiffIndex").GetInt32());
        Assert.Equal(100, row.GetProperty("sourceRegularPadId").GetInt32());
        Assert.Equal(4200, row.GetProperty("cadPadId").GetInt32());
        Assert.NotEmpty(row.GetProperty("values").EnumerateArray());

        Assert.Equal("typed", v22.GetProperty("source").GetString());
        Assert.Equal(10, v22.GetProperty("anchorDiffIndex").GetInt32());
        Assert.Equal(120, v22.GetProperty("combinePercent").GetInt32());
        Assert.Equal(12, v22.GetProperty("targetDiffIndex1").GetInt32());
        Assert.Equal(40, v22.GetProperty("targetRatioPercent1").GetInt32());
        Assert.Equal(65535, v22.GetProperty("targetDiffIndex2").GetInt32());
        Assert.Equal(0, v22.GetProperty("targetRatioPercent2").GetInt32());
        Assert.Equal(0, v22.GetProperty("flags").GetInt32());

        var legs = v22.GetProperty("legs");
        Assert.True(legs.GetArrayLength() >= 2);
        var firstLeg = legs[0];
        Assert.True(firstLeg.TryGetProperty("slot", out _));
        Assert.True(firstLeg.TryGetProperty("targetDiffIndex", out _));
        Assert.True(firstLeg.TryGetProperty("ratioPercent", out _));
        Assert.True(firstLeg.TryGetProperty("isNone", out _));
    }


    [Fact]
    public async Task ExecuteAsync_QueryMultiOwner_OverrideReusesOneResolvedResultAndReturnsOverrideEvidence()
    {
        var (shell, cad) = BuildMultiOwnerQueryFixture();
        using var shellScope = shell;
        var useCase = new RuntimeQueryUseCase(shell);
        var before = await QueryStep3CompensationCacheMetricsAsync(useCase);

        var coldResponse = await ExecuteMultiOwnerAsync(useCase, cad.Id, "1.0");

        Assert.True(coldResponse.Ok, $"{coldResponse.Error?.Code}:{coldResponse.Error?.Message}");
        var coldRoot = SerializeToRootElement(coldResponse.Data);
        var threshold = coldRoot.GetProperty("threshold");
        Assert.Equal("override", threshold.GetProperty("source").GetString());
        Assert.Equal(1.0, threshold.GetProperty("overlapPercent").GetDouble(), 6);
        Assert.Equal(0.1, threshold.GetProperty("currentSettingPercent").GetDouble(), 6);
        Assert.Equal(0, coldRoot.GetProperty("summary").GetProperty("ownerCountGreaterThanOne").GetInt32());
        Assert.Empty(coldRoot.GetProperty("regulars").EnumerateArray());

        var afterCold = await QueryStep3CompensationCacheMetricsAsync(useCase);
        Assert.Equal(0, afterCold.HitCount - before.HitCount);
        Assert.Equal(1, afterCold.MissCount - before.MissCount);

        var repeatResponse = await ExecuteMultiOwnerAsync(useCase, cad.Id, "1.0");

        Assert.True(repeatResponse.Ok, $"{repeatResponse.Error?.Code}:{repeatResponse.Error?.Message}");
        var afterRepeat = await QueryStep3CompensationCacheMetricsAsync(useCase);
        Assert.Equal(1, afterRepeat.HitCount - afterCold.HitCount);
        Assert.Equal(0, afterRepeat.MissCount - afterCold.MissCount);

        var settingsResponse = await ExecuteMultiOwnerAsync(useCase, cad.Id);

        Assert.True(settingsResponse.Ok, $"{settingsResponse.Error?.Code}:{settingsResponse.Error?.Message}");
        var settingsRoot = SerializeToRootElement(settingsResponse.Data);
        Assert.Equal("settings", settingsRoot.GetProperty("threshold").GetProperty("source").GetString());
        Assert.Equal(1, settingsRoot.GetProperty("summary").GetProperty("ownerCountGreaterThanOne").GetInt32());
        var settingsRegular = Assert.Single(settingsRoot.GetProperty("regulars").EnumerateArray());
        Assert.Equal(2, settingsRegular.GetProperty("ownerCadPadCount").GetInt32());
        Assert.Equal(
            [31, 32],
            settingsRegular.GetProperty("ownerCadPadIds").EnumerateArray().Select(static id => id.GetInt32()).ToArray());
    }


    [Fact]
    public async Task ExecuteAsync_QueryMultiOwner_PreservesVisibilityAndNotReadyErrorsWithoutFallbackCompensation()
    {
        var (shell, cad) = BuildMultiOwnerQueryFixture();
        using var shellScope = shell;
        var vm = shell.FreeformHelper;
        vm.CadPads.Clear();
        var useCase = new RuntimeQueryUseCase(shell);
        var before = await QueryStep3CompensationCacheMetricsAsync(useCase);

        var response = await ExecuteMultiOwnerAsync(useCase, cad.Id);

        Assert.False(response.Ok);
        Assert.Equal("PAD_NOT_FOUND", response.Error?.Code);
        Assert.Equal("CAD pad 31 is not visible.", response.Error?.Message);
        Assert.Equal(before, await QueryStep3CompensationCacheMetricsAsync(useCase));

        vm.CadPads.Add(cad);
        FreeformHelperViewModelPrivateAccess.Grid(vm) = null;
        var notReadyResponse = await ExecuteMultiOwnerAsync(useCase, cad.Id);

        Assert.False(notReadyResponse.Ok);
        Assert.Equal("NOT_READY", notReadyResponse.Error?.Code);
        Assert.Equal(
            "Notch compensation is unavailable. Build grid and ensure CAD is visible.",
            notReadyResponse.Error?.Message);
        Assert.Equal(before, await QueryStep3CompensationCacheMetricsAsync(useCase));
    }


    [Fact]
    public async Task ExecuteAsync_QueryPad_IncludesSharedNotchDisplayProjection()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var vm = shell.FreeformHelper;
        var cad = CreateCadPad(id: 21, minX: 0, minY: 3, maxX: 3, maxY: 7);
        var blocker = CreateCadPad(id: 22, minX: 5, minY: 0, maxX: 10, maxY: 10);
        var regular = CreateRegularPad(regularPadId: 104, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, icIndex: 0, diffIndex: 10);
        vm.CadPads.Add(cad);
        vm.CadPads.Add(blocker);
        vm.RegularPads.Add(regular);
        vm.EnableToRegular = true;
        vm.EnableToFull = true;
        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(new[] { cad, blocker });
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: [0.0, 10.0],
            yEdges: [0.0, 10.0],
            pads: new[] { regular });

        var useCase = new RuntimeQueryUseCase(shell);
        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "pad",
            Args: new Dictionary<string, string>
            {
                ["cad-id"] = "21"
            }));

        Assert.True(response.Ok);
        Assert.Null(response.Error);

        var root = SerializeToRootElement(response.Data);
        var inspectorSnapshot = vm.BuildCadPadInspectorSnapshot(cad.Id);
        var inspectorCad = Assert.IsType<CadPadInspectorSnapshot>(inspectorSnapshot?.Cad);
        var inspectorNotch = Assert.IsType<PadInspectorNotchSnapshot>(inspectorCad.Notch);
        var expectedDisplay = NotchDisplayProjector.Build(inspectorNotch);
        var display = root.GetProperty("snapshot")
            .GetProperty("cad")
            .GetProperty("notch")
            .GetProperty("display");
        Assert.Equal(expectedDisplay.ToFullValueText, display.GetProperty("toFullValueText").GetString());
        Assert.Equal(expectedDisplay.ToFullReasonShortText, display.GetProperty("toFullReasonShortText").GetString());
        Assert.Equal(expectedDisplay.Stage3AreaText, display.GetProperty("stage3AreaText").GetString());
    }

    private static async Task<(long HitCount, long MissCount)> QueryStep3CompensationCacheMetricsAsync(
        RuntimeQueryUseCase useCase)
    {
        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "status",
            Args: null));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var metrics = SerializeToRootElement(response.Data)
            .GetProperty("cache")
            .GetProperty("step3Compensation");
        return (
            metrics.GetProperty("hitCount").GetInt64(),
            metrics.GetProperty("missCount").GetInt64());
    }

    private static Task<RuntimeQueryResponseEnvelope> ExecuteMultiOwnerAsync(
        RuntimeQueryUseCase useCase,
        int cadPadId,
        string? overlapPercent = null)
    {
        var args = new Dictionary<string, string>
        {
            ["cad-id"] = cadPadId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (overlapPercent is not null)
        {
            args["overlap-percent"] = overlapPercent;
        }

        return useCase.ExecuteAsync(new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "multi-owner", args));
    }

    private static (ShellViewModel Shell, CadPad TargetCad) BuildMultiOwnerQueryFixture()
    {
        var shell = new ShellViewModel { IsDevPageAvailable = true };
        var vm = shell.FreeformHelper;
        var target = CreateCadPad(id: 31, minX: 0, minY: 0, maxX: 5, maxY: 10);
        var minorOwner = CreateCadPad(id: 32, minX: 9.95, minY: 0, maxX: 10, maxY: 10);
        var regular = CreateRegularPad(
            regularPadId: 200,
            row: 0,
            col: 0,
            minX: 0,
            minY: 0,
            maxX: 10,
            maxY: 10,
            icIndex: 0,
            diffIndex: 10);
        vm.CadPads.Add(target);
        vm.CadPads.Add(minorOwner);
        vm.RegularPads.Add(regular);
        vm.EnableToRegular = true;
        vm.EnableToFull = true;
        vm.ToFullStrictOverlapPercent = 0.1m;
        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet([target, minorOwner]);
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: [0.0, 10.0],
            yEdges: [0.0, 10.0],
            pads: [regular]);
        return (shell, target);
    }

    [Fact]
    public async Task ExecuteAsync_NotchQuery_KeepsPayloadStructureAndAppliesLimits()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        var cad = BuildToFullNotchQueryFixture(helper);
        var resolved = Assert.IsType<NotchV22ResolvedResult>(helper.GetCadV22ResolvedResult(cad.Id));
        var compensation = resolved.Compensation;
        var allocation = resolved.TargetAllocation;
        Assert.True(compensation.RegularDebugInfos.Count >= 3);
        Assert.True(allocation.Targets.Count >= 3);
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            RuntimeQueryProtocol.Version,
            "notch",
            new Dictionary<string, string>
            {
                ["cad-id"] = cad.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["limit"] = "2",
                ["target-limit"] = "2",
                ["polygon-limit"] = "1"
            }));

        Assert.True(response.Ok, response.Error?.Message);
        var root = SerializeToRootElement(response.Data);
        Assert.Equal(ExpectedNotchQueryShape, DescribeJsonShape(root));
        Assert.Equal("notch-v22", root.GetProperty("kind").GetString());
        Assert.Equal(helper.GetNotchStep3Revision(), root.GetProperty("cache").GetProperty("step3Revision").GetInt32());
        Assert.Equal(cad.Id, root.GetProperty("cadPad").GetProperty("id").GetInt32());
        Assert.Equal(cad.Name, root.GetProperty("cadPad").GetProperty("name").GetString());
        Assert.Equal(cad.Area, root.GetProperty("cadPad").GetProperty("area").GetDouble());

        var ratios = root.GetProperty("ratios");
        Assert.Equal(compensation.ToRegularRatio, ratios.GetProperty("toRegularRatio").GetDouble());
        Assert.Equal(compensation.ToFullRatio, ratios.GetProperty("toFullRatio").GetDouble());
        Assert.Equal(compensation.CombinedRatio, ratios.GetProperty("combinedRatio").GetDouble());
        Assert.Equal(compensation.OverlapRegularCount, ratios.GetProperty("overlapRegularCount").GetInt32());

        var threshold = root.GetProperty("threshold");
        Assert.Equal(helper.GetNotchMultiOwnerStrictOverlapPercent(), threshold.GetProperty("strictOverlapPercent").GetDouble());
        Assert.Equal(
            threshold.GetProperty("strictOverlapPercent").GetDouble() / 100.0,
            threshold.GetProperty("strictOverlapRatio").GetDouble());
        Assert.Equal(helper.EnableToFullRuleEngine, threshold.GetProperty("ruleEngineEnabled").GetBoolean());
        Assert.Equal(helper.EnableToFullRuleTrace, threshold.GetProperty("ruleTraceEnabled").GetBoolean());

        var expectedRegulars = compensation.RegularDebugInfos
            .OrderByDescending(static info => info.OverlapArea)
            .ThenBy(static info => info.RegularPadId)
            .Take(2)
            .ToList();
        var regularsJson = root.GetProperty("regulars");
        Assert.Equal(
            expectedRegulars.Select(static info => info.RegularPadId),
            regularsJson.EnumerateArray().Select(static item => item.GetProperty("regularPadId").GetInt32()));
        Assert.Equal(
            expectedRegulars.Select(static info => info.ToFullRuleTrace.Count),
            regularsJson.EnumerateArray().Select(static item => item.GetProperty("ruleTrace").GetArrayLength()));
        var summary = root.GetProperty("summary");
        Assert.Equal(compensation.RegularDebugInfos.Count, summary.GetProperty("totalRegulars").GetInt32());
        Assert.Equal(2, summary.GetProperty("returnedRegulars").GetInt32());
        Assert.Equal(compensation.RegularDebugInfos.Count - 2, summary.GetProperty("truncatedRegulars").GetInt32());
        Assert.Equal(
            compensation.RegularDebugInfos.Count(static info => info.IsBoundaryRegular),
            summary.GetProperty("boundaryRegularCount").GetInt32());
        Assert.Equal(
            compensation.RegularDebugInfos.Count(static info => info.IsToFullApplied),
            summary.GetProperty("toFullAppliedCount").GetInt32());
        Assert.Equal(
            compensation.RegularDebugInfos.Count(static info => info.BlockerCandidateCount > 0),
            summary.GetProperty("blockerRegularCount").GetInt32());

        AssertNotchQueryAllocationAndPolygons(root, resolved);
    }

    private static void AssertNotchQueryAllocationAndPolygons(System.Text.Json.JsonElement root, NotchV22ResolvedResult resolved)
    {
        var allocation = resolved.TargetAllocation;
        var allocationJson = root.GetProperty("stage3Allocation");
        Assert.Equal(allocation.Targets.Count, allocationJson.GetProperty("targetCount").GetInt32());
        Assert.Equal(2, allocationJson.GetProperty("returnedTargets").GetInt32());
        Assert.Equal(allocation.Targets.Count - 2, allocationJson.GetProperty("truncatedTargets").GetInt32());
        Assert.Equal(
            allocation.Targets.Take(2).Select(static target => target.DiffIndex),
            allocationJson.GetProperty("targets").EnumerateArray().Select(static target => target.GetProperty("diffIndex").GetInt32()));

        var polygons = root.GetProperty("toFullPolygons");
        Assert.Equal(resolved.Stage1SeedPolygons.Count, polygons.GetProperty("seedCount").GetInt32());
        Assert.Equal(resolved.Stage3FinalOutlinePolygons.Count, polygons.GetProperty("finalCount").GetInt32());
        Assert.Equal(Math.Min(1, resolved.Stage1SeedPolygons.Count), polygons.GetProperty("returnedSeedBounds").GetInt32());
        Assert.Equal(Math.Min(1, resolved.Stage3FinalOutlinePolygons.Count), polygons.GetProperty("returnedFinalBounds").GetInt32());
        Assert.Equal(polygons.GetProperty("returnedSeedBounds").GetInt32(), polygons.GetProperty("seedBounds").GetArrayLength());
        Assert.Equal(polygons.GetProperty("returnedFinalBounds").GetInt32(), polygons.GetProperty("finalBounds").GetArrayLength());
    }

    private static string DescribeJsonShape(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                return "{" + string.Join(
                    ",",
                    element.EnumerateObject().Select(static property =>
                    {
                        var child = DescribeJsonShape(property.Value);
                        return child.Length == 0 ? property.Name : property.Name + ":" + child;
                    })) + "}";
            case System.Text.Json.JsonValueKind.Array:
                return element.GetArrayLength() == 0 ? "[]" : "[" + DescribeJsonShape(element[0]) + "]";
            default:
                return string.Empty;
        }
    }

    private static CadPad BuildToFullNotchQueryFixture(FreeformHelperViewModel helper)
    {
        const double cadHeight = 5.4;
        const double emittedArea = 18.09;
        const double roundedZeroArea = 18.0;
        var anchorMaxX = (4570.0 - (2 * emittedArea) - roundedZeroArea) / cadHeight;
        var emittedMaxX = anchorMaxX + (emittedArea / cadHeight);
        var secondEmittedMaxX = emittedMaxX + (emittedArea / cadHeight);
        var cadMaxX = secondEmittedMaxX + (roundedZeroArea / cadHeight);
        var gridMaxX = secondEmittedMaxX + 800.0;
        var cad = CreateCadPad(274, 0, 0, cadMaxX, cadHeight);
        var regulars = new[]
        {
            CreateRegularPad(4808, 0, 0, 0, 0, anchorMaxX, 10, 0, 83),
            CreateRegularPad(4809, 0, 1, anchorMaxX, 0, emittedMaxX, 10, 0, 84),
            CreateRegularPad(4810, 0, 2, emittedMaxX, 0, secondEmittedMaxX, 10, 0, 85),
            CreateRegularPad(4811, 0, 3, secondEmittedMaxX, 0, gridMaxX, 10, 0, 86),
        };
        foreach (var regular in regulars)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Id;
            regular.MatchScore = 1.0;
            helper.RegularPads.Add(regular);
        }

        helper.CadPads.Add(cad);
        helper.EnableToRegular = true;
        helper.EnableToFull = true;
        helper.ToFullStrictOverlapPercent = 0.1m;
        helper.EnableBoundaryVirtualAreaCap = true;
        helper.EnableToFullRuleEngine = true;
        helper.EnableToFullRuleTrace = true;
        FreeformHelperViewModelPrivateAccess.Cad(helper) = new CadPadSet([cad]);
        FreeformHelperViewModelPrivateAccess.Grid(helper) = new RegularGrid(
            rows: 1,
            cols: 4,
            xEdges: [0, anchorMaxX, emittedMaxX, secondEmittedMaxX, gridMaxX],
            yEdges: [0, 10],
            pads: regulars);
        helper.CadIndexMaps.OutputFwDiffIndexByCadId[cad.Id] = 83;
        helper.CadIndexMaps.IcIndexByCadId[cad.Id] = 0;
        helper.InvalidateWorkflowDataSnapshot();
        return cad;
    }

    private const string ExpectedNotchQueryShape =
        "{kind,cache:{step3Revision,cacheKey},cadPad:{id,name,layer,icIndex,diffIndex,diffSource,area}," +
        "ratios:{toRegularRatio,toFullRatio,combinedRatio,stage3Area,isToFullEnabled,overlapAreaTotal," +
        "overlappedRegularAreaTotal,overlapRegularCount,display:{toRegularRatioText,toFullRatioText," +
        "combinedRatioText,toRegularValueText,toFullValueText,combinedValueText,toFullReasonShortText," +
        "toFullReasonFullText,stage3AreaText}}," +
        "threshold:{strictOverlapPercent,strictOverlapRatio,ruleEngineEnabled,ruleTraceEnabled}," +
        "stage3Allocation:{cadArea,stage3Area,strictAreaThreshold,targetCount,returnedTargets,truncatedTargets," +
        "targets:[{icIndex,diffIndex,effectiveArea,ratio,ratioPercentRounded,passesStrictThreshold,isAnchorDiff," +
        "toFullAppliedRegularCount,regularCount,regularPadIds:[]}]," +
        "display:{targetAllocationSummaryText,targetAllocationLines:[],ownerSummaryText}}," +
        "summary:{totalRegulars,returnedRegulars,truncatedRegulars,boundaryRegularCount,toFullAppliedCount," +
        "blockerRegularCount,isToFullEnabled}," +
        "toFullPolygons:{seedCount,finalCount,returnedSeedBounds,returnedFinalBounds," +
        "seedBounds:[{minX,minY,maxX,maxY}],finalBounds:[{minX,minY,maxX,maxY}]}," +
        "regulars:[{regularPadId,regularIndex,icIndex,diffIndex,row,col,regularArea,overlapArea,sourceArea," +
        "blockedArea,reachableArea,sourceCellCount,blockedCellCount,reachableCellCount,totalCellCount," +
        "blockerCandidateCount,ownerCadPadCount,ownerCadPadIds:[],isBoundaryRegular,isToFullBoundaryCandidate," +
        "hasEffectiveExpansion,hasDirectionalBlocker,toFullRuleCode,ruleTrace:[{rule,passed,detail}]," +
        "isToFullApplied}],diagnosticsText}";

    [Fact]
    public void BuildCadPadInspectorSnapshot_FullAndFastPaths_KeepFieldContract()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        var cad = BuildToFullNotchQueryFixture(helper);

        var coldFast = Assert.IsType<CadPadInspectorSnapshot>(
            helper.BuildCadPadInspectorSnapshot(cad.Id, false, false, false, false)?.Cad);
        Assert.Null(coldFast.Notch);
        Assert.False(coldFast.IsNotchRowEligible);
        Assert.Equal("Notch rows: computing...", coldFast.NotchRowSummary);
        Assert.Equal("Matched regular pad(s): 4.", coldFast.MatchDetailsText);
        Assert.Empty(coldFast.MatchedRegularDetails);
        Assert.Equal("Computing", Assert.Single(coldFast.RuleTrace).Rule);

        var fullSnapshot = Assert.IsType<PadInspectorSnapshot>(helper.BuildCadPadInspectorSnapshot(cad.Id));
        Assert.Equal("cad", fullSnapshot.Kind);
        Assert.Null(fullSnapshot.Regular);
        var full = Assert.IsType<CadPadInspectorSnapshot>(fullSnapshot.Cad);
        var resolved = Assert.IsType<NotchV22ResolvedResult>(helper.GetCadV22ResolvedResult(cad.Id));
        Assert.Equal(cad.Id, full.CadPadId);
        Assert.Equal(cad.Name, full.Name);
        Assert.Equal(cad.Layer, full.Layer);
        Assert.Equal(cad.Area, full.Area);
        Assert.Equal(cad.Bounds, full.Bounds);
        Assert.Equal(cad.Centroid, full.Centroid);
        Assert.Equal(cad.Polygon.Vertices.Length, full.Vertices);
        Assert.Equal(83, full.DxfIndex);
        Assert.Equal("83", full.DxfIndexDisplayText);
        Assert.Null(full.CadOutputFwDiffOverride);
        Assert.False(full.IsDxfIndexAnchor);
        Assert.Equal("Auto (geometry seed)", full.CadOutputFwDiffAssignmentModeText);
        Assert.Equal(0, full.IcIndex);
        Assert.Equal("IC1/diff83 (+3)", full.MatchText);
        Assert.Equal([4808, 4809, 4810, 4811], full.MatchedRegularPadIds);
        Assert.Equal(4, full.MatchedRegularDetails.Count);
        Assert.StartsWith("Regular: IC1/diff83 (regId 4808)", full.MatchDetailsText, StringComparison.Ordinal);
        Assert.True(full.IsNotchRowEligible);
        Assert.Equal("Notch rows: 2 (v2.1, v2.2)", full.NotchRowSummary);
        Assert.Equal(
            ["Diff idx", "Match", "Notch export", "To Regular", "To Full"],
            full.RuleTrace.Select(static entry => entry.Rule));

        var notch = Assert.IsType<PadInspectorNotchSnapshot>(full.Notch);
        var compensation = resolved.Compensation;
        Assert.Equal(compensation.ToRegularRatio, notch.ToRegularRatio);
        Assert.Equal(compensation.ToFullRatio, notch.ToFullRatio);
        Assert.Equal(compensation.CombinedRatio, notch.CombinedRatio);
        Assert.Equal(compensation.IsToFullEnabled, notch.IsToFullEnabled);
        Assert.Equal(compensation.Stage3Area, notch.Stage3Area);
        Assert.Equal(resolved.TargetAllocation.StrictAreaThreshold, notch.StrictAreaThreshold);
        Assert.Equal(
            resolved.TargetAllocation.Targets.Select(static target => (target.IcIndex, target.DiffIndex, target.RegularCount)),
            notch.Targets.Select(static target => (target.IcIndex, target.DiffIndex, target.RegularCount)));
        Assert.False(string.IsNullOrWhiteSpace(notch.Diagnostics));
        Assert.Same(resolved.TargetAllocation.TargetCoverageProjection, notch.TargetCoverageProjection);
        Assert.Equal(helper.CurrentNotchComputationMode, notch.ComputationMode);

        var warmFast = Assert.IsType<CadPadInspectorSnapshot>(
            helper.BuildCadPadInspectorSnapshot(cad.Id, false, false, false, false)?.Cad);
        var warmNotch = Assert.IsType<PadInspectorNotchSnapshot>(warmFast.Notch);
        Assert.Equal(notch.CombinedRatio, warmNotch.CombinedRatio);
        Assert.Equal(notch.Targets.Count, warmNotch.Targets.Count);
        Assert.Null(warmNotch.Diagnostics);
        Assert.Equal("Notch rows: computing...", warmFast.NotchRowSummary);
        Assert.False(warmFast.IsNotchRowEligible);

        var precomputed = Assert.IsType<CadPadInspectorSnapshot>(
            helper.BuildCadPadInspectorSnapshot(cad.Id, true, false, false, false, compensation, "precomputed diagnostics")?.Cad);
        Assert.Equal("precomputed diagnostics", precomputed.Notch?.Diagnostics);
        Assert.Equal(notch.CombinedRatio, precomputed.Notch?.CombinedRatio);
    }
}
