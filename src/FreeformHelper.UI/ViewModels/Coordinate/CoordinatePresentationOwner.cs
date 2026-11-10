// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.UI.ViewModels.Coordinate;

/// <summary>Owns display choices and the single accepted render. UI thread only.</summary>
internal sealed class CoordinatePresentationOwner<TGuideBasisKind> where TGuideBasisKind : struct, Enum
{
    internal readonly record struct Seed(
        CoordinatePlannerCadLayerOption SelectedLayerOption,
        bool ShowRegular,
        bool ShowRegularAxisLabels,
        bool ShowCalibrationDetails,
        CoordinatePlannerOverlayModeOption SelectedOverlayModeOption);

    private readonly IReadOnlyList<CadPad> _cadPads;
    private readonly Func<CoordinatePlannerRequest, string?, TGuideBasisKind, CoordinatePlannerRenderState> _build;
    private readonly Func<IEnumerable<CoordinateArtifactRow>, CoordinateArtifactRow[]> _sortRows;
    private readonly Func<string?> _readSelectedArtifactKey;
    private readonly Action<string?> _restoreSelection;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private CoordinatePlannerCadLayerOption _selectedLayerOption;
    private bool _showRegular;
    private bool _showRegularAxisLabels;
    private bool _showCalibrationDetails;
    private CoordinatePlannerOverlayModeOption _selectedOverlayModeOption;
    private CoordinatePlannerRenderState _render;

    internal CoordinatePresentationOwner(
        Seed seed,
        CoordinatePlannerRenderState initialRender,
        IReadOnlyList<CadPad> cadPads,
        Func<CoordinatePlannerRequest, string?, TGuideBasisKind, CoordinatePlannerRenderState> build,
        Func<IEnumerable<CoordinateArtifactRow>, CoordinateArtifactRow[]> sortRows,
        Func<string?> readSelectedArtifactKey,
        Action<string?> restoreSelection,
        Action<string> setStatus,
        Action requestFit,
        IReadOnlyList<CoordinatePlannerCadLayerOption> layerOptions,
        IReadOnlyList<CoordinatePlannerOverlayModeOption> overlayModeOptions,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _render = initialRender;
        _cadPads = cadPads;
        _build = build;
        _sortRows = sortRows;
        _readSelectedArtifactKey = readSelectedArtifactKey;
        _restoreSelection = restoreSelection;
        _setStatus = setStatus;
        LayerOptions = layerOptions;
        OverlayModeOptions = overlayModeOptions;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _selectedLayerOption = seed.SelectedLayerOption;
        _showRegular = seed.ShowRegular;
        _showRegularAxisLabels = seed.ShowRegularAxisLabels;
        _showCalibrationDetails = seed.ShowCalibrationDetails;
        _selectedOverlayModeOption = seed.SelectedOverlayModeOption;
        FitCanvasCommand = new RelayCommand(requestFit);
    }

    public CoordinatePlannerRenderState Render => _render;
    public IRelayCommand FitCanvasCommand { get; }
    public IReadOnlyList<CoordinatePlannerCadLayerOption> LayerOptions { get; }
    public IReadOnlyList<CoordinatePlannerOverlayModeOption> OverlayModeOptions { get; }

    public CoordinatePlannerCadLayerOption SelectedLayerOption
    {
        get => _selectedLayerOption;
        set => SetValue(ref _selectedLayerOption, value, nameof(SelectedLayerOption), UpdateCadPadsForCanvas);
    }

    public bool ShowRegular
    {
        get => _showRegular;
        set => SetValue(ref _showRegular, value, nameof(ShowRegular));
    }

    public bool ShowRegularAxisLabels
    {
        get => _showRegularAxisLabels;
        set => SetValue(ref _showRegularAxisLabels, value, nameof(ShowRegularAxisLabels));
    }

    public bool ShowCalibrationDetails
    {
        get => _showCalibrationDetails;
        set => SetValue(ref _showCalibrationDetails, value, nameof(ShowCalibrationDetails));
    }

    public CoordinatePlannerOverlayModeOption SelectedOverlayModeOption
    {
        get => _selectedOverlayModeOption;
        set => SetValue(ref _selectedOverlayModeOption, value, nameof(SelectedOverlayModeOption));
    }

    public IReadOnlyList<CadPad> CadPadsForCanvas => _render.CadPads;

    public IReadOnlyList<CoordinatePlannerPointRow> AaCornerRows => _render.AaCornerRows;

