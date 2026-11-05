using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the draft values of the active-area pixel counts in the settings window and the one text
/// that tells the user whether they fit the active-area size.
/// </summary>
public sealed partial class ActiveAreaPixelsDraft : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Evaluation), nameof(StatusText), nameof(HasStatus))]
    private decimal _pixelsX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Evaluation), nameof(StatusText), nameof(HasStatus))]
    private decimal _pixelsY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Evaluation), nameof(StatusText), nameof(HasStatus))]
    private decimal _widthMm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Evaluation), nameof(StatusText), nameof(HasStatus))]
    private decimal _heightMm;

    private decimal _loadedPixelsX;
    private decimal _loadedPixelsY;

    /// <summary>
    /// Gets a value indicating whether the user changed the X pixel count after the draft was loaded.
    /// </summary>
    public bool IsPixelsXEdited => PixelsX != _loadedPixelsX;

    /// <summary>
    /// Gets a value indicating whether the user changed the Y pixel count after the draft was loaded.
    /// </summary>
    public bool IsPixelsYEdited => PixelsY != _loadedPixelsY;

    /// <summary>
    /// Gets the check of the draft pixel counts against the draft active-area size.
    /// </summary>
    public ActiveAreaPixelsEvaluation Evaluation => ActiveAreaPixelPitch.Evaluate(
        (double)WidthMm,
        (double)HeightMm,
        PixelsX >= 1m ? (int)Math.Min(Math.Round(PixelsX), int.MaxValue) : null,
        PixelsY >= 1m ? (int)Math.Min(Math.Round(PixelsY), int.MaxValue) : null);

    /// <summary>
    /// Gets a value indicating whether <see cref="StatusText"/> holds a message.
    /// </summary>
    public bool HasStatus => Evaluation.Status != ActiveAreaPixelsStatus.NotSet;

    /// <summary>
    /// Gets the message that explains the state of the pixel counts. Warnings start with a word, not a color.
    /// </summary>
    public string StatusText
    {
        get
        {
            var evaluation = Evaluation;
            return evaluation.Status switch
            {
                ActiveAreaPixelsStatus.Incomplete => "Check: set both X and Y, or clear both.",
                ActiveAreaPixelsStatus.Invalid => string.Create(
                    CultureInfo.InvariantCulture,
                    $"Check: pixel counts must be 1 to {ActiveAreaPixelPitch.MaxPixels}."),
                ActiveAreaPixelsStatus.PitchMismatch => string.Create(
                    CultureInfo.InvariantCulture,
                    $"Check: pixel size differs, X {evaluation.PitchXMm:0.####} mm, Y {evaluation.PitchYMm:0.####} mm. Compare with the AA size."),
                ActiveAreaPixelsStatus.Ok when evaluation.PitchXMm > 0 => string.Create(
                    CultureInfo.InvariantCulture,
                    $"Pixel size X {evaluation.PitchXMm:0.####} mm, Y {evaluation.PitchYMm:0.####} mm."),
                _ => string.Empty,
            };
        }
    }

    /// <summary>
    /// Sets all four draft values at once from the owner settings.
    /// </summary>
    /// <param name="widthMm">The active area width in millimeters.</param>
    /// <param name="heightMm">The active area height in millimeters.</param>
    /// <param name="pixelsX">The pixel count along X, 0 when not set.</param>
    /// <param name="pixelsY">The pixel count along Y, 0 when not set.</param>
    public void Load(decimal widthMm, decimal heightMm, decimal pixelsX, decimal pixelsY)
    {
        WidthMm = widthMm;
        HeightMm = heightMm;
        PixelsX = pixelsX;
        PixelsY = pixelsY;
        _loadedPixelsX = pixelsX;
        _loadedPixelsY = pixelsY;
    }
}
