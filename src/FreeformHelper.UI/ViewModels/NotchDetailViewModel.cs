using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed class NotchDetailViewModel
{
    public NotchDetailViewModel(
        CadPad cadPad,
        int? dxfIndex,
        FreeformType freeformType,
        IReadOnlyList<NotchDetailAllocation> allocations,
        IReadOnlyList<RegularPad> previewRegularPads,
        int thresholdQ7,
        int maxAllocationQ7,
        string thresholdSummary,
        double toRegularRatio,
        double toFullRatio,
        double combinedRatio,
        bool isToFullEnabled,
        string toRegularRatioText,
        string toFullRatioText,
        string combinedRatioText,
        IReadOnlyList<Polygon2> toFullSeedPolygons,
        IReadOnlyList<Polygon2> toFullPolygons,
        IReadOnlyList<NotchV22RegularDebugInfo> regularDebugInfos)
    {
        CadPad = cadPad;
        DxfIndex = dxfIndex;
        FreeformType = freeformType;
        Allocations = allocations;
        PreviewRegularPads = previewRegularPads;
        ThresholdQ7 = thresholdQ7;
        MaxAllocationQ7 = maxAllocationQ7;
        ThresholdSummary = thresholdSummary;
        ToRegularRatio = toRegularRatio;
        ToFullRatio = toFullRatio;
        CombinedRatio = combinedRatio;
        IsToFullEnabled = isToFullEnabled;
        ToRegularRatioText = toRegularRatioText;
        ToFullRatioText = toFullRatioText;
        CombinedRatioText = combinedRatioText;
        ToFullSeedPolygons = toFullSeedPolygons;
        ToFullPolygons = toFullPolygons;
        RegularDebugInfos = regularDebugInfos;
    }

    public CadPad CadPad { get; }
    public int? DxfIndex { get; }
    public FreeformType FreeformType { get; }
    public IReadOnlyList<NotchDetailAllocation> Allocations { get; }
    public IReadOnlyList<RegularPad> PreviewRegularPads { get; }
    public int ThresholdQ7 { get; }
    public int MaxAllocationQ7 { get; }
    public string ThresholdSummary { get; }
    public double ToRegularRatio { get; }
    public double ToFullRatio { get; }
    public double CombinedRatio { get; }
    public bool IsToFullEnabled { get; }
    public string ToRegularRatioText { get; }
    public string ToFullRatioText { get; }
    public string CombinedRatioText { get; }
    public IReadOnlyList<Polygon2> ToFullSeedPolygons { get; }
    public IReadOnlyList<Polygon2> ToFullPolygons { get; }
    public IReadOnlyList<NotchV22RegularDebugInfo> RegularDebugInfos { get; }

    public string CadTitle => DxfIndex.HasValue ? $"CAD idx {DxfIndex.Value} (CAD id {CadPad.Id})" : $"CAD id {CadPad.Id}";
    public string CadLayer => string.IsNullOrWhiteSpace(CadPad.Layer) ? "Layer: -" : $"Layer: {CadPad.Layer}";
    public string FreeformLabel => FreeformType == FreeformType.None ? "Freeform: None" : $"Freeform: {FreeformType}";
    public bool IsBelowThreshold => ThresholdQ7 > 0 && MaxAllocationQ7 < ThresholdQ7;
}

public sealed class NotchDetailAllocation
{
    public NotchDetailAllocation(int regularIndex, int row, int col, int icIndex, int q7, double ratio)
    {
        RegularIndex = regularIndex;
        Row = row;
        Col = col;
        IcIndex = icIndex;
        Q7 = q7;
        Ratio = ratio;
    }

    public int RegularIndex { get; }
    public int Row { get; }
    public int Col { get; }
    public int IcIndex { get; }
    public int Q7 { get; }
    public double Ratio { get; }

    public string RegularLabel => $"reg[{RegularIndex}]";
    public string GridLabel => $"IC{IcIndex} R{Row} C{Col}";
    public string Q7Text => $"{Q7}/128";
    public string RatioText => $"{Ratio:P1}";
}
