using System.Reflection;
using FreeformHelper.Infrastructure.Project;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class UiSnapshotPersistenceContractTests
{
    [Fact]
    public void RootContract_MatchesProjectUiSnapshotSettableProperties()
    {
        var actual = GetSettablePropertyNames(typeof(ProjectUiSnapshot));
        var expected = UiSnapshotPersistenceContract.RootProperties.OrderBy(static name => name).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SectionContract_MatchesAllSnapshotSettableProperties()
    {
        var expectedSectionTypes = new[]
        {
            typeof(UiGridSnapshot),
            typeof(UiMatchingSnapshot),
            typeof(UiViewSnapshot),
            typeof(UiNotchSnapshot),
            typeof(UiImportSnapshot),
            typeof(LayerSelectionSnapshot),
        };

        foreach (var sectionType in expectedSectionTypes)
        {
            Assert.True(
                UiSnapshotPersistenceContract.SectionProperties.ContainsKey(sectionType),
                $"Section contract missing type: {sectionType.Name}");

            var actual = GetSettablePropertyNames(sectionType);
            var expected = UiSnapshotPersistenceContract.SectionProperties[sectionType]
                .OrderBy(static name => name)
                .ToList();

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void SectionContract_DoesNotContainUnexpectedTypes()
    {
        var actualTypes = UiSnapshotPersistenceContract.SectionProperties.Keys
            .OrderBy(static type => type.Name)
            .ToList();

        var expectedTypes = new[]
        {
            typeof(LayerSelectionSnapshot),
            typeof(UiGridSnapshot),
            typeof(UiImportSnapshot),
            typeof(UiMatchingSnapshot),
            typeof(UiNotchSnapshot),
            typeof(UiViewSnapshot),
        }
        .OrderBy(static type => type.Name)
        .ToList();

        Assert.Equal(expectedTypes, actualTypes);
    }

    private static List<string> GetSettablePropertyNames(Type snapshotType)
    {
        return snapshotType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead && property.CanWrite)
            .Where(static property => property.GetCustomAttribute<LegacyUiSnapshotFieldAttribute>() is null)
            .Select(static property => property.Name)
            .OrderBy(static name => name)
            .ToList();
    }
}
