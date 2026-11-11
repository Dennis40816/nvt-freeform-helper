using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels.Project;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly DxfLayerImagePreferenceState _dxfLayerImagePreferenceState;

    private DxfLayerImageExportRequest _dxfLayerImageExportPreferences => _dxfLayerImagePreferenceState.Value;

    internal void RememberDxfLayerImageExportRequest(DxfLayerImageExportRequest request) =>
        _dxfLayerImagePreferenceState.Remember(request);

    private void ApplyDxfLayerImageExportSnapshot(UiViewSnapshot snapshot) =>
        _dxfLayerImagePreferenceState.Apply(snapshot);
}
