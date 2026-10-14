namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the notch algorithm settings that the main view model edits as plain values: version
/// switches, thresholds, To Regular and To Full gates and the Step 3 caps. The view model replaces
/// the whole value when one of them changes and forwards one property per member so the bindings
/// keep their names.
/// </summary>
/// <param name="EnableV21">Whether Notch Algorithm Version 2.1 is enabled.</param>
/// <param name="EnableV22">Whether the Notch 2.2 output path is enabled.</param>
/// <param name="LenScale">The length scale factor for notch calculations.</param>
/// <param name="NullValue">The null value used in notch tables.</param>
/// <param name="NotchThresholdQ7">The v2.1 threshold gate in Q7 (/128).</param>
/// <param name="NotchThresholdPercent">The v2.2 threshold gate in percent.</param>
/// <param name="LinkNotchThresholds">Whether the v2.2 threshold follows the v2.1 threshold.</param>
/// <param name="EnableToRegular">Whether Notch 2.2 To Regular compensation is enabled.</param>
/// <param name="EnableToFull">Whether Notch 2.2 To Full compensation is enabled.</param>
/// <param name="ToFullStrictOverlapPercent">The strict overlap threshold (%) of the Notch 2.2 multi-owner gate.</param>
/// <param name="EnableToFullRuleEngine">Whether Step 3 uses the split To Full rule engine path.</param>
/// <param name="EnableToFullRuleTrace">Whether Step 3 keeps per-regular To Full rule trace entries.</param>
/// <param name="EnableBoundaryVirtualAreaCap">Whether Step 3 caps the virtual To Full area.</param>
/// <param name="BoundaryVirtualAreaCapPercent">The maximum virtual To Full area as a percent of the inside overlap area.</param>
/// <param name="EnableTargetCoverageGuard">Whether Step 3 caps the target-side coverage after the CurrentGain row selection.</param>
/// <param name="TargetCoverageCapPercent">The maximum target-side coverage percent for uniform-field safety.</param>
public sealed record NotchAlgorithmSettings(
    bool EnableV21,
    bool EnableV22,
    decimal LenScale,
    decimal NullValue,
    decimal NotchThresholdQ7,
    decimal NotchThresholdPercent,
    bool LinkNotchThresholds,
    bool EnableToRegular,
    bool EnableToFull,
    decimal ToFullStrictOverlapPercent,
    bool EnableToFullRuleEngine,
    bool EnableToFullRuleTrace,
    bool EnableBoundaryVirtualAreaCap,
    decimal BoundaryVirtualAreaCapPercent,
    bool EnableTargetCoverageGuard,
    decimal TargetCoverageCapPercent)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static NotchAlgorithmSettings Initial { get; } = new(
        false,
        false,
        0m,
        0m,
        0m,
        0m,
        true,
        true,
        true,
        0.1m,
        true,
        false,
        true,
        100m,
        true,
        120m);
}
