using System.Globalization;
using System.Text.Json;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class TypedMappingBoundaryTests
{
    [Fact]
    public void DefaultIndicesAndUnmappedPad_AreZero()
    {
        Assert.Equal(new IcIndex(0), default(IcIndex));
        Assert.Equal(new DiffIndex(0), default(DiffIndex));
        var pad = CreatePad();
        Assert.Equal((0, 0, 0), (pad.IcIndex, pad.DiffIndex, pad.RegularFwDiffIndex));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MinValue + 1)]
    [InlineData(-2)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(int.MaxValue - 1)]
    [InlineData(int.MaxValue)]
    public void Indices_PreserveInt32ValuesAndEquality(int value)
    {
        var ic = new IcIndex(value);
        var diff = new DiffIndex(value);
        Assert.Equal(value, ic.Value);
        Assert.Equal(value, diff.Value);
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), ic.ToString());
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), diff.ToString());
        Assert.True(ic == new IcIndex(value));
        Assert.True(diff == new DiffIndex(value));
        Assert.Equal(ic.GetHashCode(), new IcIndex(value).GetHashCode());
        Assert.Equal(diff.GetHashCode(), new DiffIndex(value).GetHashCode());
        int different = value == int.MaxValue ? int.MinValue : value + 1;
        Assert.True(ic != new IcIndex(different));
        Assert.True(diff != new DiffIndex(different));
        Assert.False(ic.Equals((object)diff));
    }

    [Fact]
    public void Indices_OrderLikeIntegersAcrossInt32Range()
    {
        int[] values = { int.MaxValue, -1, 0, int.MinValue, 13, -2, int.MaxValue - 1, int.MinValue + 1 };
        Assert.Equal(values.Order(), values.Select(value => new IcIndex(value)).Order().Select(index => index.Value));
        Assert.Equal(values.Order(), values.Select(value => new DiffIndex(value)).Order().Select(index => index.Value));

        foreach (int left in values)
        {
            foreach (int right in values)
            {
                var leftIc = new IcIndex(left);
                var rightIc = new IcIndex(right);
                var leftDiff = new DiffIndex(left);
                var rightDiff = new DiffIndex(right);
                Assert.Equal(left.CompareTo(right), leftIc.CompareTo(rightIc));
                Assert.Equal(left.CompareTo(right), leftDiff.CompareTo(rightDiff));
                Assert.Equal(left < right, leftIc < rightIc);
                Assert.Equal(left > right, leftIc > rightIc);
                Assert.Equal(left <= right, leftIc <= rightIc);
                Assert.Equal(left >= right, leftIc >= rightIc);
                Assert.Equal(left < right, leftDiff < rightDiff);
                Assert.Equal(left > right, leftDiff > rightDiff);
                Assert.Equal(left <= right, leftDiff <= rightDiff);
                Assert.Equal(left >= right, leftDiff >= rightDiff);
            }
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 11)]
    [InlineData(-1, -2)]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void AssignMapping_TypedEntryReplacesPairAndPreservesOtherState(int icIndex, int diffIndex)
    {
        // The integer entry is covered by DiffIndexMapperTransitionTests; this pins the typed entry.
        var typed = CreatePad();
        var polygon = typed.Polygon;
        var otherState = (typed.Row, typed.Col, typed.Index, typed.RegularPadId, typed.Bounds, typed.Area, typed.Centroid,
            typed.MatchedCadPadId, typed.MatchScore, typed.Freeform);
        typed.AssignMapping(new IcIndex(9), new DiffIndex(8));

        typed.AssignMapping(new IcIndex(icIndex), new DiffIndex(diffIndex));

        Assert.Equal((icIndex, diffIndex, diffIndex), (typed.IcIndex, typed.DiffIndex, typed.RegularFwDiffIndex));
        Assert.Same(polygon, typed.Polygon);
        Assert.Equal(otherState, (typed.Row, typed.Col, typed.Index, typed.RegularPadId, typed.Bounds, typed.Area, typed.Centroid,
            typed.MatchedCadPadId, typed.MatchScore, typed.Freeform));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LegacySettersAndAlias_LastWriteWinsAroundTypedAssignment(bool aliasFirst)
    {
        var pad = CreatePad();
        var otherState = (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform);
        pad.AssignMapping(new IcIndex(int.MinValue), new DiffIndex(int.MaxValue));
        pad.IcIndex = -4;
        Assert.Equal((-4, int.MaxValue), (pad.IcIndex, pad.DiffIndex));

        if (aliasFirst)
        {
            pad.RegularFwDiffIndex = -2;
            Assert.Equal((-4, -2), (pad.IcIndex, pad.DiffIndex));
            pad.DiffIndex = 7;
        }
        else
        {
            pad.DiffIndex = -2;
            Assert.Equal((-4, -2), (pad.IcIndex, pad.RegularFwDiffIndex));
            pad.RegularFwDiffIndex = 7;
        }

        Assert.Equal((-4, 7, 7), (pad.IcIndex, pad.DiffIndex, pad.RegularFwDiffIndex));
        pad.AssignMapping(3, -9);
        Assert.Equal((3, -9, -9), (pad.IcIndex, pad.DiffIndex, pad.RegularFwDiffIndex));
        pad.AssignMapping(new IcIndex(4), new DiffIndex(11));
        Assert.Equal((4, 11, 11), (pad.IcIndex, pad.DiffIndex, pad.RegularFwDiffIndex));
        Assert.Equal(otherState, (pad.MatchedCadPadId, pad.MatchScore, pad.Freeform));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -2)]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void MappingJson_PreservesNumericPropertiesAndExistingShape(int icIndex, int diffIndex)
    {
        var pad = CreatePad();
        pad.AssignMapping(new IcIndex(icIndex), new DiffIndex(diffIndex));
        var json = JsonSerializer.SerializeToElement(pad);

        Assert.Equal(JsonValueKind.Number, json.GetProperty("IcIndex").ValueKind);
        Assert.Equal(JsonValueKind.Number, json.GetProperty("DiffIndex").ValueKind);
        Assert.Equal(JsonValueKind.Number, json.GetProperty("RegularFwDiffIndex").ValueKind);
        Assert.Equal(icIndex, json.GetProperty("IcIndex").GetInt32());
        Assert.Equal(diffIndex, json.GetProperty("DiffIndex").GetInt32());
        Assert.Equal(diffIndex, json.GetProperty("RegularFwDiffIndex").GetInt32());
        string[] expectedProperties =
        {
            "Row", "Col", "Index", "RegularPadId", "Polygon", "Bounds", "Area", "Centroid",
            "IcIndex", "DiffIndex", "RegularFwDiffIndex", "MatchedCadPadId", "MatchScore", "Freeform",
        };
        Assert.Equal(expectedProperties.Order(StringComparer.Ordinal),
            json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ApplyMapping_SyntheticNotchCsvMatchesLegacyIntegerAssignment()
    {
        var mapped = TestGeometryFactory.CreateRegularGrid(rows: 2, cols: 5);
        var legacy = TestGeometryFactory.CreateRegularGrid(rows: 2, cols: 5);
        var cad = new CadPadSet(new[]
        {
            TestGeometryFactory.CreateCadPad(100, "Synthetic", 0.1, 0.1, 1.4, 0.9),
            TestGeometryFactory.CreateCadPad(101, "Synthetic", 2.1, 1.1, 3.4, 1.9),
        });
        foreach (var grid in new[] { mapped, legacy })
        {
            foreach (int index in new[] { 0, 7 })
            {
                grid.Pads[index].MatchedCadPadId = index == 0 ? 100 : 101;
                grid.Pads[index].MatchScore = 0.75;
                grid.Pads[index].Freeform = FreeformType.XWay;
            }
        }

        var settings = new ProjectSettings
        {
            Grid = new GridSettings
            {
                XChannels = 5,
                YChannels = 2,
                CascadeNum = 2,
                PerIcXChannels = new List<int> { 2, 3 },
                ScanOrder = ScanOrder.RightToLeft_TopToBottom,
            },
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };
        DiffIndexMapper.ApplyMapping(mapped, settings.Grid);

        // Pins the mapper output through the CSV: the integers are the expected RightToLeft_TopToBottom
        // mapping for per-IC widths {2, 3} (see DiffIndexMapperTransitionTests), not a run of older code.
        int[] icIndices = { 0, 0, 1, 1, 1, 0, 0, 1, 1, 1 };
        int[] diffIndices = { 3, 2, 5, 4, 3, 1, 0, 2, 1, 0 };
        for (int index = 0; index < legacy.Pads.Count; index++)
        {
            legacy.Pads[index].AssignMapping(icIndices[index], diffIndices[index]);
        }

        var mappedTable = new NotchTableGenerator().Generate(cad, mapped, settings);
        var legacyTable = new NotchTableGenerator().Generate(cad, legacy, settings);
        Assert.Equal(2, mappedTable.Rows.Count);
        Assert.Contains(mappedTable.Rows, row => row.IcIndex == 0);
        Assert.Contains(mappedTable.Rows, row => row.IcIndex == 1);
        Assert.Equal(NotchTableExporter.ExportAsCsv(legacyTable), NotchTableExporter.ExportAsCsv(mappedTable));
    }

    private static RegularPad CreatePad()
    {
        return new RegularPad(3, 2, 17, TestGeometryFactory.CreateRect(2, 3, 4, 5))
        {
            MatchedCadPadId = 42,
            MatchScore = 0.625,
            Freeform = FreeformType.XYWay,
        };
    }
}
