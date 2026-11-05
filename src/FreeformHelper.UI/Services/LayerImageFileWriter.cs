// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>Renders and writes planned layer images in job order.</summary>
public static class LayerImageFileWriter
{
    /// <summary>Writes the chosen path for one job, or sibling stem files for several jobs.</summary>
    /// <remarks>Failures propagate unchanged; files written by earlier jobs remain on disk.</remarks>
    public static IReadOnlyList<string> Write(
        IReadOnlyList<LayerImageOutputJob> jobs,
        Func<string, IReadOnlyList<CadPad>> padsOfLayer,
        LayerImageSize size,
        decimal lineWidthPixels,
        bool useDarkTheme,
        LayerImageFileEncoding encoding,
        string chosenPath)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(padsOfLayer);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentException.ThrowIfNullOrWhiteSpace(chosenPath);
        string extension = "." + encoding.Extension;
        string? directory = Path.GetDirectoryName(chosenPath);
        string folder = string.IsNullOrEmpty(directory) ? "." : directory;
        var destinations = LayerImageOutputPaths.Resolve(jobs, folder, extension, chosenPath, File.Exists);
        var reserved = new HashSet<string>(destinations, StringComparer.OrdinalIgnoreCase);
        var writtenPaths = new List<string>(jobs.Count);
        for (int index = 0; index < jobs.Count; index++)
        {
            var job = jobs[index];
            string path = destinations[index];
            var layers = job.Layers.Select(name => new LayerImageLayer(name, padsOfLayer(name))).ToArray();
            var request = LayerImageRenderRequest.FromSize(layers, size, lineWidthPixels, useDarkTheme);
            using var bitmap = LayerImageRenderer.Render(request);
            Directory.CreateDirectory(folder);
            using var encoded = new MemoryStream();
            encoding.Encode(bitmap, encoded);
            using var stream = OpenDestination(jobs.Count == 1, ref path, job.FileStem, folder, extension, reserved);
            encoded.Position = 0;
            encoded.CopyTo(stream);
            writtenPaths.Add(path);
        }

        return writtenPaths.AsReadOnly();
    }

    // The path the user confirmed may be replaced. A generated sibling is created exclusively, and a file that
    // appeared after the names were chosen moves the job to the next free numbered name.
    private static FileStream OpenDestination(
        bool replaceChosenPath,
        ref string path,
        string stem,
        string folder,
        string extension,
        HashSet<string> reserved)
    {
        if (replaceChosenPath)
        {
            return File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (File.Exists(path))
            {
                path = LayerImageOutputPaths.NextFree(folder, stem, extension, reserved, File.Exists);
                reserved.Add(path);
            }
        }
    }
}
