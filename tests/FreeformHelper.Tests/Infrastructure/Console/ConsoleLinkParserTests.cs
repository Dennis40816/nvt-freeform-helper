using FreeformHelper.UI.Logging;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ConsoleLinkParserTests
{
    [Fact]
    public void Parse_DirectoryPath_IsDetected()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FreeformHelper_LinkDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var text = $"00:00:00 Info Logging initialized. dir={tempDir}";
            var links = ConsoleLinkParser.Parse(text);

            var link = Assert.Single(links);
            Assert.False(link.IsUrl);
            Assert.Equal(tempDir, link.Target);
            Assert.Equal(text.IndexOf(tempDir, StringComparison.Ordinal), link.StartOffset);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Parse_FilePathWithLineColumn_IsDetected()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FreeformHelper_LinkFile_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "Sample.cs");
        File.WriteAllText(filePath, "// sample");
        try
        {
            var text = $"{filePath}(12,34): error CS0000";
            var links = ConsoleLinkParser.Parse(text);

            var link = Assert.Single(links);
            Assert.False(link.IsUrl);
            Assert.Equal(filePath, link.Target);
            Assert.Equal(12, link.Line);
            Assert.Equal(34, link.Column);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Parse_HttpsUrl_IsDetected()
    {
        const string url = "https://example.com/path?q=1";
        var text = $"See {url} for details";
        var links = ConsoleLinkParser.Parse(text);

        var link = Assert.Single(links);
        Assert.True(link.IsUrl);
        Assert.Equal(url, link.Target);
    }

    [Fact]
    public void Parse_RelativeFileNameWithExtension_IsDetected()
    {
        const string fileName = "FreeformHelper.UI.csproj";
        var text = $"Build failed: {fileName}.";

        var links = ConsoleLinkParser.Parse(text);

        var link = Assert.Single(links);
        Assert.False(link.IsUrl);
        Assert.Equal(fileName, link.Target);
        Assert.Equal(text.IndexOf(fileName, StringComparison.Ordinal), link.StartOffset);
    }

    [Fact]
    public void Parse_UrlAndPath_CanCoexist()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FreeformHelper_LinkMixed_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "mixed.log");
        File.WriteAllText(filePath, "ok");
        try
        {
            const string url = "https://example.com/docs";
            var text = $"Open {url} then check {filePath}";

            var links = ConsoleLinkParser.Parse(text);

            Assert.Equal(2, links.Count);
            Assert.Contains(links, link => link.IsUrl && string.Equals(link.Target, url, StringComparison.Ordinal));
            Assert.Contains(links, link => !link.IsUrl && string.Equals(link.Target, filePath, StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void FindAtOffset_FindsLink_WhenCaretAtEnd()
    {
        const string url = "https://example.com";
        var text = $"Ref: {url}";
        var links = ConsoleLinkParser.Parse(text);
        var urlStart = text.IndexOf(url, StringComparison.Ordinal);

        var link = ConsoleLinkParser.FindAtOffset(links, urlStart + url.Length);
        Assert.NotNull(link);
        Assert.Equal(url, link!.Target);
    }
}

