using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{


    [Fact]
    public async Task ExecuteAsync_QuerySelectCad_ReturnsSelectionTimings()
    {
        using var shell = new ShellViewModel();
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
        SetPrivateField(shell.FreeformHelper, "_projectFile", projectFile);
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
        SetPrivateField(vm, "_grid", null);
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
        using var shell = new ShellViewModel();
        var vm = shell.FreeformHelper;
        var cad = CreateCadPad(id: 21, minX: 0, minY: 3, maxX: 3, maxY: 7);
        var blocker = CreateCadPad(id: 22, minX: 5, minY: 0, maxX: 10, maxY: 10);
        var regular = CreateRegularPad(regularPadId: 104, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, icIndex: 0, diffIndex: 10);
        vm.CadPads.Add(cad);
        vm.CadPads.Add(blocker);
        vm.RegularPads.Add(regular);
        vm.EnableToRegular = true;
        vm.EnableToFull = true;
        SetPrivateField(vm, "_cad", new CadPadSet(new[] { cad, blocker }));
        SetPrivateField(vm, "_grid", new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: [0.0, 10.0],
            yEdges: [0.0, 10.0],
            pads: new[] { regular }));

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
        var shell = new ShellViewModel();
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
        SetPrivateField(vm, "_cad", new CadPadSet([target, minorOwner]));
        SetPrivateField(vm, "_grid", new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: [0.0, 10.0],
            yEdges: [0.0, 10.0],
            pads: [regular]));
        return (shell, target);
    }
}
