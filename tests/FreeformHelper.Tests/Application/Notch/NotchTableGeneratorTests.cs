using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
    private static (object Candidate, NotchV22ResolvedResult Resolved) CreateA1ResolvedCandidate(
        NotchCompensationModel model, NotchV22CompensationResult compensation, int anchorIc = 0,
        int sourceDiff = 0)
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        grid.Pads[1].IcIndex = 1;
        foreach (var pad in grid.Pads)
        {
            pad.Freeform = FreeformType.XWay;
        }
        var cad = CreateCadWithBounds(0, 0, 20, 10);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = model,
                EnableToRegular = true,
                EnableToFull = true,
                EnableBoundaryVirtualAreaCap = false,
                EnableTargetCoverageGuard = false,
                MultiOwnerStrictOverlapPercent = 0.1,
                ThresholdPercentV22 = 0,
                EnabledVersions = [NotchAlgorithmVersion.V22],
            },
        };
        var switches = settings.Notch.ResolveStep3Switches();
        var areaMode = NotchV22TargetAllocationPolicy.ResolveAreaMode(model);
        var identity = NotchV22ResolvedResultService.CreateIdentity(
            cad.Pads[0], grid, cad.Pads, null, model,
            switches.EnableToRegular, switches.EnableToFull,
            settings.Notch.EnableToFullRuleEngine, settings.Notch.EnableToFullRuleTrace,
            false, settings.Notch.BoundaryVirtualAreaCapRatio, 0.001, anchorIc, sourceDiff, areaMode);
        var resolved = NotchV22ResolvedResultService.Build(
            cad.Pads[0], compensation, 0.001, anchorIc, sourceDiff, areaMode, identity);
        var batch = new NotchTableGenerator().ResolveCadAllocationBatch(
            cad, grid, settings, cadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [cad.Pads[0].Id] = sourceDiff },
            selectedSparseResultRequest: new(cad.Pads[0].Id, anchorIc, sourceDiff, resolved));
        Assert.Same(resolved, batch.SelectedSparseResult!.ResolvedResult);
        var canonical = batch.GetType().GetField("CanonicalCandidateBuild")!.GetValue(batch)!;
        var candidates = (System.Collections.IDictionary)canonical.GetType()
            .GetProperty("CandidatesByDiff")!.GetValue(canonical)!;
        var bucket = (System.Collections.IEnumerable)candidates[(anchorIc, sourceDiff)]!;
        return (Assert.Single(bucket.Cast<object>()), resolved);
    }

    private static readonly int[] _crossIcIndices = [0, 1];

    private static readonly int[] _expectedLegacyDiffSet = [0, 1];
    private static readonly int[] _cadPadIds200 = [200];
    private static readonly int[] _cadPadIds201 = [201];
    private static readonly string[] _expectedCadAllocationGenerationPhases =
    [
        "1/4:Build allocation profiles",
        "2/4:Build canonical candidates",
        "3/4:Merge candidates by diff",
        "4/4:Finalize canonical exports",
    ];
    private static readonly string[] _expectedSubMicroLeftAnchorRows =
    [
        "V22|IC0|DIFF10|REG0|CAD7|10,150,20,75,65535,0,0",
        "V21|IC0|DIFF10|REG0|CAD7|10,150,150,20,1,96,65535,0,0",
    ];
    private static readonly string[] _expectedSubMicroRightAnchorRows =
    [
        "V22|IC0|DIFF20|REG1|CAD7|20,150,10,75,65535,0,0",
        "V21|IC0|DIFF20|REG1|CAD7|20,150,150,10,1,96,65535,0,0",
    ];

    [Fact]
    public void Generate_CadAllocationMode_ProducesFewerRowsThanLegacy_WhenCadSpansMultipleRegularPads()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 20, 10); // two regular pads share one CAD pad

        var matchSettings = new MatchingSettings
        {
            Mode = MatchMode.LegacyOverlap,
            MatchThreshold = 0.35,
            FreeformAxisThreshold = 0.2,
            EnableAutoDetectXy = false
        };

        var matchResult = PadMatcher.Match(cad, grid);
        TestInfrastructure.FreeformDetectorTestSupport.AutoTag(cad, grid, matchSettings, matchResult);

        var legacySettings = new ProjectSettings
        {
            Matching = matchSettings,
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V21,
                    NotchAlgorithmVersion.V22
                }
            }
        };

        var allocationSettings = new ProjectSettings
        {
            Matching = matchSettings,
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V21,
                    NotchAlgorithmVersion.V22
                }
            }
        };

        var generator = new NotchTableGenerator();
        var legacyTable = generator.Generate(cad, grid, legacySettings);
        var allocationTable = generator.Generate(cad, grid, allocationSettings);

        Assert.Equal(4, legacyTable.Rows.Count); // 2 regular pads * (V21 + V22)
        Assert.Equal(2, allocationTable.Rows.Count); // 1 CAD anchor * (V21 + V22)
        Assert.All(allocationTable.Rows, row => Assert.Equal(cad.Pads[0].Id, row.CadPadId));
    }

    [Fact]
    public void Generate_CadAllocationMode_UsesGeometryAnchor_WhenLegacyMatchMissing()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);
        var reg = grid.Pads[0];

        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = null;
        reg.MatchScore = 0.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Single(table.Rows);
        Assert.Equal(cad.Pads[0].Id, table.Rows[0].CadPadId);
        Assert.Equal(reg.DiffIndex, table.Rows[0].DiffIndex);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_UsesCadOutputFwDiffAsSource_AndKeepsRegularFwDiffAsTarget()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(5, 0, 20, 10); // 1/3 on diff0, 2/3 on diff1
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Pads[0].Id;
            regular.MatchScore = 1.0;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(
            cad,
            grid,
            settings,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int>
            {
                [cad.Pads[0].Id] = 0,
            });

        var row = Assert.Single(table.Rows);
        Assert.Equal(cad.Pads[0].Id, row.CadPadId);
        Assert.Equal(1, row.RegularPadIndex); // geometry anchor remains the right regular pad
        Assert.Equal(0, row.DiffIndex); // final source follows CAD output FW diff
        Assert.NotNull(row.V22Node);
        Assert.Equal(0, row.V22Node!.AnchorDiffIndex);
        Assert.Equal(1, row.V22Node.TargetDiffIndex1); // target stays in regular FW diff space
        Assert.Equal(1, row.Values[2]);
        Assert.False(table.ToFullCoverageAudit.HasExpectations);
        Assert.False(table.ToFullCoverageAudit.HasMissingCoverage);
    }

    [Fact]
    public void BuildToFullCoverageAudit_ReportsMissingTargetDiffsAcrossIcBuckets_WhenCanonicalRowsDoNotCoverAllToFullLegs()
    {
        var coveredLeg = NotchTableGeneratorPrivateAccess.NewV22DiffLeg(21, 40, true);
        var missingLeg = NotchTableGeneratorPrivateAccess.NewV22DiffLeg(22, 30, true);
        var ignoredLeg = NotchTableGeneratorPrivateAccess.NewV22DiffLeg(23, 20, false);
        var legs = CreatePrivateList(coveredLeg.GetType(), coveredLeg, missingLeg, ignoredLeg);
        var candidate = NotchTableGeneratorPrivateAccess.NewV22CadCandidate(
            0,
            20,
            100,
            200,
            1d,
            null,
            100,
            "CAD=200",
            10d,
            true,
            true,
            legs);
        var ic2CoveredLeg = NotchTableGeneratorPrivateAccess.NewV22DiffLeg(41, 25, true);
        var ic2MissingLeg = NotchTableGeneratorPrivateAccess.NewV22DiffLeg(42, 20, true);
        var ic2Legs = CreatePrivateList(ic2CoveredLeg.GetType(), ic2CoveredLeg, ic2MissingLeg);
        var ic2Candidate = NotchTableGeneratorPrivateAccess.NewV22CadCandidate(
            1,
            40,
            101,
            201,
            1d,
            null,
            100,
            "CAD=201",
            12d,
            true,
            true,
            ic2Legs);
        var candidatesByDiff = CreateCandidateDictionary(
            candidate.GetType(),
            ((0, 20), new[] { candidate }),
            ((1, 40), new[] { ic2Candidate }));
        var canonicalRows = new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 20,
                regularPadIndex: 100,
                cadPadId: 200,
                v22Node: new NotchV22Node(20, 100, 21, 40, 65535, 0, 0),
                comment: "main"),
            new NotchTableRow(
                icIndex: 1,
                diffIndex: 40,
                regularPadIndex: 101,
                cadPadId: 201,
                v22Node: new NotchV22Node(40, 100, 41, 25, 65535, 0, 0),
                comment: "main-ic2"),
        };

        var audit = NotchTableGeneratorPrivateAccess.BuildToFullCoverageAudit(null, candidatesByDiff, canonicalRows, 65535);

        Assert.True(audit.HasExpectations);
        Assert.True(audit.HasMissingCoverage);
        Assert.Equal(2, audit.BucketCount);
        Assert.Equal(4, audit.ExpectedTargetDiffCount);
        Assert.Equal(2, audit.CoveredTargetDiffCount);
        Assert.Equal(2, audit.MissingGaps.Count);

        var firstGap = audit.MissingGaps[0];
        Assert.Equal(0, firstGap.IcIndex);
        Assert.Equal(20, firstGap.SourceDiffIndex);
        Assert.Equal(22, firstGap.TargetDiffIndex);
        Assert.Equal(_cadPadIds200, firstGap.CadPadIds);

        var secondGap = audit.MissingGaps[1];
        Assert.Equal(1, secondGap.IcIndex);
        Assert.Equal(40, secondGap.SourceDiffIndex);
        Assert.Equal(42, secondGap.TargetDiffIndex);
        Assert.Equal(_cadPadIds201, secondGap.CadPadIds);
    }

    [Fact]
    public void Generate_CadAllocationMode_IsDeterministicAcrossRuns()
    {
        var grid = CreateGrid(rows: 2, cols: 2);
        grid.Pads[0].IcIndex = 0;
        grid.Pads[1].IcIndex = 0;
        grid.Pads[2].IcIndex = 1;
        grid.Pads[3].IcIndex = 1;
        grid.Pads[0].DiffIndex = 0;
        grid.Pads[1].DiffIndex = 1;
        grid.Pads[2].DiffIndex = 0;
        grid.Pads[3].DiffIndex = 1;

        var cad = new CadPadSet(new[]
        {
            new CadPad(
                1,
                "C1",
                "PAD",
                new Polygon2(new[]
                {
                    new Point2(0, 0),
                    new Point2(10, 0),
                    new Point2(10, 20),
                    new Point2(0, 20),
                })),
            new CadPad(
                2,
                "C2",
                "PAD",
                new Polygon2(new[]
                {
                    new Point2(10, 0),
                    new Point2(20, 0),
                    new Point2(20, 20),
                    new Point2(10, 20),
                })),
        });

        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchScore = 1.0;
            regular.MatchedCadPadId = regular.Col == 0 ? 1 : 2;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V21,
                    NotchAlgorithmVersion.V22
                }
            }
        };

        var generator = new NotchTableGenerator();
        var baseline = generator.Generate(cad, grid, settings).Rows
            .Select(BuildRowSnapshot)
            .ToArray();

        for (var run = 0; run < 5; run++)
        {
            var rerun = generator.Generate(cad, grid, settings).Rows
                .Select(BuildRowSnapshot)
                .ToArray();
            Assert.Equal(baseline, rerun);
        }
    }

    private static RegularGrid CreateGrid(int rows = 1, int cols = 1, double cellWidth = 10, double cellHeight = 10)
    {
        var xEdges = Enumerable.Range(0, cols + 1).Select(i => i * cellWidth).ToArray();
        var yEdges = Enumerable.Range(0, rows + 1).Select(i => i * cellHeight).ToArray();

        var pads = new List<RegularPad>(rows * cols);
        var index = 0;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var x0 = xEdges[col];
                var x1 = xEdges[col + 1];
                var y0 = yEdges[row];
                var y1 = yEdges[row + 1];

                var poly = new Polygon2(new[]
                {
                    new Point2(x0, y0),
                    new Point2(x1, y0),
                    new Point2(x1, y1),
                    new Point2(x0, y1),
                });

                var pad = new RegularPad(row, col, index, poly)
                {
                    DiffIndex = index,
                    IcIndex = 0
                };

                pads.Add(pad);
                index++;
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    private static CadPadSet CreateCad(RegularGrid grid)
    {
        var reg = grid.Pads[0];
        var cad = new CadPad(1, "C1", "PAD", reg.Polygon);
        return new CadPadSet(new[] { cad });
    }

    private static (RegularGrid Grid, CadPadSet Cad, ProjectSettings Settings) CreateSingleCadAllocationScenario()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
        grid.Pads[0].MatchScore = 1.0;
        return (grid, cad, new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        });
    }

    private static (RegularGrid Grid, CadPadSet Cad, ProjectSettings Settings) CreateLegacyTriangleScenario()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
        return (grid, cad, new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                LenScale = 7,
                NullValue = 54321,
            },
        });
    }

    private static (RegularGrid Grid, CadPadSet Cad) CreateTwoRegularLegacyScenario()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 20, 10);
        foreach (var reg in grid.Pads)
        {
            reg.Freeform = FreeformType.XWay;
            reg.MatchedCadPadId = cad.Pads[0].Id;
            reg.MatchScore = 1.0;
        }

        return (grid, cad);
    }

    private static CadPadSet CreateCadWithBounds(double minX, double minY, double maxX, double maxY)
    {
        var poly = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        var cad = new CadPad(1, "C1", "PAD", poly);
        return new CadPadSet(new[] { cad });
    }

    private static CadPadSet CreateCadRightTriangleHalfCell()
    {
        var poly = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(0, 10),
        });

        var cad = new CadPad(1, "C1", "PAD", poly);
        return new CadPadSet(new[] { cad });
    }

    private static string BuildRowSnapshot(NotchTableRow row)
    {
        return $"{row.Version}|IC{row.IcIndex}|DIFF{row.DiffIndex}|REG{row.RegularPadIndex}|CAD{row.CadPadId}|{string.Join(',', row.Values)}|{row.Comment}";
    }

    private static string[] BuildPayloadSnapshots(NotchTable table)
    {
        return table.Rows
            .Select(static row =>
                $"{row.Version}|IC{row.IcIndex}|DIFF{row.DiffIndex}|REG{row.RegularPadIndex}|CAD{row.CadPadId}|{string.Join(',', row.Values)}")
            .ToArray();
    }

    private static object CreatePrivateList(Type elementType, params object[] items)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = Assert.IsAssignableFrom<System.Collections.IList>(Activator.CreateInstance(listType));
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    private static object CreateCandidateDictionary(
        Type candidateType,
        params ((int IcIndex, int DiffIndex) Key, object[] Candidates)[] entries)
    {
        var listType = typeof(List<>).MakeGenericType(candidateType);
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof((int IcIndex, int DiffIndex)), listType);
        var dictionary = Assert.IsAssignableFrom<System.Collections.IDictionary>(Activator.CreateInstance(dictionaryType));
        foreach (var entry in entries)
        {
            dictionary.Add(entry.Key, CreatePrivateList(candidateType, entry.Candidates));
        }

        return dictionary;
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class SnapshotOnlySet(params int[] values) : HashSet<int>(values), IReadOnlySet<int>
    {
        bool IReadOnlySet<int>.Contains(int item) =>
            throw new InvalidOperationException("Caller set retained after snapshot.");
    }
}
