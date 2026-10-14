namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the flags that decide when the first grid build of the session starts and when the canvas fits after a grid build.
/// </summary>
internal sealed class GridStartupState
{
    /// <summary>Gets or sets a value indicating whether the canvas fits to the content after the next grid build.</summary>
    internal bool FitAfterNextBuild { get; set; }

    /// <summary>Gets or sets a value indicating whether the first grid build of the session was already marked.</summary>
    internal bool InitialGridBuiltMarked { get; set; }

    /// <summary>Gets or sets a value indicating whether the first grid build was requested and is not finished.</summary>
    internal bool InitialBuildRequested { get; set; }
}
