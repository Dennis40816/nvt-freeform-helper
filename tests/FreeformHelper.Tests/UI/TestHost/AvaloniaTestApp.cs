using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Fonts;
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
            .With(AppFontBootstrapper.CreateFontManagerOptions())
            .ConfigureFonts(static fontManager => fontManager.AddFontCollection(
                new EmbeddedFontCollection(
                    new Uri("fonts:SystemFonts"),
                    AppFontBootstrapper.InterSystemFontSourceUri)))
            .WithInterFont()
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
