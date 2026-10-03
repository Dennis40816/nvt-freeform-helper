using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class FreeformHelperViewModelTokenDefaultsTests
{
    [Fact]
    public async Task Constructor_OffUiThread_DoesNotCreateDispatcherTimer()
    {
        var timerField = typeof(FreeformHelperViewModel).GetField(
            "_appGeneralSettingsPersistTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(timerField);

        var viewModel = await Task.Run(static () => new FreeformHelperViewModel());

        Assert.Null(timerField.GetValue(viewModel));
    }

    [AvaloniaFact]
    public void FreeformHelperViewModel_DefaultCanvasColors_ComeFromTokens()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current ?? throw new InvalidOperationException("Application.Current was not initialized.");
        var theme = app.ActualThemeVariant ?? ThemeVariant.Default;
        var vm = new FreeformHelperViewModel();
        var timerField = typeof(FreeformHelperViewModel).GetField(
            "_appGeneralSettingsPersistTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(timerField?.GetValue(vm));
        Assert.Equal(GetRequiredColor(app, theme, "ColorCanvasCadLine"), vm.CadLineColor);
        Assert.Equal(GetRequiredColor(app, theme, "ColorCanvasRegularLine"), vm.RegularLineColor);
        Assert.Equal(GetRequiredColor(app, theme, "ColorCanvasRegularSelected"), vm.RegularSelectedColor);
    }

    [AvaloniaFact]
    public async Task FreeformHelperViewModel_OutsideUiDispatcher_KeepsCanvasColorDefaults()
    {
        HeadlessAppBootstrap.EnsureInitialized();

        var vm = await Task.Run(static () => new FreeformHelperViewModel());

        Assert.Equal(default, vm.CadLineColor);
        Assert.Equal(default, vm.RegularLineColor);
        Assert.Equal(default, vm.RegularSelectedColor);
    }

    [AvaloniaFact]
    public async Task ResolveTokenColor_OutsideUiDispatcher_DoesNotReadApplicationTheme()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Assert.NotNull(global::Avalonia.Application.Current);

        var result = await Task.Run(static () =>
        {
            var vm = new FreeformHelperViewModel();
            var tokenColor = FreeformHelperViewModel.ResolveTokenColor("ColorCanvasCadLine");
            return (vm, tokenColor);
        });

        Assert.Null(result.tokenColor);
        Assert.Equal(default, result.vm.CadLineColor);
        Assert.Equal(default, result.vm.RegularLineColor);
        Assert.Equal(default, result.vm.RegularSelectedColor);
    }

    private static Color GetRequiredColor(global::Avalonia.Application app, ThemeVariant theme, string key)
    {
        if (!app.Resources.TryGetResource(key, theme, out var resource))
        {
            throw new Xunit.Sdk.XunitException($"Resource '{key}' was not found.");
        }

        return resource switch
        {
            Color color => color,
            SolidColorBrush brush => brush.Color,
            _ => throw new Xunit.Sdk.XunitException($"Resource '{key}' was not a Color or SolidColorBrush.")
        };
    }
}
