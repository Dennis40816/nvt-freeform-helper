using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ConsoleTargetResolverTests
{
    private const string TestAssemblyFile = "FreeformHelper.Tests.dll";

    [Fact]
    public void Resolve_ForARootedExistingFolder_ReturnsAFolder()
    {
        var folder = AppContext.BaseDirectory;

        var result = ConsoleTargetResolver.Resolve(folder);

        Assert.True(result.Exists);
        Assert.True(result.IsDirectory);
        Assert.Equal(Path.GetFullPath(folder), result.FullPath);
    }

    [Fact]
    public void Resolve_ForARootedExistingFile_ReturnsAFile()
    {
        var file = Path.Combine(AppContext.BaseDirectory, TestAssemblyFile);

        var result = ConsoleTargetResolver.Resolve(file);

        Assert.True(result.Exists);
        Assert.False(result.IsDirectory);
        Assert.Equal(Path.GetFullPath(file), result.FullPath);
    }

    [Fact]
    public void Resolve_ForAQuotedRootedFile_IgnoresTheQuotes()
    {
        var file = Path.Combine(AppContext.BaseDirectory, TestAssemblyFile);

        var result = ConsoleTargetResolver.Resolve("\"" + file + "\"");

        Assert.True(result.Exists);
    }

    [Fact]
    public void Resolve_ForARelativeFileUnderTheApplicationFolder_FindsIt()
    {
        var result = ConsoleTargetResolver.Resolve(TestAssemblyFile);

        Assert.True(result.Exists);
        Assert.False(result.IsDirectory);
    }

    [Fact]
    public void Resolve_ForAMissingRootedPath_ReturnsNothing()
    {
        var missing = Path.Combine(AppContext.BaseDirectory, $"missing-{Guid.NewGuid():N}.txt");

        var result = ConsoleTargetResolver.Resolve(missing);

        Assert.False(result.Exists);
        Assert.Null(result.FullPath);
    }

    [Theory]
    [InlineData(@"\\server\share\a.txt")]
    [InlineData("//server/share/a.txt")]
    [InlineData(@"\\?\C:\Windows\notepad.exe")]
    [InlineData(@"/\server\share\a.txt")]
    [InlineData(@"\/server/share/a.txt")]
    [InlineData(@"/\\server\share")]
    [InlineData(@"/\localhost\C$\Windows")]
    public void Resolve_ForANetworkOrDevicePath_ReturnsNothingWithoutProbing(string text)
    {
        Assert.False(ConsoleTargetResolver.Resolve(text).Exists);
    }

    [Theory]
    [InlineData("bad|name?.txt")]
    [InlineData("C:\\dir\\a<b>.txt")]
    public void Resolve_ForAnIllegalPath_ReturnsNothingInsteadOfThrowing(string text)
    {
        var exception = Record.Exception(() => ConsoleTargetResolver.Resolve(text));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ForBlankText_ReturnsNothing(string text)
    {
        Assert.False(ConsoleTargetResolver.Resolve(text).Exists);
    }
}
