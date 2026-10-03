namespace FreeformHelper.Application.Settings;

/// <summary>
/// Specifies how notch rows are selected before algorithm-specific row construction.
/// </summary>
public enum NotchComputationMode
{
    /// <summary>
    /// Legacy path: regular pads are treated as anchors and use their matched CAD pad.
    /// This is preserved for compatibility with existing projects and outputs.
    /// </summary>
    LegacyRegularAnchor = 0,

    /// <summary>
    /// CAD-centric path: derive notch candidates from CAD-to-regular weighted allocation.
    /// Keeps a dedicated mode switch so projects can compare with legacy outputs.
    /// </summary>
    CadAllocation = 1,
}
