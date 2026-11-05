# Performance review: PadCanvas rendering and grid build

- Date: 2026-11-05
- Scope: read-only review of `PadCanvas.Render` and the regular-grid build path. No production code was changed.
- Base: `main` at `5e48bec`.
- Benchmarks added: `tests/FreeformHelper.Tests/UI/Benchmarks/PadCanvasRenderBenchmarkTests.cs`,
  `tests/FreeformHelper.Tests/Application/Grid/RegularGridBuildBenchmarkTests.cs`, and the helper
  `tests/FreeformHelper.Tests/TestInfrastructure/RelativeBenchmark.cs`.

## How to read the numbers

All numbers are **relative**. Each table compares variants measured in the same process against a baseline row.
Allocation counts (`GC.GetAllocatedBytesForCurrentThread`) are deterministic and repeat to the byte between runs.
Times come from a shared cloud box and moved by up to about ±25 % between two runs, so treat time ratios as
ranges. The tests assert only that the compared variants give the same result. They assert no time.

Run them with:

```
dotnet build tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Release
build/bin/FreeformHelper.Tests/Release/net10.0/FreeformHelper.Tests -trait "Category=Perf" -showLiveOutput
```

The tables print to the test output. The whole set takes about 10 s.

**Input.** The private `example/` submodule could not be cloned in the cloud box, so the BOE 36.35 data was not
used. The canvas scenarios use a synthetic 96 x 64 panel (6,144 regular pads, plus 6,144 octagon CAD pads, one per
cell). The view is 1,280 x 800 at zoom 2.8, so every pad is on screen. The canvas is hosted in a headless `Window`
with the app resources loaded, and it renders into a Skia `RenderTargetBitmap`. So the frame time includes real
Skia rasterization, and the managed allocation is ours alone. Run the same tests on the 36.35 project before you
pick the order of the fixes. This follows the "real inputs first" rule.

## Measured results

### PadCanvas.Render, one op = one frame (baseline A)

| Scenario | Time (2 runs) | Bytes/frame | Alloc vs A |
|---|---:|---:|---:|
| A regular only, static view | 1.00x | 349,944 | 1.00x |
| B A + axis labels | 1.07x to 1.18x | 1,766,765 | 5.05x |
| C0 25 % freeform pads, hatch off | 0.91x to 1.11x | 349,840 | 1.00x |
| C1 25 % freeform pads, hatch on | **3.17x to 3.72x** | 4,232,848 | **12.10x** |
| D regular only, pan 1 px every frame | 0.96x to 1.38x | 743,304 | 2.12x |
| E regular + CAD, static view | 2.57x to 3.42x | 353,632 | 1.01x |
| F regular + CAD, pan 1 px every frame | 2.87x to 3.92x | 1,398,768 | 4.00x |

| Scenario | Time | Bytes/op | Alloc vs steady |
|---|---:|---:|---:|
| steady frame | 1.00x | 349,944 | 1.00x |
| first frame after a `RegularPads` change | 0.69x to 0.76x | 1,485,216 | 4.24x |

The first-frame time is within noise of the steady frame. Only its allocation is a clear signal.

### Polygon2.Bounds, one op = one pass over 6,144 polygons (baseline: current, 4 vertices)

| Variant | Time (2 runs) | Bytes/op |
|---|---:|---:|
| 4 vertices, `Polygon2.Bounds` (current) | 1.00x | 344,064 (56 B per call) |
| 4 vertices, loop over `ImmutableArray` without boxing | 1.57x to 1.72x | 0 |
| 4 vertices, cached `Rect2` | **0.07x** | 0 |
| 8 vertices, `Polygon2.Bounds` (current) | 1.19x to 1.24x | 344,064 |
| 8 vertices, loop without boxing | 0.42x to 0.47x | 0 |
| 8 vertices, cached `Rect2` | **0.05x** | 0 |

The plain loop uses `Math.Min`/`Math.Max`, which is slower than `<` on 4 points. The point is the cached row:
a cached `Rect2` is 14x to 20x cheaper per access, and it allocates nothing.

### Grid build 64 x 96 = 6,144 pads, one op = one build (baseline: global scopes)

| Variant | Time (3 runs) | Bytes/op | Alloc |
|---|---:|---:|---:|
| `BuildFromSettings`, `ColumnGlobal`/`RowGlobal` | 1.00x | 2,021,152 | 1.00x |
| `BuildFromSettings`, `RowLocal`/`ColumnLocal` (default) | 1.71x to 2.43x | 2,369,440 | 1.17x |
| `GridBuildService.Build` (default scopes + diff-index mapping) | 1.99x to 2.31x | 2,370,328 | 1.17x |
| pad objects only (`Polygon2` + `RegularPad`) | 0.47x to 0.87x | 1,130,552 | 0.56x |
| `RegularGrid` constructor bounds pass | 0.29x to 0.30x | 344,184 | 0.17x |

## Root cause behind most of the allocation

