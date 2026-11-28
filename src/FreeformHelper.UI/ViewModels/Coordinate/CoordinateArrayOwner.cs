// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerArrayHandleMath;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.UI.ViewModels.Coordinate;

/// <summary>Owns array editing, its batch guard, and custom recipes. UI thread only.</summary>
internal sealed class CoordinateArrayOwner
{
    internal readonly record struct Seed(
        CoordinatePlannerCustomArrayCornerOption SelectedCustomArrayCornerOption,
        bool ShowCustomArray,
        decimal CustomArrayColumnCount,
        decimal CustomArrayRowCount,
        decimal CustomArrayTopLeftMachineX,
        decimal CustomArrayTopLeftMachineY,
        decimal CustomArrayTopRightMachineX,
        decimal CustomArrayTopRightMachineY,
        decimal CustomArrayBottomRightMachineX,
        decimal CustomArrayBottomRightMachineY,
        decimal CustomArrayBottomLeftMachineX,
        decimal CustomArrayBottomLeftMachineY);

    private readonly CoordinatePlannerCustomArtifactsModel _customArtifacts = new();
    private readonly Func<CoordinatePlannerSnapshot> _readSnapshot;
    private readonly Func<CoordinatePlannerGeometryInput> _readGeometry;
    private readonly Action _rebuild;
    private readonly Action<string> _selectArtifactKey;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private CoordinatePlannerCustomArrayCornerOption _selectedCustomArrayCornerOption;
    private bool _showCustomArray;
    private decimal _customArrayColumnCount;
    private decimal _customArrayRowCount;
    private decimal _customArrayTopLeftMachineX;
    private decimal _customArrayTopLeftMachineY;
    private decimal _customArrayTopRightMachineX;
    private decimal _customArrayTopRightMachineY;
    private decimal _customArrayBottomRightMachineX;
    private decimal _customArrayBottomRightMachineY;
    private decimal _customArrayBottomLeftMachineX;
    private decimal _customArrayBottomLeftMachineY;
    private bool _isBatchUpdatingCustomArrayCorners;

    internal CoordinateArrayOwner(
        Seed seed,
        Func<CoordinatePlannerSnapshot> readSnapshot,
        Func<CoordinatePlannerGeometryInput> readGeometry,
        Action rebuild,
        Action<string> selectArtifactKey,
        Action<string> setStatus,
        IReadOnlyList<CoordinatePlannerCustomArrayCornerOption> cornerOptions,
        Action<string> propertyChanging,
        Action<string> propertyChanged)
    {
        _readSnapshot = readSnapshot;
        _readGeometry = readGeometry;
        _rebuild = rebuild;
        _selectArtifactKey = selectArtifactKey;
        _setStatus = setStatus;
        CustomArrayCornerOptions = cornerOptions;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        _selectedCustomArrayCornerOption = seed.SelectedCustomArrayCornerOption;
        _showCustomArray = seed.ShowCustomArray;
        _customArrayColumnCount = seed.CustomArrayColumnCount;
        _customArrayRowCount = seed.CustomArrayRowCount;
        _customArrayTopLeftMachineX = seed.CustomArrayTopLeftMachineX;
        _customArrayTopLeftMachineY = seed.CustomArrayTopLeftMachineY;
        _customArrayTopRightMachineX = seed.CustomArrayTopRightMachineX;
        _customArrayTopRightMachineY = seed.CustomArrayTopRightMachineY;
        _customArrayBottomRightMachineX = seed.CustomArrayBottomRightMachineX;
        _customArrayBottomRightMachineY = seed.CustomArrayBottomRightMachineY;
        _customArrayBottomLeftMachineX = seed.CustomArrayBottomLeftMachineX;
        _customArrayBottomLeftMachineY = seed.CustomArrayBottomLeftMachineY;
        var geometry = readGeometry();
        CustomEditor = new CoordinatePlannerCustomEditorModel(
            geometry.MachineOriginX, geometry.MachineOriginY, geometry.MachineWidth, geometry.MachineHeight);
        CustomRecipeRows = _customArtifacts.RecipeRows;
        UseActiveAreaSafeCornersCommand = new RelayCommand(UseActiveAreaSafeCorners);
        AddCustomPointCommand = new RelayCommand(AddCustomPoint);
        AddCustomPathCommand = new RelayCommand(AddCustomPath);
        ClearCustomArtifactsCommand = new RelayCommand(ClearCustomArtifacts, () => HasCustomArtifacts);
    }

