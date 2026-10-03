using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.ViewModels;

public enum NotchApplySimulationCanvasViewMode
{
    Before = 0,
    After = 1,
    Delta = 2,
    ChangedOnly = 3,
}

public enum NotchApplySimulationCanvasColorMode
{
    Auto = 0,
    Threshold = 1,
}

public readonly record struct NotchApplySimulationCanvasViewOption(
    NotchApplySimulationCanvasViewMode Value,
    string Display);

public readonly record struct NotchApplySimulationCanvasColorOption(
    NotchApplySimulationCanvasColorMode Value,
    string Display);

public readonly record struct NotchApplySimulationVersionOption(
    NotchAlgorithmVersion Value,
    string Display);

public readonly record struct NotchApplySimulationAreaOption(
    int? IcIndex,
    string Display);

public readonly record struct NotchApplySimulationFrameOption(
    int Value,
    string Display);

public sealed record NotchApplySimulationImpactItemViewModel(
    string RoleText,
    string SummaryText,
    string DetailText,
    string DeltaText,
    string SourceRowsText);

public sealed record SimulationSafetyRiskDiffViewModel(
    int Rank,
    int RegularPadId,
    int IcIndex,
    int DiffIndex,
    string LocationText,
    string AfterText,
    string DeltaText,
    string MarginText,
    string StatusText,
    bool IsViolation);

public sealed record SimulationSelectedFlowLegViewModel(
    string RoleText,
    string SummaryText,
    string DetailText,
    string DeltaText,
    string SourceRowsText);

public sealed partial class NotchApplySimulationSourceFileItemViewModel : ObservableObject
{
    public NotchApplySimulationSourceFileItemViewModel(
        string fileName,
        string summaryText,
        string shapeText,
        bool hasDiagnostics)
    {
        FileName = fileName;
        SummaryText = summaryText;
        ShapeText = shapeText;
        HasDiagnostics = hasDiagnostics;
    }

    public string FileName { get; }

    public string SummaryText { get; }

    public string ShapeText { get; }

    public bool HasDiagnostics { get; }
}

public sealed record NotchApplySimulationAaDisplayCell(
    int RegularPadId,
    int OriginalRow,
    int OriginalCol,
    int DisplayRow,
    int DisplayCol,
    int IcIndex,
    int DiffIndex,
    double BeforeValue,
    double AfterValue,
    double DeltaValue,
    double DisplayValue,
    bool IsChanged,
    string ValueText);
