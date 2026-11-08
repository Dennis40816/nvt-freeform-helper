// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Infrastructure.Dxf;

public sealed partial class DxfPadImporter
{
    /// <summary>
    /// Imports a DXF for CAD replacement, rejecting empty or incomplete input before returning any state.
    /// </summary>
    public static CadPadSet ImportForCadLoad(string path, out DxfLayerCatalog catalog, DxfImportOptions options)
    {
        using var reader = new StreamReader(path);
        return ImportForCadLoad(reader, Path.GetFileName(path), options, out catalog);
    }

    /// <summary>
    /// Applies the same CAD replacement validation to a project's embedded DXF.
    /// </summary>
    public static CadPadSet ImportForCadLoad(Stream stream, DxfImportOptions options)
    {
        using var reader = new StreamReader(stream);
        return ImportForCadLoad(reader, "embedded DXF", options, out _);
    }

    private static CadPadSet ImportForCadLoad(
        TextReader reader, string sourceName, DxfImportOptions options, out DxfLayerCatalog catalog)
    {
        var tokens = ReadCompleteEntityPairs(reader).ToList();
        var pads = ImportFromTokens(tokens, options);
        if (pads.Pads.Count == 0)
        {
            throw new InvalidDataException($"DXF file '{sourceName}' has no usable pads.");
        }

        catalog = DxfLayerCatalogReader.ReadFromPairs(tokens);
        return pads;
    }

    private static IEnumerable<(int code, string value)> ReadCompleteEntityPairs(TextReader reader)
    {
        string? entityType = null;
        var inPolyline = false;
        foreach (var pair in ReadPairs(reader, requireCompletePairs: true))
        {
            if (pair.code == 0)
            {
                var type = pair.value;
                var isVertex = string.Equals(type, "VERTEX", StringComparison.OrdinalIgnoreCase);
                var isSeqend = string.Equals(type, "SEQEND", StringComparison.OrdinalIgnoreCase);
                if (inPolyline && !isVertex && !isSeqend)
                {
                    throw new InvalidDataException("DXF contains an incomplete POLYLINE entity (missing SEQEND).");
                }

                inPolyline = string.Equals(type, "POLYLINE", StringComparison.OrdinalIgnoreCase) || (inPolyline && isVertex);
                entityType = isSeqend || IsCadLoadBoundary(type) ? null : type;
            }

            yield return pair;
        }

        if (inPolyline || entityType is not null)
        {
            throw new InvalidDataException($"DXF ends inside an incomplete {(inPolyline ? "POLYLINE" : entityType)} entity.");
        }
    }

    private static bool IsCadLoadBoundary(string type)
    {
        return string.Equals(type, "SECTION", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "ENDSEC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "EOF", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "ENDBLK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "ENDTAB", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadGroupValue(TextReader reader, bool requireCompletePairs)
    {
        var value = reader.ReadLine();
        if (value is null && requireCompletePairs)
        {
            throw new InvalidDataException("DXF ends with an incomplete group code/value pair.");
        }

        return value;
    }
}
