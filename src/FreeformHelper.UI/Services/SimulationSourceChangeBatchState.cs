// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Owns source revision and nesting-safe publication. Mutable state is UI-thread only.</summary>
internal sealed class SimulationSourceChangeBatchState(Action sourceChanged)
{
    private readonly Action _sourceChanged = sourceChanged;
    private int _suspendDepth;
    private bool _pending;

    public int Revision { get; private set; }

    public void Notify()
    {
        if (_suspendDepth > 0)
        {
            _pending = true;
            return;
        }

        Raise();
    }

    public IDisposable Begin()
    {
        _suspendDepth++;
        return new SimulationWorkspaceSourceChangeBatchScope(this);
    }

    private void End()
    {
        if (_suspendDepth <= 0)
        {
            return;
        }

        _suspendDepth--;
        if (_suspendDepth == 0 && _pending)
        {
            _pending = false;
            Raise();
        }
    }

    private void Raise()
    {
        Revision++;
        _sourceChanged();
    }

    private sealed class SimulationWorkspaceSourceChangeBatchScope(SimulationSourceChangeBatchState owner) : IDisposable
    {
        private SimulationSourceChangeBatchState? _owner = owner;

        public void Dispose()
        {
            if (_owner is null)
            {
                return;
            }

            _owner.End();
            _owner = null;
        }
    }
}
