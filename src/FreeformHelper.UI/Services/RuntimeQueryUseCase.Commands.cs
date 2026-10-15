using FreeformHelper.Application.Services;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    /// <summary>
    /// Runs one operation of a command and maps its failure to the failure response with the given code.
    /// An exception that <see cref="ExceptionPolicy.IsRecoverable"/> rejects propagates to the caller.
    /// </summary>
    /// <param name="operationAsync">The operation to run.</param>
    /// <param name="failureCode">The CLI contract code of the failure response.</param>
    /// <param name="describeFailure">Builds the failure message from the exception.</param>
    /// <returns>The failure response, or null when the operation succeeded.</returns>
    internal static async Task<RuntimeQueryResponseEnvelope?> RunCommandOperationAsync(
        Func<Task> operationAsync,
        string failureCode,
        Func<Exception, string> describeFailure)
    {
        ArgumentNullException.ThrowIfNull(operationAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        ArgumentNullException.ThrowIfNull(describeFailure);

        try
        {
            await operationAsync();
            return null;
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            return RuntimeQueryResponseEnvelope.Failure(code: failureCode, message: describeFailure(ex));
        }
    }
}
