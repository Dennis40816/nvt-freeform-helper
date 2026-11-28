using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Presentation;

namespace FreeformHelper.UI.ViewModels;

public sealed class NotchDetailViewModel : ObservableObject
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
        double combinedRatio,
        string toRegularRatioText,
        string toFullRatioText,
        string combinedRatioText,
        IReadOnlyList<Polygon2> toFullPolygons)
    {
        CadPad = cadPad;
        DxfIndex = dxfIndex;
        FreeformType = freeformType;
        Allocations = allocations;
        _basePreviewRegularPads = previewRegularPads;
        PreviewRegularPads = previewRegularPads;
        ThresholdQ7 = thresholdQ7;
        MaxAllocationQ7 = maxAllocationQ7;
        ThresholdSummary = thresholdSummary;
        CombinedRatio = combinedRatio;
        ToRegularRatioText = toRegularRatioText;
        ToFullRatioText = toFullRatioText;
        CombinedRatioText = combinedRatioText;
        ToFullPolygons = toFullPolygons;
    }

    public CadPad CadPad { get; }
    public int? DxfIndex { get; }
    public FreeformType FreeformType { get; }
    public IReadOnlyList<NotchDetailAllocation> Allocations { get; }
    public IReadOnlyList<RegularPad> PreviewRegularPads { get; private set; }
    public int ThresholdQ7 { get; }
    public int MaxAllocationQ7 { get; }
    public string ThresholdSummary { get; }
    public double CombinedRatio { get; }
    public string ToRegularRatioText { get; }
    public string ToFullRatioText { get; }
    public string CombinedRatioText { get; }
    public IReadOnlyList<Polygon2> ToFullPolygons { get; }
    // UI thread only; set once by ShowExplanation, then read by the view.
    private readonly IReadOnlyList<RegularPad> _basePreviewRegularPads;
    private NotchDetailExplanation _explanation = NotchDetailExplanation.WithoutRows("Working out rows...");

    public ImmutableArray<NotchSourcePresentation> Sources => _explanation.Sources;
    public ImmutableArray<CadPad> NeighborCadPads => _explanation.NeighborCadPads;
    public string ExplanationMessage => _explanation.Message;
    public bool HasSources => Sources.Length > 0;
    public bool HasNoSources => !HasSources;
    public bool HasSourceSelector => Sources.Length > 1;
    public bool HasCompetition => SelectedSource?.HasCompetition == true;

    public void ShowExplanation(NotchDetailExplanation explanation)
    {
        _explanation = explanation;
        // The preview also needs every cell the rows send a share to.
        PreviewRegularPads = _basePreviewRegularPads
            .Concat(explanation.CellPads.Where(cell => _basePreviewRegularPads.All(pad => pad.RegularPadId != cell.RegularPadId)))
            .ToImmutableArray();
        OnPropertyChanged(nameof(PreviewRegularPads));
        OnPropertyChanged(nameof(Sources));
        OnPropertyChanged(nameof(NeighborCadPads));
        OnPropertyChanged(nameof(ExplanationMessage));
        OnPropertyChanged(nameof(HasSources));
        OnPropertyChanged(nameof(HasNoSources));
        OnPropertyChanged(nameof(HasSourceSelector));
        SelectedSource = Sources.Length > 0 ? Sources[0] : null;
    }

    // UI thread only; this is the sole state changed by the source selector.
    private NotchSourcePresentation? _selectedSource;
    public NotchSourcePresentation? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                OnPropertyChanged(nameof(HasCompetition));
            }
        }
    }

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
