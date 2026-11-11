using FreeformHelper.Application.CadImport;

namespace FreeformHelper.UI.CadImport;

public sealed partial class LayerSelectionViewModel
{
    // Preserve staged getter values and callback-before-changed publication. UI thread only.
    private LayerSourceSelection _layerSource = LayerSourceSelection.Initial;

    /// <summary>
    /// Gets or sets helper text for DXF regular-source validation and suggested IC/grid settings.
    /// </summary>
    public string DxfRegularSourceHint
    {
        get => _layerSource.DxfRegularSourceHint;
        set
        {
            if (_layerSource.DxfRegularSourceHint == value)
            {
                return;
            }

            OnPropertyChanging(nameof(DxfRegularSourceHint));
            _layerSource = _layerSource with { DxfRegularSourceHint = value };
            OnPropertyChanged(nameof(DxfRegularSourceHint));
        }
    }

    /// <summary>
    /// Gets or sets the currently selected layer option for DXF regular source mode.
    /// </summary>
    public RegularSourceLayerOption SelectedRegularSourceLayerOption
    {
        get => _layerSource.SelectedRegularSourceLayerOption;
        set
        {
            if (_layerSource.SelectedRegularSourceLayerOption == value)
            {
                return;
            }

            OnPropertyChanging(nameof(SelectedRegularSourceLayerOption));
            _layerSource = _layerSource with { SelectedRegularSourceLayerOption = value };
            Workflow.OnSourceSelectionChanged();
            OnPropertyChanged(nameof(SelectedRegularSourceLayerOption));
        }
    }

    /// <summary>
    /// Gets or sets the helper text shown under bounds layer selector,
    /// including truncation hint when available layers exceed dropdown cap.
    /// </summary>
    public string BoundLayerOptionSummary
    {
        get => _layerSource.BoundLayerOptionSummary;
        set
        {
            if (_layerSource.BoundLayerOptionSummary == value)
            {
                return;
            }

            OnPropertyChanging(nameof(BoundLayerOptionSummary));
            _layerSource = _layerSource with { BoundLayerOptionSummary = value };
            OnPropertyChanged(nameof(BoundLayerOptionSummary));
        }
    }

    /// <summary>
    /// Gets or sets the currently selected layer option for bounding the grid.
    /// </summary>
    public BoundLayerOption SelectedBoundLayerOption
    {
        get => _layerSource.SelectedBoundLayerOption;
        set
        {
            if (_layerSource.SelectedBoundLayerOption == value)
            {
                return;
            }

            OnPropertyChanging(nameof(SelectedBoundLayerOption));
            _layerSource = _layerSource with { SelectedBoundLayerOption = value };
            Workflow.OnBoundSelectionChanged();
            OnPropertyChanged(nameof(SelectedBoundLayerOption));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether any CAD layers are available.
    /// Used to hide the layer section when no DXF is loaded.
    /// </summary>
    public bool HasCadLayers
    {
        get => _layerSource.HasCadLayers;
        set
        {
            if (_layerSource.HasCadLayers == value)
            {
                return;
            }

            OnPropertyChanging(nameof(HasCadLayers));
            _layerSource = _layerSource with { HasCadLayers = value };
            OnPropertyChanged(nameof(HasCadLayers));
        }
    }
}