`Polygon2.Bounds` (`src/FreeformHelper.Domain/Geometry/Polygon2.cs:34`) recomputes the bounds on every access
through `Rect2.FromPoints(IEnumerable<Point2>)` (`src/FreeformHelper.Domain/Geometry/Rect2.cs:32`). The
`ImmutableArray<Point2>` is boxed to `IEnumerable<Point2>`, and its enumerator is boxed too: 56 bytes and an
interface-dispatched loop per call. `RegularPad.Bounds` (`RegularPad.cs:41`) and `CadPad.Bounds` (`CadPad.cs:31`)
forward to it, and the canvas, the spatial indexes, the matcher and the grid all read `Bounds` in their inner loops.
From the tables:

- Static frame: 344,064 of 349,944 bytes (98 %) is `Bounds` boxing, one call per drawn regular pad
  (`PadCanvas.Rendering.Regular.cs:58`).
- Pan frame (D): about 688 KB of 743 KB (93 %) is `Bounds`: once in `RegularSpatialIndex.Query`
  (`PadCanvas.Spatial.cs:446`) and once in the draw.
- Axis labels (B): 1,376,256 of the 1,416,821 extra bytes (97 %) come from the four `Bounds` passes in
  `GetRegularBounds` (`PadCanvas.View.Bounds.cs:148-156`).
- Grid change (first frame): `ValidateSeparableGrid` and the constructor read `Bounds` twice per pad
  (`PadCanvas.Spatial.cs:329-398`).
- Grid build: the whole `RegularGrid` bounds pass (`RegularGrid.cs:68-81`) is `Bounds` boxing.

`Polygon2` is sealed and its `Vertices` cannot change after construction, so a cached value gives the same result.

## Findings ranked by payoff

Each row gives the place, the measured relative cost, and the proposed fix. "Code read" means the item was found
by reading the code and the benchmarks do not isolate it.

