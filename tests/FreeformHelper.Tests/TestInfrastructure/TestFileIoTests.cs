using System.Text;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

public sealed class TestFileIoTests
{
    [Fact]
    public async Task WriteContent_WithoutEncoding_WritesUtf8WithoutByteOrderMark_AndRoundTrips()
    {
        using var workspace = TestWorkspace.Create();
        var path = workspace.PathFor("plain.txt");

        TestFileIo.WriteContent(path, "a\nb");
        await TestFileIo.WriteContentAsync(workspace.PathFor("async.txt"), "é", TestContext.Current.CancellationToken);

        Assert.Equal([(byte)'a', (byte)'\n', (byte)'b'], TestFileIo.ReadBytes(path));
        Assert.Equal("a\nb", TestFileIo.ReadContent(path));
        Assert.Equal("é", await TestFileIo.ReadContentAsync(workspace.PathFor("async.txt"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void WriteContent_WithUtf8Encoding_WritesTheMark_AndReadContentDropsIt()
    {
        using var workspace = TestWorkspace.Create();
        var path = workspace.PathFor("bom.txt");

        TestFileIo.WriteContent(path, "x", Encoding.UTF8);

        Assert.Equal([0xEF, 0xBB, 0xBF, (byte)'x'], TestFileIo.ReadBytes(path));
        Assert.Equal("x", TestFileIo.ReadContent(path));
    }

    [Fact]
    public void CopyContent_RefusesToOverwriteUnlessAsked_AndLinesAreSplit()
    {
        using var workspace = TestWorkspace.Create();
        var source = workspace.PathFor("source.txt");
        var target = workspace.PathFor("target.txt");
        TestFileIo.WriteContent(source, "one\r\ntwo\nthree");

        TestFileIo.CopyContent(source, target, overwrite: false);

        Assert.Throws<IOException>(() => TestFileIo.CopyContent(source, target, overwrite: false));
        TestFileIo.CopyContent(source, target, overwrite: true);
        Assert.Equal(["one", "two", "three"], TestFileIo.ReadContentLines(target));
    }

    [Fact]
    public void Workspace_Dispose_DeletesTheFolder_AndNewFilePathIsUnique()
    {
        var workspace = TestWorkspace.Create();
        var directory = workspace.Directory;
        TestFileIo.WriteContent(workspace.PathFor("a.txt"), "a");

        workspace.Dispose();

        Assert.False(Directory.Exists(directory));
        Assert.NotEqual(TestWorkspace.NewFilePath("p", "json"), TestWorkspace.NewFilePath("p", ".json"));
        Assert.EndsWith(".json", TestWorkspace.NewFilePath("p", "json"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConditionWait_UntilAsync_ReturnsWhenTrue_AndThrowsTimeoutWhenNever()
    {
        var calls = 0;

        await ConditionWait.UntilAsync(() => ++calls >= 3, TimeSpan.FromSeconds(5), "three polls", TestContext.Current.CancellationToken);
        var timeout = await Assert.ThrowsAsync<TimeoutException>(() =>
            ConditionWait.UntilAsync(static () => false, TimeSpan.FromMilliseconds(60), "never", TestContext.Current.CancellationToken));

        Assert.Equal(3, calls);
        Assert.Contains("never", timeout.Message, StringComparison.Ordinal);
    }
}
