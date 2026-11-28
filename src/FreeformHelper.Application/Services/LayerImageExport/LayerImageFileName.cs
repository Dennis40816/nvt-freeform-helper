// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;

namespace FreeformHelper.Application.Services;

/// <summary>Preserves layer names while replacing characters that cannot be used in a file name.</summary>
public static class LayerImageFileName
{
    /// <summary>Sanitizes a layer token without changing valid characters or the existing empty-name fallback.</summary>
    public static string SanitizeToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "layer";
        }

        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        return builder.Length == 0 ? "layer" : builder.ToString();
    }
}
