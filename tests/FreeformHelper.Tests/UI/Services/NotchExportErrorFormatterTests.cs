using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportErrorFormatterTests
{
    [Fact]
    public void Format_WhenCombineOverflowPresent_ReturnsFriendlyOverflowMessage()
    {
        var ex = new InvalidOperationException(
            "Outer export failure.",
            new InvalidOperationException("Combine ratio exceeds 255% at IC3/diff120 (CAD 4767): raw=301.00%."));

        var message = NotchExportErrorFormatter.Format("C", ex);

        Assert.Contains("C export failed: combine ratio overflow (>255%)", message, StringComparison.Ordinal);
        Assert.Contains("IC3/diff120", message, StringComparison.Ordinal);
        Assert.Contains("CAD 4767", message, StringComparison.Ordinal);
        Assert.Contains("Suggested action:", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_WhenGenericError_ReturnsMessageWithExportKindAndException()
    {
        var ex = new InvalidOperationException("Something else failed.");

        var message = NotchExportErrorFormatter.Format("CSV", ex);

        Assert.Equal("CSV export failed: Something else failed.", message);
    }

    [Fact]
    public void Format_WhenExporterOverflowPatternPresent_ReturnsNormalizedLocation()
    {
        var ex = new InvalidOperationException("v2.2 Combine ratio exceeds 255% at IC4 diff88: 301.80%.");

        var message = NotchExportErrorFormatter.Format("CSV", ex);

        Assert.Contains("CSV export failed: combine ratio overflow (>255%)", message, StringComparison.Ordinal);
        Assert.Contains("IC4/diff88", message, StringComparison.Ordinal);
        Assert.Contains("raw 301.80%", message, StringComparison.Ordinal);
    }
}