    public IReadOnlyList<CoordinatePlannerCustomArrayCornerOption> CustomArrayCornerOptions { get; }
    public CoordinatePlannerCustomEditorModel CustomEditor { get; }
    public ReadOnlyObservableCollection<CoordinatePlannerCustomRecipeRow> CustomRecipeRows { get; }
    public IRelayCommand UseActiveAreaSafeCornersCommand { get; }
    public IRelayCommand AddCustomPointCommand { get; }
    public IRelayCommand AddCustomPathCommand { get; }
    public IRelayCommand ClearCustomArtifactsCommand { get; }
    public bool HasCustomArtifacts => _customArtifacts.HasAny;

    public CoordinatePlannerCustomArrayCornerOption SelectedCustomArrayCornerOption
    {
        get => _selectedCustomArrayCornerOption;
        set => SetValue(ref _selectedCustomArrayCornerOption, value, nameof(SelectedCustomArrayCornerOption), () => SelectedCornerChanged(value));
    }

    public bool ShowCustomArray
    {
        get => _showCustomArray;
        set => SetValue(ref _showCustomArray, value, nameof(ShowCustomArray), _rebuild);
    }

    public decimal CustomArrayColumnCount
    {
        get => _customArrayColumnCount;
        set => SetValue(ref _customArrayColumnCount, value, nameof(CustomArrayColumnCount), _rebuild);
    }

    public decimal CustomArrayRowCount
    {
        get => _customArrayRowCount;
        set => SetValue(ref _customArrayRowCount, value, nameof(CustomArrayRowCount), _rebuild);
    }

    public decimal CustomArrayTopLeftMachineX
    {
        get => _customArrayTopLeftMachineX;
        set => SetValue(ref _customArrayTopLeftMachineX, value, nameof(CustomArrayTopLeftMachineX), () => OnCustomArrayCornerValueChanged("array-tl"));
    }

    public decimal CustomArrayTopLeftMachineY
    {
        get => _customArrayTopLeftMachineY;
        set => SetValue(ref _customArrayTopLeftMachineY, value, nameof(CustomArrayTopLeftMachineY), () => OnCustomArrayCornerValueChanged("array-tl"));
    }

    public decimal CustomArrayTopRightMachineX
    {
        get => _customArrayTopRightMachineX;
        set => SetValue(ref _customArrayTopRightMachineX, value, nameof(CustomArrayTopRightMachineX), () => OnCustomArrayCornerValueChanged("array-tr"));
    }

    public decimal CustomArrayTopRightMachineY
    {
        get => _customArrayTopRightMachineY;
        set => SetValue(ref _customArrayTopRightMachineY, value, nameof(CustomArrayTopRightMachineY), () => OnCustomArrayCornerValueChanged("array-tr"));
    }

    public decimal CustomArrayBottomRightMachineX
    {
        get => _customArrayBottomRightMachineX;
        set => SetValue(ref _customArrayBottomRightMachineX, value, nameof(CustomArrayBottomRightMachineX), () => OnCustomArrayCornerValueChanged("array-br"));
    }

    public decimal CustomArrayBottomRightMachineY
    {
        get => _customArrayBottomRightMachineY;
        set => SetValue(ref _customArrayBottomRightMachineY, value, nameof(CustomArrayBottomRightMachineY), () => OnCustomArrayCornerValueChanged("array-br"));
    }

    public decimal CustomArrayBottomLeftMachineX
    {
        get => _customArrayBottomLeftMachineX;
        set => SetValue(ref _customArrayBottomLeftMachineX, value, nameof(CustomArrayBottomLeftMachineX), () => OnCustomArrayCornerValueChanged("array-bl"));
    }

    public decimal CustomArrayBottomLeftMachineY
    {
        get => _customArrayBottomLeftMachineY;
        set => SetValue(ref _customArrayBottomLeftMachineY, value, nameof(CustomArrayBottomLeftMachineY), () => OnCustomArrayCornerValueChanged("array-bl"));
    }

    public CoordinatePlannerArrayInput CaptureInput() => new(
        ShowCustomArray, CustomArrayColumnCount, CustomArrayRowCount,
        CustomArrayTopLeftMachineX, CustomArrayTopLeftMachineY,
        CustomArrayTopRightMachineX, CustomArrayTopRightMachineY,
        CustomArrayBottomRightMachineX, CustomArrayBottomRightMachineY,
        CustomArrayBottomLeftMachineX, CustomArrayBottomLeftMachineY);

    public IReadOnlyList<CoordinateCustomPointRequest> PointRequests() => _customArtifacts.PointRequests();
    public IReadOnlyList<CoordinateCustomPathRequest> PathRequests() => _customArtifacts.PathRequests();

    private void SelectedCornerChanged(CoordinatePlannerCustomArrayCornerOption value)
    {
        NotifySelectedCustomArrayCornerChanged();
        _selectArtifactKey(value.Key);
    }

