namespace FreeformHelper.Application.Export;

/// <summary>
/// Controls how much diagnostic detail is emitted by notch text export.
/// </summary>
public enum NotchExportProfile
{
    /// <summary>
    /// Minimal firmware-oriented output. No-op v2.2 rows and simulation-only helpers are pruned.
    /// </summary>
    Release = 0,

    /// <summary>
    /// Full debug output. Keeps no-op rows and FW simulation baseline helpers for traceability.
    /// </summary>
    Debug = 1,
}
