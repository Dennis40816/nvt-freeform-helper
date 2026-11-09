using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.Services;
using RecipeRow = FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel.CoordinatePlannerCustomRecipeRow;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the custom points and custom paths of the coordinate planner and the table rows that describe them.
/// The view model owns the notifications and the snapshot rebuild. This model owns the data and its rules.
/// </summary>
internal sealed class CoordinatePlannerCustomArtifactsModel
{
    private readonly List<CoordinateCustomPointRequest> _points = new();
    private readonly List<CoordinateCustomPathRequest> _paths = new();
    private readonly ObservableCollection<RecipeRow> _recipeRows = new();
    private int _nextPointId = 1;
    private int _nextPathId = 1;

    public CoordinatePlannerCustomArtifactsModel()
    {
        RecipeRows = new ReadOnlyObservableCollection<RecipeRow>(_recipeRows);
    }

    public ReadOnlyObservableCollection<RecipeRow> RecipeRows { get; }

    public int PointCount => _points.Count;

    public int PathCount => _paths.Count;

    public bool HasAny => _points.Count > 0 || _paths.Count > 0;

    public IReadOnlyList<CoordinateCustomPointRequest> PointRequests() => _points.ToArray();

    public IReadOnlyList<CoordinateCustomPathRequest> PathRequests() => _paths.ToArray();

    /// <summary>Adds a point and returns the label that was stored (trimmed, or "Point" when empty).</summary>
    public string AddPoint(string labelText, double machineX, double machineY)
    {
        var label = NormalizeLabel(labelText, "Point");
        var key = (_nextPointId++).ToString(CultureInfo.InvariantCulture);
        _points.Add(new CoordinateCustomPointRequest(key, label, machineX, machineY));
        RebuildRecipeRows();
        return label;
    }

    /// <summary>Adds a path and returns the label that was stored (trimmed, or "Path" when empty).</summary>
    public string AddPath(
        string labelText,
        double startMachineX,
        double startMachineY,
        double endMachineX,
        double endMachineY,
        int stepCount)
    {
        var label = NormalizeLabel(labelText, "Path");
        var key = (_nextPathId++).ToString(CultureInfo.InvariantCulture);
        _paths.Add(new CoordinateCustomPathRequest(
            key,
            label,
            startMachineX,
            startMachineY,
            endMachineX,
            endMachineY,
            stepCount));
        RebuildRecipeRows();
        return label;
    }

    public void Clear()
    {
        _points.Clear();
        _paths.Clear();
        RebuildRecipeRows();
    }

    private static string NormalizeLabel(string label, string fallback)
    {
        var normalized = label.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private void RebuildRecipeRows()
    {
        _recipeRows.Clear();
        foreach (var point in _points)
        {
            _recipeRows.Add(new RecipeRow(
                $"custom-point-{point.Key}",
                point.Label,
                "Point",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Machine ({0:0.###}, {1:0.###})",
                    point.MachineX,
                    point.MachineY)));
        }

        foreach (var path in _paths)
        {
            _recipeRows.Add(new RecipeRow(
                $"custom-path-{path.Key}",
                path.Label,
                "Path",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Machine ({0:0.###}, {1:0.###}) -> ({2:0.###}, {3:0.###}) · {4} step(s)",
                    path.StartMachineX,
                    path.StartMachineY,
                    path.EndMachineX,
                    path.EndMachineY,
                    Math.Max(0, path.StepCount))));
        }
    }
}
