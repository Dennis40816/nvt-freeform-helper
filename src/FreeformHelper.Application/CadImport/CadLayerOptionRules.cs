using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.CadImport;

/// <summary>Projects layer options without UI state, I/O or notification effects.</summary>
public static class CadLayerOptionRules
{
    public static CadLayerOptionSnapshot Build(CadLayerOptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var names = BuildLayerToggleNames(request.Cad, request.Catalog);
        var lookup = BuildLayerSelectionLookup(request.LayerSelections);
        var toggles = names.Select(name => new CadLayerSelection(name,
            lookup.TryGetValue(name, out var selected) ? selected : ShouldSelectLayerByDefault(name, request.Catalog))).ToList();
        var bounds = BuildBoundLayerOptions(names, request.PendingBoundLayerName, request.SelectedBoundLayerName,
            request.MaxBoundLayerDropdownLayers, out var boundName, out var boundSummary);
        var sources = BuildRegularSourceLayerOptions(names, request.PendingRegularSourceLayerName,
            request.SelectedRegularSourceLayerName, out var sourceName);
        var edits = names.Where(static name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToList();
        return new CadLayerOptionSnapshot(toggles, toggles.Count > 0, bounds, boundName, boundSummary, sources, sourceName,
            edits, ResolveDxfEditTargetLayerName(edits, request.SelectedDxfEditTargetLayerName),
            ResolveDxfEditTargetLayerName(edits, request.SelectedDxfEditRotationLayerName));
    }

    private static Dictionary<string, bool> BuildLayerSelectionLookup(IReadOnlyList<CadLayerSelection>? snapshot)
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

    private static List<string> BuildLayerToggleNames(CadPadSet cad, IReadOnlyDictionary<string, bool> catalog)
    {
        var importedNames = CadLayerVisibilityRules.GetLayerNames(cad);
        if (catalog.Count == 0)
        {
            return importedNames.ToList();
        }

        return importedNames
            .Concat(catalog.Keys)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldSelectLayerByDefault(string layerName, IReadOnlyDictionary<string, bool> catalog)
    {
        if (!catalog.TryGetValue(layerName, out var item))
        {
            return true;
        }

        if (item)
        {
            return true;
        }

        return string.Equals(layerName, "regular", StringComparison.OrdinalIgnoreCase);
    }

    private static List<BoundLayerOption> BuildBoundLayerOptions(
        List<string> layerNames,
        string? pendingBoundLayerName,
        string? selectedBoundLayerName,
        int maxBoundLayerDropdownLayers,
        out string? resolvedSelectedBoundLayerName,
        out string boundLayerOptionSummary)
    {
        var options = new List<BoundLayerOption>
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

        desired = CanonicalizeDesired(distinctNames, desired);
        var displayNames = SelectDisplayNames(distinctNames, desired, maxBoundLayerDropdownLayers);

        options.AddRange(displayNames.Select(name => new BoundLayerOption(name, name)));

        boundLayerOptionSummary = distinctNames.Count > displayNames.Count
            ? $"Hidden bounds layer is valid. Showing {displayNames.Count}/{distinctNames.Count}."
            : "Hidden bounds layer is still valid.";

        resolvedSelectedBoundLayerName = options.FirstOrDefault(option =>
            string.Equals(option.Name, desired, StringComparison.OrdinalIgnoreCase))?.Name;

        return options;
    }

    private static List<RegularSourceLayerOption> BuildRegularSourceLayerOptions(
        List<string> layerNames,
        string? pendingRegularSourceLayerName,
        string? selectedRegularSourceLayerName,
        out string? resolvedSelectedRegularSourceLayerName)
    {
        var options = new List<RegularSourceLayerOption>
        {
            new(null, "Select layer")
        };
        options.AddRange(layerNames.Select(name => new RegularSourceLayerOption(name, name)));

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
    private static string? CanonicalizeDesired(List<string> distinctNames, string? desired)
    {
        if (!string.IsNullOrWhiteSpace(desired))
        {
            var canonicalDesired = distinctNames.FirstOrDefault(name =>
                string.Equals(name, desired, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(canonicalDesired))
            {
                desired = canonicalDesired;
            }
        }

        return desired;
    }

    private static List<string> SelectDisplayNames(List<string> distinctNames, string? desired, int maxBoundLayerDropdownLayers)
    {
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

        return displayNames;
    }
}

public sealed record CadLayerOptionRequest(
    CadPadSet Cad,
    IReadOnlyDictionary<string, bool> Catalog,
    IReadOnlyList<CadLayerSelection>? LayerSelections,
    string? PendingBoundLayerName,
    string? SelectedBoundLayerName,
    string? PendingRegularSourceLayerName,
    string? SelectedRegularSourceLayerName,
    string? SelectedDxfEditTargetLayerName,
    string? SelectedDxfEditRotationLayerName,
    int MaxBoundLayerDropdownLayers);

public sealed record CadLayerOptionSnapshot(
    IReadOnlyList<CadLayerSelection> ToggleStates,
    bool HasCadLayers,
    IReadOnlyList<BoundLayerOption> BoundLayerOptions,
    string? SelectedBoundLayerName,
    string BoundLayerOptionSummary,
    IReadOnlyList<RegularSourceLayerOption> RegularSourceLayerOptions,
    string? SelectedRegularSourceLayerName,
    IReadOnlyList<string> DxfEditLayerOptions,
    string SelectedDxfEditTargetLayerName,
    string SelectedDxfEditRotationLayerName);

public readonly record struct CadLayerSelection(string Name, bool IsSelected);
