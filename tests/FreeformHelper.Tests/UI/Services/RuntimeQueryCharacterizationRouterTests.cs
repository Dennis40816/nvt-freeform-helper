using Xunit;

namespace FreeformHelper.Tests;

public sealed class RuntimeQueryCharacterizationRouterTests
{
    [Theory]
    [InlineData("known")]
    [InlineData(" KNOWN ")]
    [InlineData("KnOwN")]
    [InlineData("\tknown\r\n")]
    public async Task KnownName_TrimsAndIgnoresLetterCase(string command)
    {
        var calls = 0;
        var response = await RuntimeQueryCharacterizationSubject.RouteAsync(command, null, "known", _ => calls++);

        Assert.True(response.Ok);
        Assert.Null(response.ErrorCode);
        Assert.Null(response.ErrorMessage);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(null, "Unknown query command ''.")]
    [InlineData("", "Unknown query command ''.")]
    [InlineData("  ", "Unknown query command '  '.")]
    [InlineData("\t", "Unknown query command '\t'.")]
    [InlineData(" MISSING ", "Unknown query command ' MISSING '.")]
    public async Task UnknownName_ReturnsExactMessageWithoutCallingHandler(string? command, string expectedMessage)
    {
        var calls = 0;
        var response = await RuntimeQueryCharacterizationSubject.RouteAsync(command, null, "known", _ => calls++);

        Assert.False(response.Ok);
        Assert.Equal("UNKNOWN_COMMAND", response.ErrorCode);
        Assert.Equal(expectedMessage, response.ErrorMessage);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Handler_ReceivesOriginalArgumentDictionaryUnchanged()
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" Key "] = " value ",
            ["KEY"] = "",
            ["flag"] = "YES"
        };
        IReadOnlyDictionary<string, string>? received = null;

        var response = await RuntimeQueryCharacterizationSubject.RouteAsync("known", args, "known", value => received = value);

        Assert.True(response.Ok);
        Assert.Same(args, received);
        Assert.Equal(3, args.Count);
        Assert.Equal(" value ", args[" Key "]);
        Assert.Equal("", args["KEY"]);
        Assert.Equal("YES", args["flag"]);
    }

    [Fact]
    public async Task Handler_ReceivesNullArgumentsUnchanged()
    {
        var calls = 0;
        await RuntimeQueryCharacterizationSubject.RouteAsync("known", null, "known", args =>
        {
            Assert.Null(args);
            calls++;
        });

        Assert.Equal(1, calls);
    }
}
