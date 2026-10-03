using System.Globalization;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using NLog;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Updates the list of layer toggles in the UI based on the layers found in the imported CAD data.
    /// </summary>
    /// <param name="cad">The <see cref="CadPadSet"/> containing the layers.</param>
    private void UpdateLayerToggles(CadPadSet cad)
    {
        var snapshot = BuildCadLayerUiSnapshot(cad, layerSelections: null);
        ApplyCadLayerUiSnapshot(snapshot);
    }

    private bool ApplyRegularSourceLayerAutoHidePolicy(bool filterCadPads = true)
    {
        if (RegularSourceMode != RegularSourceMode.FromDxfLayer)
        {
            return false;
        }

        var regularLayer = SelectedRegularSourceLayerOption?.Name;
        if (string.IsNullOrWhiteSpace(regularLayer))
        {
            return false;
        }

        var target = LayerToggles.FirstOrDefault(toggle =>
            string.Equals(toggle.Name, regularLayer, StringComparison.OrdinalIgnoreCase));
        if (target is null || !target.IsSelected)
        {
            return false;
        }

        _suppressLayerToggleChange = true;
        try
        {
            target.IsSelected = false;
        }
        finally
        {
            _suppressLayerToggleChange = false;
        }

        if (!_isLoadingSettings)
        {
            MarkUnsaved();
        }

        if (filterCadPads)
        {
            FilterCadPadsByLayer();
        }

        Logger.Info(CultureInfo.InvariantCulture, "Auto-hidden regular source layer from CAD view: {0}", regularLayer);
        return true;
    }

    private void RefreshDxfRegularSourceHint()
    {
        if (RegularSourceMode != RegularSourceMode.FromDxfLayer)
        {
            DxfRegularSourceHint = string.Empty;
            return;
        }

        if (_cad is null)
        {
            DxfRegularSourceHint = "DXF regular source: load DXF first.";
            return;
        }

        var layerName = SelectedRegularSourceLayerOption?.Name;
        if (string.IsNullOrWhiteSpace(layerName))
        {
            DxfRegularSourceHint = "DXF regular source: select a regular layer.";
            return;
        }

        var layerPads = _cad.Pads
            .Where(p => !IsCadPadEffectivelyHidden(p.Id))
            .Where(p => string.Equals(p.Layer, layerName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (layerPads.Count == 0)
        {
            DxfRegularSourceHint = $"Regular layer '{layerName}' has no visible pads.";
            return;
        }

        var x = Math.Max(1, (int)Math.Round(XChannels));
        var y = Math.Max(1, (int)Math.Round(YChannels));
        var expected = x * y;
        if (layerPads.Count == expected)
        {
            DxfRegularSourceHint = $"Regular layer '{layerName}': pads={layerPads.Count}, matches X*Y={x}*{y}.";
            return;
        }

        var cascade = Math.Max(1, (int)Math.Round(CascadeNum));
        DxfRegularSourceHint = GridRebuildOrchestrator.BuildDxfRegularSourceMismatchHint(layerPads.Count, x, y, cascade);
    }
}
