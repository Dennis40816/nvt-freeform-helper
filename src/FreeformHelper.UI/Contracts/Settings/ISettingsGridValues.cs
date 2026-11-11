// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Projects existing live values through their original setters; implementations add no storage.</summary>
internal interface ISettingsGridValues
{
    decimal XChannels { get; set; }
    decimal YChannels { get; set; }
    decimal CascadeNum { get; set; }
    decimal GridPaddingPercent { get; set; }
    decimal ActiveAreaWidth { get; set; }
    decimal ActiveAreaHeight { get; set; }
    decimal PanelBiasX { get; set; }
    decimal PanelBiasY { get; set; }
    bool RecalcBoundsOnLayerFilter { get; set; }
    ScanOrder SelectedScanOrder { get; set; }
    GridAlignmentMode GridAlignmentMode { get; set; }
    RegularSourceMode RegularSourceMode { get; set; }
    bool IsWidthRowLocal { get; set; }
    bool IsHeightColumnLocal { get; set; }
    bool UseLocalSizing { get; set; }
    decimal CoordinatePixelWidth { get; set; }
    decimal CoordinatePixelHeight { get; set; }
}
