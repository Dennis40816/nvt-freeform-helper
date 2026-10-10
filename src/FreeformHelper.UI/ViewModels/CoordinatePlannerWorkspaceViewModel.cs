using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
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
    private readonly bool _highlightUnmatched;
    private readonly bool _highlightFreeform;
    private readonly List<CoordinateCustomPointRequest> _customPointRequests = new();
    private readonly List<CoordinateCustomPathRequest> _customPathRequests = new();
    private readonly ObservableCollection<CoordinatePlannerCustomRecipeRow> _customRecipeRows = new();
    private CadPad[] _cadPadsForCanvas = Array.Empty<CadPad>();
    private CoordinatePlannerSnapshot _snapshot;
    private CoordinateArtifactSnapshot _artifactSnapshot;
    private Rect2 _previewBounds;
    private string _activeAreaSourceText = "AA outline: regular grid bounds";
    private string _guideSourceText = "Guide reference: AA outline";
    private int _nextCustomPointId = 1;
    private int _nextCustomPathId = 1;
    private bool _isBatchUpdatingCustomArrayCorners;

    public event EventHandler? FitCanvasRequested;
    public event Action<CoordinatePlannerWorkspacePreferences>? WorkspacePreferencesChanged;
    public Func<CoordinateArtifactRow, Task>? RequestArtifactDetailAsync { get; set; }

    public CoordinatePlannerWorkspaceViewModel(
        CoordinatePlannerWorkspaceUseCase useCase,
        CoordinatePlannerWorkspaceSession session)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _snapshot = CoordinatePlannerComputationService.BuildSnapshot(session.Grid, BuildRequestFromDefaults(session));
        _artifactSnapshot = CoordinateArtifactService.BuildSnapshot(
            _snapshot,
            _snapshot.ActiveAreaBounds,
            _activeAreaSourceText,
            _guideSourceText);
        _previewBounds = _snapshot.ActiveAreaBounds;
        _highlightUnmatched = false;
        _highlightFreeform = false;

        LayerOptions = BuildLayerOptions(session.VisibleLayerNames);
        AaBoundsOptions = BuildAaBoundsOptions(session.VisibleLayerNames);
        GuideBasisOptions =
        [
            new(CoordinatePlannerGuideBasisMode.ActiveArea, "AA outline"),
            new(CoordinatePlannerGuideBasisMode.RegularGrid, "Regular grid"),
        ];
        GuideGenerationOptions =
        [
            new(CoordinatePlannerGuideGenerationMode.Count, "Count"),
            new(CoordinatePlannerGuideGenerationMode.Pitch, "Pitch"),
            new(CoordinatePlannerGuideGenerationMode.ExplicitPositions, "Positions"),
        ];
        CustomArrayCornerOptions =
        [
            new("array-tl", "TL"),
            new("array-tr", "TR"),
            new("array-br", "BR"),
            new("array-bl", "BL"),
        ];
        _selectedLayerOption = LayerOptions[1];
        _selectedAaBoundsOption = ResolveInitialAaBoundsOption(
            AaBoundsOptions,
            session.VisibleLayerNames,
            session.PreferredActiveAreaOutlineLayerName);
        _selectedGuideBasisOption = GuideBasisOptions[0];
        _selectedHorizontalGuideGenerationOption = GuideGenerationOptions[0];
        _selectedVerticalGuideGenerationOption = GuideGenerationOptions[0];
        _selectedCustomArrayCornerOption = CustomArrayCornerOptions[0];
        _machineOriginX = 0m;
        _machineOriginY = 0m;
        _machineWidth = (decimal)session.DefaultMachineWidth;
        _machineHeight = (decimal)session.DefaultMachineHeight;
        _pixelWidth = session.DefaultPixelWidth;
        _pixelHeight = session.DefaultPixelHeight;
        _showRegular = session.DefaultShowRegular;
        _copperPillarDiameter = 0m;
        _horizontalGuideCount = 5;
        _verticalGuideCount = 5;
        _horizontalGuidePitch = 10m;
        _verticalGuidePitch = 10m;
        _horizontalGuideInset = 0m;
        _verticalGuideInset = 0m;
        _showBistRectangle = true;
        _showCustomArray = false;
        _customArrayColumnCount = 5;
        _customArrayRowCount = 5;
        _customArrayTopLeftMachineX = _machineOriginX;
        _customArrayTopLeftMachineY = _machineOriginY;
        _customArrayTopRightMachineX = _machineOriginX + _machineWidth;
        _customArrayTopRightMachineY = _machineOriginY;
        _customArrayBottomRightMachineX = _machineOriginX + _machineWidth;
        _customArrayBottomRightMachineY = _machineOriginY + _machineHeight;
        _customArrayBottomLeftMachineX = _machineOriginX;
        _customArrayBottomLeftMachineY = _machineOriginY + _machineHeight;

        OverlayModeOptions =
        [
            new(CoordinatePlannerOverlayMode.Focus, "Focus"),
            new(CoordinatePlannerOverlayMode.Essentials, "Essentials"),
            new(CoordinatePlannerOverlayMode.All, "All"),
        ];
        _selectedOverlayModeOption = OverlayModeOptions[1];
        CustomRecipeRows = new ReadOnlyObservableCollection<CoordinatePlannerCustomRecipeRow>(_customRecipeRows);
        _customPointMachineX = _machineOriginX + (_machineWidth * 0.5m);
        _customPointMachineY = _machineOriginY + (_machineHeight * 0.5m);
        _customPathStartMachineX = _machineOriginX;
        _customPathStartMachineY = _machineOriginY + (_machineHeight * 0.5m);
        _customPathEndMachineX = _machineOriginX + _machineWidth;
        _customPathEndMachineY = _customPathStartMachineY;
        _customPathStepCount = 5m;

        SyncFromWorkspaceCommand = new RelayCommand(SyncFromWorkspace);
        UseActiveAreaSafeCornersCommand = new RelayCommand(UseActiveAreaSafeCorners);
        FitCanvasCommand = new RelayCommand(() => FitCanvasRequested?.Invoke(this, EventArgs.Empty));
        ExportPreviewPngCommand = new AsyncRelayCommand(ExportPreviewPngAsync);
        CopySelectedArtifactCommand = new AsyncRelayCommand(CopySelectedArtifactAsync);
        CopyAllArtifactsCommand = new AsyncRelayCommand(CopyAllArtifactsAsync);
        CopyArtifactsCommand = new AsyncRelayCommand(CopyArtifactsAsync);
        ExportArtifactsCsvCommand = new AsyncRelayCommand(ExportArtifactsCsvAsync);
        ExportArtifactsJsonCommand = new AsyncRelayCommand(ExportArtifactsJsonAsync);
        AddCustomPointCommand = new RelayCommand(AddCustomPoint);
        AddCustomPathCommand = new RelayCommand(AddCustomPath);
        ClearCustomArtifactsCommand = new RelayCommand(ClearCustomArtifacts, () => HasCustomArtifacts);
        SortArtifactRowsCommand = new RelayCommand<string?>(SortArtifactRows);
        ShowArtifactDetailCommand = new AsyncRelayCommand<CoordinateArtifactRow?>(ShowArtifactDetailAsync);

        UpdateCadPadsForCanvas();
        RebuildSnapshot();
    }

    public int SourceRevision => _session.SourceRevision;

    public IReadOnlyList<CadPad> CadPadsForCanvas => _cadPadsForCanvas;

    public IReadOnlyList<RegularPad> RegularPads => _session.Grid.Pads;
    public bool HighlightUnmatched => _highlightUnmatched;
    public bool HighlightFreeform => _highlightFreeform;

    public IReadOnlyList<CoordinatePlannerCadLayerOption> LayerOptions { get; }

    public IReadOnlyList<CoordinatePlannerBoundsOption> AaBoundsOptions { get; }

    public IReadOnlyList<CoordinatePlannerGuideBasisOption> GuideBasisOptions { get; }

    public IReadOnlyList<CoordinatePlannerGuideGenerationOption> GuideGenerationOptions { get; }

    public IReadOnlyList<CoordinatePlannerCustomArrayCornerOption> CustomArrayCornerOptions { get; }

    public IReadOnlyList<CoordinatePlannerOverlayModeOption> OverlayModeOptions { get; }

    public IReadOnlyList<CoordinatePlannerPointRow> AaCornerRows { get; private set; } = Array.Empty<CoordinatePlannerPointRow>();

    public IReadOnlyList<CoordinatePlannerPointRow> BistCornerRows { get; private set; } = Array.Empty<CoordinatePlannerPointRow>();

    public IReadOnlyList<CoordinatePlannerPointRow> CustomArrayCornerRows { get; private set; } = Array.Empty<CoordinatePlannerPointRow>();

    public IReadOnlyList<CoordinatePlannerPointRow> CustomArrayPointRows { get; private set; } = Array.Empty<CoordinatePlannerPointRow>();

    public IReadOnlyList<CoordinatePlannerLineRow> HorizontalGuideRows { get; private set; } = Array.Empty<CoordinatePlannerLineRow>();

    public IReadOnlyList<CoordinatePlannerLineRow> VerticalGuideRows { get; private set; } = Array.Empty<CoordinatePlannerLineRow>();

    public IReadOnlyList<CoordinateArtifactRow> ArtifactRows { get; private set; } = Array.Empty<CoordinateArtifactRow>();

    public ReadOnlyObservableCollection<CoordinatePlannerCustomRecipeRow> CustomRecipeRows { get; }

    public CoordinatePlannerSnapshot Snapshot => _snapshot;

    public CoordinateArtifactSnapshot ArtifactSnapshot => _artifactSnapshot;

    public Rect2 PreviewBounds => _previewBounds;

    public IRelayCommand SyncFromWorkspaceCommand { get; }

    public IRelayCommand UseActiveAreaSafeCornersCommand { get; }

    public IRelayCommand FitCanvasCommand { get; }

    public IAsyncRelayCommand ExportPreviewPngCommand { get; }

    public IAsyncRelayCommand CopySelectedArtifactCommand { get; }

    public IAsyncRelayCommand CopyAllArtifactsCommand { get; }

    public IAsyncRelayCommand CopyArtifactsCommand { get; }

    public IAsyncRelayCommand ExportArtifactsCsvCommand { get; }

    public IAsyncRelayCommand ExportArtifactsJsonCommand { get; }

    public IRelayCommand AddCustomPointCommand { get; }

    public IRelayCommand AddCustomPathCommand { get; }

    public IRelayCommand ClearCustomArtifactsCommand { get; }

    public IRelayCommand<string?> SortArtifactRowsCommand { get; }

    public IAsyncRelayCommand<CoordinateArtifactRow?> ShowArtifactDetailCommand { get; }

    public Func<Task<bool>>? RequestExportPreviewPngAsync { get; set; }

    public Func<string, Task<bool>>? RequestSetClipboardTextAsync { get; set; }

    public Func<CoordinatePlannerTextExportRequest, Task<bool>>? RequestSaveTextFileAsync { get; set; }

    [ObservableProperty]
    private CoordinatePlannerCadLayerOption _selectedLayerOption;

    [ObservableProperty]
    private CoordinatePlannerBoundsOption _selectedAaBoundsOption;

    [ObservableProperty]
    private CoordinatePlannerGuideBasisOption _selectedGuideBasisOption;

    [ObservableProperty]
    private CoordinatePlannerGuideGenerationOption _selectedHorizontalGuideGenerationOption;

    [ObservableProperty]
    private CoordinatePlannerGuideGenerationOption _selectedVerticalGuideGenerationOption;

    [ObservableProperty]
    private CoordinatePlannerCustomArrayCornerOption _selectedCustomArrayCornerOption;

    [ObservableProperty]
    private decimal _machineOriginX;

    [ObservableProperty]
    private decimal _machineOriginY;

    [ObservableProperty]
    private decimal _machineWidth;

    [ObservableProperty]
    private decimal _machineHeight;

    [ObservableProperty]
    private decimal _pixelWidth;

    [ObservableProperty]
    private decimal _pixelHeight;

    [ObservableProperty]
    private bool _showRegular;

    [ObservableProperty]
    private bool _showRegularAxisLabels;

    [ObservableProperty]
    private bool _showCalibrationDetails;

    [ObservableProperty]
    private CoordinatePlannerOverlayModeOption _selectedOverlayModeOption;

    [ObservableProperty]
    private CoordinateArtifactRow? _selectedArtifactRow;

    [ObservableProperty]
    private string _artifactSortKey = "Default";

    [ObservableProperty]
    private bool _artifactSortDescending;

    [ObservableProperty]
    private decimal _copperPillarDiameter;

    [ObservableProperty]
    private decimal _horizontalGuideCount;

    [ObservableProperty]
    private decimal _verticalGuideCount;

    [ObservableProperty]
    private decimal _horizontalGuidePitch;

    [ObservableProperty]
    private decimal _verticalGuidePitch;

    [ObservableProperty]
    private decimal _horizontalGuideInset;

    [ObservableProperty]
    private decimal _verticalGuideInset;

    [ObservableProperty]
    private string _horizontalGuidePositionListText = string.Empty;

    [ObservableProperty]
    private string _verticalGuidePositionListText = string.Empty;

    [ObservableProperty]
    private string _customPointLabelText = "Point";

    [ObservableProperty]
    private decimal _customPointMachineX;

    [ObservableProperty]
    private decimal _customPointMachineY;

    [ObservableProperty]
    private string _customPathLabelText = "Path";

    [ObservableProperty]
    private decimal _customPathStartMachineX;

    [ObservableProperty]
    private decimal _customPathStartMachineY;

    [ObservableProperty]
    private decimal _customPathEndMachineX;

    [ObservableProperty]
    private decimal _customPathEndMachineY;

    [ObservableProperty]
    private decimal _customPathStepCount;

    [ObservableProperty]
    private bool _showBistRectangle;

    [ObservableProperty]
    private bool _showCustomArray;

    [ObservableProperty]
    private decimal _customArrayColumnCount;

    [ObservableProperty]
    private decimal _customArrayRowCount;

    [ObservableProperty]
    private decimal _customArrayTopLeftMachineX;

    [ObservableProperty]
    private decimal _customArrayTopLeftMachineY;

    [ObservableProperty]
    private decimal _customArrayTopRightMachineX;

    [ObservableProperty]
    private decimal _customArrayTopRightMachineY;

    [ObservableProperty]
    private decimal _customArrayBottomRightMachineX;

    [ObservableProperty]
    private decimal _customArrayBottomRightMachineY;

    [ObservableProperty]
    private decimal _customArrayBottomLeftMachineX;

    [ObservableProperty]
    private decimal _customArrayBottomLeftMachineY;

    [ObservableProperty]
    private string _statusText = "Coordinate planner ready.";

    public bool ShowCad => _cadPadsForCanvas.Length > 0;

    public bool HasBistRectangle => _snapshot.Rectangles.Count > 0;

    public bool HasCustomArray => ShowCustomArray;

    public bool HasCustomArtifacts => _customPointRequests.Count > 0 || _customPathRequests.Count > 0;

    public string WorkspaceSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "AA {0:0.###} x {1:0.###} mm · {2} x {3} px",
            _session.DefaultMachineWidth,
            _session.DefaultMachineHeight,
            _session.DefaultPixelWidth,
            _session.DefaultPixelHeight);

    public string LayerSummaryText => $"Layer: {SelectedLayerOption.Display}";

    public string ActiveAreaSourceSummaryText => _activeAreaSourceText;

    public string GuideSourceSummaryText => _guideSourceText;

    public string MachineSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Machine: origin ({0:0.###}, {1:0.###}) · size {2:0.###} x {3:0.###} mm",
            MachineOriginX,
            MachineOriginY,
            MachineWidth,
            MachineHeight);

    public string PixelSummaryText => $"Pixels: {ClampPositiveInt(PixelWidth)} x {ClampPositiveInt(PixelHeight)}";

    public string CopperSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Copper pillar: {0:0.###} mm dia",
            Math.Max(0m, CopperPillarDiameter));

    public string GuideSummaryText =>
        $"Guides: H {BuildGuideModeSummaryText(SelectedHorizontalGuideGenerationOption.Mode, ClampNonNegativeInt(HorizontalGuideCount), HorizontalGuidePitch, HorizontalGuideInset, HorizontalGuidePositionListText)} / V {BuildGuideModeSummaryText(SelectedVerticalGuideGenerationOption.Mode, ClampNonNegativeInt(VerticalGuideCount), VerticalGuidePitch, VerticalGuideInset, VerticalGuidePositionListText)}";

    public bool IsHorizontalGuideCountMode => SelectedHorizontalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Count;

    public bool IsHorizontalGuidePitchMode => SelectedHorizontalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Pitch;

    public bool IsHorizontalGuideExplicitMode => SelectedHorizontalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.ExplicitPositions;

    public bool IsVerticalGuideCountMode => SelectedVerticalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Count;

    public bool IsVerticalGuidePitchMode => SelectedVerticalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.Pitch;

    public bool IsVerticalGuideExplicitMode => SelectedVerticalGuideGenerationOption.Mode == CoordinatePlannerGuideGenerationMode.ExplicitPositions;

    public string ArtifactSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0} coordinate row(s)",
            ArtifactRows.Count);

    public string ArtifactSortStatusText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Sort: {0} {1}",
            GetArtifactSortDisplay(ArtifactSortKey),
            ArtifactSortDescending ? "desc" : "asc");

    public string SafeCoordinatePolicyText =>
        CopperPillarDiameter > 0m
            ? "Safe clamps by copper radius."
            : "Safe = raw until copper dia is set.";

    public string BistRectangleSummaryText
    {
        get
        {
            if (_snapshot.Rectangles.Count == 0)
            {
                return "BIST center blank rect disabled.";
            }
            var rect = _snapshot.Rectangles[0];

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
                Math.Max(0, ClampNonNegativeInt(CustomArrayColumnCount)),
                Math.Max(0, ClampNonNegativeInt(CustomArrayRowCount)),
                CustomArrayCornerRows.Count,
                CustomArrayPointRows.Count);
        }
    }

    public decimal SelectedCustomArrayCornerMachineX
    {
        get => GetCustomArrayCornerMachineX(SelectedCustomArrayCornerOption.Key);
        set => SetSelectedCustomArrayCornerMachineX(value);
    }

    public decimal SelectedCustomArrayCornerMachineY
    {
        get => GetCustomArrayCornerMachineY(SelectedCustomArrayCornerOption.Key);
        set => SetSelectedCustomArrayCornerMachineY(value);
    }

    public string SelectedCustomArrayCornerSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0} · machine ({1:0.###}, {2:0.###}) mm",
            string.IsNullOrWhiteSpace(SelectedCustomArrayCornerOption.Display) ? "Corner" : SelectedCustomArrayCornerOption.Display,
            SelectedCustomArrayCornerMachineX,
            SelectedCustomArrayCornerMachineY);

    public string CustomArtifactSummaryText =>
        HasCustomArtifacts
            ? string.Format(
                CultureInfo.InvariantCulture,
                "Custom artifacts: {0} point(s), {1} path(s).",
                _customPointRequests.Count,
                _customPathRequests.Count)
            : "Custom artifacts disabled.";

}
