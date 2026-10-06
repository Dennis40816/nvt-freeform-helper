using Xunit;

namespace FreeformHelper.Tests;

[Collection("RuntimeQueryIpcHost")]
public sealed class RuntimeQueryCharacterizationRequestTests
{
    [Fact]
    public async Task NullRequest_ReturnsExactInvalidRequest()
    {
        var response = await RuntimeQueryCharacterizationSubject.ExecuteAsync(null);

        Assert.False(response.Ok);
        Assert.Equal("INVALID_REQUEST", response.ErrorCode);
        Assert.Equal("Request is null.", response.ErrorMessage);
    }

    [Theory]
    [InlineData("2", "help", "Unsupported request version '2'. Expected '1'.")]
    [InlineData("2", "missing", "Unsupported request version '2'. Expected '1'.")]
    [InlineData("", "missing", "Unsupported request version ''. Expected '1'.")]
    [InlineData(" 1 ", "missing", "Unsupported request version ' 1 '. Expected '1'.")]
    [InlineData("01", "missing", "Unsupported request version '01'. Expected '1'.")]
    public async Task WrongVersion_IsCheckedBeforeKnownOrUnknownCommand(string version, string command, string message)
    {
        var response = await RuntimeQueryCharacterizationSubject.ExecuteAsync(new(version, command));

        Assert.False(response.Ok);
        Assert.Equal("UNSUPPORTED_VERSION", response.ErrorCode);
        Assert.Equal(message, response.ErrorMessage);
    }

    [Fact]
    public async Task SupportedVersion_ReachesRouter()
    {
        var response = await RuntimeQueryCharacterizationSubject.ExecuteAsync(new("1", " MISSING "));

        Assert.False(response.Ok);
        Assert.Equal("UNKNOWN_COMMAND", response.ErrorCode);
        Assert.Equal("Unknown query command ' MISSING '.", response.ErrorMessage);
    }
}
