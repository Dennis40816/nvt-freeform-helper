using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Infrastructure.Dxf;

public sealed partial class DxfPadImporter
{
    private static void ExpandInsert(
        ParsedInsert insert,
        string? inheritedLayer,
        Transform2 parentTransform,
        IReadOnlyDictionary<string, BlockDefinition> blocks,
        List<CadPad> pads,
        ref int nextId,
        DxfImportOptions options,
        HashSet<string> recursionStack)
    {
        if (string.IsNullOrWhiteSpace(insert.BlockName))
        {
            return;
        }

        if (!blocks.TryGetValue(insert.BlockName, out var block))
        {
            return;
        }

        if (!recursionStack.Add(block.Name))
        {
            return;
        }

        try
        {
            var insertLayer = ResolveLayer(insert.Layer, inheritedLayer);
            var baseTransform = BuildInsertTransform(insert, block.BasePoint);
            var combinedBase = Transform2.Combine(parentTransform, baseTransform);

            for (var row = 0; row < Math.Max(1, insert.Rows); row++)
            {
                for (var col = 0; col < Math.Max(1, insert.Columns); col++)
                {
                    var cellOffset = Transform2.Translation(col * insert.ColumnSpacing, row * insert.RowSpacing);
                    var cellTransform = Transform2.Combine(combinedBase, cellOffset);

                    foreach (var poly in block.Polylines)
                    {
                        var effectiveLayer = ResolveLayer(poly.Layer, insertLayer);
                        var transformed = poly.Vertices.Select(cellTransform.Apply).ToList();
                        TryCommitPolyline(pads, ref nextId, effectiveLayer, poly.IsClosed, transformed, options);
                    }

                    foreach (var nestedInsert in block.Inserts)
                    {
                        ExpandInsert(
                            nestedInsert,
                            insertLayer,
                            cellTransform,
                            blocks,
                            pads,
                            ref nextId,
                            options,
                            recursionStack);
                    }
                }
            }
        }
        finally
        {
            recursionStack.Remove(block.Name);
        }
    }

    private static Transform2 BuildInsertTransform(ParsedInsert insert, Point2 blockBasePoint)
    {
        var transform = Transform2.Translation(insert.InsertionX, insert.InsertionY);
        transform = Transform2.Combine(transform, Transform2.RotationDegrees(insert.RotationDegrees));
        transform = Transform2.Combine(transform, Transform2.Scale(insert.ScaleX, insert.ScaleY));
        transform = Transform2.Combine(transform, Transform2.Translation(-blockBasePoint.X, -blockBasePoint.Y));
        return transform;
    }

    private static string ResolveLayer(string entityLayer, string? inheritedLayer)
    {
        if (string.IsNullOrWhiteSpace(entityLayer) || string.Equals(entityLayer, "0", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(inheritedLayer))
            {
                return inheritedLayer;
            }

            return "0";
        }

        return entityLayer;
    }
}
