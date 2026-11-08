// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Tests;

/// <summary>Drops every posted callback, like a dispatcher that no longer runs.</summary>
internal sealed class AbandonedSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
    }
}
