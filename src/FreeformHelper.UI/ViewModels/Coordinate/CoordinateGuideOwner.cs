// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.UI.ViewModels.Coordinate;

/// <summary>Owns guide drafts and synchronous transitions. UI thread only.</summary>
internal sealed class CoordinateGuideOwner<TGuideBasisKind> where TGuideBasisKind : struct, Enum
{
    internal readonly record struct Seed(
        CoordinatePlannerGuideBasisOption SelectedGuideBasisOption,
        CoordinatePlannerGuideGenerationOption SelectedHorizontalGuideGenerationOption,
        CoordinatePlannerGuideGenerationOption SelectedVerticalGuideGenerationOption,
        decimal HorizontalGuideCount,
        decimal VerticalGuideCount,
        decimal HorizontalGuidePitch,
        decimal VerticalGuidePitch,
        decimal HorizontalGuideInset,
        decimal VerticalGuideInset,
        string HorizontalGuidePositionListText,
        string VerticalGuidePositionListText);

    private readonly Action _rebuild;
    private readonly Func<CoordinatePlannerGuideBasisMode, TGuideBasisKind> _toBasisKind;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private CoordinatePlannerGuideBasisOption _selectedGuideBasisOption;
    private CoordinatePlannerGuideGenerationOption _selectedHorizontalGuideGenerationOption;
    private CoordinatePlannerGuideGenerationOption _selectedVerticalGuideGenerationOption;
    private decimal _horizontalGuideCount;
    private decimal _verticalGuideCount;
    private decimal _horizontalGuidePitch;
    private decimal _verticalGuidePitch;
    private decimal _horizontalGuideInset;
    private decimal _verticalGuideInset;
    private string _horizontalGuidePositionListText;
    private string _verticalGuidePositionListText;

    internal CoordinateGuideOwner(
        Seed seed,
        Action rebuild,
        IReadOnlyList<CoordinatePlannerGuideBasisOption> basisOptions,
        IReadOnlyList<CoordinatePlannerGuideGenerationOption> generationOptions,
        Func<CoordinatePlannerGuideBasisMode, TGuideBasisKind> toBasisKind,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _rebuild = rebuild;
        _toBasisKind = toBasisKind;
        GuideBasisOptions = basisOptions;
        GuideGenerationOptions = generationOptions;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _selectedGuideBasisOption = seed.SelectedGuideBasisOption;
        _selectedHorizontalGuideGenerationOption = seed.SelectedHorizontalGuideGenerationOption;
        _selectedVerticalGuideGenerationOption = seed.SelectedVerticalGuideGenerationOption;
        _horizontalGuideCount = seed.HorizontalGuideCount;
        _verticalGuideCount = seed.VerticalGuideCount;
        _horizontalGuidePitch = seed.HorizontalGuidePitch;
        _verticalGuidePitch = seed.VerticalGuidePitch;
        _horizontalGuideInset = seed.HorizontalGuideInset;
        _verticalGuideInset = seed.VerticalGuideInset;
        _horizontalGuidePositionListText = seed.HorizontalGuidePositionListText;
        _verticalGuidePositionListText = seed.VerticalGuidePositionListText;
    }

    public IReadOnlyList<CoordinatePlannerGuideBasisOption> GuideBasisOptions { get; }
    public IReadOnlyList<CoordinatePlannerGuideGenerationOption> GuideGenerationOptions { get; }

    public CoordinatePlannerGuideBasisOption SelectedGuideBasisOption
    {
        get => _selectedGuideBasisOption;
        set => SetValue(ref _selectedGuideBasisOption, value, nameof(SelectedGuideBasisOption), _rebuild);
    }

    public CoordinatePlannerGuideGenerationOption SelectedHorizontalGuideGenerationOption
    {
        get => _selectedHorizontalGuideGenerationOption;
        set => SetValue(ref _selectedHorizontalGuideGenerationOption, value, nameof(SelectedHorizontalGuideGenerationOption), HorizontalModeChanged);
    }

    public CoordinatePlannerGuideGenerationOption SelectedVerticalGuideGenerationOption
    {
        get => _selectedVerticalGuideGenerationOption;
        set => SetValue(ref _selectedVerticalGuideGenerationOption, value, nameof(SelectedVerticalGuideGenerationOption), VerticalModeChanged);
    }

    public decimal HorizontalGuideCount
    {
        get => _horizontalGuideCount;
        set => SetValue(ref _horizontalGuideCount, value, nameof(HorizontalGuideCount), _rebuild);
    }

    public decimal VerticalGuideCount
    {
        get => _verticalGuideCount;
        set => SetValue(ref _verticalGuideCount, value, nameof(VerticalGuideCount), _rebuild);
    }

