namespace FreeformHelper.Application.Services;

/// <summary>
/// Matches DXF layer names against a free-text search query.
/// </summary>
public static class LayerNameSearch
{
    /// <summary>
    /// Returns whether the layer name contains every whitespace-separated term of the query.
    /// </summary>
    /// <param name="layerName">The layer name to test.</param>
    /// <param name="query">The search text. A null, empty, or whitespace query matches every name.</param>
    /// <returns><see langword="true"/> when every term occurs in the name, ignoring case.</returns>
    public static bool Matches(string layerName, string? query)
    {
        ArgumentNullException.ThrowIfNull(layerName);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        foreach (string term in query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!layerName.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
