using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SettingsWindowViewModel
{
    public ObservableCollection<CascadeIcDraftRow> CascadeIcSettings
    {
        get => _grid.CascadeIcSettings;
        set => _grid.CascadeIcSettings = value;
    }

    public string CascadeLayoutSummary => _grid.CascadeLayoutSummary;

    internal IReadOnlyList<int> PerIcXChannels => _grid.PerIcXChannels;

    internal IReadOnlyList<int> PerIcYChannels => _grid.PerIcYChannels;

    private static CascadeDraftLayout BuildCascadeDraftLayout(
        int cascadeNum,
        int xChannels,
        int yChannels,
        IEnumerable<int> perIcXChannels,
        IEnumerable<int> perIcYChannels)
    {
        var layout = CascadeIcLayoutService.Build(cascadeNum, xChannels, yChannels, perIcXChannels, perIcYChannels);
        return new CascadeDraftLayout(
            layout.Rows.Select(static row => (row.IcIndex, row.XChannels, row.YChannels)).ToArray(),
            layout.TotalXChannels,
            layout.TotalYChannels,
            layout.PerIcXChannels,
            layout.PerIcYChannels);
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
            var clamped = SettingsGridDraftOwner.ClampPositiveInt(value);
            if (clamped != value)
            {
                XChannels = clamped;
            }
        }

        partial void OnYChannelsChanged(decimal value)
        {
            var clamped = SettingsGridDraftOwner.ClampPositiveInt(value);
            if (clamped != value)
            {
                YChannels = clamped;
            }
        }
    }
}
