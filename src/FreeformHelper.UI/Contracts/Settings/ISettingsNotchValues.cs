// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Projects existing live values through their original setters; implementations add no storage.</summary>
internal interface ISettingsNotchValues
{
    decimal LenScale { get; set; }
    decimal NullValue { get; set; }
    decimal NotchThresholdQ7 { get; set; }
    decimal NotchThresholdPercent { get; set; }
    decimal ToFullStrictOverlapPercent { get; set; }
    decimal BoundaryVirtualAreaCapPercent { get; set; }
    decimal TargetCoverageCapPercent { get; set; }
    decimal NotchPreviewVisualizationStep { get; set; }
    decimal NotchPreviewAutoPlayIntervalMs { get; set; }
    bool EnableV21 { get; set; }
    bool EnableV22 { get; set; }
    bool LinkNotchThresholds { get; set; }
    bool EnableToRegular { get; set; }
    bool EnableToFull { get; set; }
    bool EnableToFullRuleEngine { get; set; }
    bool EnableToFullRuleTrace { get; set; }
    bool EnableBoundaryVirtualAreaCap { get; set; }
    bool EnableTargetCoverageGuard { get; set; }
    bool ShowNotchCanvasPreview { get; set; }
    bool ShowNotchToRegularLabels { get; set; }
    bool NotchPreviewAutoPlayEnabled { get; set; }
}
