using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ProjectModelCollectionIsolationTests
{
    [Fact]
    public void ProjectFile_CollectionSetters_CloneInputCollections()
    {
        var freeformOverrides = new Dictionary<int, FreeformType> { [100] = FreeformType.XWay };
        var hiddenCadPadIds = new HashSet<int> { 1, 2 };
        var combinedGroups = new List<ProjectDxfCombinedCadGroup>
        {
            new()
            {
                GroupId = 7,
                SourceCadIds = new List<int> { 10, 11 },
            },
        };

        var file = new ProjectFile
        {
            FreeformOverrides = freeformOverrides,
            HiddenCadPadIds = hiddenCadPadIds,
            DxfCombinedCadGroups = combinedGroups,
        };

        freeformOverrides[101] = FreeformType.YWay;
        hiddenCadPadIds.Add(3);
        combinedGroups.Add(new ProjectDxfCombinedCadGroup { GroupId = 8 });

        Assert.False(file.FreeformOverrides.ContainsKey(101));
        Assert.DoesNotContain(3, file.HiddenCadPadIds);
        Assert.Single(file.DxfCombinedCadGroups);

        file.CadPadCustomValues = null!;
        file.DxfRegularMappingOverrides = null!;
        Assert.Empty(file.CadPadCustomValues);
        Assert.Empty(file.DxfRegularMappingOverrides);
    }

    [Fact]
    public void ProjectUiSnapshot_ListSetters_CloneInputCollections()
    {
        var icX = new List<int> { 12, 12 };
        var icY = new List<int> { 8, 8 };
        var layerSelections = new List<LayerSelectionSnapshot>
        {
            new() { Name = "AA", IsSelected = true },
        };
        var enabledVersions = new List<string> { "V21", "V22" };

        var snapshot = new ProjectUiSnapshot
        {
            Grid = new UiGridSnapshot
            {
                IcX = icX,
                IcY = icY,
            },
            View = new UiViewSnapshot
            {
                LayerSelections = layerSelections,
            },
            Notch = new UiNotchSnapshot
            {
                EnabledVersions = enabledVersions,
            },
        };

        icX.Add(99);
        icY.Add(99);
        layerSelections.Add(new LayerSelectionSnapshot { Name = "REG", IsSelected = false });
        enabledVersions.Add("V23");

        Assert.Equal(2, snapshot.Grid.IcX.Count);
        Assert.Equal(2, snapshot.Grid.IcY.Count);
        Assert.Single(snapshot.View.LayerSelections);
        Assert.NotNull(snapshot.Notch.EnabledVersions);
        Assert.Equal(2, snapshot.Notch.EnabledVersions.Count);

        snapshot.Grid.IcX = null!;
        snapshot.Notch.EnabledVersions = null!;
        Assert.Empty(snapshot.Grid.IcX);
        Assert.Null(snapshot.Notch.EnabledVersions);
    }
}
