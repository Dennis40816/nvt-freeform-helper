// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.Application.Services;

/// <summary>Resolves the destination file of every planned job without touching an existing file.</summary>
public static class LayerImageOutputPaths
{
    /// <summary>
    /// One job writes to the path the user chose and confirmed. Several jobs write next to it, and a name that is
    /// already taken (on disk or by an earlier job) gets a number suffix, so nothing existing is overwritten.
    /// </summary>
    public static IReadOnlyList<string> Resolve(
        IReadOnlyList<LayerImageOutputJob> jobs,
        string folder,
        string extension,
        string chosenPath,
        Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(chosenPath);
        ArgumentNullException.ThrowIfNull(exists);
        if (jobs.Count == 1)
        {
            return Array.AsReadOnly(new[] { chosenPath });
        }

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new List<string>(jobs.Count);
        foreach (var job in jobs)
        {
            string path = Path.Combine(folder, job.FileStem + extension);
            for (int number = 2; exists(path) || taken.Contains(path); number++)
            {
                path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{job.FileStem} ({number}){extension}"));
            }

            taken.Add(path);
            paths.Add(path);
        }

        return paths.AsReadOnly();
    }
}
