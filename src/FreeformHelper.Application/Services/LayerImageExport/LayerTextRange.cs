// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Describes a substring to highlight in a layer name.</summary>
/// <param name="Start">The zero-based character index.</param>
/// <param name="Length">The number of characters.</param>
public readonly record struct LayerTextRange(int Start, int Length);
