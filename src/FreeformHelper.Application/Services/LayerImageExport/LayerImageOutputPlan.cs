// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text;

namespace FreeformHelper.Application.Services;

/// <summary>Plans ordered image jobs with unique file stems without performing file IO.</summary>
public static class LayerImageOutputPlanner
{
    private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>Plans either one combined image or one image per layer, owning a copy of every published layer list.</summary>
    /// <remarks>Suffix collisions are resolved in input order using ordinal case-insensitive comparison.</remarks>
    public static IReadOnlyList<LayerImageOutputJob> Plan(
        IReadOnlyList<string> checkedLayers,
        LayerImageOutputMode mode,
        string baseName)
    {
        ArgumentNullException.ThrowIfNull(checkedLayers);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown layer image output mode.");
        }

        if (checkedLayers.Count == 0)
        {
            return Array.Empty<LayerImageOutputJob>();
        }

        if (mode == LayerImageOutputMode.OneImage)
        {
            return Array.AsReadOnly(new[]
            {
                new LayerImageOutputJob(baseName, Array.AsReadOnly(checkedLayers.ToArray())),
            });
        }

        var jobs = new List<LayerImageOutputJob>(checkedLayers.Count);
        var usedStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string layer in checkedLayers)
        {
            string stem = baseName + "_" + SanitizeLayerName(layer);
            string uniqueStem = stem;
            int suffix = 2;
            while (!usedStems.Add(uniqueStem))
            {
                uniqueStem = string.Create(CultureInfo.InvariantCulture, $"{stem}_{suffix}");
                suffix++;
            }

            jobs.Add(new LayerImageOutputJob(uniqueStem, Array.AsReadOnly(new[] { layer })));
        }

        return jobs.AsReadOnly();
    }

    private static string SanitizeLayerName(string name)
    {
        var result = new StringBuilder(name.Length);
        bool inWhitespace = false;
        foreach (char character in name)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!inWhitespace)
                {
                    result.Append('_');
                }

                inWhitespace = true;
            }
            else
            {
                result.Append(_invalidFileNameChars.Contains(character) ? '_' : character);
                inWhitespace = false;
            }
        }

        return result.ToString();
    }
}