    public decimal HorizontalGuidePitch
    {
        get => _horizontalGuidePitch;
        set => SetValue(ref _horizontalGuidePitch, value, nameof(HorizontalGuidePitch), _rebuild);
    }

    public decimal VerticalGuidePitch
    {
        get => _verticalGuidePitch;
        set => SetValue(ref _verticalGuidePitch, value, nameof(VerticalGuidePitch), _rebuild);
    }

    public decimal HorizontalGuideInset
    {
        get => _horizontalGuideInset;
        set => SetValue(ref _horizontalGuideInset, value, nameof(HorizontalGuideInset), _rebuild);
    }

    public decimal VerticalGuideInset
    {
        get => _verticalGuideInset;
        set => SetValue(ref _verticalGuideInset, value, nameof(VerticalGuideInset), _rebuild);
    }

    public string HorizontalGuidePositionListText
    {
        get => _horizontalGuidePositionListText;
        set => SetValue(ref _horizontalGuidePositionListText, value, nameof(HorizontalGuidePositionListText), _rebuild);
    }

    public string VerticalGuidePositionListText
    {
        get => _verticalGuidePositionListText;
        set => SetValue(ref _verticalGuidePositionListText, value, nameof(VerticalGuidePositionListText), _rebuild);
    }

    public string GuideSummaryText =>
        $"Guides: H {BuildGuideModeSummaryText(SelectedHorizontalGuideGenerationOption.Mode, CoordinatePlannerRequestBuilder.NonNegativeInt(HorizontalGuideCount), HorizontalGuidePitch, HorizontalGuideInset, HorizontalGuidePositionListText)} / V {BuildGuideModeSummaryText(SelectedVerticalGuideGenerationOption.Mode, CoordinatePlannerRequestBuilder.NonNegativeInt(VerticalGuideCount), VerticalGuidePitch, VerticalGuideInset, VerticalGuidePositionListText)}";

    public bool IsHorizontalGuideCountMode => SelectedHorizontalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Count;

    public bool IsHorizontalGuidePitchMode => SelectedHorizontalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Pitch;

    public bool IsHorizontalGuideExplicitMode => SelectedHorizontalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.ExplicitPositions;

    public bool IsVerticalGuideCountMode => SelectedVerticalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Count;

    public bool IsVerticalGuidePitchMode => SelectedVerticalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Pitch;

    public bool IsVerticalGuideExplicitMode => SelectedVerticalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.ExplicitPositions;

    public TGuideBasisKind BasisKind => _toBasisKind(SelectedGuideBasisOption.Mode);

    public CoordinatePlannerGuideInput CaptureHorizontalInput() => new(
        HorizontalGuideCount, SelectedHorizontalGuideGenerationOption.Mode,
        HorizontalGuidePitch, HorizontalGuideInset, HorizontalGuidePositionListText);

    public CoordinatePlannerGuideInput CaptureVerticalInput() => new(
        VerticalGuideCount, SelectedVerticalGuideGenerationOption.Mode,
        VerticalGuidePitch, VerticalGuideInset, VerticalGuidePositionListText);

    private void HorizontalModeChanged()
    {
        _rebuild();
        _propertyChanged(nameof(IsHorizontalGuideCountMode));
        _propertyChanged(nameof(IsHorizontalGuidePitchMode));
        _propertyChanged(nameof(IsHorizontalGuideExplicitMode));
    }

    private void VerticalModeChanged()
    {
        _rebuild();
        _propertyChanged(nameof(IsVerticalGuideCountMode));
        _propertyChanged(nameof(IsVerticalGuidePitchMode));
        _propertyChanged(nameof(IsVerticalGuideExplicitMode));
    }

    private static string BuildGuideModeSummaryText(
        CoordinatePlannerGuideGenerationMode mode,
        int count,
        decimal pitch,
        decimal inset,
        string explicitPositions)
    {
        return mode switch
        {
            CoordinatePlannerGuideGenerationMode.Pitch => string.Format(
                CultureInfo.InvariantCulture,
                "pitch {0:0.###} mm, inset {1:0.###}",
                Math.Max(0m, pitch),
                Math.Max(0m, inset)),
            CoordinatePlannerGuideGenerationMode.ExplicitPositions => string.Format(
                CultureInfo.InvariantCulture,
                "{0} explicit, inset {1:0.###}",
                CoordinatePlannerRequestBuilder.ParsePositions(explicitPositions).Length,
                Math.Max(0m, inset)),
            _ => string.Format(
                CultureInfo.InvariantCulture,
                "{0} count, inset {1:0.###}",
                count,
                Math.Max(0m, inset)),
        };
    }

    private void SetValue<T>(ref T field, T value, string name, Action? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        _propertyChanging(name);
        field = value;
        changed?.Invoke();
        _propertyChanged(name);
    }
}
