// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Security;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Normalizes a project load path and resolves rejection facts using the caller's existence probe.
/// </summary>
internal static class ProjectPathResolver
{
    internal static ProjectPathResolution Resolve(string? path, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ProjectPathResolution(null, "INVALID_ARGUMENTS", "Argument '--path' is required.");
        }

        var normalizedPath = TryGetFullProjectPath(path, out var pathError);
        if (normalizedPath is null)
        {
            return new ProjectPathResolution(null, "INVALID_ARGUMENTS", $"Invalid project path '{path}': {pathError!.Message}");
        }

        if (!fileExists(normalizedPath))
        {
            return new ProjectPathResolution(normalizedPath, "PATH_NOT_FOUND", $"Project file not found: {normalizedPath}");
        }

        return new ProjectPathResolution(normalizedPath, null, null);
    }

    // Path.GetFullPath documents exactly these four exception types; anything else is not an input problem.
    private static string? TryGetFullProjectPath(string path, out Exception? error)
    {
        error = null;
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (ArgumentException ex)
        {
            error = ex;
        }
        catch (NotSupportedException ex)
        {
            error = ex;
        }
        catch (PathTooLongException ex)
        {
            error = ex;
        }
        catch (SecurityException ex)
        {
            error = ex;
        }

        return null;
    }
}

internal readonly record struct ProjectPathResolution(string? NormalizedPath, string? Code, string? Message);
