// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Projects existing live values through their original setters; implementations add no storage.</summary>
internal interface ISettingsAppearanceValues
{
    decimal CadFillOpacity { get; set; }
    decimal CadLineOpacity { get; set; }
    decimal RegularFillOpacity { get; set; }
    decimal RegularLineOpacity { get; set; }
    decimal AreaBucketTolerance { get; set; }
    decimal MaxAreaBuckets { get; set; }
    decimal CadLineWidth { get; set; }
    decimal RegularLineWidth { get; set; }
    decimal RegularSelectedFillOpacity { get; set; }
    string CadLineColorHex { get; set; }
    string RegularLineColorHex { get; set; }
    string RegularSelectedColorHex { get; set; }
    bool ShowCad { get; set; }
    bool ShowRegular { get; set; }
    bool HighlightUnmatched { get; set; }
    bool HighlightFreeform { get; set; }
    bool ColorCadByArea { get; set; }
}
