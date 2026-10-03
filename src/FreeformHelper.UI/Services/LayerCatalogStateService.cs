using System.Globalization;
using FreeformHelper.Infrastructure.Dxf;
using NLog;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Owns DXF layer catalog loading and cache state for the workspace.
/// </summary>
public sealed class LayerCatalogStateService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly DxfLayerCatalogReader _reader;
    private DxfLayerCatalog _catalog = DxfLayerCatalog.Empty;
    private DxfLayerCatalog _cache = DxfLayerCatalog.Empty;
    private string? _cachePath;
    private long _cachePathLength = -1;
    private DateTime _cachePathWriteUtc;
    private int _cacheEmbeddedFingerprint;
    private int _cacheEmbeddedLength = -1;

    public LayerCatalogStateService(DxfLayerCatalogReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public DxfLayerCatalog Catalog => _catalog;

    public void Clear()
    {
        _catalog = DxfLayerCatalog.Empty;
    }

    public void TryLoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Clear();
            return;
        }

        var fileInfo = new FileInfo(path);
        var writeUtc = fileInfo.LastWriteTimeUtc;
        var length = fileInfo.Length;
        if (_cache.Layers.Count > 0 &&
            !string.IsNullOrWhiteSpace(_cachePath) &&
            string.Equals(_cachePath, path, StringComparison.OrdinalIgnoreCase) &&
            _cachePathLength == length &&
            _cachePathWriteUtc == writeUtc)
        {
            _catalog = _cache;
            Logger.Debug(CultureInfo.InvariantCulture, "DXF layer catalog cache hit for path: {0}", path);
            return;
        }

        try
        {
            _catalog = DxfLayerCatalogReader.ReadFromPath(path);
            _cache = _catalog;
            _cachePath = path;
            _cachePathLength = length;
            _cachePathWriteUtc = writeUtc;
            _cacheEmbeddedFingerprint = 0;
            _cacheEmbeddedLength = -1;
            Logger.Info(CultureInfo.InvariantCulture, "DXF layer catalog loaded from path ({0} layers).", _catalog.Layers.Count);
        }
        catch (Exception ex)
        {
            Clear();
            _cache = DxfLayerCatalog.Empty;
            _cachePath = null;
            _cachePathLength = -1;
            _cachePathWriteUtc = default;
            Logger.Warn(ex, "Failed to parse DXF layer catalog from path: {0}", path);
        }
    }

    public void TryLoadFromEmbedded(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            Clear();
            return;
        }

        var fingerprint = ComputeFingerprint(data);
        if (_cache.Layers.Count > 0 &&
            _cacheEmbeddedLength == data.Length &&
            _cacheEmbeddedFingerprint == fingerprint)
        {
            _catalog = _cache;
            Logger.Debug(CultureInfo.InvariantCulture, "DXF layer catalog cache hit for embedded bytes (len={0}).", data.Length);
            return;
        }

        try
        {
            using var ms = new MemoryStream(data);
            _catalog = DxfLayerCatalogReader.ReadFromStream(ms);
            _cache = _catalog;
            _cacheEmbeddedLength = data.Length;
            _cacheEmbeddedFingerprint = fingerprint;
            _cachePath = null;
            _cachePathLength = -1;
            _cachePathWriteUtc = default;
            Logger.Info(CultureInfo.InvariantCulture, "DXF layer catalog loaded from embedded bytes ({0} layers).", _catalog.Layers.Count);
        }
        catch (Exception ex)
        {
            Clear();
            _cache = DxfLayerCatalog.Empty;
            _cacheEmbeddedLength = -1;
            _cacheEmbeddedFingerprint = 0;
            Logger.Warn(ex, "Failed to parse DXF layer catalog from embedded bytes.");
        }
    }

    private static int ComputeFingerprint(byte[] data)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        const int maxSamples = 32;
        var sampleCount = Math.Min(maxSamples, data.Length);
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + data.Length;
            hash = (hash * 31) + data[0];
            hash = (hash * 31) + data[^1];
            for (var i = 0; i < sampleCount; i++)
            {
                var index = sampleCount == 1
                    ? 0
                    : (int)((long)i * (data.Length - 1) / (sampleCount - 1));
                hash = (hash * 31) + data[index];
            }

            return hash;
        }
    }
}

