using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Infrastructure.Dxf;

public sealed partial class DxfPadImporter
{
    private static void ParseEntitySection(
        List<(int code, string value)> tokens,
        string sectionName,
        List<ParsedPolyline> polylines,
        List<ParsedInsert> inserts)
    {
        for (var index = 0; index < tokens.Count;)
        {
            if (!IsSectionHeader(tokens, index, sectionName))
            {
                index++;
                continue;
            }

            index += 2; // skip SECTION + name
            while (index < tokens.Count)
            {
                var (code, value) = tokens[index];
                if (code == 0 && value == "ENDSEC")
                {
                    index++;
                    break;
                }

                if (code != 0)
                {
                    index++;
                    continue;
                }

                index = ParseEntity(tokens, index, tokens.Count, polylines.Add, inserts.Add);
            }
        }
    }

    private static Dictionary<string, BlockDefinition> ParseBlockDefinitions(List<(int code, string value)> tokens)
    {
        var result = new Dictionary<string, BlockDefinition>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < tokens.Count;)
        {
            if (!IsSectionHeader(tokens, index, "BLOCKS"))
            {
                index++;
                continue;
            }

            index += 2; // skip SECTION + name
            while (index < tokens.Count)
            {
                var (code, value) = tokens[index];
                if (code == 0 && value == "ENDSEC")
                {
                    index++;
                    break;
                }

                if (code == 0 && value == "BLOCK")
                {
                    var block = ParseBlock(tokens, ref index, tokens.Count);
                    if (!string.IsNullOrWhiteSpace(block.Name))
                    {
                        result[block.Name] = block;
                    }

                    continue;
                }

                index++;
            }
        }

