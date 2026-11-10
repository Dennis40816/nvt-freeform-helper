// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal static class NotchExportFingerprints
{
    internal static int ComputeNotchExportSettingsFingerprint(ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        unchecked
        {
            var notch = settings.Notch;
            var hash = 17;
            hash = (hash * 31) + (int)notch.ComputationMode;
            hash = (hash * 31) + (int)notch.CompensationModel;
            hash = (hash * 31) + notch.LenScale;
            hash = (hash * 31) + notch.NullValue;
            hash = (hash * 31) + notch.ThresholdQ7;
            hash = (hash * 31) + notch.ThresholdPercentV22.GetHashCode();
            hash = (hash * 31) + (notch.LinkVersionThresholds ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToRegular ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToFull ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToFullRuleEngine ? 1 : 0);
            hash = (hash * 31) + (notch.EnableToFullRuleTrace ? 1 : 0);
            hash = (hash * 31) + (notch.EnableBoundaryVirtualAreaCap ? 1 : 0);
            hash = (hash * 31) + notch.BoundaryVirtualAreaCapRatio.GetHashCode();
            hash = (hash * 31) + (notch.EnableTargetCoverageGuard ? 1 : 0);
            hash = (hash * 31) + notch.TargetCoverageCapPercent;
            hash = (hash * 31) + notch.EnabledVersions.Count;
            hash = (hash * 31) + notch.MultiOwnerStrictOverlapPercent.GetHashCode();
            foreach (var version in notch.EnabledVersions.OrderBy(static version => (int)version))
            {
                hash = (hash * 31) + (int)version;
            }

            return hash;
        }
    }

    internal static int CombineNotchExportSettingsFingerprint(
        int settingsFingerprint,
        int activeRegularHash,
        int cadOutputFwDiffHash)
    {
        unchecked
        {
            var hash = (settingsFingerprint * 31) + activeRegularHash;
            return (hash * 31) + cadOutputFwDiffHash;
        }
    }

    internal static int ComputeCadOutputFwDiffHash(IReadOnlyDictionary<int, int> cadOutputFwDiffIndexByCadId)
    {
        ArgumentNullException.ThrowIfNull(cadOutputFwDiffIndexByCadId);

        unchecked
        {
            var hash = 17;
            foreach (var pair in cadOutputFwDiffIndexByCadId.OrderBy(static pair => pair.Key))
            {
                hash = (hash * 31) + pair.Key;
                hash = (hash * 31) + pair.Value;
            }

            return hash;
        }
    }

    internal static int ComputeActiveRegularHash(IReadOnlySet<int>? activeRegularPadIds)
    {
        if (activeRegularPadIds is null || activeRegularPadIds.Count == 0)
        {
            return -1;
        }

        unchecked
        {
            var hash = 17;
            foreach (var id in activeRegularPadIds.OrderBy(static id => id))
            {
                hash = (hash * 31) + id;
            }

            return hash;
        }
    }

    internal static ulong ComputeCadPoolFingerprint(IReadOnlyList<CadPad> cadPads)
    {
        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)cadPads.Count;
            foreach (var cadPad in cadPads)
            {
                hash = (hash * 1099511628211UL) ^ (uint)cadPad.Id;
            }

            return hash;
        }
    }

    internal static ulong BuildCadFingerprint(int cadRevision, int cadPadCount, int hiddenCadPadCount)
    {
        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)cadRevision;
            hash = (hash * 1099511628211UL) ^ (uint)cadPadCount;
            hash = (hash * 1099511628211UL) ^ (uint)(hiddenCadPadCount);
            return hash;
        }
    }

    internal static ulong BuildExportGridFingerprint(int gridRevision, int gridPadCount, int indexRevision)
    {
        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)gridRevision;
            hash = (hash * 1099511628211UL) ^ (uint)gridPadCount;
            hash = (hash * 1099511628211UL) ^ (uint)indexRevision;
            return hash;
        }
    }

    internal static ulong BuildComputationGridFingerprint(int gridRevision, int gridPadCount)
    {
        unchecked
        {
            var hash = 1469598103934665603UL;
            hash = (hash * 1099511628211UL) ^ (uint)gridRevision;
            hash = (hash * 1099511628211UL) ^ (uint)gridPadCount;
            return hash;
        }
    }
}
