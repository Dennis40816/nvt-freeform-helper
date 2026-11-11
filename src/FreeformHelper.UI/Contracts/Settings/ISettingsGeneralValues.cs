// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Projects existing live values through their original setters; implementations add no storage.</summary>
internal interface ISettingsGeneralValues
{
    bool AutoReplayStep2AfterProjectLoad { get; set; }
    decimal GlobalFontSizePercent { get; set; }
    decimal HighlightStrokeWidthAdjust { get; set; }
    bool ImportOnlyClosedPolylines { get; set; }
    bool ImportBlockPolylines { get; set; }
    string CoordinatePreferredAaOutlineLayerName { get; set; }
}
