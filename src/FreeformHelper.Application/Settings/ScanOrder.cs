using System.Diagnostics.CodeAnalysis;

namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines the scanning order for assigning AFE (Analog Front-End) indices to pads within the regular grid.
/// This determines the logical mapping from grid coordinates to a linear AFE index.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "Enum names mirror established panel scan-order labels and persisted project values.")]
public enum ScanOrder
{
    /// <summary>
    /// Scans from left to right, then from top to bottom.
    /// (Column major for X, then Row major for Y, with Y inverted).
    /// </summary>
    LeftToRight_TopToBottom = 0,
    /// <summary>
    /// Scans from right to left, then from top to bottom.
    /// (Column major for X, then Row major for Y, with X and Y inverted).
    /// </summary>
    RightToLeft_TopToBottom = 1,
    /// <summary>
    /// Scans from left to right, then from bottom to top.
    /// (Column major for X, then Row major for Y).
    /// </summary>
    LeftToRight_BottomToTop = 2,
    /// <summary>
    /// Scans from right to left, then from bottom to top.
    /// (Column major for X, then Row major for Y, with X inverted).
    /// </summary>
    RightToLeft_BottomToTop = 3,
}
