namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the suspend depth and the pending flag of the simulation workspace source change batch.
/// </summary>
internal sealed class SimulationSourceChangeBatchState
{
    /// <summary>Gets or sets how many batches are open. A change notification waits while it is above zero.</summary>
    internal int SuspendDepth { get; set; }

    /// <summary>Gets or sets a value indicating whether a change arrived while a batch was open.</summary>
    internal bool Pending { get; set; }
}