| # | Where | Measured cost | Proposed fix | Changes output? |
|---|---|---|---|---|
| 1 | `Polygon2.cs:34`, `Rect2.cs:32` | 98 % of static-frame alloc, 93 % of pan-frame alloc, 97 % of axis-label alloc; cached access 14x to 20x cheaper | Compute `Bounds` once in the `Polygon2` constructor and store it in a field. Add a `Rect2.FromPoints(ReadOnlySpan<Point2>)` overload so other callers stop boxing. Consider the same for `Area()` and `Centroid()`, which the notch labels call every frame. One Domain change fixes every reader. | No |
| 2 | Freeform hatch, `PadCanvas.Rendering.Regular.cs:72-100` | Frame time 3.2x to 3.7x and alloc 12.1x with 25 % freeform pads (about 2.5 KB per hatched pad per frame) | (a) Resolve the 4 resources and build the 2 hatch pens once per frame in `Render`, next to the other `regPen*` values (`PadCanvas.Rendering.cs:170-208`), not once per pad. (b) Put all hatch lines of one frame into one `StreamGeometry` per pen and draw it once under one clip, or use a tiled hatch brush. (c) Skip the hatch when a cell is only a few pixels wide on screen. (a) is cheap; (b) and (c) remove most of the Skia cost. | (a) No. (b) No visible change if the clip is kept. (c) Yes, at low zoom |
| 3 | Axis labels, `PadCanvas.View.AxisLabels.cs:15-241`, `PadCanvas.View.Bounds.cs:148-156` | +7 % to +18 % frame time, +1.4 MB/frame (5x) | Every frame scans all pads about 8 times (`Min`/`Max`, `Where`/`OrderBy`/`ToList`, `GroupBy`) and formats every label string. Build the grid-derived data once per `RegularPads` change, in `InvalidateRegularDataCaches` (`PadCanvas.Lifecycle.cs:249`): bounds, min/max row and column, first row, first column, IC groups and the label strings. Then draw only the labels whose position is on screen. The layout cache key is a new interpolated string per label per frame (`AxisLabels.cs:280`); key it by a value tuple. The cache is cleared only on a theme change (`PadCanvas.Lifecycle.cs:181`), so zooming keeps adding one `TextLayout` per label per 0.1 pt of font size. Bound it, or keep only the current font size. | No |
| 4 | Visible query on pan, `PadCanvas.VisibleDrawListBuilder.cs:99-131`, `PadCanvas.Spatial.cs:152-200`, cache key `PadCanvas.VisibleDrawCache.cs:307-321` | Regular pan: 2.1x alloc. Regular + CAD pan: 4.0x alloc, +2 % to +15 % time vs the static CAD frame | The query cache key is the exact viewport, so every 1 px pan misses, runs the full query and copies it with `ToArray()`. The CAD query allocates a new `HashSet<int>` per query to remove duplicates. Fixes: (a) query into reusable buffers rather than iterator + `ToArray`; (b) in `CadSpatialIndex`, remove duplicates with an `int[]` stamp per pad (store the pad position in the bucket, compare it with a query counter) rather than a `HashSet`. (c) Optional: query a viewport larger by a margin and reuse it while the real viewport stays inside it. | (a), (b) No. (c) Yes: the candidate counts in `PadCanvasVisibleDrawPerfSnapshot` and the low-detail CAD decimation pattern change |
| 5 | Grid change to first frame, `PadCanvas.Spatial.cs:307-398`; CAD geometry cache `PadCanvas.Caches.cs:22-44` | 4.2x alloc on the first frame after a grid change (regular only) | `RegularSpatialIndex` makes 4 LINQ passes for `Min`/`Max`, then reads `Bounds` twice per pad; finding 1 removes most of it. Code read: `EnsureCadGeometryCache` builds a `StreamGeometry` for every CAD pad on the first full-detail frame, including pads that are off screen. Build the geometry on first draw of each pad instead. | No |
| 6 | Per-pad resource lookups in the simulation overlay, `PadCanvas.Rendering.Simulation.cs:16-30` | Code read, not benchmarked | `TryBuildSimulationRegularFillBrush` resolves 8 theme resources for every drawn pad in every frame. Resolve them once per frame, or once per resource revision (`OnVisibleDrawResourceChanged`, `PadCanvas.VisibleDrawCache.cs:290`). | No |
| 7 | Hover debug redraws, `PadCanvas.HoverDebug.cs:53-106` | Each hover pause costs 2 full frames (hide on move, show on reveal), each the size of scenario A | Draw the hover overlay in a separate light overlay visual (or adorner layer) so that showing or hiding it does not re-render the pad layer. | No |
| 8 | Notch ratio labels, `PadCanvas.Rendering.NotchPreview.cs:179-300` | Code read | Per frame: LINQ filter and sort over all CAD pads, a new `TextLayout` and `Centroid()` per label, and a placement search of up to 36 candidates against all placed labels, so O(L² x 36) for L labels. Labels of pads off screen are clamped into the viewport rather than skipped. Cache the sorted list and the layouts per preview revision, skip pads off screen, and keep placed rects in a coarse screen grid. The path already stops when more than one preview is shown with an active stage overlay (`NotchPreview.cs:20-23`), which limits the worst case. | Skipping pads off screen: yes, labels clamped to the edge disappear |
| 9 | Match coverage labels, `PadCanvas.Rendering.Labels.cs:118` and `:184` | Code read | `RegularPads.FirstOrDefault(...)` and `CadPads.FirstOrDefault(...)` inside the loop over selected pads: O(S x N) per frame. `_regularPadById` (`PadCanvas.State.cs:56`) already exists for regular pads; add the same map for CAD pads. S is small in normal use, so the payoff is low. | No |
| 10 | Per-frame constant work in `Render`, `PadCanvas.Rendering.cs:170-247`, `:255`, `:277` | Code read; under 1 % of the static-frame alloc | About 25 resource lookups and 7 to 9 new `Pen` objects per frame, a new `List<Rect>` and a LINQ `Any` over the notch previews. Cache the pens per resource revision and property change. Low payoff, but it also makes finding 2 (a) simpler. | No |
| 11 | Grid build, `RegularGridBuilder.cs:102-143`, `:185-232` | Default local scopes cost 1.7x to 2.4x the global scopes, +17 % alloc; `Polygon2` copies its input array (`Polygon2.cs:22`) | For each row and column without an override, reuse the shared edge list instead of building `Resize` and LINQ lists. Give `Polygon2` a constructor that takes an `ImmutableArray<Point2>` without a copy. Low payoff: the build runs once per grid rebuild, not per frame. | No |
| 12 | Linear fallback when the regular index is off, `PadCanvas.Spatial.cs:54-55`, `PadCanvas.VisibleDrawListBuilder.cs:111-113` | Code read | For non-separable manual sizing, `SupportsExactQueries` is false, and every pan frame scans all regular pads with LINQ. Use a bucket index like `CadSpatialIndex` for this case. | No |

## Invalidation: what is already right

These were checked and need no change. They are the "one entry point" pattern that the rules ask for:

- All redraw requests go through `RequestVisualRefresh` (`PadCanvas.ViewRefresh.cs:66-87`). It posts once per
  render tick and merges repeated requests.
- Pan and box-select redraws are limited to one per 16 ms (`PadCanvas.Input.PointerMove.cs:27-32`, `:55-60`).
- Cache invalidation is split by data: CAD changes keep the regular index and the reverse
  (`PadCanvas.Lifecycle.cs:24-25`, `:249-257`), and `PadCanvasCacheInvalidationTests` guards it.
- The visible draw lists have a two-level cache (query, then lists), keyed by data, selection, highlight and
  resource revisions (`PadCanvas.VisibleDrawCache.cs`). A static redraw costs no query (scenario A vs D).

The avoidable redraws are the hover-debug pair (finding 7) and the exact-viewport query key during pan (finding 4).

## Suggested order

1. Finding 1 alone. It is one Domain change with no output change, and it removes most of the managed allocation
   on every canvas path and in matching and grid build. The benchmarks above show the before and after.
2. Finding 2 (a) and finding 3, as the next PR. These are UI-only changes with no output change.
3. Findings 4 (a)(b), 5 and 6.
4. The rest when a real project shows them in a profile.

Before each fix, re-run the `Category=Perf` tests on the same machine for the before numbers, and compare the
allocation columns, which are exact.
