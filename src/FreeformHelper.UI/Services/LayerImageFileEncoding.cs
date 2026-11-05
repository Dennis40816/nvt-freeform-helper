// Copyright (c) 2026 Dennis Liu. All rights reserved.

using SkiaSharp;

namespace FreeformHelper.UI.Services;

/// <summary>Names the file extension of an image format and the function that encodes a bitmap into it.</summary>
/// <param name="Extension">The extension without the dot, for example <c>png</c>.</param>
/// <param name="Encode">Writes the bitmap to the stream in the format.</param>
public sealed record LayerImageFileEncoding(string Extension, Action<SKBitmap, Stream> Encode);
