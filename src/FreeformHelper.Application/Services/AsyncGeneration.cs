// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace FreeformHelper.Application.Services;

/// <summary>
/// Shared sequence for stale-result detection of asynchronous work.
/// Use this type for every new stale-result counter instead of a loose long field:
/// the owner calls <see cref="Next"/> when it starts a request and compares the result with <see cref="Read"/> when the request ends.
/// Both operations are atomic.
/// </summary>
public sealed class AsyncGeneration
{
    private long _value;

    /// <summary>Advances the sequence and returns the new value.</summary>
    public long Next() => Interlocked.Increment(ref _value);

    /// <summary>Returns the current value.</summary>
    public long Read() => Interlocked.Read(ref _value);
}
