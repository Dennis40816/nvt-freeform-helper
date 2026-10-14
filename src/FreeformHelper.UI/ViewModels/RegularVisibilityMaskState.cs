using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the loaded regular visibility mask, its source path and the mask embedded in the project, for the main view model.
/// </summary>
internal sealed class RegularVisibilityMaskState
{
    /// <summary>Gets or sets the result of the last mask load or refresh.</summary>
    internal RegularVisibilityMaskResult Mask { get; set; } = RegularVisibilityMaskResult.NotFound();

    /// <summary>Gets or sets the path of the mask file that the user imported.</summary>
    internal string? SourcePath { get; set; }

    /// <summary>Gets or sets the bytes of the mask that the project file carries.</summary>
    internal byte[]? EmbeddedMask { get; set; }

    /// <summary>Gets or sets the name of the mask that the project file carries.</summary>
    internal string? EmbeddedName { get; set; }
}
