namespace FreeformHelper.Application.Services;

/// <summary>
/// Describes how the optional active-area pixel counts relate to the active-area size.
/// </summary>
public enum ActiveAreaPixelsStatus
{
    /// <summary>Neither pixel count is set.</summary>
    NotSet,

    /// <summary>Only one of the two pixel counts is set, so none is used.</summary>
    Incomplete,

    /// <summary>A pixel count is outside the allowed range, so none is used.</summary>
    Invalid,

    /// <summary>Both pixel counts are set and the pixel pitch agrees in X and Y.</summary>
    Ok,

    /// <summary>Both pixel counts are set but the pixel pitch differs between X and Y.</summary>
    PitchMismatch,
}

/// <summary>
/// The one result of checking the active-area pixel counts. Every consumer reads this value
/// instead of calculating the pixel pitch itself.
/// </summary>
/// <param name="Status">The state of the two pixel counts.</param>
/// <param name="PixelsX">The pixel count along X, or 0 when it is not usable.</param>
/// <param name="PixelsY">The pixel count along Y, or 0 when it is not usable.</param>
/// <param name="PitchXMm">The width of one pixel in millimeters, or 0 when it cannot be calculated.</param>
/// <param name="PitchYMm">The height of one pixel in millimeters, or 0 when it cannot be calculated.</param>
/// <param name="RelativeDifference">The pitch difference between X and Y as a share of the larger pitch.</param>
public readonly record struct ActiveAreaPixelsEvaluation(
    ActiveAreaPixelsStatus Status,
    int PixelsX,
    int PixelsY,
    double PitchXMm,
    double PitchYMm,
    double RelativeDifference)
{
    /// <summary>
    /// Gets a value indicating whether consumers may use <see cref="PixelsX"/> and <see cref="PixelsY"/>.
    /// </summary>
    public bool IsUsable => Status is ActiveAreaPixelsStatus.Ok or ActiveAreaPixelsStatus.PitchMismatch;
}

/// <summary>
/// Checks the optional pixel counts of the active area against its size in millimeters.
/// </summary>
public static class ActiveAreaPixelPitch
{
    /// <summary>The largest accepted pixel count, the same limit as the layer image export.</summary>
    public const int MaxPixels = 100_000;

    /// <summary>The pitch difference between X and Y above which a warning is due.</summary>
    public const double MismatchThreshold = 0.02;

    /// <summary>
    /// Evaluates the pixel counts of the active area.
    /// </summary>
    /// <param name="widthMm">The active area width in millimeters.</param>
    /// <param name="heightMm">The active area height in millimeters.</param>
    /// <param name="pixelsX">The pixel count along X, or <c>null</c> when it is not set.</param>
    /// <param name="pixelsY">The pixel count along Y, or <c>null</c> when it is not set.</param>
    /// <returns>The evaluation of the two pixel counts.</returns>
    public static ActiveAreaPixelsEvaluation Evaluate(double widthMm, double heightMm, int? pixelsX, int? pixelsY)
    {
        if (pixelsX is null && pixelsY is null)
        {
            return new ActiveAreaPixelsEvaluation(ActiveAreaPixelsStatus.NotSet, 0, 0, 0, 0, 0);
        }

        if (pixelsX is not { } x || pixelsY is not { } y)
        {
            return new ActiveAreaPixelsEvaluation(ActiveAreaPixelsStatus.Incomplete, 0, 0, 0, 0, 0);
        }

        if (x < 1 || x > MaxPixels || y < 1 || y > MaxPixels)
        {
            return new ActiveAreaPixelsEvaluation(ActiveAreaPixelsStatus.Invalid, 0, 0, 0, 0, 0);
        }

        if (widthMm <= 0 || heightMm <= 0)
        {
            return new ActiveAreaPixelsEvaluation(ActiveAreaPixelsStatus.Ok, x, y, 0, 0, 0);
        }

        var pitchX = widthMm / x;
        var pitchY = heightMm / y;
        var relativeDifference = Math.Abs(pitchX - pitchY) / Math.Max(pitchX, pitchY);
        var status = relativeDifference > MismatchThreshold
            ? ActiveAreaPixelsStatus.PitchMismatch
            : ActiveAreaPixelsStatus.Ok;
        return new ActiveAreaPixelsEvaluation(status, x, y, pitchX, pitchY, relativeDifference);
    }
}