    public bool BeginCustomArrayCornerEdit(string key)
    {
        if (!TrySelectCustomArrayCornerOption(key))
        {
            return false;
        }

        if (!ShowCustomArray)
        {
            ShowCustomArray = true;
        }

        _selectArtifactKey(key);
        _setStatus(string.Format(
            CultureInfo.InvariantCulture,
            "4-point array {0} selected.",
            SelectedCustomArrayCornerOption.Display));
        return true;
    }

    public bool MoveCustomArrayCornerToWorldPoint(
        string key,
        Point2 worldPoint)
    {
        if (!TrySelectCustomArrayCornerOption(key))
        {
            return false;
        }

        if (!ShowCustomArray)
        {
            ShowCustomArray = true;
        }

        var geometry = _readGeometry();
        var (machineX, machineY) = MachinePointFromWorld(
            _readSnapshot().ActiveAreaBounds,
            geometry.MachineOriginX,
            geometry.MachineOriginY,
            geometry.MachineWidth,
            geometry.MachineHeight,
            worldPoint);
        SetCustomArrayCornerMachinePoint(key, machineX, machineY);
        _selectArtifactKey(key);
        return true;
    }

    public bool CompleteCustomArrayCornerEdit(string key)
    {
        if (!TrySelectCustomArrayCornerOption(key))
        {
            return false;
        }

        _selectArtifactKey(key);
        _setStatus(string.Format(
            CultureInfo.InvariantCulture,
            "4-point array {0} set to ({1:0.###}, {2:0.###}) mm.",
            SelectedCustomArrayCornerOption.Display,
            SelectedCustomArrayCornerMachineX,
            SelectedCustomArrayCornerMachineY));
        return true;
    }

    private void SetSelectedCustomArrayCornerMachineX(decimal value)
    {
        SetCustomArrayCornerMachinePoint(
            SelectedCustomArrayCornerOption.Key,
            value,
            SelectedCustomArrayCornerMachineY);
        _selectArtifactKey(SelectedCustomArrayCornerOption.Key);
    }

    private void SetSelectedCustomArrayCornerMachineY(decimal value)
    {
        SetCustomArrayCornerMachinePoint(
            SelectedCustomArrayCornerOption.Key,
            SelectedCustomArrayCornerMachineX,
            value);
        _selectArtifactKey(SelectedCustomArrayCornerOption.Key);
    }

    private bool TrySelectCustomArrayCornerOption(string key)
    {
        var option = CustomArrayCornerOptions.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(option.Key))
        {
            return false;
        }

        if (!string.Equals(SelectedCustomArrayCornerOption.Key, option.Key, StringComparison.Ordinal))
        {
            SelectedCustomArrayCornerOption = option;
        }
        else
        {
            NotifySelectedCustomArrayCornerChanged();
        }

