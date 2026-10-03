using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Represents the ViewModel for the "How To Use" section of the application.
/// It manages properties related to language selection for documentation display.
/// </summary>
public sealed partial class HowToUseViewModel : ObservableObject
{
    /// <summary>
    /// Gets or sets a value indicating whether the display language is Chinese.
    /// </summary>
    [ObservableProperty]
    private bool _isChinese;

    /// <summary>
    /// Gets a value indicating whether the display language is English.
    /// This is a derived property based on <see cref="IsChinese"/>.
    /// </summary>
    public bool IsEnglish => !IsChinese;

    /// <summary>
    /// Partial method invoked when the <see cref="IsChinese"/> property changes.
    /// Notifies listeners that the <see cref="IsEnglish"/> property has also effectively changed.
    /// </summary>
    /// <param name="value">The new value of <see cref="IsChinese"/>.</param>
    partial void OnIsChineseChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEnglish));
    }
}
