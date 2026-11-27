// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Presentation;

/// <summary>The part of Notch Detail that comes from one export-equivalent generation.</summary>
public sealed record NotchDetailExplanation(
    ImmutableArray<NotchSourcePresentation> Sources,
    string Message,
    ImmutableArray<RegularPad> CellPads,
    ImmutableArray<CadPad> NeighborCadPads)
{
    public static NotchDetailExplanation WithoutRows(string message) => new([], message, [], []);
}
