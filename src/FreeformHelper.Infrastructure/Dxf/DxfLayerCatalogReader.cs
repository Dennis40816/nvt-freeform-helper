namespace FreeformHelper.Infrastructure.Dxf;

/// <summary>
/// Reads DXF layer definitions and content categories from ASCII DXF files.
/// </summary>
public sealed class DxfLayerCatalogReader
{
    /// <summary>
    /// Reads a catalog from a DXF file path.
    /// </summary>
    public static DxfLayerCatalog ReadFromPath(string path)
    {
        using var sr = new StreamReader(path);
        return ReadFromReader(sr);
    }

    /// <summary>
    /// Reads a catalog from a DXF stream.
    /// </summary>
    public static DxfLayerCatalog ReadFromStream(Stream stream)
    {
        using var sr = new StreamReader(stream);
        return ReadFromReader(sr);
    }

    private static DxfLayerCatalog ReadFromReader(TextReader reader)
    {
        return ReadFromPairs(DxfPadImporter.ReadPairs(reader).ToList());
    }

    internal static DxfLayerCatalog ReadFromPairs(IReadOnlyList<(int code, string value)> pairs)
    {
        var layers = new Dictionary<string, LayerAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in ReadLayerDefinitions(pairs))
        {
            var accumulator = GetOrCreate(layers, name);
            accumulator.IsDefinedInLayerTable = true;
        }

        var section = string.Empty;
        string? currentEntity = null;
        var currentLayer = "0";

        foreach (var (code, value) in pairs)
        {
            if (code == 0 && value == "SECTION")
            {
                section = string.Empty;
                continue;
            }

            if (string.IsNullOrEmpty(section) && code == 2)
            {
                section = value;
                continue;
            }

            if (code == 0 && value == "ENDSEC")
            {
                CommitCurrentEntity(section, currentEntity, currentLayer, layers);
                section = string.Empty;
                currentEntity = null;
                currentLayer = "0";
                continue;
            }

            if (!string.Equals(section, "ENTITIES", StringComparison.Ordinal) &&
                !string.Equals(section, "BLOCKS", StringComparison.Ordinal))
            {
                continue;
            }

            if (code == 0)
            {
                CommitCurrentEntity(section, currentEntity, currentLayer, layers);
                currentEntity = value;
                currentLayer = "0";
                continue;
            }

            if (code == 8)
            {
                currentLayer = value;
            }
        }

        CommitCurrentEntity(section, currentEntity, currentLayer, layers);

