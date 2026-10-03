using System.Reflection;
using System.Text.Json;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("RuntimeQueryUseCaseSerial")]
public sealed partial class RuntimeQueryUseCaseTests
{
    private static readonly double[] ValidationGridXEdges = [0.0, 10.0, 20.0];
    private static readonly double[] ValidationGridYEdges = [0.0, 10.0];

    private static ShellViewModel BuildShellWithNotchValidationTable(NotchTable table)
    {
        var shell = new ShellViewModel();
        var vm = shell.FreeformHelper;
        SetPrivateField(vm, "_grid", BuildValidationGrid());
        SetPrivateField(vm, "_lastGeneratedNotchTable", table);
        return shell;
    }

    private static SimulationWorkspaceSession BuildSimulationSession()
    {
        const int nullDiffValue = 65535;
        var grid = new RegularGrid(
            rows: 1,
            cols: 2,
            xEdges: ValidationGridXEdges,
            yEdges: ValidationGridYEdges,
            pads: new[]
            {
                CreateRegularPad(regularPadId: 0, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, icIndex: 0, diffIndex: 10),
                CreateRegularPad(regularPadId: 1, row: 0, col: 1, minX: 10, minY: 0, maxX: 20, maxY: 10, icIndex: 0, diffIndex: 11),
            });
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, nullDiffValue, 0, 0),
                comment: "sample")
        });

        return new SimulationWorkspaceSession(
            grid,
            table,
            nullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadOutputFwDiffAssignmentDecisionByCadId: new Dictionary<int, CadOutputFwDiffAssignmentDecision>
            {
                [313] = new CadOutputFwDiffAssignmentDecision(
                    CadPadId: 313,
                    OrderedCadIndex: 0,
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    RawSeed: new CadBestMatchSeed(0, 0, 10, 0.95, 0.93),
                    MaskedSeed: new CadBestMatchSeed(0, 0, 10, 0.95, 0.93),
                    GeometryCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(0, 0, 10, 0.95, 0.93),
                    },
                    CsvConfirmedCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(0, 0, 10, 0.95, 0.93),
                    },
                    CurrentPrimaryDiffIndex: 10,
                    PassiveCompensationDiffIndex: null,
                    RepairSuggestionDiffIndex: null,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.NeedsReview,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.Seed,
                    Confidence: 1.0),
            });
    }

    private static NotchTable BuildTypedValidationTable()
    {
        const int nullDiff = 65535;
        return new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 100,
                cadPadId: 4200,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 10,
                    CombinePercent: 120,
                    TargetDiffIndex1: 12,
                    TargetRatioPercent1: 40,
                    TargetDiffIndex2: nullDiff,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "typed-main"),
            new(
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 101,
                cadPadId: 4201,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 12,
                    CombinePercent: 100,
                    TargetDiffIndex1: 10,
                    TargetRatioPercent1: 15,
                    TargetDiffIndex2: nullDiff,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "typed-incoming"),
        });
    }

    private static NotchTable BuildLegacyValidationTable()
    {
        const int nullDiff = 65535;
        return new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 100,
                cadPadId: 4200,
                values: new[] { 10, 120, 12, 40, nullDiff, 0, 0, 999 },
                comment: "legacy-main"),
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 101,
                cadPadId: 4201,
                values: new[] { 12, 100, 10, 15, nullDiff, 0, 0, 998 },
                comment: "legacy-incoming"),
        });
    }

    private static RegularGrid BuildValidationGrid()
    {
        var pad100 = CreateRegularPad(regularPadId: 100, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, icIndex: 0, diffIndex: 10);
        var pad101 = CreateRegularPad(regularPadId: 101, row: 0, col: 1, minX: 10, minY: 0, maxX: 20, maxY: 10, icIndex: 0, diffIndex: 12);
        return new RegularGrid(
            rows: 1,
            cols: 2,
            xEdges: ValidationGridXEdges,
            yEdges: ValidationGridYEdges,
            pads: new[] { pad100, pad101 });
    }

    private static RegularPad CreateRegularPad(
        int regularPadId,
        int row,
        int col,
        double minX,
        double minY,
        double maxX,
        double maxY,
        int icIndex,
        int diffIndex)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
        var pad = new RegularPad(row, col, regularPadId, polygon)
        {
            IcIndex = icIndex,
            DiffIndex = diffIndex
        };
        return pad;
    }

    private static CadPad CreateCadPad(int id, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
        return new CadPad(id, $"CAD{id}", "L1", polygon);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static Dictionary<TKey, TValue> GetPrivateDictionary<TKey, TValue>(object target, string fieldName)
        where TKey : notnull
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Dictionary<TKey, TValue>>(field!.GetValue(target));
    }

    private static void AssertTargetPayloadMatches(JsonElement payload, NotchV22TargetAllocation expected)
    {
        Assert.Equal(expected.IcIndex, payload.GetProperty("icIndex").GetInt32());
        Assert.Equal(expected.DiffIndex, payload.GetProperty("diffIndex").GetInt32());
        Assert.Equal(expected.EffectiveArea, payload.GetProperty("effectiveArea").GetDouble(), 6);
        Assert.Equal(expected.Ratio, payload.GetProperty("ratio").GetDouble(), 6);
        Assert.Equal(expected.RatioPercentRounded, payload.GetProperty("ratioPercentRounded").GetInt32());
        Assert.Equal(expected.PassesStrictThreshold, payload.GetProperty("passesStrictThreshold").GetBoolean());
        Assert.Equal(expected.IsAnchorDiff, payload.GetProperty("isAnchorDiff").GetBoolean());
        Assert.Equal(expected.ToFullAppliedRegularCount, payload.GetProperty("toFullAppliedRegularCount").GetInt32());
        Assert.Equal(expected.RegularCount, payload.GetProperty("regularCount").GetInt32());

        var regularPadIds = payload.GetProperty("regularPadIds");
        Assert.Equal(expected.RegularPadIds.Count, regularPadIds.GetArrayLength());
        for (var i = 0; i < expected.RegularPadIds.Count; i++)
        {
            Assert.Equal(expected.RegularPadIds[i], regularPadIds[i].GetInt32());
        }
    }

    private static void AssertStagePayloadMatchesPolygons(JsonElement payload, string expectedName, IReadOnlyList<Polygon2> polygons)
    {
        Assert.Equal(expectedName, payload.GetProperty("name").GetString());
        Assert.Equal(polygons.Count, payload.GetProperty("totalPolygons").GetInt32());
        Assert.Equal(polygons.Count, payload.GetProperty("returnedPolygons").GetInt32());

        var payloadPolygons = payload.GetProperty("polygons");
        Assert.Equal(polygons.Count, payloadPolygons.GetArrayLength());
        for (var i = 0; i < polygons.Count; i++)
        {
            var polygonPayload = payloadPolygons[i];
            Assert.Equal(i, polygonPayload.GetProperty("index").GetInt32());
            AssertBoundsMatch(polygonPayload.GetProperty("bounds"), polygons[i]);
        }
    }

    private static void AssertBoundsMatch(JsonElement payload, Polygon2 polygon)
    {
        Assert.Equal(polygon.Bounds.MinX, payload.GetProperty("minX").GetDouble(), 6);
        Assert.Equal(polygon.Bounds.MinY, payload.GetProperty("minY").GetDouble(), 6);
        Assert.Equal(polygon.Bounds.MaxX, payload.GetProperty("maxX").GetDouble(), 6);
        Assert.Equal(polygon.Bounds.MaxY, payload.GetProperty("maxY").GetDouble(), 6);
    }

    private static JsonElement SerializeToRootElement(object? data)
    {
        var json = JsonSerializer.Serialize(data, RuntimeQueryProtocol.CompactJsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

[CollectionDefinition("RuntimeQueryUseCaseSerial", DisableParallelization = true)]
public sealed class RuntimeQueryUseCaseSerialCollectionDefinition;
