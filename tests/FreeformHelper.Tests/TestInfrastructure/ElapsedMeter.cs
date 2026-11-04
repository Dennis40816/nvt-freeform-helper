namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Measures elapsed time for a report (a benchmark or a log line). It reads <see cref="TimeProvider.System"/>
/// and has the shape of <c>System.Diagnostics.Stopwatch</c> (StartNew, Stop, ElapsedMilliseconds), so a
/// measurement converts by changing the type name. Do not assert on a value from this type: an assertion on
/// elapsed time is a flaky test, and the test debt ledger counts it (elapsedAssertionsInTests). Assert on
/// state with a controllable time provider instead.
/// </summary>
internal sealed class ElapsedMeter
{
    private readonly TimeProvider _timeProvider;
    private readonly long _startTimestamp;
    private long? _stopTimestamp;

    private ElapsedMeter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _startTimestamp = timeProvider.GetTimestamp();
    }

    public TimeSpan Elapsed => _timeProvider.GetElapsedTime(_startTimestamp, _stopTimestamp ?? _timeProvider.GetTimestamp());

    public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;

    public static ElapsedMeter StartNew()
    {
        return new ElapsedMeter(TimeProvider.System);
    }

    public void Stop()
    {
        _stopTimestamp ??= _timeProvider.GetTimestamp();
    }
}
