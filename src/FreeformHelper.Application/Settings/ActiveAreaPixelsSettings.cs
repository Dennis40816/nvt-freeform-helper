namespace FreeformHelper.Application.Settings;

/// <summary>
/// Holds the optional panel pixel counts of the active area. A <c>null</c> value means the project does not state it.
/// Nothing rebuilds from these values.
/// </summary>
public sealed class ActiveAreaPixelsSettings
{
    /// <summary>
    /// Gets or sets the number of panel pixels across the active area width.
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    /// Gets or sets the number of panel pixels across the active area height.
    /// </summary>
    public int? Y { get; set; }
}
