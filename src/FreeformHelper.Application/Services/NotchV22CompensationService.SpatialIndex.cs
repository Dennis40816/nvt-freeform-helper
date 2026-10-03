using System.Collections.Concurrent;
using System.Collections.Frozen;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchV22CompensationService
{
    private sealed class CadBoundsSpatialIndex
    {
        private const double EdgeEpsilon = 1e-9;
        private const int MinAxisBuckets = 8;
        private const int MaxAxisBuckets = 128;

        private readonly IReadOnlyList<CadPad> _allPads;
        private readonly List<CadPad>?[] _buckets;
        private readonly Rect2 _bounds;
        private readonly int _rows;
        private readonly int _cols;
        private readonly double _cellWidth;
        private readonly double _cellHeight;

        public CadBoundsSpatialIndex(IReadOnlyList<CadPad> pads)
        {
            _allPads = pads;
            _bounds = BuildBounds(pads);

            if (pads.Count == 0 || _bounds.Width <= EdgeEpsilon || _bounds.Height <= EdgeEpsilon)
            {
                _rows = 1;
                _cols = 1;
                _cellWidth = Math.Max(_bounds.Width, EdgeEpsilon);
                _cellHeight = Math.Max(_bounds.Height, EdgeEpsilon);
                _buckets = new List<CadPad>?[1];
                if (pads.Count > 0)
                {
                    _buckets[0] = pads.ToList();
                }

                return;
            }

            var axisTarget = Math.Clamp((int)Math.Ceiling(Math.Sqrt(pads.Count)), MinAxisBuckets, MaxAxisBuckets);
            var aspect = Math.Clamp(_bounds.Width / Math.Max(_bounds.Height, EdgeEpsilon), 0.25, 4.0);
            _cols = Math.Clamp((int)Math.Round(axisTarget * Math.Sqrt(aspect)), MinAxisBuckets, MaxAxisBuckets);
            _rows = Math.Clamp((int)Math.Round(axisTarget / Math.Sqrt(aspect)), MinAxisBuckets, MaxAxisBuckets);
            _cellWidth = Math.Max(_bounds.Width / _cols, EdgeEpsilon);
            _cellHeight = Math.Max(_bounds.Height / _rows, EdgeEpsilon);
            _buckets = new List<CadPad>?[_rows * _cols];

            foreach (var pad in pads)
            {
                var bounds = pad.Bounds;
                var minCol = GetColumn(bounds.MinX);
                var maxCol = GetColumn(bounds.MaxX);
                var minRow = GetRow(bounds.MinY);
                var maxRow = GetRow(bounds.MaxY);
                for (var row = minRow; row <= maxRow; row++)
                {
                    for (var col = minCol; col <= maxCol; col++)
                    {
                        AddBucketPad(row, col, pad);
                    }
                }
            }
        }

        public IEnumerable<CadPad> Query(Rect2 worldRect)
        {
            if (_allPads.Count == 0 || !_bounds.Intersects(worldRect))
            {
                yield break;
            }

            if (_rows == 1 && _cols == 1)
            {
                foreach (var pad in _allPads)
                {
                    if (pad.Bounds.Intersects(worldRect))
                    {
                        yield return pad;
                    }
                }

                yield break;
            }

            var minCol = GetColumn(worldRect.MinX);
            var maxCol = GetColumn(worldRect.MaxX);
            var minRow = GetRow(worldRect.MinY);
            var maxRow = GetRow(worldRect.MaxY);
            var seenPadIds = new HashSet<int>();
            for (var row = minRow; row <= maxRow; row++)
            {
                for (var col = minCol; col <= maxCol; col++)
                {
                    var bucket = _buckets[(row * _cols) + col];
                    if (bucket is null)
                    {
                        continue;
                    }

                    foreach (var pad in bucket)
                    {
                        if (!seenPadIds.Add(pad.Id))
                        {
                            continue;
                        }

                        if (pad.Bounds.Intersects(worldRect))
                        {
                            yield return pad;
                        }
                    }
                }
            }
        }

        private static Rect2 BuildBounds(IReadOnlyList<CadPad> pads)
        {
            if (pads.Count == 0)
            {
                return new Rect2(0, 0, 0, 0);
            }

            var bounds = pads[0].Bounds;
            for (var i = 1; i < pads.Count; i++)
            {
                bounds = Rect2.Union(bounds, pads[i].Bounds);
            }

            return bounds;
        }

        private void AddBucketPad(int row, int col, CadPad pad)
        {
            var idx = (row * _cols) + col;
            var bucket = _buckets[idx];
            if (bucket is null)
            {
                bucket = new List<CadPad>();
                _buckets[idx] = bucket;
            }

            bucket.Add(pad);
        }

        private int GetColumn(double x)
        {
            if (_cols <= 1)
            {
                return 0;
            }

            if (x <= _bounds.MinX)
            {
                return 0;
            }

            if (x >= _bounds.MaxX)
            {
                return _cols - 1;
            }

            var normalized = (x - _bounds.MinX) / _cellWidth;
            return Math.Clamp((int)Math.Floor(normalized), 0, _cols - 1);
        }

        private int GetRow(double y)
        {
            if (_rows <= 1)
            {
                return 0;
            }

            if (y <= _bounds.MinY)
            {
                return 0;
            }

            if (y >= _bounds.MaxY)
            {
                return _rows - 1;
            }

            var normalized = (y - _bounds.MinY) / _cellHeight;
            return Math.Clamp((int)Math.Floor(normalized), 0, _rows - 1);
        }
    }

    public sealed class NotchV22BoundaryQueryContext
    {
        private const int MinCadCountForSpatialIndex = 64;
        private readonly IReadOnlyList<CadPad> _allCadPads;
        private readonly ConcurrentDictionary<int, IReadOnlyList<CadPad>> _blockingCadPadsByRegularIndex = new();
        private readonly ConcurrentDictionary<int, IReadOnlyList<CadPad>> _ownerCadPadsByRegularIndex = new();
        private readonly CadBoundsSpatialIndex? _cadBoundsIndex;

        internal NotchV22BoundaryQueryContext(
            IReadOnlyList<CadPad> allCadPads,
            double strictOverlapRatio)
        {
            _allCadPads = allCadPads;
            _cadBoundsIndex = allCadPads.Count >= MinCadCountForSpatialIndex
                ? new CadBoundsSpatialIndex(allCadPads)
                : null;
            StrictOverlapRatio = strictOverlapRatio;
        }

        public double StrictOverlapRatio { get; }

        public IReadOnlyList<CadPad> GetStrictOwnerCadPads(RegularPad regular)
        {
            return _ownerCadPadsByRegularIndex.GetOrAdd(regular.Index, _ =>
            {
                var threshold = Math.Max(AreaEpsilon, regular.Area * StrictOverlapRatio);
                return BuildIntersectingCadPads(regular, threshold);
            });
        }

        public CadPad[] GetBlockingCadPads(RegularPad regular, int currentCadPadId)
        {
            var cached = _blockingCadPadsByRegularIndex.GetOrAdd(regular.Index, _ =>
            {
                return BuildIntersectingCadPads(regular, AreaEpsilon);
            });

            return cached
                .Where(pad => pad.Id != currentCadPadId)
                .ToArray();
        }

        private CadPad[] BuildIntersectingCadPads(RegularPad regular, double areaThreshold)
        {
            var candidates = _cadBoundsIndex?.Query(regular.Bounds) ?? _allCadPads;
            var pads = new List<CadPad>();
            var seenPadIds = new HashSet<int>();
            foreach (var pad in candidates)
            {
                if (Polygon2.IntersectionAreaWithRect(pad.Polygon, regular.Bounds) <= areaThreshold)
                {
                    continue;
                }

                if (!seenPadIds.Add(pad.Id))
                {
                    continue;
                }

                pads.Add(pad);
            }

            pads.Sort(static (left, right) => left.Id.CompareTo(right.Id));
            return pads.ToArray();
        }
    }

    private sealed class StageBBoundaryContext
    {
        private readonly FrozenSet<int> _effectiveBoundaryRegularIndices;

        public StageBBoundaryContext(
            NotchV22BoundaryQueryContext boundaryQueryContext,
            IEnumerable<int> effectiveBoundaryRegularIndices)
        {
            BoundaryQueryContext = boundaryQueryContext ?? throw new ArgumentNullException(nameof(boundaryQueryContext));
            _effectiveBoundaryRegularIndices = effectiveBoundaryRegularIndices.ToFrozenSet();
        }

        private NotchV22BoundaryQueryContext BoundaryQueryContext { get; }

        public double StrictOverlapRatio => BoundaryQueryContext.StrictOverlapRatio;

        public FrozenSet<int> EffectiveBoundaryRegularIndices => _effectiveBoundaryRegularIndices;

        public IReadOnlyList<CadPad> GetStrictOwnerCadPads(RegularPad regular)
        {
            return BoundaryQueryContext.GetStrictOwnerCadPads(regular);
        }

        public CadPad[] GetBlockingCadPads(RegularPad regular, int currentCadPadId)
        {
            return BoundaryQueryContext.GetBlockingCadPads(regular, currentCadPadId);
        }
    }
}
