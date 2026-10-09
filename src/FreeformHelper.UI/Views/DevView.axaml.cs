using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NLog;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.Views;

public partial class DevView : UserControl
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private int _previewSpinnerSequence;

    public DevView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachedToVisualTree -= OnAttachedToVisualTree;
        UiThread.IsCurrent(out _, out var app);
        Logger.Info(CultureInfo.InvariantCulture, "DevView theme probe: Requested={0} Actual={1}",
            app?.RequestedThemeVariant,
            ActualThemeVariant);
        var probeText = BuildProbeText();
        DevProbeText.Text = probeText;
        Logger.Info(CultureInfo.InvariantCulture, "{0}", probeText.Replace(Environment.NewLine, " | "));
    }

    private async void OnDevTriggerSpinnerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DevLoadingPreviewOverlay is null)
        {
            return;
        }

        var currentSequence = System.Threading.Interlocked.Increment(ref _previewSpinnerSequence);
        DevLoadingPreviewOverlay.IsVisible = true;
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        if (currentSequence != System.Threading.Volatile.Read(ref _previewSpinnerSequence))
        {
            return;
        }

        DevLoadingPreviewOverlay.IsVisible = false;
    }

    private void LogResourceProbe(string key)
    {
        var found = this.TryFindResource(key, out var value);
        Logger.Info(CultureInfo.InvariantCulture, "DevView resource probe: {0} found={1} type={2}", key, found, value?.GetType().Name ?? "null");
    }

    private string BuildProbeText()
    {
        var brushBgFound = this.TryFindResource("BrushBgApp", out var brushBg);
        UiThread.IsCurrent(out _, out var app);
        var brushTextFound = this.TryFindResource("BrushTextPrimary", out var brushText);
        var brushElevatedFound = this.TryFindResource("BrushBgElevated", out var brushElevated);
        var baseDir = AppContext.BaseDirectory;
        var fontLoaded = false;
        var fontProbe = "FontSymbolsOutlined: not found";
        if (this.TryFindResource("FontSymbolsOutlined", out var fontResource) &&
            fontResource is FontFamily fontFamily)
        {
            var typeface = new Typeface(fontFamily);
            fontLoaded = FontManager.Current.TryGetGlyphTypeface(typeface, out _);
            fontProbe = "FontSymbolsOutlined: resource ok";
        }

        return string.Join(Environment.NewLine,
            $"Probe: baseDir={baseDir}",
            $"Theme: requested={app?.RequestedThemeVariant} actual={ActualThemeVariant}",
            $"Token BrushBgApp: found={brushBgFound} type={brushBg?.GetType().Name ?? "null"}",
            $"Token BrushTextPrimary: found={brushTextFound} type={brushText?.GetType().Name ?? "null"}",
            $"Token BrushBgElevated: found={brushElevatedFound} type={brushElevated?.GetType().Name ?? "null"}",
            $"{fontProbe}",
            $"Font Material Symbols loaded={fontLoaded}");
    }
}

