// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Services;

/// <summary>Pairs a resolved result with the inputs used by the cache key.</summary>
internal readonly record struct NotchResolvedCacheEntry(
    int Revision,
    ulong CadPoolFingerprint,
    int ActiveRegularHash,
    ulong GridFingerprint,
    bool EnableToRegular,
    bool EnableToFull,
    bool EnableToFullRuleEngine,
    bool EnableToFullRuleTrace,
    double StrictOverlapRatio,
    int? AnchorIcIndex,
    int? AnchorDiffIndex,
    NotchV22TargetAllocationAreaMode TargetAllocationAreaMode,
    NotchV22ResolvedResult Resolved);
