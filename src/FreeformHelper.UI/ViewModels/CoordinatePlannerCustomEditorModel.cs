using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The input fields of the "Custom point / path" form of the coordinate planner. Pressing Add in the workspace view
/// model reads them. They have no effect on the snapshot until then, so they need no change callbacks.
/// </summary>
public sealed partial class CoordinatePlannerCustomEditorModel : ObservableObject
{
    [ObservableProperty]
    private string _customPointLabelText = "Point";

    [ObservableProperty]
    private decimal _customPointMachineX;

    [ObservableProperty]
    private decimal _customPointMachineY;

    [ObservableProperty]
    private string _customPathLabelText = "Path";

    [ObservableProperty]
    private decimal _customPathStartMachineX;

    [ObservableProperty]
    private decimal _customPathStartMachineY;

    [ObservableProperty]
    private decimal _customPathEndMachineX;

    [ObservableProperty]
    private decimal _customPathEndMachineY;

    [ObservableProperty]
    private decimal _customPathStepCount;

    /// <summary>Starts the form with a point at the middle of the machine area and a path across its middle.</summary>
    public CoordinatePlannerCustomEditorModel(
        decimal machineOriginX,
        decimal machineOriginY,
        decimal machineWidth,
        decimal machineHeight)
    {
        _customPointMachineX = machineOriginX + (machineWidth * 0.5m);
        _customPointMachineY = machineOriginY + (machineHeight * 0.5m);
        _customPathStartMachineX = machineOriginX;
        _customPathStartMachineY = machineOriginY + (machineHeight * 0.5m);
        _customPathEndMachineX = machineOriginX + machineWidth;
        _customPathEndMachineY = _customPathStartMachineY;
        _customPathStepCount = 5m;
    }
}