        var items = layers
            .Values
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static item => new DxfLayerCatalogItem(
                item.Name,
                item.IsDefinedInLayerTable,
                item.ContentKinds,
                item.EntityCount))
            .ToList();

        return new DxfLayerCatalog(items);
    }

    private static void CommitCurrentEntity(
        string section,
        string? entityType,
        string layerName,
        Dictionary<string, LayerAccumulator> layers)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return;
        }

        if (string.Equals(entityType, "BLOCK", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entityType, "ENDBLK", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var accumulator = GetOrCreate(layers, layerName);
        accumulator.EntityCount++;

        if (string.Equals(section, "BLOCKS", StringComparison.Ordinal))
        {
            accumulator.ContentKinds |= DxfLayerContentKind.BlockSection;
        }
        else if (string.Equals(section, "ENTITIES", StringComparison.Ordinal))
        {
            accumulator.ContentKinds |= DxfLayerContentKind.EntitySection;
        }

        if (IsPolylineEntity(entityType))
        {
            accumulator.ContentKinds |= DxfLayerContentKind.Polyline;
            return;
        }

        if (IsTextEntity(entityType))
        {
            accumulator.ContentKinds |= DxfLayerContentKind.Text;
            return;
        }

        if (string.Equals(entityType, "INSERT", StringComparison.OrdinalIgnoreCase))
        {
            accumulator.ContentKinds |= DxfLayerContentKind.Insert;
            return;
        }

        accumulator.ContentKinds |= DxfLayerContentKind.Other;
    }

    private static bool IsPolylineEntity(string entityType)
    {
        return string.Equals(entityType, "LWPOLYLINE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entityType, "POLYLINE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextEntity(string entityType)
    {
        return string.Equals(entityType, "TEXT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entityType, "MTEXT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entityType, "ATTDEF", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entityType, "ATTRIB", StringComparison.OrdinalIgnoreCase);
    }

    private static LayerAccumulator GetOrCreate(
        Dictionary<string, LayerAccumulator> layers,
        string layerName)
    {
        var normalized = string.IsNullOrWhiteSpace(layerName) ? "0" : layerName;
        if (!layers.TryGetValue(normalized, out var accumulator))
        {
            accumulator = new LayerAccumulator(normalized);
            layers[normalized] = accumulator;
        }

        return accumulator;
    }

    private static List<string> ReadLayerDefinitions(IReadOnlyList<(int code, string value)> pairs)
    {
        var result = new List<string>();
        var inLayerTable = false;
        var inLayerRecord = false;
        string? currentLayerName = null;

        foreach (var (code, value) in pairs)
        {
            if (!inLayerTable)
            {
                if (code == 0 && value == "TABLE")
                {
                    inLayerRecord = false;
                    currentLayerName = null;
                }
                else if (code == 2 && value == "LAYER")
                {
                    inLayerTable = true;
                    inLayerRecord = false;
                    currentLayerName = null;
                }

                continue;
            }

            if (code == 0 && value == "ENDTAB")
            {
                if (!string.IsNullOrWhiteSpace(currentLayerName))
                {
                    result.Add(currentLayerName);
                }

                break;
            }

            if (code == 0 && value == "LAYER")
            {
                if (!string.IsNullOrWhiteSpace(currentLayerName))
                {
                    result.Add(currentLayerName);
                }

                inLayerRecord = true;
                currentLayerName = null;
                continue;
            }

            if (inLayerRecord && code == 2 && string.IsNullOrWhiteSpace(currentLayerName))
            {
                currentLayerName = value;
            }
        }

        return result;
    }

    private sealed class LayerAccumulator
    {
        public LayerAccumulator(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool IsDefinedInLayerTable { get; set; }

        public DxfLayerContentKind ContentKinds { get; set; }

        public int EntityCount { get; set; }
    }
}

/// <summary>
/// Represents DXF layer category details parsed from a DXF file.
/// </summary>
public sealed class DxfLayerCatalog
{
    public static DxfLayerCatalog Empty { get; } = new(Array.Empty<DxfLayerCatalogItem>());

    public DxfLayerCatalog(IReadOnlyList<DxfLayerCatalogItem> layers)
    {
        Layers = layers ?? Array.Empty<DxfLayerCatalogItem>();
        ByName = Layers
            .GroupBy(static layer => layer.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<DxfLayerCatalogItem> Layers { get; }

    public IReadOnlyDictionary<string, DxfLayerCatalogItem> ByName { get; }

    public bool TryGetLayer(string layerName, out DxfLayerCatalogItem item)
    {
        return ByName.TryGetValue(layerName, out item!);
    }
}

/// <summary>
/// Represents one DXF layer and parsed content categories.
/// </summary>
public sealed class DxfLayerCatalogItem
{
    public DxfLayerCatalogItem(
        string name,
        bool isDefinedInLayerTable,
        DxfLayerContentKind contentKinds,
        int entityCount)
    {
        Name = name;
        IsDefinedInLayerTable = isDefinedInLayerTable;
        ContentKinds = contentKinds;
        EntityCount = entityCount;
    }

    public string Name { get; }

    public bool IsDefinedInLayerTable { get; }

    public DxfLayerContentKind ContentKinds { get; }

    public int EntityCount { get; }

    public bool HasPolylineContent => (ContentKinds & DxfLayerContentKind.Polyline) != 0;

    public bool HasTextContent => (ContentKinds & DxfLayerContentKind.Text) != 0;

    public bool HasBlockSectionContent => (ContentKinds & DxfLayerContentKind.BlockSection) != 0;

    public bool HasEntitySectionContent => (ContentKinds & DxfLayerContentKind.EntitySection) != 0;
}

/// <summary>
/// Flags describing which content types appear on a DXF layer.
/// </summary>
[Flags]
public enum DxfLayerContentKind
{
    None = 0,
    Polyline = 1 << 0,
    Text = 1 << 1,
    BlockSection = 1 << 2,
    Insert = 1 << 3,
    Other = 1 << 4,
    EntitySection = 1 << 5,
}