        return true;
    }

    private void SetCustomArrayCornerMachinePoint(
        string key,
        decimal machineX,
        decimal machineY)
    {
        if (!IsCornerKey(key))
        {
            return;
        }

        _isBatchUpdatingCustomArrayCorners = true;
        try
        {
            switch (key)
            {
                case "array-tl":
                    CustomArrayTopLeftMachineX = RoundMachineCoordinate(machineX);
                    CustomArrayTopLeftMachineY = RoundMachineCoordinate(machineY);
                    break;
                case "array-tr":
                    CustomArrayTopRightMachineX = RoundMachineCoordinate(machineX);
                    CustomArrayTopRightMachineY = RoundMachineCoordinate(machineY);
                    break;
                case "array-br":
                    CustomArrayBottomRightMachineX = RoundMachineCoordinate(machineX);
                    CustomArrayBottomRightMachineY = RoundMachineCoordinate(machineY);
                    break;
                case "array-bl":
                    CustomArrayBottomLeftMachineX = RoundMachineCoordinate(machineX);
                    CustomArrayBottomLeftMachineY = RoundMachineCoordinate(machineY);
                    break;
            }
        }
        finally
        {
            _isBatchUpdatingCustomArrayCorners = false;
        }

        _rebuild();
        NotifySelectedCustomArrayCornerChanged();
    }

    private void OnCustomArrayCornerValueChanged(string key)
    {
        if (_isBatchUpdatingCustomArrayCorners)
        {
            return;
        }

        _rebuild();
        if (string.Equals(SelectedCustomArrayCornerOption.Key, key, StringComparison.Ordinal))
        {
            NotifySelectedCustomArrayCornerChanged();
        }
    }

    private void NotifySelectedCustomArrayCornerChanged()
    {
        _propertyChanged(nameof(SelectedCustomArrayCornerMachineX));
        _propertyChanged(nameof(SelectedCustomArrayCornerMachineY));
        _propertyChanged(nameof(SelectedCustomArrayCornerSummaryText));
    }

    private decimal GetCustomArrayCornerMachineX(string key) =>
        key switch
        {
            "array-tl" => CustomArrayTopLeftMachineX,
            "array-tr" => CustomArrayTopRightMachineX,
            "array-br" => CustomArrayBottomRightMachineX,
            "array-bl" => CustomArrayBottomLeftMachineX,
            _ => 0m,
        };

    private decimal GetCustomArrayCornerMachineY(string key) =>
        key switch
        {
            "array-tl" => CustomArrayTopLeftMachineY,
            "array-tr" => CustomArrayTopRightMachineY,
            "array-br" => CustomArrayBottomRightMachineY,
            "array-bl" => CustomArrayBottomLeftMachineY,
            _ => 0m,
        };

    private void AddCustomPoint()
    {
        var label = _customArtifacts.AddPoint(
            CustomEditor.CustomPointLabelText,
            (double)CustomEditor.CustomPointMachineX,
            (double)CustomEditor.CustomPointMachineY);
        NotifyCustomArtifactsChanged();
        _rebuild();
        _setStatus($"Custom point '{label}' added.");
    }

    private void AddCustomPath()
    {
        var label = _customArtifacts.AddPath(
            CustomEditor.CustomPathLabelText,
            (double)CustomEditor.CustomPathStartMachineX,
            (double)CustomEditor.CustomPathStartMachineY,
            (double)CustomEditor.CustomPathEndMachineX,
            (double)CustomEditor.CustomPathEndMachineY,
            Math.Max(2, CoordinatePlannerRequestBuilder.NonNegativeInt(CustomEditor.CustomPathStepCount)));
        NotifyCustomArtifactsChanged();
        _rebuild();
        _setStatus($"Custom path '{label}' added.");
    }

    private void ClearCustomArtifacts()
    {
        _customArtifacts.Clear();
        NotifyCustomArtifactsChanged();
        _rebuild();
        _setStatus("Custom coordinate artifacts cleared.");
    }

    public void UseActiveAreaSafeCorners()
    {
        var topLeft = _readSnapshot().Points.FirstOrDefault(static point => point.Key == "aa-tl");
        var topRight = _readSnapshot().Points.FirstOrDefault(static point => point.Key == "aa-tr");
        var bottomRight = _readSnapshot().Points.FirstOrDefault(static point => point.Key == "aa-br");
        var bottomLeft = _readSnapshot().Points.FirstOrDefault(static point => point.Key == "aa-bl");
        if (topLeft is null ||
            topRight is null ||
            bottomRight is null ||
            bottomLeft is null)
        {
            _setStatus("AA corners are unavailable for 4-point array initialization.");
            return;
        }

        SetCustomArrayCorners(
            topLeft.SafeMachineX,
            topLeft.SafeMachineY,
            topRight.SafeMachineX,
            topRight.SafeMachineY,
            bottomRight.SafeMachineX,
            bottomRight.SafeMachineY,
            bottomLeft.SafeMachineX,
            bottomLeft.SafeMachineY);
        ShowCustomArray = true;
        _setStatus("4-point array corners synced from current safe AA corners.");
    }

    private void SetCustomArrayCorners(
        double topLeftX,
        double topLeftY,
        double topRightX,
        double topRightY,
        double bottomRightX,
        double bottomRightY,
        double bottomLeftX,
        double bottomLeftY)
    {
        _isBatchUpdatingCustomArrayCorners = true;
        try
        {
            CustomArrayTopLeftMachineX = (decimal)topLeftX;
            CustomArrayTopLeftMachineY = (decimal)topLeftY;
            CustomArrayTopRightMachineX = (decimal)topRightX;
            CustomArrayTopRightMachineY = (decimal)topRightY;
            CustomArrayBottomRightMachineX = (decimal)bottomRightX;
            CustomArrayBottomRightMachineY = (decimal)bottomRightY;
            CustomArrayBottomLeftMachineX = (decimal)bottomLeftX;
            CustomArrayBottomLeftMachineY = (decimal)bottomLeftY;
        }
        finally
        {
            _isBatchUpdatingCustomArrayCorners = false;
        }

        _rebuild();
        NotifySelectedCustomArrayCornerChanged();
    }

    private void NotifyCustomArtifactsChanged()
    {
        ClearCustomArtifactsCommand.NotifyCanExecuteChanged();
        _propertyChanged(nameof(HasCustomArtifacts));
        _propertyChanged(nameof(CustomArtifactSummaryText));
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
                _customArtifacts.PointCount,
                _customArtifacts.PathCount)
            : "Custom artifacts disabled.";

    private void SetValue<T>(ref T field, T value, string name, Action? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        _propertyChanging(name);
        field = value;
        changed?.Invoke();
        _propertyChanged(name);
    }
}
