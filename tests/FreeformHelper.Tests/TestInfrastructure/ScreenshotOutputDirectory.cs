// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>The environment seam for opt-in visual evidence; absent variables never create a directory.</summary>
internal static class ScreenshotOutputDirectory
{
    public static string? Resolve(string variableName) => Resolve(variableName, Environment.ExpandEnvironmentVariables);

    internal static string? Resolve(string variableName, Func<string, string> expandVariables)
    {
        string reference = $"%{variableName}%";
        string path = expandVariables(reference);
        return string.IsNullOrWhiteSpace(path) || string.Equals(path, reference, StringComparison.Ordinal)
            ? null : Path.GetFullPath(path);
    }
}