        return result;
    }

    private static BlockDefinition ParseBlock(
        List<(int code, string value)> tokens,
        ref int index,
        int limit)
    {
        // Header entity starts at "0 BLOCK" and ends when next code 0 appears.
        var blockName = string.Empty;
        var baseX = 0.0;
        var baseY = 0.0;

        var headerIndex = index + 1;
        while (headerIndex < limit && tokens[headerIndex].code != 0)
        {
            var (code, value) = tokens[headerIndex];
            if (code == 2 && string.IsNullOrWhiteSpace(blockName))
            {
                blockName = value;
            }
            else if (code == 10)
            {
                baseX = ParseDouble(value);
            }
            else if (code == 20)
            {
                baseY = ParseDouble(value);
            }

            headerIndex++;
        }

        var block = new BlockDefinition(blockName, new Point2(baseX, baseY));
        index = headerIndex;

        while (index < limit)
        {
            var (code, value) = tokens[index];
            if (code != 0)
            {
                index++;
                continue;
            }

            if (value == "ENDBLK")
            {
                index++;
                break;
            }

            index = ParseEntity(tokens, index, limit, block.AddPolyline, block.AddInsert);
        }

        return block;
    }

    private static int ParseEntity(
        List<(int code, string value)> tokens,
        int startIndex,
        int limit,
        Action<ParsedPolyline> addPolyline,
        Action<ParsedInsert> addInsert)
    {
        var entityType = tokens[startIndex].value;
        if (string.Equals(entityType, "LWPOLYLINE", StringComparison.OrdinalIgnoreCase))
        {
            return ParseLwPolyline(tokens, startIndex, limit, addPolyline);
        }

        if (string.Equals(entityType, "POLYLINE", StringComparison.OrdinalIgnoreCase))
        {
            return ParseLegacyPolyline(tokens, startIndex, limit, addPolyline);
        }

        if (string.Equals(entityType, "INSERT", StringComparison.OrdinalIgnoreCase))
        {
            return ParseInsert(tokens, startIndex, limit, addInsert);
        }

        return SkipEntity(tokens, startIndex + 1, limit);
    }

    private static int ParseLwPolyline(
        List<(int code, string value)> tokens,
        int startIndex,
        int limit,
        Action<ParsedPolyline> addPolyline)
    {
        var layer = "0";
        var isClosed = false;
        var vertices = new List<Point2>();

        var index = startIndex + 1;
        while (index < limit && tokens[index].code != 0)
        {
            var (code, value) = tokens[index];
            if (code == 8)
            {
                layer = value;
            }
            else if (code == 70)
            {
                var flag = ParseInt(value, fallback: 0);
                isClosed = (flag & 1) != 0;
            }
            else if (code == 10)
            {
                vertices.Add(new Point2(ParseDouble(value), double.NaN));
            }
            else if (code == 20 && vertices.Count > 0)
            {
                var last = vertices[^1];
                if (double.IsNaN(last.Y))
                {
                    vertices[^1] = last with { Y = ParseDouble(value) };
                }
            }

            index++;
        }

        addPolyline(new ParsedPolyline(layer, isClosed, NormalizeVertices(vertices)));
        return index;
    }

    private static int ParseLegacyPolyline(
        List<(int code, string value)> tokens,
        int startIndex,
        int limit,
        Action<ParsedPolyline> addPolyline)
    {
        var layer = "0";
        var isClosed = false;
        var vertices = new List<Point2>();

        var index = startIndex + 1;
        while (index < limit)
        {
            var (code, value) = tokens[index];
            if (code == 0)
            {
                if (string.Equals(value, "VERTEX", StringComparison.OrdinalIgnoreCase))
                {
                    index = ParseVertex(tokens, index, limit, vertices);
                    continue;
                }

                if (string.Equals(value, "SEQEND", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    break;
                }

                break;
            }

            if (code == 8)
            {
                layer = value;
            }
            else if (code == 70)
            {
                var flag = ParseInt(value, fallback: 0);
                isClosed = (flag & 1) != 0;
            }

            index++;
        }

        addPolyline(new ParsedPolyline(layer, isClosed, NormalizeVertices(vertices)));
        return index;
    }

    private static int ParseVertex(
        List<(int code, string value)> tokens,
        int startIndex,
        int limit,
        List<Point2> vertices)
    {
        double? x = null;
        double? y = null;

        var index = startIndex + 1;
        while (index < limit && tokens[index].code != 0)
        {
            var (code, value) = tokens[index];
            if (code == 10)
            {
                x = ParseDouble(value);
            }
            else if (code == 20)
            {
                y = ParseDouble(value);
            }

            index++;
        }

        if (x.HasValue && y.HasValue)
        {
            vertices.Add(new Point2(x.Value, y.Value));
        }

        return index;
    }

    private static int ParseInsert(
        List<(int code, string value)> tokens,
        int startIndex,
        int limit,
        Action<ParsedInsert> addInsert)
    {
        var layer = "0";
        var blockName = string.Empty;
        var insertionX = 0.0;
        var insertionY = 0.0;
        var scaleX = 1.0;
        var scaleY = 1.0;
        var rotationDegrees = 0.0;
        var columns = 1;
        var rows = 1;
        var columnSpacing = 0.0;
        var rowSpacing = 0.0;

        var index = startIndex + 1;
        while (index < limit && tokens[index].code != 0)
        {
            var (code, value) = tokens[index];
            switch (code)
            {
                case 8:
                    layer = value;
                    break;
                case 2:
                    blockName = value;
                    break;
                case 10:
                    insertionX = ParseDouble(value);
                    break;
                case 20:
                    insertionY = ParseDouble(value);
                    break;
                case 41:
                    scaleX = ParseDouble(value);
                    break;
                case 42:
                    scaleY = ParseDouble(value);
                    break;
                case 50:
                    rotationDegrees = ParseDouble(value);
                    break;
                case 70:
                    columns = Math.Max(1, ParseInt(value, fallback: 1));
                    break;
                case 71:
                    rows = Math.Max(1, ParseInt(value, fallback: 1));
                    break;
                case 44:
                    columnSpacing = ParseDouble(value);
                    break;
                case 45:
                    rowSpacing = ParseDouble(value);
                    break;
            }

            index++;
        }

        if (!string.IsNullOrWhiteSpace(blockName))
        {
            addInsert(new ParsedInsert(
                layer,
                blockName,
                insertionX,
                insertionY,
                scaleX,
                scaleY,
                rotationDegrees,
                columns,
                rows,
                columnSpacing,
                rowSpacing));
        }

        return index;
    }

    private static int SkipEntity(List<(int code, string value)> tokens, int index, int limit)
    {
        while (index < limit && tokens[index].code != 0)
        {
            index++;
        }

        return index;
    }

    private static bool IsSectionHeader(List<(int code, string value)> tokens, int index, string sectionName)
    {
        if (index + 1 >= tokens.Count)
        {
            return false;
        }

        var current = tokens[index];
        var next = tokens[index + 1];
        return current.code == 0 &&
               current.value == "SECTION" &&
               next.code == 2 &&
               string.Equals(next.value, sectionName, StringComparison.OrdinalIgnoreCase);
    }
}
