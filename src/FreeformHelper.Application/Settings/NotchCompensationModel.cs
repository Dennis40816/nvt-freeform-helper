namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines Step3 compensation model presets used by Notch 2.2 generation.
/// </summary>
public enum NotchCompensationModel
{
    /// <summary>
    /// Current model: ToRegular/ToFull diagnostics are enabled; v2.2 rows use target-regular Stage3 coverage.
    /// </summary>
    CurrentGain = 0,

    /// <summary>
    /// Conservative model: v2.2 rows use target-regular source overlap coverage; ToFull remains support/cap only.
    /// </summary>
    ConservativeNoGain = 1,

    /// <summary>
    /// Disable To Regular and To Full for comparison baseline.
    /// </summary>
    Disabled = 2,
}
