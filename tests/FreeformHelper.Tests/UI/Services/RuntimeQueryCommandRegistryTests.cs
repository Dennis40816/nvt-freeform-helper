using System.Text.Json;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RuntimeQueryCommandRegistryTests
{
    private static readonly string[] _registeredCommandNames =
    [
        "help",
        "status",
        "selection",
        "terminal",
        "terminal-links",
        "pad",
        "notch",
        "multi-owner",
        "notch-stage",
        "notch-validation",
        "load-project",
        "run-step",
        "clear-step",
        "select-cad",
        "select-regular",
        "clear-selection",
        "set-tofull",
        "simulation",
        "export-notch",
    ];

    public static TheoryData<string> RegisteredCommandNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in _registeredCommandNames)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RegisteredCommandNames))]
    public async Task ExecuteAsync_RegisteredCommand_IsRoutedToAHandler(string command)
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new("1", command, null));

        Assert.NotEqual("UNKNOWN_COMMAND", response.Error?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_UnregisteredCommand_ReturnsUnknownCommand()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new("1", "no-such-command", null));

        Assert.False(response.Ok);
        Assert.Equal("UNKNOWN_COMMAND", response.Error?.Code);
    }

    [Fact]
    public async Task Help_ListsExactlyTheRegisteredCommands_InRegistrationFamilyOrder()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new("1", "help", null));

        var data = JsonSerializer.SerializeToElement(response.Data, RuntimeQueryProtocol.CompactJsonOptions);
        var listed = data.GetProperty("commands").EnumerateArray()
            .Select(static command => command.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(_registeredCommandNames, listed);
    }
}
