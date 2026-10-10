using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// ViewModel for the Dev page used to preview controls, tokens, and icon sizing.
/// </summary>
public sealed partial class DevViewModel : ObservableObject
{
    internal UiEventRunner? UiEvents { get; init; }

    public sealed record IconScaleProbe(string Label, double Scale);
    public sealed record StickyBlockPrototypeItem(string Title, string Summary, IReadOnlyList<string> DetailLines);

    [ObservableProperty]
    private string _status = "Dev preview";

    [ObservableProperty]
    private string _activeStickyBlockTitle = "General Settings";

    /// <summary>
    /// Gets rows for scrollbar feel preview blocks.
    /// </summary>
    public IReadOnlyList<string> ScrollPreviewRows { get; } = Enumerable
        .Range(1, 120)
        .Select(index => $"Row {index:000}  |  density probe  |  long payload ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789")
        .ToList();

    public IReadOnlyList<IconScaleProbe> IconScaleProbes { get; } = new[]
    {
        new IconScaleProbe("100%", 1.00),
        new IconScaleProbe("125%", 1.25),
        new IconScaleProbe("150%", 1.50),
    };

    public IReadOnlyList<StickyBlockPrototypeItem> StickyPrototypeBlocks { get; } = new[]
    {
        new StickyBlockPrototypeItem(
            "General Settings",
            "High-frequency layout and source controls.",
            new[]
            {
                "Grid padding / scan order / source mode.",
                "Layer selector and AA dimensions stay in this block.",
                "Expected to remain visible while scrolling details."
            }),
        new StickyBlockPrototypeItem(
            "Inspector snapshot",
            "Fast identity and match summary.",
            new[]
            {
                "IC / Diff idx / Match count.",
                "Short bullets only; no long trace lines.",
                "Acts as quick orientation anchor."
            }),
        new StickyBlockPrototypeItem(
            "Rule trace",
            "Low-frequency diagnostics with grouped sections.",
            new[]
            {
                "Identity / Match / Notch compute groups.",
                "Long text belongs here, not in top summary.",
                "Sticky title avoids losing context in deep scroll."
            }),
    };

    [RelayCommand]
    private void FocusStickyPrototypeBlock(StickyBlockPrototypeItem? item)
    {
        if (item is null)
        {
            return;
        }

        ActiveStickyBlockTitle = item.Title;
        Status = $"Sticky focus: {item.Title}";
    }
}
