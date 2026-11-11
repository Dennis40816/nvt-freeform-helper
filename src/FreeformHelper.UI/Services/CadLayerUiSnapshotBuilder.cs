using FreeformHelper.Application.CadImport;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.Services;

/// <summary>Adapts imported catalog and persisted selections to the pure option projection.</summary>
public static class CadLayerUiSnapshotBuilder
{
    public static CadLayerUiSnapshot Build(CadLayerUiSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var catalog = request.Catalog.ByName.ToDictionary(pair => pair.Key, pair => pair.Value.HasEntitySectionContent,
            StringComparer.OrdinalIgnoreCase);
        var selections = request.LayerSelections?.Select(item => new CadLayerSelection(item.Name, item.IsSelected)).ToList();
        var result = CadLayerOptionRules.Build(new CadLayerOptionRequest(request.Cad, catalog, selections,
            request.PendingBoundLayerName, request.SelectedBoundLayerName, request.PendingRegularSourceLayerName,
            request.SelectedRegularSourceLayerName, request.SelectedDxfEditTargetLayerName,
            request.SelectedDxfEditRotationLayerName, request.MaxBoundLayerDropdownLayers));
        return new CadLayerUiSnapshot(result.ToggleStates.Select(item => new CadLayerToggleState(item.Name, item.IsSelected)).ToList(),
            result.HasCadLayers, result.BoundLayerOptions, result.SelectedBoundLayerName, result.BoundLayerOptionSummary,
            result.RegularSourceLayerOptions, result.SelectedRegularSourceLayerName, result.DxfEditLayerOptions,
            result.SelectedDxfEditTargetLayerName, result.SelectedDxfEditRotationLayerName);
    }
}

public sealed record CadLayerUiSnapshotRequest(
    CadPadSet Cad,
    DxfLayerCatalog Catalog,
    IReadOnlyList<LayerSelectionSnapshot>? LayerSelections,
    string? PendingBoundLayerName,
    string? SelectedBoundLayerName,
    string? PendingRegularSourceLayerName,
    string? SelectedRegularSourceLayerName,
    string? SelectedDxfEditTargetLayerName,
    string? SelectedDxfEditRotationLayerName,
    int MaxBoundLayerDropdownLayers);

public sealed record CadLayerUiSnapshot(
    IReadOnlyList<CadLayerToggleState> ToggleStates,
    bool HasCadLayers,
    IReadOnlyList<BoundLayerOption> BoundLayerOptions,
    string? SelectedBoundLayerName,
    string BoundLayerOptionSummary,
    IReadOnlyList<RegularSourceLayerOption> RegularSourceLayerOptions,
    string? SelectedRegularSourceLayerName,
    IReadOnlyList<string> DxfEditLayerOptions,
    string SelectedDxfEditTargetLayerName,
    string SelectedDxfEditRotationLayerName);

public readonly record struct CadLayerToggleState(string Name, bool IsSelected);
