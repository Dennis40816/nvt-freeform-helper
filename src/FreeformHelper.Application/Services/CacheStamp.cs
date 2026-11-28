// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace FreeformHelper.Application.Services;

/// <summary>
/// Shared int version stamp for cache invalidation.
/// Use this type for every new cache revision instead of a loose int field.
/// The stamp starts at 1 and wraps around without an overflow check.
/// </summary>
public sealed class CacheStamp
{
    private int _value = 1;

    /// <summary>Advances the stamp by one.</summary>
    public void Bump()
    {
        unchecked
        {
            _value++;
        }
    }

    /// <summary>Returns the current stamp.</summary>
    public int Read() => _value;
}
