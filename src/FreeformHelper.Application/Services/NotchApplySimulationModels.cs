using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public enum NotchApplySimulationAggregationMode
{
    SingleFrame = 0,
    Mean = 1,
}

public enum NotchApplySimulationDuplicateDiffResolutionStrategy
{
    MergeSumActiveRegularPads = 0,
}

public sealed record NotchApplySimulationRequest(
    RegularGrid Grid,
    IReadOnlySet<int> ActiveRegularPadIds,
    IReadOnlyList<DiffFrameGridProjectionResult> FrameProjections,
    NotchTable Table,
    NotchAlgorithmVersion Version,
    int NullDiffValue,
    NotchApplySimulationAggregationMode AggregationMode = NotchApplySimulationAggregationMode.SingleFrame,
    int SelectedFrameIndex = 0,
    NotchComputationMode ComputationMode = NotchComputationMode.CadAllocation);

public sealed record NotchApplySimulationDiffCell(
    int RegularPadId,
    int RegularRow,
    int RegularCol,
    int IcIndex,
    int DiffIndex,
    double BeforeValue,
    double AfterValue)
{
    public double DeltaValue => AfterValue - BeforeValue;
}

public sealed record NotchApplySimulationLeg(
    int TargetDiffIndex,
    int RatioPercent,
    double DeltaValue);

public sealed record NotchApplySimulationAction(
    NotchAlgorithmVersion Version,
    int IcIndex,
    int AnchorDiffIndex,
    int RegularPadId,
    int? CadPadId,
    IReadOnlyList<int> SourceRowNumbers,
    int CombinePercent,
    int SourceRetainedPercent,
    double SourceBeforeValue,
    double SourceAfterValue,
    IReadOnlyList<NotchApplySimulationLeg> Legs)
{
    public double OutputTotalValue => SourceAfterValue + Legs.Sum(static leg => leg.DeltaValue);
    public bool IsNoOp => CombinePercent == 100 && SourceRetainedPercent == 100 && Legs.Count == 0;
    public bool IsLegacyApproximation => Version == NotchAlgorithmVersion.V21;
}

public sealed record NotchApplySimulationHistogramBin(
    int Index,
    string Label,
    double StartInclusive,
    double EndInclusive,
    int Count);

public sealed record NotchApplySimulationHistogram(
    string Title,
    IReadOnlyList<NotchApplySimulationHistogramBin> Bins)
{
    public int MaxCount => Bins.Count == 0 ? 0 : Bins.Max(static bin => bin.Count);
    public bool HasBins => Bins.Count > 0;
}

public sealed record NotchApplySimulationHistogramSet(
    NotchApplySimulationHistogram Before,
    NotchApplySimulationHistogram After,
    NotchApplySimulationHistogram Delta);

public sealed record NotchApplySimulationHeatmapCell(
    int RegularPadId,
    int RegularRow,
    int RegularCol,
    int IcIndex,
    int DiffIndex,
    double DeltaValue);

public sealed record NotchApplySimulationHotspot(
    int Rank,
    int RegularPadId,
    int RegularRow,
    int RegularCol,
    int IcIndex,
    int DiffIndex,
    double BeforeValue,
    double AfterValue,
    double DeltaValue);

public sealed record NotchApplySimulationHeatmap(
    int GridRows,
    int GridCols,
    double MaxAbsDelta,
    IReadOnlyList<NotchApplySimulationHeatmapCell> Cells,
    IReadOnlyList<NotchApplySimulationHotspot> Hotspots)
{
    public bool HasCells => Cells.Count > 0;
    public bool HasHotspots => Hotspots.Count > 0;
}

public sealed record NotchApplySimulationDuplicateDiffResolution(
    int IcIndex,
    int DiffIndex,
    int PrimaryRegularPadId,
    IReadOnlyList<int> SuppressedRegularPadIds,
    NotchApplySimulationDuplicateDiffResolutionStrategy Strategy);

public sealed record NotchApplySimulationDiffIdentityContract(
    NotchApplySimulationDuplicateDiffResolutionStrategy DuplicateResolutionStrategy,
    IReadOnlyList<NotchApplySimulationDuplicateDiffResolution> DuplicateResolutions)
{
    public static NotchApplySimulationDiffIdentityContract Empty { get; } = new(
        NotchApplySimulationDuplicateDiffResolutionStrategy.MergeSumActiveRegularPads,
        Array.Empty<NotchApplySimulationDuplicateDiffResolution>());

    public int DuplicateResolutionCount => DuplicateResolutions.Count;
    public bool HasDuplicateResolutions => DuplicateResolutions.Count > 0;
}

public sealed record NotchApplySimulationResult(
    bool IsSupported,
    IReadOnlyList<string> Diagnostics,
    NotchAlgorithmVersion Version,
    string ContractText,
    NotchApplySimulationAggregationMode AggregationMode,
    int ConsumedFrameCount,
    IReadOnlyList<NotchApplySimulationDiffCell> Cells,
    IReadOnlyList<NotchApplySimulationAction> Actions,
    NotchApplySimulationDiffIdentityContract DiffIdentityContract,
    NotchApplySimulationHistogramSet Histograms,
    NotchApplySimulationHeatmap Heatmap)
{
    public string? PrimaryDiagnostic => Diagnostics.Count > 0 ? Diagnostics[0] : null;
    public double BeforeTotal => Cells.Sum(static cell => cell.BeforeValue);
    public double AfterTotal => Cells.Sum(static cell => cell.AfterValue);
    public double DeltaTotal => AfterTotal - BeforeTotal;

    public static NotchApplySimulationResult Unsupported(
        NotchAlgorithmVersion version,
        NotchApplySimulationAggregationMode aggregationMode,
        int consumedFrameCount,
        params string[] diagnostics)
    {
        return new NotchApplySimulationResult(
            IsSupported: false,
            Diagnostics: diagnostics,
            Version: version,
            ContractText: string.Empty,
            AggregationMode: aggregationMode,
            ConsumedFrameCount: consumedFrameCount,
            Cells: Array.Empty<NotchApplySimulationDiffCell>(),
            Actions: Array.Empty<NotchApplySimulationAction>(),
            DiffIdentityContract: NotchApplySimulationDiffIdentityContract.Empty,
            Histograms: new NotchApplySimulationHistogramSet(
                new NotchApplySimulationHistogram("Before", Array.Empty<NotchApplySimulationHistogramBin>()),
                new NotchApplySimulationHistogram("After", Array.Empty<NotchApplySimulationHistogramBin>()),
                new NotchApplySimulationHistogram("Delta", Array.Empty<NotchApplySimulationHistogramBin>())),
            Heatmap: new NotchApplySimulationHeatmap(
                GridRows: 0,
                GridCols: 0,
                MaxAbsDelta: 0d,
                Cells: Array.Empty<NotchApplySimulationHeatmapCell>(),
                Hotspots: Array.Empty<NotchApplySimulationHotspot>()));
    }
}
