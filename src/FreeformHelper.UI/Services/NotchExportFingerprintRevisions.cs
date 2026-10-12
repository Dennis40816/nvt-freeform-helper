// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Holds the three revision counters that feed the notch export fingerprints.</summary>
internal sealed class NotchExportFingerprintRevisions
{
    internal int Cad { get; private set; } = 1;

    internal int Grid { get; private set; } = 1;

    internal int Index { get; private set; } = 1;

    internal void BumpCad()
    {
        unchecked
        {
            Cad++;
        }
    }

    internal void BumpGrid()
    {
        unchecked
        {
            Grid++;
        }
    }

    internal void BumpIndex()
    {
        unchecked
        {
            Index++;
        }
    }
}
