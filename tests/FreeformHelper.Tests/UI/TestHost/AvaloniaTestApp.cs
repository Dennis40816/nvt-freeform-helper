using Avalonia;
using Avalonia.Headless;
using FreeformHelper.UI;
using FreeformHelper.UI.Services;

[assembly: AvaloniaTestApplication(typeof(FreeformHelper.Tests.AvaloniaTestApp))]

namespace FreeformHelper.Tests;

public static class AvaloniaTestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            })
            .WithAppFonts()
            .AfterPlatformServicesSetup(static _ => HeadlessDispatcherSetup.EnsureRunLoopDispatcher())
            .AfterSetup(static builder =>
            {
                if (builder.Instance is App app && app.Styles.Count == 0)
                {
                    app.Initialize();
                }
            })
            .LogToTrace();
    }
}
