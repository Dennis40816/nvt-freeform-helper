using System.Text.Json;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    private static ShellViewModel BuildShellWithSelectablePads()
    {
        var shell = new ShellViewModel { IsDevPageAvailable = true };
        var vm = shell.FreeformHelper;
        vm.CadPads.Add(CreateCadPad(id: 101, minX: 0, minY: 0, maxX: 10, maxY: 10));
        vm.CadPads.Add(CreateCadPad(id: 102, minX: 10, minY: 0, maxX: 20, maxY: 10));
        vm.RegularPads.Add(CreateRegularPad(regularPadId: 10, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, icIndex: 0, diffIndex: 10));
        vm.RegularPads.Add(CreateRegularPad(regularPadId: 11, row: 0, col: 1, minX: 10, minY: 0, maxX: 20, maxY: 10, icIndex: 0, diffIndex: 11));
        vm.RegularPads.Add(CreateRegularPad(regularPadId: 12, row: 0, col: 2, minX: 20, minY: 0, maxX: 30, maxY: 10, icIndex: 0, diffIndex: 12));
        return shell;
    }

    private static Task<RuntimeQueryResponseEnvelope> ExecuteQueryAsync(
        ShellViewModel shell,
        string command,
        params (string Key, string Value)[] args)
    {
        var useCase = new RuntimeQueryUseCase(shell);
        return useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: command,
            Args: args.ToDictionary(static arg => arg.Key, static arg => arg.Value)));
    }

    private static int[] ReadIntArray(JsonElement array)
    {
        return array.EnumerateArray().Select(static item => item.GetInt32()).ToArray();
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectRegular_ByRegularId_SelectsThePadAndReportsIdsAndIndices()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(shell, "select-regular", ("regular-id", "11"));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal([11], ReadIntArray(root.GetProperty("selectedRegularIndices")));
        Assert.Equal([11], ReadIntArray(root.GetProperty("selectedRegularPadIds")));
        Assert.Empty(ReadIntArray(root.GetProperty("missingRegularPadIds")));
        Assert.Empty(ReadIntArray(root.GetProperty("missingRegularIndices")));
        Assert.Equal([11], ReadIntArray(root.GetProperty("selection").GetProperty("selectedRegularIndices")));
        Assert.Equal([11], shell.FreeformHelper.SelectedRegularPadIndices.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectRegular_ByRegularIds_SortsDeduplicatesAndReportsMissingIds()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(shell, "select-regular", ("regular-ids", "12,10,12,99"));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal([10, 12], ReadIntArray(root.GetProperty("selectedRegularIndices")));
        Assert.Equal([10, 12], ReadIntArray(root.GetProperty("selectedRegularPadIds")));
        Assert.Equal([99], ReadIntArray(root.GetProperty("missingRegularPadIds")));
        Assert.Equal([10, 12], shell.FreeformHelper.SelectedRegularPadIndices.OrderBy(static index => index).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectRegular_ByRegularIndexAndIndices_MergesBothArguments()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(
            shell,
            "select-regular",
            ("regular-index", "10"),
            ("regular-indices", "12,500"));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal([10, 12], ReadIntArray(root.GetProperty("selectedRegularIndices")));
        Assert.Empty(ReadIntArray(root.GetProperty("missingRegularPadIds")));
        Assert.Equal([500], ReadIntArray(root.GetProperty("missingRegularIndices")));
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectRegular_WhenNoRequestedPadIsVisible_ReturnsPadNotFoundAndKeepsSelection()
    {
        using var shell = BuildShellWithSelectablePads();
        shell.FreeformHelper.ApplyCanvasSelection(Array.Empty<int>(), [10]);

        var response = await ExecuteQueryAsync(shell, "select-regular", ("regular-ids", "98,99"));

        Assert.False(response.Ok);
        Assert.Equal("PAD_NOT_FOUND", response.Error?.Code);
        Assert.Equal([10], shell.FreeformHelper.SelectedRegularPadIndices.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectRegular_WithoutSelectionArguments_ReturnsInvalidArguments()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(shell, "select-regular");

        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
        Assert.Contains("select-regular", response.Error?.Message, StringComparison.Ordinal);
        Assert.Empty(shell.FreeformHelper.SelectedRegularPadIndices);
    }

    [Theory]
    [InlineData("regular-id", "abc")]
    [InlineData("regular-ids", "10,x")]
    [InlineData("regular-index", "-1")]
    [InlineData("regular-indices", "1,x")]
    public async Task ExecuteAsync_QuerySelectRegular_WithMalformedArgument_ReturnsInvalidArgumentsAndKeepsSelection(
        string key,
        string value)
    {
        using var shell = BuildShellWithSelectablePads();
        shell.FreeformHelper.ApplyCanvasSelection(Array.Empty<int>(), [11]);

        var response = await ExecuteQueryAsync(shell, "select-regular", (key, value));

        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
        Assert.Equal([11], shell.FreeformHelper.SelectedRegularPadIndices.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectCad_ByCadIds_ReportsMissingIdsAndSelectsTheVisibleOnes()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(shell, "select-cad", ("cad-id", "102"), ("cad-ids", "101,102,777"));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal([101, 102], ReadIntArray(root.GetProperty("selectedCadIds")));
        Assert.Equal([777], ReadIntArray(root.GetProperty("missingCadIds")));
        Assert.Equal([101, 102], ReadIntArray(root.GetProperty("selection").GetProperty("selectedCadIds")));
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectCad_WhenNoRequestedPadIsVisible_ReturnsPadNotFoundWithTheMissingIds()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(shell, "select-cad", ("cad-ids", "888,777"));

        Assert.False(response.Ok);
        Assert.Equal("PAD_NOT_FOUND", response.Error?.Code);
        Assert.Equal("CAD pads not visible: 777, 888", response.Error?.Message);
        Assert.Empty(shell.FreeformHelper.SelectedCadPadIds);
    }

    [Fact]
    public async Task ExecuteAsync_QuerySelectCad_WithoutSelectionArguments_ReturnsInvalidArguments()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(shell, "select-cad");

        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
        Assert.Contains("--cad-id", response.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SelectRegularAfterSelectCad_ReplacesTheCadSelection()
    {
        using var shell = BuildShellWithSelectablePads();
        var cadResponse = await ExecuteQueryAsync(shell, "select-cad", ("cad-id", "101"));
        Assert.True(cadResponse.Ok);
        Assert.Equal([101], shell.FreeformHelper.SelectedCadPadIds.ToArray());

        var regularResponse = await ExecuteQueryAsync(shell, "select-regular", ("regular-id", "12"));

        Assert.True(regularResponse.Ok, $"{regularResponse.Error?.Code}:{regularResponse.Error?.Message}");
        var selection = SerializeToRootElement(regularResponse.Data).GetProperty("selection");
        Assert.Empty(ReadIntArray(selection.GetProperty("selectedCadIds")));
        Assert.Equal([12], ReadIntArray(selection.GetProperty("selectedRegularIndices")));
        Assert.Empty(shell.FreeformHelper.SelectedCadPadIds);
    }

    [Fact]
    public async Task ExecuteAsync_QueryClearSelection_RemovesCadAndRegularSelection()
    {
        using var shell = BuildShellWithSelectablePads();
        shell.FreeformHelper.ApplyCanvasSelection([101, 102], [10, 11]);
        Assert.NotEmpty(shell.FreeformHelper.SelectedCadPadIds);
        Assert.NotEmpty(shell.FreeformHelper.SelectedRegularPadIndices);

        var response = await ExecuteQueryAsync(shell, "clear-selection");

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var selection = SerializeToRootElement(response.Data).GetProperty("selection");
        Assert.Empty(ReadIntArray(selection.GetProperty("selectedCadIds")));
        Assert.Empty(ReadIntArray(selection.GetProperty("selectedRegularIndices")));
        Assert.Empty(ReadIntArray(selection.GetProperty("selectedRegularPadIds")));
        Assert.Empty(shell.FreeformHelper.SelectedCadPadIds);
        Assert.Empty(shell.FreeformHelper.SelectedRegularPadIndices);
    }

    [Fact]
    public async Task ExecuteAsync_QueryClearSelection_WithNothingSelected_StillSucceeds()
    {
        using var shell = BuildShellWithSelectablePads();

        var response = await ExecuteQueryAsync(shell, "clear-selection");

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        Assert.Empty(ReadIntArray(SerializeToRootElement(response.Data).GetProperty("selection").GetProperty("selectedCadIds")));
    }
}
