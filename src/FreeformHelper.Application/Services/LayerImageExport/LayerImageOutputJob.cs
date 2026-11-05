// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Describes one image without selecting an extension or writing a file.</summary>
/// <param name="FileStem">The output name without its extension.</param>
/// <param name="Layers">The ordered layer names to render together.</param>
public sealed record LayerImageOutputJob(string FileStem, IReadOnlyList<string> Layers);
