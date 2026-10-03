namespace FreeformHelper.Domain.Pads;

/// <summary>
/// Defines the type of "freeform" behavior a <see cref="RegularPad"/> exhibits.
/// A freeform pad is a regular grid pad that is matched to a single CAD pad
/// which itself spans multiple regular grid cells.
/// </summary>
public enum FreeformType
{
    /// <summary>
    /// The regular pad is not a freeform pad; it is matched one-to-one with a CAD pad,
    /// or it is not an anchor for a multi-cell CAD pad.
    /// </summary>
    None = 0,
    /// <summary>
    /// The regular pad is part of a freeform structure that extends primarily in the X-direction (horizontally).
    /// This means one CAD pad spans multiple columns of regular pads in a single row.
    /// </summary>
    XWay = 1,
    /// <summary>
    /// The regular pad is part of a freeform structure that extends primarily in the Y-direction (vertically).
    /// This means one CAD pad spans multiple rows of regular pads in a single column.
    /// </summary>
    YWay = 2,
    /// <summary>
    /// The regular pad is part of a freeform structure that extends in both X and Y directions.
    /// This means one CAD pad spans multiple rows and multiple columns of regular pads.
    /// </summary>
    XYWay = 3,
}
