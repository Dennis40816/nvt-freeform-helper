// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

[Collection("HeadlessUiSerial")]
public sealed class BindingLogCaptureTests
{
    [AvaloniaFact]
    public async Task ActivatedMissingBindingProducesADiagnosticAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var capture = new AvaloniaBindingLogCapture();
        var target = new TextBlock { DataContext = new LogSource() };
        var window = new Window { Content = target };
        try
        {
            target.Bind(TextBlock.TextProperty, new Binding("DeliberatelyMissing"));
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Contains(capture.Errors, static entry => entry.Area == "Binding");
            Assert.Same(window, TopLevel.GetTopLevel(target));
        }
        finally
        {
            window.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    [AvaloniaFact]
    public void BothLogOverloadsCaptureWarningsAndRestoreThePreviousSink()
    {
        var previous = Logger.Sink;
        IReadOnlyList<BindingLogEvent> events;
        using (var capture = new AvaloniaBindingLogCapture())
        {
            capture.Phase = "self-test";
            capture.Log(LogEventLevel.Warning, "Binding", null, "Without arguments");
            capture.Log(LogEventLevel.Error, "Binding", null, "Target {Property}, value {Value}", TextBlock.TextProperty, "private-value");
            events = capture.Errors;
        }

        Assert.Same(previous, Logger.Sink);
        Assert.Collection(events,
            first => Assert.Equal("Without arguments", first.Template),
            second => Assert.Equal(nameof(TextBlock.Text), second.Target));
        Assert.All(events, static entry => Assert.Equal("self-test", entry.Phase));
        Assert.DoesNotContain(events, static entry => entry.Template.Contains("private-value", StringComparison.Ordinal));
    }

    public sealed class LogSource
    {
        public string Name => "available";
    }
}
