// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Infrastructure.Dxf;

public sealed partial class DxfPadImporter
{
    // One instance is used synchronously by one import, before allocating expanded geometry.
    private sealed class InsertExpansionCounter
    {
        private const int MaxInsertAxisCount = 10_000;
        private const int MaxInsertCopies = 100_000;
        private const int MaxExpandedPadsPerImport = 2_000_000;
        private const int MaxExpandedCellsPerImport = 5_000_000;
        private const int MaxExpandedVerticesPerImport = 40_000_000;

        private readonly IReadOnlyDictionary<string, BlockDefinition> _blocks;
        private readonly Dictionary<string, ExpansionCost> _blockCosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _recursionStack = new(StringComparer.OrdinalIgnoreCase);

        public InsertExpansionCounter(IReadOnlyDictionary<string, BlockDefinition> blocks)
        {
            _blocks = blocks;
        }

        public void Validate(IEnumerable<ParsedInsert> inserts)
        {
            var total = default(ExpansionCost);
            foreach (var insert in inserts)
            {
                total = Check(total.Add(CountInsert(insert, out _)));
            }
        }

        // Pads and vertices are the geometry that will be allocated. Cells are the row and column steps the
        // expander walks, including nested INSERTs that resolve to no block and blocks that hold no pads.
        private ExpansionCost CountInsert(ParsedInsert insert, out bool containsCycle)
        {
            var copies = GetInsertCopies(insert);
            containsCycle = false;
            if (!_blocks.TryGetValue(insert.BlockName, out var block))
            {
                return new ExpansionCost(0, 1, 0);
            }

            var inner = CountBlock(block, out containsCycle);
            return Check(new ExpansionCost(copies * inner.Pads, copies * (1 + inner.Cells), copies * inner.Vertices));
        }

        private ExpansionCost CountBlock(BlockDefinition block, out bool containsCycle)
        {
            containsCycle = false;
            if (_blockCosts.TryGetValue(block.Name, out var cached))
            {
                return cached;
            }

            if (!_recursionStack.Add(block.Name))
            {
                containsCycle = true;
                return new ExpansionCost(0, 1, 0);
            }

            try
            {
                // Count candidates even if later geometry validation drops a degenerate pad.
                long polylines = 0;
                long vertices = 0;
                foreach (var poly in block.Polylines)
                {
                    polylines++;
                    vertices += poly.Vertices.Count;
                }

                var cost = Check(new ExpansionCost(polylines, 0, vertices));
                foreach (var nested in block.Inserts)
                {
                    cost = Check(cost.Add(CountInsert(nested, out var nestedCycle)));
                    containsCycle |= nestedCycle;
                }

                // A cycle's cost depends on its ancestors, so only cache acyclic subtrees.
                if (!containsCycle)
                {
                    _blockCosts[block.Name] = cost;
                }

                return cost;
            }
            finally
            {
                _recursionStack.Remove(block.Name);
            }
        }

        private static long GetInsertCopies(ParsedInsert insert)
        {
            var columns = Math.Max(1, insert.Columns);
            var rows = Math.Max(1, insert.Rows);
            if (columns > MaxInsertAxisCount || rows > MaxInsertAxisCount)
            {
                throw new InvalidDataException(FormattableString.Invariant(
                    $"DXF INSERT exceeds the column or row limit of {MaxInsertAxisCount} per axis."));
            }

            var copies = (long)columns * rows;
            if (copies > MaxInsertCopies)
            {
                throw new InvalidDataException(FormattableString.Invariant(
                    $"DXF INSERT exceeds the copy limit of {MaxInsertCopies} per INSERT."));
            }

            return copies;
        }

        private static ExpansionCost Check(ExpansionCost cost)
        {
            if (cost.Pads > MaxExpandedPadsPerImport)
            {
                throw new InvalidDataException(FormattableString.Invariant(
                    $"DXF import exceeds the total expanded pad limit of {MaxExpandedPadsPerImport}."));
            }

            if (cost.Cells > MaxExpandedCellsPerImport)
            {
                throw new InvalidDataException(FormattableString.Invariant(
                    $"DXF import exceeds the total expanded INSERT cell limit of {MaxExpandedCellsPerImport}."));
            }

            if (cost.Vertices > MaxExpandedVerticesPerImport)
            {
                throw new InvalidDataException(FormattableString.Invariant(
                    $"DXF import exceeds the total expanded vertex limit of {MaxExpandedVerticesPerImport}."));
            }

            return cost;
        }

        private readonly record struct ExpansionCost(long Pads, long Cells, long Vertices)
        {
            public ExpansionCost Add(ExpansionCost other) => new(Pads + other.Pads, Cells + other.Cells, Vertices + other.Vertices);
        }
    }
}
