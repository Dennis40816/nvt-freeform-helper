// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>Captures the inputs for one resolved Notch computation without copying its source collections.</summary>
internal sealed record NotchComputationRequest(
    RegularGrid Grid,
    CadPad CadPad,
    IReadOnlyList<CadPad> AllCadPads,
    IReadOnlySet<int>? ActiveRegularPadIds,
    ProjectSettings Settings,
    NotchCompensationModel CompensationModel,
    bool EnableToRegular,
    bool EnableToFull,
    bool EnableToFullRuleEngine,
    bool EnableToFullRuleTrace,
    double StrictOverlapRatio,
    int? AnchorIcIndex,
    int? AnchorDiffIndex,
    NotchV22TargetAllocationAreaMode AllocationAreaMode,
    ulong CadPoolFingerprint,
    int ActiveRegularHash,
    ulong GridFingerprint);
