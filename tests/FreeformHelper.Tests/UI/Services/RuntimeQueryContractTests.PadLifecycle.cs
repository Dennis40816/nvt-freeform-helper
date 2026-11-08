// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryContractTests
{
    [AvaloniaFact]
    public async Task PadAfterRemovingRegularSourceReturnsCadGeometryWithoutNotchAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = await CreateMatchedShellAsync();
        var helper = shell.FreeformHelper;
        var cad = Assert.Single(helper.CadPads);
        helper.SelectedRegularSourceModeOption = Assert.Single(helper.RegularSourceModeOptions,
            option => option.Value == RegularSourceMode.FromDxfLayer);
        helper.SelectedRegularSourceLayerOption = Assert.Single(helper.RegularSourceLayerOptions,
            option => option.Name is null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(helper);
        Assert.Empty(helper.RegularPads);

        var root = await QueryAsync(new RuntimeQueryUseCase(shell), "pad",
            "cad-id=" + cad.Id.ToString(CultureInfo.InvariantCulture));

        Assert.Equal("cad", root.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("notchV22").ValueKind);
        var snapshot = root.GetProperty("snapshot").GetProperty("cad");
        Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("notch").ValueKind);
        Assert.Empty(root.GetProperty("matchedRegularIds").EnumerateArray());
        var pad = root.GetProperty("cadPad");
        Assert.Equal(cad.Id, pad.GetProperty("id").GetInt32());
        Assert.Equal(200d, pad.GetProperty("area").GetDouble());
        Assert.Equal(10d, pad.GetProperty("centroid").GetProperty("x").GetDouble());
        Assert.Equal(5d, pad.GetProperty("centroid").GetProperty("y").GetDouble());
        Assert.Equal(4, pad.GetProperty("vertices").GetInt32());
    }

    [AvaloniaFact]
    public async Task RegularPadQueryTracksSelectionAndClearedMatchingResultsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = await CreateMatchedShellAsync();
        var helper = shell.FreeformHelper;
        var cad = Assert.Single(helper.CadPads);
        var regular = helper.RegularPads[0];
        var arguments = "regular-id=" + regular.RegularPadId.ToString(CultureInfo.InvariantCulture);
        var useCase = new RuntimeQueryUseCase(shell);
        helper.ApplyCanvasSelection([], [regular.Index]);

        var matched = await QueryAsync(useCase, "pad", arguments);

        var pad = matched.GetProperty("regularPad");
        Assert.True(pad.GetProperty("isSelected").GetBoolean());
        Assert.True(regular.MatchScore > 0);
        Assert.Equal(regular.MatchScore, pad.GetProperty("matchScore").GetDouble());
        Assert.Equal(cad.Id, pad.GetProperty("matchedCadPadId").GetInt32());
        var details = matched.GetProperty("snapshot").GetProperty("regular").GetProperty("matchedCadDetails");
        var detail = Assert.Single(details.EnumerateArray());
        Assert.Equal(cad.Id, detail.GetProperty("cadPadId").GetInt32());
        Assert.Equal(0.5, detail.GetProperty("cadCoverage").GetDouble());
        Assert.Equal(1d, detail.GetProperty("regularCoverage").GetDouble());

        helper.ClearStep1ResultCommand.Execute(null);
        helper.ApplyCanvasSelection([], []);
        var cleared = await QueryAsync(useCase, "pad", arguments);

        var clearedPad = cleared.GetProperty("regularPad");
        Assert.False(clearedPad.GetProperty("isSelected").GetBoolean());
        Assert.Equal(0d, clearedPad.GetProperty("matchScore").GetDouble());
        Assert.Equal(JsonValueKind.Null, clearedPad.GetProperty("matchedCadPadId").ValueKind);
        Assert.Equal("None", clearedPad.GetProperty("freeform").GetString());
        Assert.Empty(cleared.GetProperty("matchedCadIds").EnumerateArray());
        Assert.Empty(cleared.GetProperty("snapshot").GetProperty("regular").GetProperty("matchedCadDetails").EnumerateArray());
    }
}
