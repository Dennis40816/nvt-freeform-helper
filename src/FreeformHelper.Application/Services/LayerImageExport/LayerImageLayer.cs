// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>Describes one ordered layer and its CAD pads for image rendering.</summary>
public sealed record LayerImageLayer(string Name, IReadOnlyList<CadPad> Pads);
