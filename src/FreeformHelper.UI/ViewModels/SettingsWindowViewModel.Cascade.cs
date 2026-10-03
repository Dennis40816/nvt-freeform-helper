using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SettingsWindowViewModel
{
    [ObservableProperty] private ObservableCollection<CascadeIcDraftRow> _cascadeIcSettings = new();

    public string CascadeLayoutSummary =>
        $"IC {CascadeIcSettings.Count} · X {XChannels:0} · Y {YChannels:0}";

    internal IReadOnlyList<int> PerIcXChannels => GetCascadeLayout().PerIcXChannels;

    internal IReadOnlyList<int> PerIcYChannels => GetCascadeLayout().PerIcYChannels;

    private void InitializeCascadeIcSettings(FreeformHelperViewModel owner)
    {
        var layout = CascadeIcLayoutService.Build(
            ClampPositiveInt(CascadeNum),
            ClampPositiveInt(XChannels),
            ClampPositiveInt(YChannels),
            owner.CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.XChannels))),
            owner.CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.YChannels))));

        ReplaceCascadeIcSettings(layout);
    }

    private CascadeIcLayout GetCascadeLayout()
    {
        return CascadeIcLayoutService.Build(
            ClampPositiveInt(CascadeNum),
            ClampPositiveInt(XChannels),
            ClampPositiveInt(YChannels),
            CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.XChannels))),
            CascadeIcSettings.Select(static row => Math.Max(1, (int)Math.Round(row.YChannels))));
    }

    private void ReplaceCascadeIcSettings(CascadeIcLayout layout)
    {
        DetachCascadeIcRows();

        var rows = new ObservableCollection<CascadeIcDraftRow>(
            layout.Rows.Select(static row =>
                new CascadeIcDraftRow(row.IcIndex, row.XChannels, row.YChannels)));
        foreach (var row in rows)
        {
            row.PropertyChanged += OnCascadeIcDraftRowChanged;
        }

        CascadeIcSettings = rows;
        RefreshCascadeTotalsFromRows();
    }

    private void DetachCascadeIcRows()
    {
        foreach (var row in CascadeIcSettings)
        {
            row.PropertyChanged -= OnCascadeIcDraftRowChanged;
        }
    }

    private void OnCascadeIcDraftRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(CascadeIcDraftRow.XChannels) or nameof(CascadeIcDraftRow.YChannels)))
        {
            return;
        }

        RefreshCascadeTotalsFromRows();
    }

    private void RefreshCascadeTotalsFromRows()
    {
        var layout = GetCascadeLayout();
        XChannels = layout.TotalXChannels;
        YChannels = layout.TotalYChannels;
        OnPropertyChanged(nameof(CascadeLayoutSummary));
    }

    private void RefreshCascadeRowsForCurrentCount()
    {
        var layout = GetCascadeLayout();
        ReplaceCascadeIcSettings(layout);
    }

    partial void OnCascadeNumChanged(decimal value)
    {
        var clamped = ClampPositiveInt(value);
        if (clamped != value)
        {
            CascadeNum = clamped;
            return;
        }

        RefreshCascadeRowsForCurrentCount();
    }

    partial void OnXChannelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(CascadeLayoutSummary));
    }

    partial void OnYChannelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(CascadeLayoutSummary));
    }

    private static int ClampPositiveInt(decimal value)
    {
        return Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    public sealed partial class CascadeIcDraftRow : ObservableObject
    {
        [ObservableProperty] private int _icIndex;
        [ObservableProperty] private decimal _xChannels;
        [ObservableProperty] private decimal _yChannels;

        public CascadeIcDraftRow(int icIndex, int xChannels, int yChannels)
        {
            _icIndex = Math.Max(1, icIndex);
            _xChannels = Math.Max(1, xChannels);
            _yChannels = Math.Max(1, yChannels);
        }

        public string IcLabel => $"IC {IcIndex}";

        partial void OnIcIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IcLabel));
        }

        partial void OnXChannelsChanged(decimal value)
        {
            var clamped = ClampPositiveInt(value);
            if (clamped != value)
            {
                XChannels = clamped;
            }
        }

        partial void OnYChannelsChanged(decimal value)
        {
            var clamped = ClampPositiveInt(value);
            if (clamped != value)
            {
                YChannels = clamped;
            }
        }
    }
}
