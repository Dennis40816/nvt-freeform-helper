// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.UI.ViewModels.Coordinate;

/// <summary>Owns editable geometry and synchronous notifications. UI thread only.</summary>
internal sealed class CoordinateGeometryOwner
{
    internal readonly record struct Seed(
        CoordinatePlannerBoundsOption SelectedAaBoundsOption,
        decimal MachineOriginX,
        decimal MachineOriginY,
        decimal MachineWidth,
        decimal MachineHeight,
        decimal PixelWidth,
        decimal PixelHeight,
        decimal CopperPillarDiameter,
        bool ShowBistRectangle);

    private readonly Seed _initial;
    private readonly Action _rebuild;
    private readonly Action<CoordinatePlannerWorkspacePreferences> _publishPreferences;
    private readonly Action _useSafeCorners;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private CoordinatePlannerBoundsOption _selectedAaBoundsOption;
    private decimal _machineOriginX;
    private decimal _machineOriginY;
    private decimal _machineWidth;
    private decimal _machineHeight;
    private decimal _pixelWidth;
    private decimal _pixelHeight;
    private decimal _copperPillarDiameter;
    private bool _showBistRectangle;

    internal CoordinateGeometryOwner(
        Seed seed,
        Action rebuild,
        Action<CoordinatePlannerWorkspacePreferences> publishPreferences,
        Action useSafeCorners,
        Action<string> setStatus,
        IReadOnlyList<CoordinatePlannerBoundsOption> aaBoundsOptions,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _initial = seed;
        _rebuild = rebuild;
        _publishPreferences = publishPreferences;
        _useSafeCorners = useSafeCorners;
        _setStatus = setStatus;
        AaBoundsOptions = aaBoundsOptions;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _selectedAaBoundsOption = seed.SelectedAaBoundsOption;
        _machineOriginX = seed.MachineOriginX;
        _machineOriginY = seed.MachineOriginY;
        _machineWidth = seed.MachineWidth;
        _machineHeight = seed.MachineHeight;
        _pixelWidth = seed.PixelWidth;
        _pixelHeight = seed.PixelHeight;
        _copperPillarDiameter = seed.CopperPillarDiameter;
        _showBistRectangle = seed.ShowBistRectangle;
        SyncFromWorkspaceCommand = new RelayCommand(SyncFromWorkspace);
    }

    public IReadOnlyList<CoordinatePlannerBoundsOption> AaBoundsOptions { get; }
    public IRelayCommand SyncFromWorkspaceCommand { get; }

    public CoordinatePlannerBoundsOption SelectedAaBoundsOption
    {
        get => _selectedAaBoundsOption;
        set => SetValue(ref _selectedAaBoundsOption, value, nameof(SelectedAaBoundsOption), RebuildAndPublishPreferences);
    }

    public decimal MachineOriginX
    {
        get => _machineOriginX;
        set => SetValue(ref _machineOriginX, value, nameof(MachineOriginX), _rebuild);
    }

    public decimal MachineOriginY
    {
        get => _machineOriginY;
        set => SetValue(ref _machineOriginY, value, nameof(MachineOriginY), _rebuild);
    }

    public decimal MachineWidth
    {
        get => _machineWidth;
        set => SetValue(ref _machineWidth, value, nameof(MachineWidth), _rebuild);
    }

    public decimal MachineHeight
    {
        get => _machineHeight;
        set => SetValue(ref _machineHeight, value, nameof(MachineHeight), _rebuild);
    }

    public decimal PixelWidth
    {
        get => _pixelWidth;
        set => SetValue(ref _pixelWidth, value, nameof(PixelWidth), RebuildAndPublishPreferences);
    }

    public decimal PixelHeight
    {
        get => _pixelHeight;
        set => SetValue(ref _pixelHeight, value, nameof(PixelHeight), RebuildAndPublishPreferences);
    }

    public decimal CopperPillarDiameter
    {
        get => _copperPillarDiameter;
        set => SetValue(ref _copperPillarDiameter, value, nameof(CopperPillarDiameter), _rebuild);
    }

    public bool ShowBistRectangle
    {
        get => _showBistRectangle;
        set => SetValue(ref _showBistRectangle, value, nameof(ShowBistRectangle), _rebuild);
    }

    public string MachineSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Machine: origin ({0:0.###}, {1:0.###}) · size {2:0.###} x {3:0.###} mm",
            MachineOriginX,
            MachineOriginY,
            MachineWidth,
            MachineHeight);

    public string PixelSummaryText => $"Pixels: {CoordinatePlannerRequestBuilder.PositiveInt(PixelWidth)} x {CoordinatePlannerRequestBuilder.PositiveInt(PixelHeight)}";

    public string CopperSummaryText =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Copper pillar: {0:0.###} mm dia",
            Math.Max(0m, CopperPillarDiameter));

    public string SafeCoordinatePolicyText =>
        CopperPillarDiameter > 0m
            ? "Safe clamps by copper radius."
            : "Safe = raw until copper dia is set.";

    public string? SelectedAaOutlineLayerName
    {
        get
        {
            if (SelectedAaBoundsOption.Mode != CoordinatePlannerBoundsMode.LayerBounds)
            {
                return null;
            }

            var layerName = SelectedAaBoundsOption.LayerName?.Trim();
            return string.IsNullOrWhiteSpace(layerName) ? null : layerName;
        }
    }

    public CoordinatePlannerGeometryInput CaptureInput() => new(
        MachineOriginX, MachineOriginY, MachineWidth, MachineHeight,
        PixelWidth, PixelHeight, CopperPillarDiameter, ShowBistRectangle);

    public CoordinatePlannerWorkspacePreferences BuildWorkspacePreferences()
    {
        var selectedAaOutlineLayerName = SelectedAaOutlineLayerName;
        return new CoordinatePlannerWorkspacePreferences(
            CoordinatePlannerRequestBuilder.PositiveInt(PixelWidth),
            CoordinatePlannerRequestBuilder.PositiveInt(PixelHeight),
            selectedAaOutlineLayerName ?? string.Empty);
    }

    private void SyncFromWorkspace()
    {
        MachineOriginX = 0m;
        MachineOriginY = 0m;
        MachineWidth = _initial.MachineWidth;
        MachineHeight = _initial.MachineHeight;
        PixelWidth = _initial.PixelWidth;
        PixelHeight = _initial.PixelHeight;
        _useSafeCorners();
        _setStatus("Coordinate planner synced from current workspace.");
    }

    private void RebuildAndPublishPreferences()
    {
        _rebuild();
        _publishPreferences(BuildWorkspacePreferences());
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
