using FreeformHelper.Application.Settings;

namespace FreeformHelper.Infrastructure.Project;

/// <summary>
/// Centralizes project file migration and normalization rules.
/// Append-only schema policy: never remove legacy fields; prefer migrating in place and preserving unknown JSON.
/// </summary>
public static class ProjectFileMigrator
{
    public static ProjectFile MigrateInPlace(ProjectFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (string.IsNullOrWhiteSpace(file.SchemaVersion))
        {
            file.SchemaVersion = ProjectSchema.CurrentVersion;
        }

        file.Settings ??= new ProjectSettings();
        EnsureSettingsNotNull(file.Settings);

        file.UiSnapshot ??= new ProjectUiSnapshot();
        EnsureUiSnapshotNotNull(file.UiSnapshot);

        file.FreeformOverrides ??= new Dictionary<int, Domain.Pads.FreeformType>();
        file.CadPadCustomValues ??= new Dictionary<int, double>();
        file.HiddenCadPadIds ??= new HashSet<int>();
        file.VisibleDuplicateCadPadIds ??= new HashSet<int>();
        file.DxfCadLayerOverrides ??= new Dictionary<int, string>();
        file.DxfCadGeometryOverrides ??= new Dictionary<int, ProjectCadGeometrySnapshot>();
        file.DxfCombinedCadGroups ??= new List<ProjectDxfCombinedCadGroup>();
        file.DxfRegularMappingOverrides ??= new Dictionary<int, int>();
        file.CadOutputFwDiffIndexOverrides ??= new Dictionary<int, int>();
        file.CadOutputFwDiffIndexAnchorCadPadByIc ??= new Dictionary<int, int>();

        foreach (var geometryOverride in file.DxfCadGeometryOverrides.Values)
        {
            geometryOverride.Vertices ??= new List<Domain.Geometry.Point2>();
        }

        foreach (var group in file.DxfCombinedCadGroups)
        {
            group.SourceCadIds ??= new List<int>();
            group.OutputPads ??= new List<ProjectCadPadSnapshot>();
            foreach (var outputPad in group.OutputPads)
            {
                outputPad.Name ??= string.Empty;
                outputPad.Layer ??= string.Empty;
                outputPad.Vertices ??= new List<Domain.Geometry.Point2>();
            }
        }

        return file;
    }

    private static void EnsureSettingsNotNull(ProjectSettings settings)
    {
        settings.Grid ??= new GridSettings();
        settings.Matching ??= new MatchingSettings();
        settings.View ??= new ViewSettings();
        settings.Notch ??= new NotchSettings();
        settings.IndexMapping ??= new IndexMappingSettings();

        var grid = settings.Grid;
        if (grid.MaxChannelsX <= 0 || grid.MaxChannelsX == GridSettings.LegacyChannelLimit)
        {
            grid.MaxChannelsX = GridSettings.DefaultChannelLimit;
        }
        if (grid.MaxChannelsY <= 0 || grid.MaxChannelsY == GridSettings.LegacyChannelLimit)
        {
            grid.MaxChannelsY = GridSettings.DefaultChannelLimit;
        }
        grid.MaxChannelsX = Math.Max(grid.MaxChannelsX, Math.Max(1, grid.XChannels));
        grid.MaxChannelsY = Math.Max(grid.MaxChannelsY, Math.Max(1, grid.YChannels));

        grid.PerIcXChannels ??= new List<int>();
        grid.PerIcYChannels ??= new List<int>();
        grid.ColumnWidths ??= new List<double>();
        grid.ColumnOverrides ??= new List<bool>();
        grid.RowHeights ??= new List<double>();
        grid.RowOverrides ??= new List<bool>();
        grid.RowWidthOverrides ??= new List<List<double>>();
        grid.RowWidthOverrideFlags ??= new List<List<bool>>();
        grid.ColumnHeightOverrides ??= new List<List<double>>();
        grid.ColumnHeightOverrideFlags ??= new List<List<bool>>();

        EnsureNestedLists(grid.RowWidthOverrides);
        EnsureNestedLists(grid.RowWidthOverrideFlags);
        EnsureNestedLists(grid.ColumnHeightOverrides);
        EnsureNestedLists(grid.ColumnHeightOverrideFlags);

        settings.Notch.ReplaceEnabledVersions(settings.Notch.EnabledVersions);
    }

    private static void EnsureUiSnapshotNotNull(ProjectUiSnapshot snapshot)
    {
        snapshot.Grid ??= new UiGridSnapshot();
        snapshot.Matching ??= new UiMatchingSnapshot();
        snapshot.View ??= new UiViewSnapshot();
        snapshot.Notch ??= new UiNotchSnapshot();
        snapshot.Import ??= new UiImportSnapshot();

        snapshot.Grid.IcX ??= new List<int>();
        snapshot.Grid.IcY ??= new List<int>();
        snapshot.View.LayerSelections ??= new List<LayerSelectionSnapshot>();
    }

    private static void EnsureNestedLists<T>(List<List<T>> list)
    {
        if (list.Count == 0)
        {
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            list[i] ??= new List<T>();
        }
    }
}
