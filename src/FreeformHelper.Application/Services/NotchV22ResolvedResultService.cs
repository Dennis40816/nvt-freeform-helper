using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Resolves the single final Notch 2.2 result model used by UI/query layers.
/// </summary>
public sealed class NotchV22ResolvedResultService
{
    private readonly NotchV22FinalOutlineService _finalOutlineService = new();

    public NotchV22ResolvedResult Build(
        CadPad cadPad,
        NotchV22CompensationResult compensation,
        double strictOverlapRatio,
        int? anchorIcIndex = null,
        int? anchorDiffIndex = null,
        NotchV22TargetAllocationAreaMode allocationAreaMode = NotchV22TargetAllocationAreaMode.SourceAreaDominant,
        NotchV22ResolvedResultIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(cadPad);
        ArgumentNullException.ThrowIfNull(compensation);

        var stage1SeedPolygons = compensation.IsToFullEnabled
            ? compensation.ToFullSeedPolygons
            : Array.Empty<Polygon2>();
        var stage2CandidatePolygons = compensation.IsToFullEnabled
            ? compensation.ToFullCandidatePolygons
            : Array.Empty<Polygon2>();
        var stage3FinalOutlinePolygons = compensation.IsToFullEnabled
            ? _finalOutlineService.Build(cadPad, compensation.ToFullPolygons, compensation.IsToFullEnabled)
            : Array.Empty<Polygon2>();
        var targetAllocation = NotchV22TargetAllocationService.Build(
            cadPad,
            compensation,
            strictOverlapRatio,
            anchorIcIndex,
            anchorDiffIndex,
            allocationAreaMode);

        return new NotchV22ResolvedResult(
            compensation,
            stage1SeedPolygons,
            stage2CandidatePolygons,
            stage3FinalOutlinePolygons,
            targetAllocation)
        {
            Identity = identity,
        };
    }

    public static NotchV22ResolvedResultIdentity CreateIdentity(
        CadPad cadPad, RegularGrid grid, IReadOnlyList<CadPad> cadPool,
        IReadOnlySet<int>? activeRegularPadIds,
        NotchCompensationModel compensationModel, bool enableToRegular, bool enableToFull,
        bool enableToFullRuleEngine, bool enableToFullRuleTrace,
        bool enableBoundaryVirtualAreaCap,
        double boundaryVirtualAreaCapRatio, double strictOverlapRatio,
        int? anchorIcIndex, int? anchorDiffIndex,
        NotchV22TargetAllocationAreaMode allocationAreaMode) =>
        new(
            $"{cadPad.Id}:{CadPadGeometrySignature.BuildExact(cadPad.Polygon)}",
            BuildGridSignature(grid),
            HashSignature(string.Join("|", cadPool.OrderBy(static pad => pad.Id).Select(static pad =>
                $"{pad.Id}:{CadPadGeometrySignature.BuildExact(pad.Polygon)}"))),
            activeRegularPadIds is null
                ? "*"
                : string.Join(",", activeRegularPadIds.OrderBy(static id => id)),
            compensationModel,
            enableToRegular,
            enableToFull,
            enableToFullRuleEngine,
            enableToFullRuleTrace,
            enableBoundaryVirtualAreaCap,
            boundaryVirtualAreaCapRatio,
            NotchV22CompensationService.NormalizeStrictOverlapRatio(strictOverlapRatio),
            anchorIcIndex,
            anchorDiffIndex,
            allocationAreaMode);

    private static string BuildGridSignature(RegularGrid grid)
    {
        var xEdges = string.Join(",", grid.XEdges.Select(static value => value.ToString("R", CultureInfo.InvariantCulture)));
        var yEdges = string.Join(",", grid.YEdges.Select(static value => value.ToString("R", CultureInfo.InvariantCulture)));
        var pads = string.Join("|", grid.Pads.OrderBy(static pad => pad.RegularPadId).Select(static pad =>
            FormattableString.Invariant(
                $"{pad.RegularPadId}:{pad.Row}:{pad.Col}:{pad.IcIndex}:{pad.DiffIndex}:{pad.MatchedCadPadId}:{(int)pad.Freeform}:{pad.MatchScore:R}:{CadPadGeometrySignature.BuildExact(pad.Polygon)}")));
        return HashSignature($"{grid.Rows}:{grid.Cols};X={xEdges};Y={yEdges};P={pads}");
    }

    private static string HashSignature(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record NotchV22ResolvedResult(
    NotchV22CompensationResult Compensation,
    IReadOnlyList<Polygon2> Stage1SeedPolygons,
    IReadOnlyList<Polygon2> Stage2CandidatePolygons,
    IReadOnlyList<Polygon2> Stage3FinalOutlinePolygons,
    NotchV22TargetAllocationSummary TargetAllocation)
{
    public NotchV22ResolvedResultIdentity? Identity { get; init; }
}

public sealed record NotchV22ResolvedResultIdentity(
    string CadSignature, string GridSignature,
    string CadPoolSignature, string ActiveRegularPadIdsSignature,
    NotchCompensationModel CompensationModel, bool EnableToRegular, bool EnableToFull,
    bool EnableToFullRuleEngine, bool EnableToFullRuleTrace,
    bool EnableBoundaryVirtualAreaCap,
    double BoundaryVirtualAreaCapRatio, double StrictOverlapRatio,
    int? AnchorIcIndex, int? AnchorDiffIndex,
    NotchV22TargetAllocationAreaMode AllocationAreaMode);
