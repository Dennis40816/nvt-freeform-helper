using System.Diagnostics;
using System.Globalization;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Shared helpers for progress reporting across long-running UI operations.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private const double ProgressUiMinDelta = 0.01;

    private static Action<double> CreateUiProgressReporter(Action<double> setProgress, double minDelta = ProgressUiMinDelta)
    {
        ArgumentNullException.ThrowIfNull(setProgress);

        var lastProgress = -1.0;
        return progress =>
        {
            var clamped = Math.Clamp(progress, 0.0, 1.0);
            if (clamped < 1.0 && clamped - lastProgress < minDelta)
            {
                return;
            }

            lastProgress = clamped;
            if (UiThread.TryGetRunningDispatcher(out var dispatcher))
            {
                dispatcher!.Post(() => setProgress(clamped));
            }
            else
            {
                setProgress(clamped);
            }
        };
    }

    private async Task RunUiProgressOperationAsync(
        string operationName,
        Action<bool> setBusy,
        Action<double> setProgress,
        Func<Action<double>, Task> operationAsync,
        Action<Exception> onError,
        Action? onStart = null,
        bool showModalSpinner = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(setProgress);
        ArgumentNullException.ThrowIfNull(operationAsync);
        ArgumentNullException.ThrowIfNull(onError);

        async Task ExecuteCoreAsync()
        {
            var timer = Stopwatch.StartNew();
            try
            {
                setBusy(true);
                setProgress(0.0);
                onStart?.Invoke();

                var reportProgress = CreateUiProgressReporter(setProgress);
                await operationAsync(reportProgress);
            }
            catch (Exception ex)
            {
                onError(ex);
            }
            finally
            {
                timer.Stop();
                setProgress(1.0);
                setBusy(false);
                Logger.Info(CultureInfo.InvariantCulture, "{0} finished in {1} ms.", operationName, timer.ElapsedMilliseconds);
            }
        }

        if (showModalSpinner)
        {
            await RunWithModalLoadingSpinnerAsync(ExecuteCoreAsync);
            return;
        }

        await ExecuteCoreAsync();
    }

    private async Task RunUiOperationAsync(
        string operationName,
        Func<Task> operationAsync,
        Action<Exception> onError,
        Action? onStart = null,
        bool showModalSpinner = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operationAsync);
        ArgumentNullException.ThrowIfNull(onError);

        async Task ExecuteCoreAsync()
        {
            var timer = Stopwatch.StartNew();
            try
            {
                onStart?.Invoke();
                await operationAsync();
            }
            catch (Exception ex)
            {
                onError(ex);
            }
            finally
            {
                timer.Stop();
                Logger.Info(CultureInfo.InvariantCulture, "{0} finished in {1} ms.", operationName, timer.ElapsedMilliseconds);
            }
        }

        if (showModalSpinner)
        {
            await RunWithModalLoadingSpinnerAsync(ExecuteCoreAsync);
            return;
        }

        await ExecuteCoreAsync();
    }

    private async Task<TResult> RunUiOperationAsync<TResult>(
        string operationName,
        Func<Task<TResult>> operationAsync,
        Func<Exception, TResult> onError,
        Action? onStart = null,
        bool showModalSpinner = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operationAsync);
        ArgumentNullException.ThrowIfNull(onError);

        async Task<TResult> ExecuteCoreAsync()
        {
            var timer = Stopwatch.StartNew();
            try
            {
                onStart?.Invoke();
                return await operationAsync();
            }
            catch (Exception ex)
            {
                return onError(ex);
            }
            finally
            {
                timer.Stop();
                Logger.Info(CultureInfo.InvariantCulture, "{0} finished in {1} ms.", operationName, timer.ElapsedMilliseconds);
            }
        }

        if (showModalSpinner)
        {
            return await RunWithModalLoadingSpinnerAsync(ExecuteCoreAsync);
        }

        return await ExecuteCoreAsync();
    }
}

