using System.Xml.Linq;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LoggingConfigurationTests
{
    [Fact]
    public void NLogConfig_FileTarget_UsesUnifiedLayoutContract()
    {
        var doc = LoadNLogConfig();
        var ns = doc.Root!.Name.Namespace;

        var fileTarget = doc
            .Descendants(ns + "target")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("name"), "logfile", StringComparison.Ordinal));

        Assert.NotNull(fileTarget);
        var layout = (string?)fileTarget!.Attribute("layout");
        Assert.False(string.IsNullOrWhiteSpace(layout));
        Assert.Contains("${longdate} | ${level:uppercase=true} | ${logger} | ${message}", layout, StringComparison.Ordinal);
        Assert.Contains("${onexception:${newline}${exception:format=ToString}}", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void NLogConfig_DefaultRule_WritesInfoPlusToFileAndConsole()
    {
        var doc = LoadNLogConfig();
        var ns = doc.Root!.Name.Namespace;

        var rule = doc
            .Descendants(ns + "logger")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("name"), "*", StringComparison.Ordinal));

        Assert.NotNull(rule);
        Assert.Equal("Info", (string?)rule!.Attribute("minlevel"));
        Assert.Equal("logfile,appconsole", (string?)rule.Attribute("writeTo"));
    }

    [Fact]
    public void LoggingPolicy_Doc_CoversLevelAndFormatContract()
    {
        var path = Path.Combine(TestPaths.RepoRoot, "docs", "logging-policy.md");
        Assert.True(File.Exists(path), $"logging policy not found: {path}");

        var content = File.ReadAllText(path);
        Assert.Contains("`Debug`", content, StringComparison.Ordinal);
        Assert.Contains("`Info`", content, StringComparison.Ordinal);
        Assert.Contains("`Warn`", content, StringComparison.Ordinal);
        Assert.Contains("`Error`", content, StringComparison.Ordinal);
        Assert.Contains("Format Contract", content, StringComparison.Ordinal);
    }

    private static XDocument LoadNLogConfig()
    {
        var path = Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI", "NLog.config");
        Assert.True(File.Exists(path), $"NLog.config not found: {path}");
        return XDocument.Load(path);
    }

}
