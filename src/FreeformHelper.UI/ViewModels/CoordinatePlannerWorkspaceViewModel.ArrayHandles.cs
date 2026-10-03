using System.Globalization;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CoordinatePlannerWorkspaceViewModel
{
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

        SelectArtifactRowByKey(key);
        StatusText = string.Format(
            CultureInfo.InvariantCulture,
            "4-point array {0} selected.",
            SelectedCustomArrayCornerOption.Display);
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

        var (machineX, machineY) = BuildMachinePointFromWorld(worldPoint);
        SetCustomArrayCornerMachinePoint(key, machineX, machineY);
        SelectArtifactRowByKey(key);
        return true;
    }

    public bool CompleteCustomArrayCornerEdit(string key)
    {
        if (!TrySelectCustomArrayCornerOption(key))
        {
            return false;
        }

        SelectArtifactRowByKey(key);
        StatusText = string.Format(
            CultureInfo.InvariantCulture,
            "4-point array {0} set to ({1:0.###}, {2:0.###}) mm.",
            SelectedCustomArrayCornerOption.Display,
            SelectedCustomArrayCornerMachineX,
            SelectedCustomArrayCornerMachineY);
        return true;
    }

    private void SetSelectedCustomArrayCornerMachineX(decimal value)
    {
        SetCustomArrayCornerMachinePoint(
            SelectedCustomArrayCornerOption.Key,
            value,
            SelectedCustomArrayCornerMachineY);
        SelectArtifactRowByKey(SelectedCustomArrayCornerOption.Key);
    }

    private void SetSelectedCustomArrayCornerMachineY(decimal value)
    {
        SetCustomArrayCornerMachinePoint(
            SelectedCustomArrayCornerOption.Key,
            SelectedCustomArrayCornerMachineX,
            value);
        SelectArtifactRowByKey(SelectedCustomArrayCornerOption.Key);
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
        if (!IsCustomArrayCornerKey(key))
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

        RebuildSnapshot();
        NotifySelectedCustomArrayCornerChanged();
    }

    private void OnCustomArrayCornerValueChanged(string key)
    {
        if (_isBatchUpdatingCustomArrayCorners)
        {
            return;
        }

        RebuildSnapshot();
        if (string.Equals(SelectedCustomArrayCornerOption.Key, key, StringComparison.Ordinal))
        {
            NotifySelectedCustomArrayCornerChanged();
        }
    }

    private void NotifySelectedCustomArrayCornerChanged()
    {
        OnPropertyChanged(nameof(SelectedCustomArrayCornerMachineX));
        OnPropertyChanged(nameof(SelectedCustomArrayCornerMachineY));
        OnPropertyChanged(nameof(SelectedCustomArrayCornerSummaryText));
    }

    private void SelectArtifactRowByKey(string key)
    {
        var row = ArtifactRows.FirstOrDefault(artifact =>
            string.Equals(artifact.Key, key, StringComparison.Ordinal));
        if (row is not null)
        {
            SelectedArtifactRow = row;
        }
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

    private (decimal X, decimal Y) BuildMachinePointFromWorld(Point2 worldPoint)
    {
        var bounds = _snapshot.ActiveAreaBounds;
        var normX = bounds.Width <= 1e-9
            ? 0d
            : (worldPoint.X - bounds.MinX) / bounds.Width;
        var normY = bounds.Height <= 1e-9
            ? 0d
            : (bounds.MaxY - worldPoint.Y) / bounds.Height;
        normX = Math.Clamp(normX, 0d, 1d);
        normY = Math.Clamp(normY, 0d, 1d);

        var machineWidth = Math.Max(0.001m, MachineWidth);
        var machineHeight = Math.Max(0.001m, MachineHeight);
        return (
            RoundMachineCoordinate(MachineOriginX + (machineWidth * (decimal)normX)),
            RoundMachineCoordinate(MachineOriginY + (machineHeight * (decimal)normY)));
    }

    private static bool IsCustomArrayCornerKey(string key) =>
        key is "array-tl" or "array-tr" or "array-br" or "array-bl";

    private static decimal RoundMachineCoordinate(decimal value) =>
        decimal.Round(value, 3, MidpointRounding.AwayFromZero);
}
