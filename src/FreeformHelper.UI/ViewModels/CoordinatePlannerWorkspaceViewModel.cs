using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Coordinate;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CoordinatePlannerWorkspaceViewModel : ObservableObject
{
    internal UiEventRunner? UiEvents { get; init; }

    public enum CoordinatePlannerCadLayerMode
    {
        None,
        AllVisible,
        SingleLayer,
    }

    public enum CoordinatePlannerBoundsMode
    {
        RegularGrid,
        LayerBounds,
    }

    public enum CoordinatePlannerOverlayMode
    {
        Focus,
        Essentials,
        All,
    }

    public enum CoordinatePlannerGuideBasisMode
    {
        ActiveArea,
        RegularGrid,
    }

    public enum CoordinatePlannerGuideGenerationMode
    {
        Count,
        Pitch,
        ExplicitPositions,
    }

    public readonly record struct CoordinatePlannerCadLayerOption(
        CoordinatePlannerCadLayerMode Mode,
        string? LayerName,
        string Display);

    public readonly record struct CoordinatePlannerBoundsOption(
        CoordinatePlannerBoundsMode Mode,
        string? LayerName,
        string Display);

    public readonly record struct CoordinatePlannerGuideBasisOption(
        CoordinatePlannerGuideBasisMode Mode,
        string Display);

    public readonly record struct CoordinatePlannerGuideGenerationOption(
        CoordinatePlannerGuideGenerationMode Mode,
        string Display);

    public readonly record struct CoordinatePlannerCustomArrayCornerOption(
        string Key,
        string Display);

    public readonly record struct CoordinatePlannerPointRow(
        string Label,
        string Category,
        string PixelText,
        string MachineText,
        string SafeMachineText);

    public readonly record struct CoordinatePlannerLineRow(
        string Label,
        string PixelText,
        string MachineText,
        string SafeMachineText);

    public readonly record struct CoordinatePlannerOverlayModeOption(
        CoordinatePlannerOverlayMode Mode,
        string Display);

    public sealed record CoordinatePlannerCustomRecipeRow(
        string Key,
        string Label,
        string Kind,
        string DetailText);

    public sealed record CoordinatePlannerTextExportRequest(
        string Title,
        string SuggestedFileName,
        string DefaultExtension,
        string FileTypeName,
        IReadOnlyList<string> Patterns,
        string Content);

    public readonly record struct CoordinatePlannerWorkspacePreferences(
        int PixelWidth,
        int PixelHeight,
        string PreferredAaOutlineLayerName);

    private readonly CoordinatePlannerWorkspaceSession _session;
    private readonly CoordinateGeometryOwner _geometry;
    private readonly CoordinateGuideOwner<CoordinatePlannerGuideBasisKind> _guides;
    private readonly bool _highlightUnmatched;
    private readonly bool _highlightFreeform;
    private readonly CoordinateArrayOwner _array;
    private readonly CoordinateArtifactOwner _artifacts;
    private readonly CoordinatePresentationOwner<CoordinatePlannerGuideBasisKind> _presentation;

    public event EventHandler? FitCanvasRequested;
    public event Action<CoordinatePlannerWorkspacePreferences>? WorkspacePreferencesChanged;
    public Func<CoordinateArtifactRow, Task>? RequestArtifactDetailAsync
    {
        get => _artifacts.RequestArtifactDetailAsync;
        set => _artifacts.RequestArtifactDetailAsync = value;
    }

    public CoordinatePlannerWorkspaceViewModel(
        CoordinatePlannerWorkspaceUseCase useCase,
        CoordinatePlannerWorkspaceSession session)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        _session = session ?? throw new ArgumentNullException(nameof(session));
        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(session.Grid, BuildRequestFromDefaults(session));
        var artifactSnapshot = CoordinateArtifactService.BuildSnapshot(
            snapshot, snapshot.ActiveAreaBounds, "AA outline: regular grid bounds", "Guide reference: AA outline");
        var initialRender = new CoordinatePlannerRenderState(snapshot, artifactSnapshot, snapshot.ActiveAreaBounds,
            "AA outline: regular grid bounds", "Guide reference: AA outline", Array.Empty<CadPad>(),
            Array.Empty<CoordinatePlannerPointRow>(), Array.Empty<CoordinatePlannerPointRow>(), Array.Empty<CoordinatePlannerPointRow>(),
            Array.Empty<CoordinatePlannerLineRow>(), Array.Empty<CoordinatePlannerLineRow>(), Array.Empty<CoordinateArtifactRow>());
        _highlightUnmatched = false;
        _highlightFreeform = false;

        var layerOptions = BuildLayerOptions(session.VisibleLayerNames);
        var aaBoundsOptions = BuildAaBoundsOptions(session.VisibleLayerNames);
        var guideBasisOptions = CoordinatePlannerPresentationCatalog.GuideBasisOptions;
        var guideGenerationOptions = CoordinatePlannerPresentationCatalog.GuideGenerationOptions;
        var cornerOptions = CoordinatePlannerPresentationCatalog.CustomArrayCornerOptions;
        var overlayModeOptions = CoordinatePlannerPresentationCatalog.OverlayModeOptions;
        _geometry = new CoordinateGeometryOwner(
            new(ResolveInitialAaBoundsOption(aaBoundsOptions, session.VisibleLayerNames, session.PreferredActiveAreaOutlineLayerName),
                0m, 0m, (decimal)session.DefaultMachineWidth, (decimal)session.DefaultMachineHeight,
                session.DefaultPixelWidth, session.DefaultPixelHeight, 0m, true),
            RebuildSnapshot, preferences => WorkspacePreferencesChanged?.Invoke(preferences),
            UseActiveAreaSafeCorners, value => StatusText = value, aaBoundsOptions, OnPropertyChanging, OnPropertyChanged);
        _guides = new CoordinateGuideOwner<CoordinatePlannerGuideBasisKind>(
            new(guideBasisOptions[0], guideGenerationOptions[0], guideGenerationOptions[0],
                5m, 5m, 10m, 10m, 0m, 0m, string.Empty, string.Empty),
            RebuildSnapshot, guideBasisOptions, guideGenerationOptions, ToGuideBasisKind, OnPropertyChanging, OnPropertyChanged);
        _artifacts = new CoordinateArtifactOwner(
            new(null, "Default", false), () => Render, ReplaceArtifactRows, value => StatusText = value,
            RunArtifactDetailAsync, OnPropertyChanging, OnPropertyChanged);
        _presentation = new CoordinatePresentationOwner<CoordinatePlannerGuideBasisKind>(
            new(layerOptions[1], session.DefaultShowRegular, false, false, overlayModeOptions[1]),
            initialRender, session.CadPads, BuildRender, _artifacts.SortRows, () => SelectedArtifactRow?.Key,
            _artifacts.RestoreSelection, value => StatusText = value,
            () => FitCanvasRequested?.Invoke(this, EventArgs.Empty),
            layerOptions, overlayModeOptions, OnPropertyChanging, OnPropertyChanged);
        _array = new CoordinateArrayOwner(
            new(cornerOptions[0], false, 5m, 5m, MachineOriginX, MachineOriginY,
                MachineOriginX + MachineWidth, MachineOriginY,
                MachineOriginX + MachineWidth, MachineOriginY + MachineHeight,
                MachineOriginX, MachineOriginY + MachineHeight),
            () => Snapshot, _geometry.CaptureInput, RebuildSnapshot, _artifacts.SelectByKey,
            value => StatusText = value, cornerOptions, OnPropertyChanging, OnPropertyChanged);

        _presentation.UpdateCadPadsForCanvas();
        RebuildSnapshot();
    }

    public int SourceRevision => _session.SourceRevision;

    public IReadOnlyList<CadPad> CadPadsForCanvas => _presentation.CadPadsForCanvas;

    public IReadOnlyList<RegularPad> RegularPads => _session.Grid.Pads;
    public bool HighlightUnmatched => _highlightUnmatched;
    public bool HighlightFreeform => _highlightFreeform;

    public IReadOnlyList<CoordinatePlannerCadLayerOption> LayerOptions => _presentation.LayerOptions;

    public IReadOnlyList<CoordinatePlannerBoundsOption> AaBoundsOptions => _geometry.AaBoundsOptions;

    public IReadOnlyList<CoordinatePlannerGuideBasisOption> GuideBasisOptions => _guides.GuideBasisOptions;

    public IReadOnlyList<CoordinatePlannerGuideGenerationOption> GuideGenerationOptions => _guides.GuideGenerationOptions;

    public IReadOnlyList<CoordinatePlannerCustomArrayCornerOption> CustomArrayCornerOptions => _array.CustomArrayCornerOptions;

    public IReadOnlyList<CoordinatePlannerOverlayModeOption> OverlayModeOptions => _presentation.OverlayModeOptions;

    public IReadOnlyList<CoordinatePlannerPointRow> AaCornerRows => _presentation.AaCornerRows;

    public IReadOnlyList<CoordinatePlannerPointRow> CustomArrayCornerRows => _presentation.CustomArrayCornerRows;

    public IReadOnlyList<CoordinatePlannerPointRow> CustomArrayPointRows => _presentation.CustomArrayPointRows;

    public IReadOnlyList<CoordinatePlannerLineRow> HorizontalGuideRows => _presentation.HorizontalGuideRows;

    public IReadOnlyList<CoordinatePlannerLineRow> VerticalGuideRows => _presentation.VerticalGuideRows;

    public IReadOnlyList<CoordinateArtifactRow> ArtifactRows => _artifacts.ArtifactRows;

    public ReadOnlyObservableCollection<CoordinatePlannerCustomRecipeRow> CustomRecipeRows => _array.CustomRecipeRows;

    public CoordinatePlannerCustomEditorModel CustomEditor => _array.CustomEditor;

    public CoordinatePlannerSnapshot Snapshot => _presentation.Snapshot;

    public Rect2 PreviewBounds => _presentation.PreviewBounds;

    public IRelayCommand SyncFromWorkspaceCommand => _geometry.SyncFromWorkspaceCommand;

    public IRelayCommand UseActiveAreaSafeCornersCommand => _array.UseActiveAreaSafeCornersCommand;

    public IRelayCommand FitCanvasCommand => _presentation.FitCanvasCommand;

    public IAsyncRelayCommand ExportPreviewPngCommand => _artifacts.ExportPreviewPngCommand;

    public IAsyncRelayCommand CopySelectedArtifactCommand => _artifacts.CopySelectedArtifactCommand;

    public IAsyncRelayCommand CopyAllArtifactsCommand => _artifacts.CopyAllArtifactsCommand;

    public IAsyncRelayCommand ExportArtifactsCsvCommand => _artifacts.ExportArtifactsCsvCommand;

    public IAsyncRelayCommand ExportArtifactsJsonCommand => _artifacts.ExportArtifactsJsonCommand;

    public IRelayCommand AddCustomPointCommand => _array.AddCustomPointCommand;

    public IRelayCommand AddCustomPathCommand => _array.AddCustomPathCommand;

    public IRelayCommand ClearCustomArtifactsCommand => _array.ClearCustomArtifactsCommand;

    public IRelayCommand<string?> SortArtifactRowsCommand => _artifacts.SortArtifactRowsCommand;

    public IAsyncRelayCommand<CoordinateArtifactRow?> ShowArtifactDetailCommand => _artifacts.ShowArtifactDetailCommand;

    public Func<Task<bool>>? RequestExportPreviewPngAsync
    {
        get => _artifacts.RequestExportPreviewPngAsync;
        set => _artifacts.RequestExportPreviewPngAsync = value;
    }

    public Func<string, Task<bool>>? RequestSetClipboardTextAsync
    {
        get => _artifacts.RequestSetClipboardTextAsync;
        set => _artifacts.RequestSetClipboardTextAsync = value;
    }

    public Func<CoordinatePlannerTextExportRequest, Task<bool>>? RequestSaveTextFileAsync
    {
        get => _artifacts.RequestSaveTextFileAsync;
        set => _artifacts.RequestSaveTextFileAsync = value;
    }

    public CoordinatePlannerCadLayerOption SelectedLayerOption
    {
        get => _presentation.SelectedLayerOption;
        set => _presentation.SelectedLayerOption = value;
    }

    public CoordinatePlannerBoundsOption SelectedAaBoundsOption
    {
        get => _geometry.SelectedAaBoundsOption;
        set => _geometry.SelectedAaBoundsOption = value;
    }

    public CoordinatePlannerGuideBasisOption SelectedGuideBasisOption
    {
        get => _guides.SelectedGuideBasisOption;
        set => _guides.SelectedGuideBasisOption = value;
    }

    public CoordinatePlannerGuideGenerationOption SelectedHorizontalGuideGenerationOption
    {
        get => _guides.SelectedHorizontalGuideGenerationOption;
        set => _guides.SelectedHorizontalGuideGenerationOption = value;
    }

    public CoordinatePlannerGuideGenerationOption SelectedVerticalGuideGenerationOption
    {
        get => _guides.SelectedVerticalGuideGenerationOption;
        set => _guides.SelectedVerticalGuideGenerationOption = value;
    }

    public CoordinatePlannerCustomArrayCornerOption SelectedCustomArrayCornerOption
    {
        get => _array.SelectedCustomArrayCornerOption;
        set => _array.SelectedCustomArrayCornerOption = value;
    }

    public decimal MachineOriginX
    {
        get => _geometry.MachineOriginX;
        set => _geometry.MachineOriginX = value;
    }

    public decimal MachineOriginY
    {
        get => _geometry.MachineOriginY;
        set => _geometry.MachineOriginY = value;
    }

    public decimal MachineWidth
    {
        get => _geometry.MachineWidth;
        set => _geometry.MachineWidth = value;
    }

    public decimal MachineHeight
    {
        get => _geometry.MachineHeight;
        set => _geometry.MachineHeight = value;
    }

    public decimal PixelWidth
    {
        get => _geometry.PixelWidth;
        set => _geometry.PixelWidth = value;
    }

    public decimal PixelHeight
    {
        get => _geometry.PixelHeight;
        set => _geometry.PixelHeight = value;
    }

    public bool ShowRegular
    {
        get => _presentation.ShowRegular;
        set => _presentation.ShowRegular = value;
    }

    public bool ShowRegularAxisLabels
    {
        get => _presentation.ShowRegularAxisLabels;
        set => _presentation.ShowRegularAxisLabels = value;
    }

    public bool ShowCalibrationDetails
    {
        get => _presentation.ShowCalibrationDetails;
        set => _presentation.ShowCalibrationDetails = value;
    }

    public CoordinatePlannerOverlayModeOption SelectedOverlayModeOption
    {
        get => _presentation.SelectedOverlayModeOption;
        set => _presentation.SelectedOverlayModeOption = value;
    }

    public CoordinateArtifactRow? SelectedArtifactRow
    {
        get => _artifacts.SelectedArtifactRow;
        set => _artifacts.SelectedArtifactRow = value;
    }

    public string ArtifactSortKey
    {
        get => _artifacts.ArtifactSortKey;
        set => _artifacts.ArtifactSortKey = value;
    }

    public bool ArtifactSortDescending
    {
        get => _artifacts.ArtifactSortDescending;
        set => _artifacts.ArtifactSortDescending = value;
    }

    public decimal CopperPillarDiameter
    {
        get => _geometry.CopperPillarDiameter;
        set => _geometry.CopperPillarDiameter = value;
    }

    public decimal HorizontalGuideCount
    {
        get => _guides.HorizontalGuideCount;
        set => _guides.HorizontalGuideCount = value;
    }

    public decimal VerticalGuideCount
    {
        get => _guides.VerticalGuideCount;
        set => _guides.VerticalGuideCount = value;
    }

    public decimal HorizontalGuidePitch
    {
        get => _guides.HorizontalGuidePitch;
        set => _guides.HorizontalGuidePitch = value;
    }

    public decimal VerticalGuidePitch
    {
        get => _guides.VerticalGuidePitch;
        set => _guides.VerticalGuidePitch = value;
    }

    public decimal HorizontalGuideInset
    {
        get => _guides.HorizontalGuideInset;
        set => _guides.HorizontalGuideInset = value;
    }

    public decimal VerticalGuideInset
    {
        get => _guides.VerticalGuideInset;
        set => _guides.VerticalGuideInset = value;
    }

    public string HorizontalGuidePositionListText
    {
        get => _guides.HorizontalGuidePositionListText;
        set => _guides.HorizontalGuidePositionListText = value;
    }

    public string VerticalGuidePositionListText
    {
        get => _guides.VerticalGuidePositionListText;
        set => _guides.VerticalGuidePositionListText = value;
    }

    public bool ShowBistRectangle
    {
        get => _geometry.ShowBistRectangle;
        set => _geometry.ShowBistRectangle = value;
    }

    public bool ShowCustomArray
    {
        get => _array.ShowCustomArray;
        set => _array.ShowCustomArray = value;
    }

    public decimal CustomArrayColumnCount
    {
        get => _array.CustomArrayColumnCount;
        set => _array.CustomArrayColumnCount = value;
    }

    public decimal CustomArrayRowCount
    {
        get => _array.CustomArrayRowCount;
        set => _array.CustomArrayRowCount = value;
    }

    public decimal CustomArrayTopLeftMachineX
    {
        get => _array.CustomArrayTopLeftMachineX;
        set => _array.CustomArrayTopLeftMachineX = value;
    }

    public decimal CustomArrayTopLeftMachineY
    {
        get => _array.CustomArrayTopLeftMachineY;
        set => _array.CustomArrayTopLeftMachineY = value;
    }

    public decimal CustomArrayTopRightMachineX
    {
        get => _array.CustomArrayTopRightMachineX;
        set => _array.CustomArrayTopRightMachineX = value;
    }

    public decimal CustomArrayTopRightMachineY
    {
        get => _array.CustomArrayTopRightMachineY;
        set => _array.CustomArrayTopRightMachineY = value;
    }

    public decimal CustomArrayBottomRightMachineX
    {
        get => _array.CustomArrayBottomRightMachineX;
        set => _array.CustomArrayBottomRightMachineX = value;
    }

    public decimal CustomArrayBottomRightMachineY
    {
        get => _array.CustomArrayBottomRightMachineY;
        set => _array.CustomArrayBottomRightMachineY = value;
    }

    public decimal CustomArrayBottomLeftMachineX
    {
        get => _array.CustomArrayBottomLeftMachineX;
        set => _array.CustomArrayBottomLeftMachineX = value;
    }

    public decimal CustomArrayBottomLeftMachineY
    {
        get => _array.CustomArrayBottomLeftMachineY;
        set => _array.CustomArrayBottomLeftMachineY = value;
    }

    [ObservableProperty]
    private string _statusText = "Coordinate planner ready.";

    public bool ShowCad => _presentation.ShowCad;

    public bool HasBistRectangle => _presentation.HasBistRectangle;

    public bool HasCustomArtifacts => _array.HasCustomArtifacts;

    public string WorkspaceSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "AA {0:0.###} x {1:0.###} mm · {2} x {3} px",
            _session.DefaultMachineWidth,
            _session.DefaultMachineHeight,
            _session.DefaultPixelWidth,
            _session.DefaultPixelHeight);

    public string LayerSummaryText => _presentation.LayerSummaryText;

    public string ActiveAreaSourceSummaryText => _presentation.ActiveAreaSourceSummaryText;

    public string GuideSourceSummaryText => _presentation.GuideSourceSummaryText;

    public string MachineSummaryText => _geometry.MachineSummaryText;

    public string PixelSummaryText => _geometry.PixelSummaryText;

    public string CopperSummaryText => _geometry.CopperSummaryText;

    public string GuideSummaryText => _guides.GuideSummaryText;

    public bool IsHorizontalGuideCountMode => _guides.IsHorizontalGuideCountMode;

    public bool IsHorizontalGuidePitchMode => _guides.IsHorizontalGuidePitchMode;

    public bool IsHorizontalGuideExplicitMode => _guides.IsHorizontalGuideExplicitMode;

    public bool IsVerticalGuideCountMode => _guides.IsVerticalGuideCountMode;

    public bool IsVerticalGuidePitchMode => _guides.IsVerticalGuidePitchMode;

    public bool IsVerticalGuideExplicitMode => _guides.IsVerticalGuideExplicitMode;

    public string ArtifactSummaryText => _artifacts.ArtifactSummaryText;

    public string ArtifactSortStatusText => _artifacts.ArtifactSortStatusText;

    public string SafeCoordinatePolicyText => _geometry.SafeCoordinatePolicyText;

    public string BistRectangleSummaryText => _presentation.BistRectangleSummaryText;

    public string CustomArraySummaryText
    {
        get
        {
            if (!ShowCustomArray)
            {
                return "4-point array disabled.";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "4-point array: {0} x {1} dot(s), corners {2}, plotted dots {3}.",
                Math.Max(0, CoordinatePlannerRequestBuilder.NonNegativeInt(CustomArrayColumnCount)),
                Math.Max(0, CoordinatePlannerRequestBuilder.NonNegativeInt(CustomArrayRowCount)),
                CustomArrayCornerRows.Count,
                CustomArrayPointRows.Count);
        }
    }

    public decimal SelectedCustomArrayCornerMachineX
    {
        get => _array.SelectedCustomArrayCornerMachineX;
        set => _array.SelectedCustomArrayCornerMachineX = value;
    }

    public decimal SelectedCustomArrayCornerMachineY
    {
        get => _array.SelectedCustomArrayCornerMachineY;
        set => _array.SelectedCustomArrayCornerMachineY = value;
    }

    public string SelectedCustomArrayCornerSummaryText => _array.SelectedCustomArrayCornerSummaryText;

    public string CustomArtifactSummaryText => _array.CustomArtifactSummaryText;

    private CoordinatePlannerRenderState Render => _presentation.Render;

    public bool HasSelectedArtifact => _artifacts.HasSelectedArtifact;

    public string BistGenerationStatusText => _presentation.BistGenerationStatusText;

    public const string SyncConsequenceText = "Resets calibration and overwrites the four array corners; turns the array on.";

    public const string SafeCornersConsequenceText = "Overwrites the four corners and turns the array on.";

    public const string ClearCustomConsequenceText = "Clears every custom point and path.";

    public const string GuidePositionsHelpText = "Enter millimetres separated by commas, semicolons, spaces or new lines. Invalid entries are skipped; negative values become zero.";

    public bool BeginCustomArrayCornerEdit(string key) => _array.BeginCustomArrayCornerEdit(key);

    public bool CompleteCustomArrayCornerEdit(string key) => _array.CompleteCustomArrayCornerEdit(key);

    public bool MoveCustomArrayCornerToWorldPoint(string key, Point2 worldPoint) => _array.MoveCustomArrayCornerToWorldPoint(key, worldPoint);

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(BistGenerationStatusText));
    }
}
