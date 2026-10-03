using System.Diagnostics;
using System.Globalization;
using NLog;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Central startup/load perf markers for log-based baseline scripts.
/// Emits stable key-value lines so scripts can parse without relying on localized text.
/// </summary>
internal static class StartupPerfTracker
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
    private static long _lastElapsedMs;
    private static int _markerIndex;
    private static readonly bool EmitAsInfo = IsTruthy(Environment.GetEnvironmentVariable("FREEFORM_PERF_MARKERS_INFO"));

    public static void Mark(string stage, string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return;
        }

        var elapsedMs = Stopwatch.ElapsedMilliseconds;
        var previous = Interlocked.Exchange(ref _lastElapsedMs, elapsedMs);
        var deltaMs = elapsedMs - previous;
        if (deltaMs < 0)
        {
            deltaMs = 0;
        }

        var index = Interlocked.Increment(ref _markerIndex);
        if (string.IsNullOrWhiteSpace(detail))
        {
            LogMarker(
                index,
                stage.Trim(),
                elapsedMs,
                deltaMs,
                null);
            return;
        }

        LogMarker(
            index,
            stage.Trim(),
            elapsedMs,
            deltaMs,
            detail.Trim());
    }

    private static void LogMarker(int index, string stage, long elapsedMs, long deltaMs, string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            const string message = "PERF STARTUP #{0}: stage={1}; elapsed={2}ms; delta={3}ms.";
            if (EmitAsInfo)
            {
                Logger.Info(CultureInfo.InvariantCulture, message, index, stage, elapsedMs, deltaMs);
            }
            else
            {
                Logger.Debug(CultureInfo.InvariantCulture, message, index, stage, elapsedMs, deltaMs);
            }

            return;
        }

        const string detailMessage = "PERF STARTUP #{0}: stage={1}; elapsed={2}ms; delta={3}ms; detail={4}.";
        if (EmitAsInfo)
        {
            Logger.Info(CultureInfo.InvariantCulture, detailMessage, index, stage, elapsedMs, deltaMs, detail);
        }
        else
        {
            Logger.Debug(CultureInfo.InvariantCulture, detailMessage, index, stage, elapsedMs, deltaMs, detail);
        }
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }
}
