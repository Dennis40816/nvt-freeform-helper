// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using GuideGenerationMode = FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode;

namespace FreeformHelper.UI.ViewModels;

internal static class CoordinatePlannerRequestBuilder
{
    internal static CoordinatePlannerRequest Build(
        CoordinatePlannerGeometryInput geometry,
        CoordinatePlannerGuideInput horizontal,
        CoordinatePlannerGuideInput vertical,
        CoordinatePlannerArrayInput array,
        IReadOnlyList<CoordinateCustomPointRequest> points,
        IReadOnlyList<CoordinateCustomPathRequest> paths)
    {
        return new CoordinatePlannerRequest(
            MachineOriginX: (double)geometry.MachineOriginX,
            MachineOriginY: (double)geometry.MachineOriginY,
            MachineWidth: Math.Max(0.001, (double)geometry.MachineWidth),
            MachineHeight: Math.Max(0.001, (double)geometry.MachineHeight),
            PixelWidth: PositiveInt(geometry.PixelWidth),
            PixelHeight: PositiveInt(geometry.PixelHeight),
            HorizontalGuideCount: NonNegativeInt(horizontal.Count),
            VerticalGuideCount: NonNegativeInt(vertical.Count),
            CopperPillarDiameter: Math.Max(0d, (double)geometry.CopperPillarDiameter),
            ShowBistRectangle: geometry.ShowBistRectangle,
            ShowCustomArray: array.ShowCustomArray,
            CustomArrayColumnCount: NonNegativeInt(array.ColumnCount),
            CustomArrayRowCount: NonNegativeInt(array.RowCount),
            CustomArrayTopLeftMachineX: (double)array.TopLeftMachineX,
            CustomArrayTopLeftMachineY: (double)array.TopLeftMachineY,
            CustomArrayTopRightMachineX: (double)array.TopRightMachineX,
            CustomArrayTopRightMachineY: (double)array.TopRightMachineY,
            CustomArrayBottomRightMachineX: (double)array.BottomRightMachineX,
            CustomArrayBottomRightMachineY: (double)array.BottomRightMachineY,
            CustomArrayBottomLeftMachineX: (double)array.BottomLeftMachineX,
            CustomArrayBottomLeftMachineY: (double)array.BottomLeftMachineY,
            HorizontalGuideMode: ToApplicationGuideMode(horizontal.Mode),
            VerticalGuideMode: ToApplicationGuideMode(vertical.Mode),
            HorizontalGuidePitch: Math.Max(0d, (double)horizontal.Pitch),
            VerticalGuidePitch: Math.Max(0d, (double)vertical.Pitch),
            HorizontalGuideInset: Math.Max(0d, (double)horizontal.Inset),
            VerticalGuideInset: Math.Max(0d, (double)vertical.Inset),
            HorizontalGuidePositions: ParsePositions(horizontal.Positions),
            VerticalGuidePositions: ParsePositions(vertical.Positions),
            CustomPoints: points,
            CustomPaths: paths);
    }

    internal static CoordinatePlannerRequest FromDefaults(double width, double height, int pixelWidth, int pixelHeight)
    {
        return new CoordinatePlannerRequest(
            0d,
            0d,
            Math.Max(0.001, width),
            Math.Max(0.001, height),
            Math.Max(1, pixelWidth),
            Math.Max(1, pixelHeight),
            5,
            5,
            0d,
            true,
            false,
            5,
            5,
            0d,
            0d,
            Math.Max(0.001, width),
            0d,
            Math.Max(0.001, width),
            Math.Max(0.001, height),
            0d,
            Math.Max(0.001, height));
    }

    internal static CoordinateGuideGenerationMode ToApplicationGuideMode(GuideGenerationMode mode) =>
        mode switch
        {
            GuideGenerationMode.Pitch => CoordinateGuideGenerationMode.Pitch,
            GuideGenerationMode.ExplicitPositions => CoordinateGuideGenerationMode.ExplicitPositions,
            _ => CoordinateGuideGenerationMode.Count,
        };

    internal static double[] ParsePositions(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<double>();
        }

        return text
            .Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? (double?)value : null)
            .Where(static value => value.HasValue)
            .Select(static value => Math.Max(0d, value!.Value))
            .ToArray();
    }

    internal static int PositiveInt(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    internal static int NonNegativeInt(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Max(0, rounded);
    }
}

internal sealed record CoordinatePlannerGeometryInput(
    decimal MachineOriginX,
    decimal MachineOriginY,
    decimal MachineWidth,
    decimal MachineHeight,
    decimal PixelWidth,
    decimal PixelHeight,
    decimal CopperPillarDiameter,
    bool ShowBistRectangle);

internal sealed record CoordinatePlannerGuideInput(
    decimal Count,
    GuideGenerationMode Mode,
    decimal Pitch,
    decimal Inset,
    string Positions);

internal sealed record CoordinatePlannerArrayInput(
    bool ShowCustomArray,
    decimal ColumnCount,
    decimal RowCount,
    decimal TopLeftMachineX,
    decimal TopLeftMachineY,
    decimal TopRightMachineX,
    decimal TopRightMachineY,
    decimal BottomRightMachineX,
    decimal BottomRightMachineY,
    decimal BottomLeftMachineX,
    decimal BottomLeftMachineY);
