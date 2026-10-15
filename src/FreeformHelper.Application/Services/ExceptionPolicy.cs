namespace FreeformHelper.Application.Services;

/// <summary>
/// Names which exceptions a best-effort boundary may turn into a failure result.
/// </summary>
public static class ExceptionPolicy
{
    /// <summary>
    /// Returns false for exceptions that mean the process can no longer be trusted to continue: out of memory
    /// and its subclasses. A boundary must let those propagate instead of reporting them as an operation failure.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <returns>True when the boundary may report the exception as a failure of the operation.</returns>
    public static bool IsRecoverable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is not OutOfMemoryException;
    }
}
