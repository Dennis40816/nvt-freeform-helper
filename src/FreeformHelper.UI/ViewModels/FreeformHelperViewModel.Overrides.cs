using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void SetFreeformForSelection(FreeformType type) => GridMatching.SetFreeformForSelection(type);

    internal void SetFreeformForPadIndices(IReadOnlyCollection<int> padIndices, FreeformType type) =>
        GridMatching.SetFreeformForPadIndices(padIndices, type);

    private void SyncManualFreeformOverrides(IEnumerable<RegularPad> pads, FreeformType type) =>
        PadOverrideService.SetManualFreeformOverrides(_projectFile, pads, type);

    public double GetCadPadCustomValue(int cadId) => ManualSizing.GetCadPadCustomValue(cadId);

    public int ToDisplayRow(int actualRow) => ManualSizing.ToDisplayRow(actualRow);
}
