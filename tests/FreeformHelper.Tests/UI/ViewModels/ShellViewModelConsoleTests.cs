using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using NLog;
using NLog.Config;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShellViewModelConsoleTests
{
    [AvaloniaFact]
    public async Task ConsoleSession_AfterLogWrite_ProjectionShowsTheMessageOnce()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var previousConfiguration = LogManager.Configuration;
        try
        {
            LogManager.Configuration = new LoggingConfiguration();
            using var shell = new ShellViewModel();
            var controller = shell.ConsoleSession.Controller;
            var sourceId = $"shell-console-{Guid.NewGuid():N}";

            ConsoleLogTestSupport.Write(sourceId, "one write");
            await ConsoleLogTestSupport.WaitForProjectionAsync(
                controller,
                projection => ConsoleLogTestSupport.RowTexts(projection, sourceId).Count > 0);

            Assert.Equal(["one write"], ConsoleLogTestSupport.RowTexts(controller.Projection, sourceId));
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
        }
    }

    [AvaloniaFact]
    public async Task Dispose_StopsRefreshingTheProjection()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var previousConfiguration = LogManager.Configuration;
        try
        {
            LogManager.Configuration = new LoggingConfiguration();
            var shell = new ShellViewModel();
            var controller = shell.ConsoleSession.Controller;
            var sourceId = $"shell-dispose-{Guid.NewGuid():N}";
            ConsoleLogTestSupport.Write(sourceId, "before dispose");
            await ConsoleLogTestSupport.WaitForProjectionAsync(
                controller,
                projection => ConsoleLogTestSupport.RowTexts(projection, sourceId).Count > 0);
            var notifications = 0;
            controller.PropertyChanged += (_, _) => notifications++;

            shell.Dispose();
            shell.Dispose();
            ConsoleLogTestSupport.Write(sourceId, "after dispose");
            using (var snapshot = await ConsoleLogTestSupport.CaptureAsync())
            {
                Assert.Contains(snapshot.Entries, entry => entry.SourceId == sourceId && entry.TextContent.Length == "after dispose".Length);
            }

            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, notifications);
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
        }
    }
}
