namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Provides stable display labels for notch algorithm versions.
/// </summary>
public static class NotchAlgorithmVersionDisplay
{
    /// <summary>
    /// Converts internal version enum to user-facing label.
    /// </summary>
    public static string ToDisplayLabel(this NotchAlgorithmVersion version)
    {
        return version switch
        {
            NotchAlgorithmVersion.V21 => "v2.1",
            NotchAlgorithmVersion.V22 => "v2.2",
            _ => $"v{(int)version}"
        };
    }
}

