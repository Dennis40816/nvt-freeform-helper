using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Builds a single UI snapshot for CAD-layer dependent controls during DXF/project load.
/// This keeps layer-name discovery and option shaping off the UI thread.
/// </summary>
public static class CadLayerUiSnapshotBuilder
{
    public static CadLayerUiSnapshot Build(CadLayerUiSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var layerNames = BuildLayerToggleNames(request.Cad, request.Catalog);
        var layerSelectionLookup = BuildLayerSelectionLookup(request.LayerSelections);
        var toggleStates = layerNames
            .Select(name => new CadLayerToggleState(
                name,
                layerSelectionLookup.TryGetValue(name, out var isSelected)
                    ? isSelected
                    : ShouldSelectLayerByDefault(name, request.Catalog)))
            .ToList();

        var boundLayerOptions = BuildBoundLayerOptions(
            layerNames,
            request.PendingBoundLayerName,
            request.SelectedBoundLayerName,
            request.MaxBoundLayerDropdownLayers,
            out var selectedBoundLayerName,
            out var boundLayerOptionSummary);

        var regularSourceLayerOptions = BuildRegularSourceLayerOptions(
            layerNames,
            request.PendingRegularSourceLayerName,
            request.SelectedRegularSourceLayerName,
            out var selectedRegularSourceLayerName);

        var dxfEditLayerOptions = layerNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedDxfEditTargetLayerName = ResolveDxfEditTargetLayerName(
            dxfEditLayerOptions,
            request.SelectedDxfEditTargetLayerName);
        var selectedDxfEditRotationLayerName = ResolveDxfEditTargetLayerName(
            dxfEditLayerOptions,
            request.SelectedDxfEditRotationLayerName);

        return new CadLayerUiSnapshot(
            ToggleStates: toggleStates,
            HasCadLayers: toggleStates.Count > 0,
            BoundLayerOptions: boundLayerOptions,
            SelectedBoundLayerName: selectedBoundLayerName,
            BoundLayerOptionSummary: boundLayerOptionSummary,
            RegularSourceLayerOptions: regularSourceLayerOptions,
            SelectedRegularSourceLayerName: selectedRegularSourceLayerName,
            DxfEditLayerOptions: dxfEditLayerOptions,
            SelectedDxfEditTargetLayerName: selectedDxfEditTargetLayerName,
            SelectedDxfEditRotationLayerName: selectedDxfEditRotationLayerName);
    }

    private static Dictionary<string, bool> BuildLayerSelectionLookup(IReadOnlyList<LayerSelectionSnapshot>? snapshot)
    {
        if (snapshot is null || snapshot.Count == 0)
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        return snapshot
            .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Last().IsSelected,
                StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> BuildLayerToggleNames(CadPadSet cad, DxfLayerCatalog catalog)
    {
        var importedNames = LayerFilterUseCase.GetLayerNames(cad);
        if (catalog.Layers.Count == 0)
        {
            return importedNames.ToList();
        }

        return importedNames
            .Concat(catalog.Layers.Select(static item => item.Name))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldSelectLayerByDefault(string layerName, DxfLayerCatalog catalog)
    {
        if (!catalog.TryGetLayer(layerName, out var item))
        {
            return true;
        }

        if (item.HasEntitySectionContent)
        {
            return true;
        }

        return string.Equals(layerName, "regular", StringComparison.OrdinalIgnoreCase);
    }

    private static List<FreeformHelperViewModel.BoundLayerOption> BuildBoundLayerOptions(
        List<string> layerNames,
        string? pendingBoundLayerName,
        string? selectedBoundLayerName,
        int maxBoundLayerDropdownLayers,
        out string? resolvedSelectedBoundLayerName,
        out string boundLayerOptionSummary)
    {
        var options = new List<FreeformHelperViewModel.BoundLayerOption>
        {
            new(null, "Auto (visible layers)")
        };

        var desired = string.IsNullOrWhiteSpace(pendingBoundLayerName)
            ? selectedBoundLayerName
            : pendingBoundLayerName;

        var distinctNames = layerNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(desired))
        {
            var canonicalDesired = distinctNames.FirstOrDefault(name =>
                string.Equals(name, desired, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(canonicalDesired))
            {
                desired = canonicalDesired;
            }
        }

        var displayNames = distinctNames.Take(maxBoundLayerDropdownLayers).ToList();
        if (!string.IsNullOrWhiteSpace(desired) &&
            distinctNames.Any(name => string.Equals(name, desired, StringComparison.OrdinalIgnoreCase)) &&
            !displayNames.Any(name => string.Equals(name, desired, StringComparison.OrdinalIgnoreCase)))
        {
            if (displayNames.Count >= maxBoundLayerDropdownLayers && displayNames.Count > 0)
            {
                displayNames[^1] = desired;
            }
            else
            {
                displayNames.Add(desired);
            }

            displayNames = displayNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        options.AddRange(displayNames.Select(name => new FreeformHelperViewModel.BoundLayerOption(name, name)));

        boundLayerOptionSummary = distinctNames.Count > displayNames.Count
            ? $"Hidden bounds layer is valid. Showing {displayNames.Count}/{distinctNames.Count}."
            : "Hidden bounds layer is still valid.";

        resolvedSelectedBoundLayerName = options.FirstOrDefault(option =>
            string.Equals(option.Name, desired, StringComparison.OrdinalIgnoreCase))?.Name;

        return options;
    }

    private static List<FreeformHelperViewModel.RegularSourceLayerOption> BuildRegularSourceLayerOptions(
        List<string> layerNames,
        string? pendingRegularSourceLayerName,
        string? selectedRegularSourceLayerName,
        out string? resolvedSelectedRegularSourceLayerName)
    {
        var options = new List<FreeformHelperViewModel.RegularSourceLayerOption>
        {
            new(null, "Select layer")
        };
        options.AddRange(layerNames.Select(name => new FreeformHelperViewModel.RegularSourceLayerOption(name, name)));

        var desired = string.IsNullOrWhiteSpace(pendingRegularSourceLayerName)
            ? selectedRegularSourceLayerName
            : pendingRegularSourceLayerName;

        resolvedSelectedRegularSourceLayerName = options.FirstOrDefault(option =>
            string.Equals(option.Name, desired, StringComparison.OrdinalIgnoreCase))?.Name;
        return options;
    }

    private static string ResolveDxfEditTargetLayerName(
        List<string> layerNames,
        string? selectedDxfEditTargetLayerName)
    {
        if (layerNames.Count == 0)
        {
            return string.Empty;
        }

        var selected = layerNames.FirstOrDefault(name =>
            string.Equals(name, selectedDxfEditTargetLayerName, StringComparison.OrdinalIgnoreCase));
        return selected ?? layerNames[0];
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
    IReadOnlyList<FreeformHelperViewModel.BoundLayerOption> BoundLayerOptions,
    string? SelectedBoundLayerName,
    string BoundLayerOptionSummary,
    IReadOnlyList<FreeformHelperViewModel.RegularSourceLayerOption> RegularSourceLayerOptions,
    string? SelectedRegularSourceLayerName,
    IReadOnlyList<string> DxfEditLayerOptions,
    string SelectedDxfEditTargetLayerName,
    string SelectedDxfEditRotationLayerName);

public readonly record struct CadLayerToggleState(string Name, bool IsSelected);
