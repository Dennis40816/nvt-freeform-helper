using System.Reflection;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Tells a development build from a release build. A development build carries a pre-release suffix such as
/// <c>-g.4</c> in its version (the repository <c>VERSION</c> file). A release build has no suffix.
/// </summary>
internal static class AppBuildChannel
{
    /// <summary>
    /// Gets a value indicating whether this app is a development build.
    /// </summary>
    internal static bool IsDevelopmentBuild { get; } = IsDevelopmentVersion(
        typeof(AppBuildChannel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Tells whether <paramref name="informationalVersion"/> names a development build.
    /// An unknown version counts as a development build, so a missing version never hides a page.
    /// </summary>
    /// <param name="informationalVersion">The informational version, for example <c>1.0.0-g.4+abc123</c>.</param>
    /// <returns><see langword="true"/> when the version has a pre-release suffix or is unknown.</returns>
    internal static bool IsDevelopmentVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return true;
        }

        var metadataStart = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        var version = metadataStart < 0 ? informationalVersion : informationalVersion[..metadataStart];
        return version.Contains('-');
    }
}