    public IReadOnlyList<CoordinatePlannerPointRow> CustomArrayCornerRows => _render.CustomArrayCornerRows;

    public IReadOnlyList<CoordinatePlannerPointRow> CustomArrayPointRows => _render.CustomArrayPointRows;

    public IReadOnlyList<CoordinatePlannerLineRow> HorizontalGuideRows => _render.HorizontalGuideRows;

    public IReadOnlyList<CoordinatePlannerLineRow> VerticalGuideRows => _render.VerticalGuideRows;

    public CoordinatePlannerSnapshot Snapshot => _render.Snapshot;

    public Rect2 PreviewBounds => _render.PreviewBounds;

    public bool ShowCad => _render.CadPads.Length > 0;

    public bool HasBistRectangle => _render.Snapshot.Rectangles.Count > 0;

    public string LayerSummaryText => $"Layer: {SelectedLayerOption.Display}";

    public string ActiveAreaSourceSummaryText => _render.ActiveAreaSourceText;

    public string GuideSourceSummaryText => _render.GuideSourceText;

    public string BistRectangleSummaryText
    {
        get
        {
            if (_render.Snapshot.Rectangles.Count == 0)
            {
                return "BIST center blank rect disabled.";
            }
            var rect = _render.Snapshot.Rectangles[0];

            return string.Format(
                CultureInfo.InvariantCulture,
                "BIST rect px ({0:0.###}, {1:0.###}) -> ({2:0.###}, {3:0.###}) · machine ({4:0.###}, {5:0.###}) -> ({6:0.###}, {7:0.###})",
                rect.PixelLeft,
                rect.PixelTop,
                rect.PixelRight,
                rect.PixelBottom,
                rect.MachineLeft,
                rect.MachineTop,
                rect.MachineRight,
                rect.MachineBottom);
        }
    }

    public string BistGenerationStatusText => HasBistRectangle ? "Generated" : "Not generated";

    public void ReplaceArtifactRows(CoordinateArtifactRow[] rows)
    {
        _render = _render with { ArtifactRows = rows };
    }

    public void Rebuild(CoordinatePlannerRequest request, string? outlineLayer, TGuideBasisKind guideBasisKind)
    {
        var selectedArtifactKey = _readSelectedArtifactKey();
        var result = _build(request, outlineLayer, guideBasisKind);
        var artifactRows = _sortRows(result.ArtifactRows);
        _render = result with { CadPads = _render.CadPads, ArtifactRows = artifactRows };
        _restoreSelection(selectedArtifactKey);

        _setStatus(string.Format(
            CultureInfo.InvariantCulture,
            "Coordinate planner ready. Corners {0}, guides {1}, BIST {2}, array dots {3}.",
            AaCornerRows.Count,
            HorizontalGuideRows.Count + VerticalGuideRows.Count,
            HasBistRectangle ? "on" : "off",
            CustomArrayPointRows.Count));

        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.Snapshot));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.PreviewBounds));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.AaCornerRows));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.CustomArrayCornerRows));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.CustomArrayPointRows));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.HorizontalGuideRows));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.VerticalGuideRows));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.ArtifactRows));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.HasBistRectangle));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.MachineSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.PixelSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.CopperSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.GuideSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.ArtifactSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.ArtifactSortStatusText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.SafeCoordinatePolicyText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.BistRectangleSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.CustomArraySummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.SelectedCustomArrayCornerMachineX));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.SelectedCustomArrayCornerMachineY));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.SelectedCustomArrayCornerSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.HasCustomArtifacts));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.CustomArtifactSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.ActiveAreaSourceSummaryText));
        _propertyChanged(nameof(CoordinatePlannerWorkspaceViewModel.GuideSourceSummaryText));
    }

    public void UpdateCadPadsForCanvas()
    {
        _render = _render with
        {
            CadPads = SelectedLayerOption.Mode switch
            {
                CoordinatePlannerCadLayerMode.None => Array.Empty<CadPad>(),
                CoordinatePlannerCadLayerMode.AllVisible => _cadPads.ToArray(),
                CoordinatePlannerCadLayerMode.SingleLayer => _cadPads
                    .Where(pad => string.Equals(pad.Layer, SelectedLayerOption.LayerName, StringComparison.Ordinal))
                    .ToArray(),
                _ => Array.Empty<CadPad>(),
            },
        };

        _propertyChanged(nameof(CadPadsForCanvas));
        _propertyChanged(nameof(ShowCad));
        _propertyChanged(nameof(LayerSummaryText));
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
