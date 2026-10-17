using System.Diagnostics;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Finds the process that started the spinner, so the spinner can close when that process is gone.
/// </summary>
internal static class SpinnerParentProcess
{
    internal static bool TryGet(int processId, out Process? process)
    {
        return TryGet(processId, Process.GetProcessById, out process);
    }

    /// <summary>
    /// Returns false when the process is not there. <see cref="Process.GetProcessById(int)"/> documents
    /// <see cref="ArgumentException"/> (no running process has this id) and <see cref="InvalidOperationException"/>
    /// (the process cannot be reached). Any other exception reaches the caller.
    /// </summary>
    internal static bool TryGet(int processId, Func<int, Process> getProcessById, out Process? process)
    {
        try
        {
            process = getProcessById(processId);
            return true;
        }
        catch (ArgumentException)
        {
            process = null;
            return false;
        }
        catch (InvalidOperationException)
        {
            process = null;
            return false;
        }
    }
}
