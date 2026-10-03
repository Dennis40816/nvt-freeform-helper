using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    // --- View State ---

    private double _zoom = 1.0; // Current zoom level of the canvas.
    private Vector _pan = new(0, 0); // Current pan offset of the canvas.

    // --- Interaction State ---

    private bool _isMiddlePanning; // Flag indicating if middle mouse button panning is active.
    private bool _isSpacePanning; // Flag indicating if spacebar panning is active.
    private bool _isSpacePressed; // Flag indicating if the spacebar is currently pressed.
    private bool _isLeftPointerDown; // Flag indicating if the left mouse button is currently down.
    private Point _lastPointer; // Stores the last pointer position for drag calculations.
    private Point _pointerDown; // Stores the pointer position when a button was pressed down.
    private const double DragThreshold = 4.0; // Minimum drag distance before a drag operation is initiated.
    private static readonly long PanRedrawIntervalTicks = TimeSpan.FromMilliseconds(16).Ticks;
    private static readonly long BoxSelectionRedrawIntervalTicks = TimeSpan.FromMilliseconds(33).Ticks;
    private long _lastPanRedrawTicks;
    private long _lastBoxSelectionRedrawTicks;

    private bool _isBoxSelecting; // Flag indicating if box selection is active.
    private Point _boxStart; // Starting point of the box selection.
    private Point _boxEnd; // Current/ending point of the box selection.

    // --- Selection State ---

    private readonly HashSet<int> _selectedCadIds = new(); // Set of IDs of currently selected CAD pads.
    private readonly HashSet<int> _selectedRegIdx = new(); // Set of indices of currently selected regular pads.
    private readonly HashSet<int> _highlightedCadIds = new(); // CAD pads highlighted by overlap diagnostics.
    private readonly HashSet<int> _diffIndexOverrideCadIds = new(); // CAD pads with manual CAD Output FW Diff overrides.
    private readonly HashSet<int> _diffIndexAnchorCadPadIds = new(); // CAD pads used as per-IC CAD Output FW Diff anchors.
    private readonly Dictionary<int, NotchCanvasPreviewItem> _notchPreviewByCadId = new(); // Selected CAD preview payload.
    private readonly List<Polygon2> _notchToFullSeedPolygons = new(); // Polygon-overlap seed polygons for staged To Full visualization.
    private readonly List<Polygon2> _notchToFullCandidatePolygons = new(); // Boundary regular candidate polygons for staged To Full visualization.
    private readonly Dictionary<int, NotchApplySimulationAaDisplayCell> _simulationOverlayByRegularPadId = new();
    private readonly Dictionary<int, List<PadMatchLink>> _matchLinksByCadId = new();
    private readonly Dictionary<int, List<PadMatchLink>> _matchLinksByRegularId = new();
    private double _simulationOverlayMinValue;
    private double _simulationOverlayMaxValue;
    private double _simulationOverlayMaxAbsDelta;
    private double _simulationOverlayAutoNegativeClampAbs;
    private double _simulationOverlayAutoPositiveClamp;
    private Dictionary<int, CadPad>? _cadPadById;
    private Dictionary<int, RegularPad>? _regularPadById;
    private readonly List<RegularPad> _visibleRegularUnselected = new();
    private readonly List<RegularPad> _visibleRegularSelected = new();
    private readonly List<CadPad> _visibleCadUnselected = new();
    private readonly List<CadPad> _visibleCadSelected = new();
    private static readonly TimeSpan HoverDebugRevealDelay = TimeSpan.FromMilliseconds(260);
    private DispatcherTimer? _hoverDebugRevealTimer;
    private Point _hoverDebugPendingPointer;
    private bool _hoverDebugPendingRegularOnlyMode;
    private bool _hoverDebugVisible;
    private HitResult _hoverDebugHit;
    private bool _hoverDebugRegularOnlyMode;

    // Axis label hit regions (screen-space) for click selection.
    private readonly List<AxisLabelHit> _axisLabelHits = new();

    // --- Caches for Performance ---

    private Rect2? _cachedWorldBounds; // Cached bounding box of all content in world coordinates.
    private Dictionary<int, Color>? _cadAreaColorCache; // Cache for CAD pad colors when coloring by area.

    private Dictionary<int, Geometry>? _cadGeometryCache; // Cache for Avalonia Geometry objects of CAD pads.
    private Dictionary<int, ISolidColorBrush>? _cadAreaBrushCache; // Cache for Avalonia brushes for area-colored CAD pads.
    private CadSpatialIndex? _cadIndex; // Spatial index for efficient querying of CAD pads.
    private RegularSpatialIndex? _regularIndex; // Spatial index for efficient querying of regular pads.
    private readonly PadCanvasSelectionEngine _selectionEngine;
    private readonly PadCanvasVisibleDrawListBuilder _visibleDrawListBuilder;

    /// <summary>
    /// Caches <see cref="ISolidColorBrush"/> instances to avoid creating new brush objects repeatedly.
    /// Brushes are typically immutable and can be reused, improving performance and reducing memory allocation.
    /// </summary>
    private readonly Dictionary<uint, ISolidColorBrush> _brushCache = new(); // General brush cache.
    private readonly Dictionary<string, TextLayout> _labelLayoutCache = new(); // Cache for rendered text layouts (labels).

    private enum AxisLabelKind
    {
        Row,
        Column,
        Ic
    }

    private readonly record struct AxisLabelHit(AxisLabelKind Kind, int Index, Rect Bounds);
}
