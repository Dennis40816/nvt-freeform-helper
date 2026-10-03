using System.Reflection;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchTableGeneratorTests
{
    private static readonly int[] CrossIcIndices = [0, 1];

    private static readonly int[] ExpectedLegacyDiffSet = [0, 1];
    private static readonly int[] CadPadIds200 = [200];
    private static readonly int[] CadPadIds201 = [201];
    private static readonly string[] ExpectedCadAllocationGenerationPhases =
    [
        "1/4:Build allocation profiles",
        "2/4:Build canonical candidates",
        "3/4:Merge candidates by diff",
        "4/4:Finalize canonical exports",
    ];
    private static readonly string[] ExpectedSubMicroLeftAnchorRows =
    [
        "V22|IC0|DIFF10|REG0|CAD7|10,150,20,75,65535,0,0",
        "V21|IC0|DIFF10|REG0|CAD7|10,150,150,20,1,96,65535,0,0",
    ];
    private static readonly string[] ExpectedSubMicroRightAnchorRows =
    [
        "V22|IC0|DIFF20|REG1|CAD7|20,150,10,75,65535,0,0",
        "V21|IC0|DIFF20|REG1|CAD7|20,150,150,10,1,96,65535,0,0",
    ];

    [Fact]
    public void Generate_Skips_When_Freeform_None()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_V22_BuildsCompatibilityGeometryRow()
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,50,100,54321,70,0,54321,0,54321|Freeform=XWay CadPadId=1 Case=0",
            BuildRowSnapshot(row));
    }

    [Theory]
    [InlineData(FreeformType.XWay)]
    [InlineData(FreeformType.YWay)]
    [InlineData(FreeformType.XYWay)]
    public void Generate_LegacyRegularAnchor_VersionDispatchMatchesEligibility(FreeformType freeform)
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();
        grid.Pads[0].Freeform = freeform;
        settings.Notch.EnabledVersions =
        [
            NotchAlgorithmVersion.V22,
            NotchAlgorithmVersion.V21,
        ];
        var expectedVersions = freeform == FreeformType.XYWay
            ? new[] { NotchAlgorithmVersion.V21 }
            : new[] { NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22 };
        var generator = new NotchTableGenerator();

        var eligibility = generator.EvaluateCadRowEligibility(cad.Pads[0], grid, settings);
        var table = generator.Generate(cad, grid, settings);

        Assert.Equal(expectedVersions, eligibility.EligibleVersions);
        Assert.Equal(expectedVersions.Length, eligibility.EstimatedRowCount);
        Assert.Equal(grid.Pads[0].RegularPadId, eligibility.AnchorRegularPadId);
        Assert.Equal(expectedVersions, table.Rows.Select(static row => row.Version));
        Assert.All(table.Rows, static row => Assert.Equal(9, row.Values.Length));
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_FreezesRequestBeforeInitialProgressCallback()
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.ProcessedCount == 0)
            {
                settings.Notch.ComputationMode = NotchComputationMode.CadAllocation;
            }
        });

        var row = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings, progress).Rows);

        Assert.Equal(NotchComputationMode.CadAllocation, settings.Notch.ComputationMode);
        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,50,100,54321,70,0,54321,0,54321|" +
            "Freeform=XWay CadPadId=1 Case=0",
            BuildRowSnapshot(row));
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_OwnsEnabledVersionsBeforeInitialProgressCallback()
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.ProcessedCount != 0)
            {
                return;
            }

            settings.Notch.EnabledVersions.Clear();
            settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V21);
        });

        var current = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings, progress).Rows);
        var next = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings).Rows);

        Assert.Equal(NotchAlgorithmVersion.V22, current.Version);
        Assert.Equal(NotchAlgorithmVersion.V21, next.Version);
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_FreezesRowSettingsForUnbuiltRowsAndNextCallObservesChanges()
    {
        var (grid, cad) = CreateTwoRegularLegacyScenario();

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 0,
                LenScale = 7,
                NullValue = 54321,
            },
        };
        var reports = new List<NotchGenerationProgress>();
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            reports.Add(snapshot);
            if (snapshot.ProcessedCount != 1)
            {
                return;
            }

            settings.Notch.ThresholdPercentV22 = 75;
            settings.Notch.LenScale = 9;
            settings.Notch.NullValue = 12345;
        });
        var generator = new NotchTableGenerator();

        var currentRows = generator.Generate(cad, grid, settings, progress).Rows;

        Assert.Equal(2, currentRows.Count);
        var currentLaterRow = Assert.Single(currentRows, row => row.DiffIndex == 1);
        Assert.Contains(54321, currentLaterRow.Values);
        Assert.Contains(70, currentLaterRow.Values);
        Assert.Equal(
            new[]
            {
                (Processed: 0, Total: 2, Rows: 0, Cad: (int?)null, Regular: (int?)null),
                (Processed: 1, Total: 2, Rows: 1, Cad: (int?)1, Regular: (int?)0),
                (Processed: 2, Total: 2, Rows: 2, Cad: (int?)1, Regular: (int?)1),
                (Processed: 2, Total: 2, Rows: 2, Cad: (int?)null, Regular: (int?)null),
            },
            reports.Select(report =>
                (report.ProcessedCount,
                 report.TotalCount,
                 report.GeneratedRowCount,
                 report.CurrentCadPadId,
                 report.CurrentRegularPadId)));
        Assert.Empty(generator.Generate(cad, grid, settings).Rows);

        settings.Notch.ThresholdPercentV22 = 0;
        var nextLaterRow = Assert.Single(
            generator.Generate(cad, grid, settings).Rows,
            row => row.DiffIndex == 1);
        Assert.Contains(12345, nextLaterRow.Values);
        Assert.Contains(90, nextLaterRow.Values);
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_FreezesLinkedThresholdForUnbuiltRows()
    {
        var (grid, cad) = CreateTwoRegularLegacyScenario();

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V22,
                    NotchAlgorithmVersion.V21,
                },
                LinkVersionThresholds = true,
                ThresholdQ7 = 0,
            },
        };
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.ProcessedCount == 1)
            {
                settings.Notch.ThresholdQ7 = 128;
            }
        });
        var generator = new NotchTableGenerator();

        var current = generator.Generate(cad, grid, settings, progress);

        Assert.Equal(
            new[]
            {
                NotchAlgorithmVersion.V21,
                NotchAlgorithmVersion.V22,
                NotchAlgorithmVersion.V21,
                NotchAlgorithmVersion.V22,
            },
            current.Rows.Select(static row => row.Version));
        Assert.Empty(generator.Generate(cad, grid, settings).Rows);
    }

    [Fact]
    public void Generate_V21_Adds_Row_When_Cad_Matched()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);

        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Single(table.Rows);
        Assert.Equal(NotchAlgorithmVersion.V21, table.Rows[0].Version);
    }

    [Fact]
    public void Generate_V21_YWay_PreservesLegacyNeighborOrientation()
    {
        var grid = CreateGrid(rows: 2, cols: 1);
        var cad = CreateCadWithBounds(0, 5, 10, 15); // centered across the row boundary

        var top = grid.Pads.Single(pad => pad.Row == 0);
        var bottom = grid.Pads.Single(pad => pad.Row == 1);
        top.Freeform = FreeformType.YWay;
        bottom.Freeform = FreeformType.YWay;
        top.MatchedCadPadId = cad.Pads[0].Id;
        bottom.MatchedCadPadId = cad.Pads[0].Id;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var rows = new NotchTableGenerator().Generate(cad, grid, settings).Rows;
        var topRow = Assert.Single(rows, row => row.DiffIndex == top.DiffIndex);
        var bottomRow = Assert.Single(rows, row => row.DiffIndex == bottom.DiffIndex);

        Assert.Equal(bottom.DiffIndex, topRow.Values[3]);
        Assert.Equal(top.DiffIndex, bottomRow.Values[6]);
    }

    [Fact]
    public void Generate_V21_EncodesNeighborLegMagnitudeAsQ7()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 19, 10); // extends 90% into the right neighbor
        var left = grid.Pads.Single(pad => pad.Row == 0 && pad.Col == 0);
        left.Freeform = FreeformType.XWay;
        left.MatchedCadPadId = cad.Pads[0].Id;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var row = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings).Rows);

        Assert.Equal(grid.Pads[1].DiffIndex, row.Values[6]);
        Assert.Equal(1, row.Values[7]);
        Assert.Equal(115, row.Values[8]); // Round(0.9 * 128)
    }

    [Fact]
    public void Generate_Skips_When_Cad_Not_Matched()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);

        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = null;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public void ProjectCadAllocationResolvedBatch_ReusesVersionNeutralCandidatesAcrossFinalRequests()
    {
        const string expectedV22 =
            "V22|IC0|DIFF0|REG0|CAD1|0,100,65535,0,65535,0,0|CAD=1 R=100% F=100% C=100% NT";
        const string expectedV21 =
            "V21|IC0|DIFF0|REG0|CAD1|0,100,100,65535,0,0,65535,0,0|CAD=1 R=100% F=100% C=100% NT";

        AssertScenario(
            CreateCadWithBounds(-5, -5, 15, 15),
            linkVersionThresholds: false,
            thresholdQ7: 64,
            thresholdPercentV22: 20.0,
            expectedV21Rows: [],
            expectedV22Rows: [expectedV22],
            expectedCombinedRows: [expectedV22, expectedV21]);
        AssertScenario(
            CreateCadWithBounds(-5, -5, 15, 15),
            linkVersionThresholds: false,
            thresholdQ7: 32,
            thresholdPercentV22: 50.0,
            expectedV21Rows: [expectedV21],
            expectedV22Rows: [],
            expectedCombinedRows: []);
        AssertScenario(
            CreateCadWithBounds(-5, -5, 15, 15),
            linkVersionThresholds: true,
            thresholdQ7: 32,
            thresholdPercentV22: 0.0,
            expectedV21Rows: [expectedV21],
            expectedV22Rows: [expectedV22],
            expectedCombinedRows: [expectedV22, expectedV21]);
        AssertScenario(
            CreateCadWithBounds(0, 0, 30, 10),
            linkVersionThresholds: true,
            thresholdQ7: 43,
            thresholdPercentV22: 0.0,
            expectedV21Rows: [expectedV21],
            expectedV22Rows: [],
            expectedCombinedRows: []);

        static void AssertScenario(
            CadPadSet cad,
            bool linkVersionThresholds,
            int thresholdQ7,
            double thresholdPercentV22,
            string[] expectedV21Rows,
            string[] expectedV22Rows,
            string[] expectedCombinedRows)
        {
            var grid = CreateGrid();
            grid.Pads[0].Freeform = FreeformType.XWay;
            grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
            grid.Pads[0].MatchScore = 1.0;
            var generator = new NotchTableGenerator();

            var resolutionProgress = new List<NotchGenerationProgress>();
            var batch = generator.ResolveCadAllocationBatch(
                cad,
                grid,
                CreateSettings(NotchAlgorithmVersion.V21),
                new CallbackProgress<NotchGenerationProgress>(resolutionProgress.Add));
            var v21 = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
                batch,
                CreateSettings(NotchAlgorithmVersion.V21),
                includeResolutionTimings: true);
            var warmProgress = new List<NotchGenerationProgress>();
            var v22 = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
                batch,
                CreateSettings(NotchAlgorithmVersion.V22),
                new CallbackProgress<NotchGenerationProgress>(warmProgress.Add));
            var combined = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
                batch,
                CreateSettings(NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22));
            var reversed = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
                batch,
                CreateSettings(NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21));

            Assert.Equal(1, v21.GenerationPhaseTimings.CandidateBreakdown?.CandidateCount);
            Assert.Equal(expectedV21Rows, v21.Rows.Select(BuildRowSnapshot));
            Assert.Equal(expectedV22Rows, v22.Rows.Select(BuildRowSnapshot));
            Assert.Equal(expectedCombinedRows, combined.Rows.Select(BuildRowSnapshot));
            Assert.Equal(expectedCombinedRows, reversed.Rows.Select(BuildRowSnapshot));
            var warmPhases = warmProgress
                .Select(static snapshot => $"{snapshot.PhaseStep}/{snapshot.PhaseStepCount}:{snapshot.Phase}")
                .Distinct();
            Assert.Equal(
                ["1/4:Build allocation profiles", "2/4:Build canonical candidates", "3/4:Merge candidates by diff"],
                resolutionProgress
                    .Select(static snapshot => $"{snapshot.PhaseStep}/{snapshot.PhaseStepCount}:{snapshot.Phase}")
                    .Distinct());
            Assert.Equal("4/4:Finalize canonical exports", Assert.Single(warmPhases));
            Assert.Equal(0, v22.GenerationPhaseTimings.BuildProfilesElapsedMs);
            Assert.Equal(0, v22.GenerationPhaseTimings.BuildCanonicalCandidatesElapsedMs);
            Assert.Equal(0, v22.GenerationPhaseTimings.MergeCanonicalCandidatesElapsedMs);
            Assert.False(v22.GenerationPhaseTimings.CandidateBreakdown?.HasData ?? false);

            ProjectSettings CreateSettings(params NotchAlgorithmVersion[] enabledVersions) => new()
            {
                Notch = new NotchSettings
                {
                    ComputationMode = NotchComputationMode.CadAllocation,
                    EnabledVersions = new HashSet<NotchAlgorithmVersion>(enabledVersions),
                    LinkVersionThresholds = linkVersionThresholds,
                    ThresholdQ7 = thresholdQ7,
                    ThresholdPercentV22 = thresholdPercentV22,
                },
            };
        }
    }

    [Fact]
    public void ResolveCadAllocationBatchWithSelectedResult_ReusesOneWarmSparseResult()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
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
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                MultiOwnerStrictOverlapPercent = 50.0,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };
        var target = cad.Pads[0];
        var anchor = grid.Pads[0];
        var activeRegularPadIds = grid.Pads
            .Select(static regular => regular.RegularPadId)
            .ToHashSet();
        var compensation = NotchV22CompensationService.Compute(
            target,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: cad.Pads,
            activeRegularPadIds: activeRegularPadIds,
            strictOverlapRatioOverride: 0.5);
        var warmResolved = new NotchV22ResolvedResultService().Build(
            target,
            compensation,
            strictOverlapRatio: 0.5,
            anchorIcIndex: anchor.IcIndex,
            anchorDiffIndex: anchor.DiffIndex,
            allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage,
            identity: NotchV22ResolvedResultService.CreateIdentity(
                target,
                grid,
                cad.Pads,
                activeRegularPadIds,
                settings.Notch.CompensationModel,
                enableToRegular: true,
                enableToFull: true,
                enableToFullRuleEngine: settings.Notch.EnableToFullRuleEngine,
                enableToFullRuleTrace: settings.Notch.EnableToFullRuleTrace,
                enableBoundaryVirtualAreaCap: settings.Notch.EnableBoundaryVirtualAreaCap,
                boundaryVirtualAreaCapRatio: settings.Notch.BoundaryVirtualAreaCapRatio,
                strictOverlapRatio: 0.5,
                anchorIcIndex: anchor.IcIndex,
                anchorDiffIndex: anchor.DiffIndex,
                allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage));
        var request = new NotchTableGenerator.CadAllocationSparseResultRequest(
            target.Id,
            anchor.IcIndex,
            anchor.DiffIndex,
            warmResolved);
        var generator = new NotchTableGenerator();

        var batch = generator.ResolveCadAllocationBatch(
            cad,
            grid,
            settings,
            activeRegularPadIds: activeRegularPadIds,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int>
            {
                [target.Id] = anchor.DiffIndex,
            },
            selectedSparseResultRequest: request);

        var sharedResult = Assert.IsType<NotchTableGenerator.CadAllocationSparseResult>(batch.SelectedSparseResult);
        Assert.Equal((target.Id, anchor.IcIndex, anchor.DiffIndex),
            (sharedResult.CadPadId, sharedResult.AnchorIcIndex, sharedResult.AnchorDiffIndex));
        Assert.Same(warmResolved, sharedResult.ResolvedResult);
        var projected = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);
        var fresh = new NotchTableGenerator().Generate(
            cad,
            grid,
            settings,
            activeRegularPadIds: activeRegularPadIds,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int>
            {
                [target.Id] = anchor.DiffIndex,
            });
        Assert.Equal(fresh.Rows.Select(BuildRowSnapshot), projected.Rows.Select(BuildRowSnapshot));

        foreach (var mismatchedIdentity in new[]
                 {
                     warmResolved.Identity! with { CadSignature = warmResolved.Identity.CadSignature + "|other" },
                     warmResolved.Identity! with { GridSignature = warmResolved.Identity.GridSignature + "|other" },
                     warmResolved.Identity! with { CadPoolSignature = warmResolved.Identity.CadPoolSignature + "|collision" },
                     warmResolved.Identity! with { ActiveRegularPadIdsSignature = "collision" },
                     warmResolved.Identity! with { CompensationModel = NotchCompensationModel.ConservativeNoGain },
                     warmResolved.Identity! with { AnchorDiffIndex = anchor.DiffIndex + 1 },
                 })
        {
            var mismatched = warmResolved with { Identity = mismatchedIdentity };
            Assert.Throws<ArgumentException>(() => generator.ResolveCadAllocationBatch(
                cad,
                grid,
                settings,
                activeRegularPadIds: activeRegularPadIds,
                cadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [target.Id] = anchor.DiffIndex },
                selectedSparseResultRequest: request with { ReusableResult = mismatched }));
        }

        grid.Pads[1].MatchScore += 0.01;
        Assert.Throws<ArgumentException>(() => generator.ResolveCadAllocationBatch(
            cad,
            grid,
            settings,
            activeRegularPadIds: activeRegularPadIds,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [target.Id] = anchor.DiffIndex },
            selectedSparseResultRequest: request));
    }

    [Fact]
    public void ResolveCadAllocationBatchWithSelectedResult_ColdCaptureIsBoundToExactAnchor()
    {
        var (grid, cad, settings) = CreateSingleCadAllocationScenario();
        var target = cad.Pads[0];
        var anchor = grid.Pads[0];
        var generator = new NotchTableGenerator();

        var batch = generator.ResolveCadAllocationBatch(
            cad,
            grid,
            settings,
            selectedSparseResultRequest: new NotchTableGenerator.CadAllocationSparseResultRequest(
                target.Id,
                anchor.IcIndex,
                anchor.DiffIndex));

        var captured = Assert.IsType<NotchTableGenerator.CadAllocationSparseResult>(batch.SelectedSparseResult)
            .ResolvedResult;
        Assert.Equal(target.Area, captured.TargetAllocation.CadArea, 6);
        Assert.Equal(
            NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings).Rows.Select(BuildRowSnapshot),
            generator.Generate(cad, grid, settings).Rows.Select(BuildRowSnapshot));

        var mismatchedBatch = generator.ResolveCadAllocationBatch(
            cad,
            grid,
            settings,
            selectedSparseResultRequest: new NotchTableGenerator.CadAllocationSparseResultRequest(
                target.Id,
                anchor.IcIndex,
                anchor.DiffIndex + 1));
        Assert.Null(mismatchedBatch.SelectedSparseResult);
    }

    [Fact]
    public void ProjectCadAllocationResolvedBatch_UsesCurrentNullValueAndTargetCoverageGuard()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
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
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };
        var generator = new NotchTableGenerator();
        var generated = generator.GenerateCadAllocationResolvedBatch(cad, grid, settings);

        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,200,1,100,65535,0,0|CAD=1 R=150% F=133% C=200% MAIN L=1",
            BuildRowSnapshot(Assert.Single(generated.Table.Rows)));

        settings.Notch.NullValue = 54321;
        settings.Notch.EnableTargetCoverageGuard = true;
        settings.Notch.TargetCoverageCapPercent = 50;

        var projected = NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings);
        var fresh = new NotchTableGenerator().Generate(cad, grid, settings);

        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,100,1,50,54321,0,0|CAD=1 R=150% F=133% C=200% TG=100% MAIN L=1",
            BuildRowSnapshot(Assert.Single(projected.Rows)));
        Assert.Equal(fresh.Rows.Select(BuildRowSnapshot), projected.Rows.Select(BuildRowSnapshot));

        settings.Notch.CompensationModel = NotchCompensationModel.Disabled;
        Assert.Throws<ArgumentException>(() =>
            NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings));
    }

    [Fact]
    public void ProjectCadAllocationResolvedBatch_RejectsIncompatibleInputsWhenFingerprintsCollide()
    {
        var grid = CreateGrid();
        var cad = CreateCadWithBounds(-5, -5, 15, 15);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
        grid.Pads[0].MatchScore = 1.0;
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                BoundaryVirtualAreaCapRatio = 7.4079,
                MultiOwnerStrictOverlapPercent = 66.73,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };
        var generated = new NotchTableGenerator().GenerateCadAllocationResolvedBatch(cad, grid, settings);
        var firstFingerprint = NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(settings);

        settings.Notch.BoundaryVirtualAreaCapRatio = 1.6586;
        settings.Notch.MultiOwnerStrictOverlapPercent = 79.07;

        Assert.Equal(
            firstFingerprint,
            NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(settings));
        Assert.Throws<ArgumentException>(() =>
            NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings));
    }

    [Fact]
    public void ProjectCadAllocationResolvedBatch_FinalThresholdFiltersRowsAndCoverageAuditFromSameCandidateView()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
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
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 80.0,
            },
        };
        var progressSnapshots = new List<NotchGenerationProgress>();

        var generator = new NotchTableGenerator();
        var generated = generator.GenerateCadAllocationResolvedBatch(
            cad,
            grid,
            settings,
            new CallbackProgress<NotchGenerationProgress>(progressSnapshots.Add));
        var table = generated.Table;

        Assert.Equal(1, table.GenerationPhaseTimings.CandidateBreakdown?.CandidateCount);
        Assert.Empty(table.Rows);
        Assert.Same(NotchToFullCoverageAudit.Empty, table.ToFullCoverageAudit);
        Assert.Equal(1, progressSnapshots.Last(snapshot => snapshot.PhaseStep == 3).GeneratedRowCount);
        Assert.Equal(0, progressSnapshots.Last(snapshot => snapshot.PhaseStep == 4).GeneratedRowCount);

        settings.Notch.ThresholdPercentV22 = 0.0;
        var admitted = NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings);
        var fresh = new NotchTableGenerator().Generate(cad, grid, settings);
        Assert.Equal(fresh.Rows.Select(BuildRowSnapshot), admitted.Rows.Select(BuildRowSnapshot));
        Assert.Equivalent(fresh.ToFullCoverageAudit, admitted.ToFullCoverageAudit, strict: true);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Generate_CadAllocationMode_FreezesProjectionThresholdAtCandidatePhaseStart(
        int mutateAtProcessedCount,
        bool expectsRow)
    {
        var grid = CreateGrid();
        var cad = CreateCadWithBounds(-5, -5, 15, 15);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
        grid.Pads[0].MatchScore = 1.0;
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 20.0,
            },
        };
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.PhaseStep == 2 && snapshot.ProcessedCount == mutateAtProcessedCount)
            {
                settings.Notch.ThresholdPercentV22 = 50.0;
            }
        });

        var table = new NotchTableGenerator().Generate(cad, grid, settings, progress);

        Assert.Equal(50.0, settings.Notch.ThresholdPercentV22);
        Assert.Equal(expectsRow ? 1 : 0, table.Rows.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Generate_CadAllocationMode_FreezesResolutionInputsAtCandidatePhaseStart(
        int mutateAtProcessedCount)
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
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
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                EnableToFullRuleEngine = true,
                EnableBoundaryVirtualAreaCap = true,
                BoundaryVirtualAreaCapRatio = 1.0,
                EnableTargetCoverageGuard = true,
                TargetCoverageCapPercent = 50,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V21,
                    NotchAlgorithmVersion.V22,
                },
            },
        };
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.PhaseStep != 2 || snapshot.ProcessedCount != mutateAtProcessedCount)
            {
                return;
            }

            settings.Notch.CompensationModel = NotchCompensationModel.Disabled;
            settings.Notch.EnableToRegular = false;
            settings.Notch.EnableToFull = false;
            settings.Notch.EnableToFullRuleEngine = false;
            settings.Notch.EnableToFullRuleTrace = true;
            settings.Notch.EnableBoundaryVirtualAreaCap = false;
            settings.Notch.BoundaryVirtualAreaCapRatio = 0.0;
            settings.Notch.EnableTargetCoverageGuard = false;
            settings.Notch.TargetCoverageCapPercent = 255;
        });

        var table = new NotchTableGenerator().Generate(cad, grid, settings, progress);

        Assert.Equal(NotchCompensationModel.Disabled, settings.Notch.CompensationModel);
        var expectedRows = mutateAtProcessedCount == 0
            ? new[]
            {
                "V22|IC0|DIFF0|REG0|CAD1|0,100,1,33,65535,0,0|CAD=1 R=100% F=100% C=100% MAIN L=1",
                "V21|IC0|DIFF0|REG0|CAD1|0,100,100,1,1,42,65535,0,0|CAD=1 R=100% F=100% C=100% MAIN L=1",
            }
            : new[]
            {
                "V22|IC0|DIFF0|REG0|CAD1|0,100,1,50,65535,0,0|CAD=1 R=150% F=133% C=200% TG=100% MAIN L=1",
                "V21|IC0|DIFF0|REG0|CAD1|0,100,100,1,1,64,65535,0,0|CAD=1 R=150% F=133% C=200% TG=100% MAIN L=1",
            };
        Assert.Equal(expectedRows, table.Rows.Select(BuildRowSnapshot));
    }

    [Theory]
    [InlineData(0, NotchAlgorithmVersion.V21, 54321)]
    [InlineData(1, NotchAlgorithmVersion.V22, 65535)]
    public void Generate_CadAllocationMode_FreezesProjectionRequestAtCandidatePhaseStart(
        int mutateAtProcessedCount,
        NotchAlgorithmVersion expectedVersion,
        int expectedNullValue)
    {
        var (grid, cad, settings) = CreateSingleCadAllocationScenario();
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.PhaseStep == 2 && snapshot.ProcessedCount == mutateAtProcessedCount)
            {
                settings.Notch.NullValue = 54321;
                settings.Notch.EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V21,
                };
            }
        });

        var row = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings, progress).Rows);

        Assert.Equal(54321, settings.Notch.NullValue);
        Assert.Equal(expectedVersion, row.Version);
        Assert.Equal(expectedNullValue, row.Values[expectedVersion == NotchAlgorithmVersion.V21 ? 3 : 2]);
    }

    [Theory]
    [InlineData(1, 1, 9)]
    [InlineData(2, 0, 7)]
    public void Generate_CadAllocationMode_SnapshotsCadOutputFwDiffMapAfterProfiles(
        int mutateAtPhaseStep,
        int mutateAtProcessedCount,
        int expectedDiffIndex)
    {
        var (grid, cad, settings) = CreateSingleCadAllocationScenario();
        var cadOutputFwDiffIndexByCadId = new Dictionary<int, int>
        {
            [cad.Pads[0].Id] = 7,
        };
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.PhaseStep == mutateAtPhaseStep &&
                snapshot.ProcessedCount == mutateAtProcessedCount)
            {
                cadOutputFwDiffIndexByCadId[cad.Pads[0].Id] = 9;
            }
        });

        var row = Assert.Single(new NotchTableGenerator().Generate(
            cad,
            grid,
            settings,
            progress,
            cadOutputFwDiffIndexByCadId: cadOutputFwDiffIndexByCadId).Rows);

        Assert.Equal(9, cadOutputFwDiffIndexByCadId[cad.Pads[0].Id]);
        Assert.Equal(
            $"V22|IC0|DIFF{expectedDiffIndex}|REG0|CAD1|{expectedDiffIndex},100,0,100,65535,0,0|" +
            "CAD=1 R=100% F=100% C=100% MAIN L=1",
            BuildRowSnapshot(row));
    }

    [Fact]
    public void Generate_CadAllocationMode_DoesNotRetainCallerActiveSetAfterSnapshot()
    {
        var (grid, cad, settings) = CreateSingleCadAllocationScenario();

        var row = Assert.Single(new NotchTableGenerator().Generate(
            cad,
            grid,
            settings,
            activeRegularPadIds: new SnapshotOnlySet(grid.Pads[0].RegularPadId)).Rows);

        Assert.Equal(0, row.DiffIndex);
    }

    [Fact]
    public void CadAllocationCandidateHelpers_ConsumeOnlyFrozenGenerationContext()
    {
        const BindingFlags methodFlags = BindingFlags.Static | BindingFlags.NonPublic;
        var generatorType = typeof(NotchTableGenerator);
        var contextType = generatorType.GetNestedType("CadAllocationGenerationContext", BindingFlags.NonPublic);
        Assert.NotNull(contextType);
        foreach (var methodName in new[]
                 {
                     "BuildCanonicalCandidatesByDiff",
                     "BuildCanonicalCandidatesForProfile",
                     "BuildV22CadCandidate",
                 })
        {
            var method = Assert.Single(generatorType.GetMethods(methodFlags), method => method.Name == methodName);
            Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == contextType);
            Assert.DoesNotContain(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(ProjectSettings) ||
                             parameter.ParameterType == typeof(NotchSettings));
        }

        var contextFields = contextType!.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var contextProperties = contextType.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotEmpty(contextFields);
        Assert.All(contextFields, field => Assert.True(field.IsInitOnly));
        Assert.All(contextProperties, property => Assert.Null(property.SetMethod));
        Assert.DoesNotContain(
            contextProperties,
            property => property.PropertyType == typeof(ProjectSettings) ||
                        property.PropertyType == typeof(NotchSettings));
        Assert.DoesNotContain(
            contextFields,
            field => field.FieldType == typeof(ProjectSettings) ||
                     field.FieldType == typeof(NotchSettings));
    }

    [Fact]
    public void Generate_CadAllocationMode_SelectsSingleAnchorPerCad()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 20, 10); // spans two regular pads

        var matchSettings = new MatchingSettings
        {
            Mode = MatchMode.LegacyOverlap,
            MatchThreshold = 0.35,
            FreeformAxisThreshold = 0.2,
            EnableAutoDetectXy = false
        };

        var matcher = new PadMatcher();
        var matchResult = PadMatcher.Match(cad, grid, matchSettings);
        FreeformDetector.AutoTagFreeforms(cad, grid, matchSettings, matchResult);

        var settings = new ProjectSettings
        {
            Matching = matchSettings,
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Single(table.Rows);
        Assert.Equal(cad.Pads[0].Id, table.Rows[0].CadPadId);
        Assert.Equal(0, table.Rows[0].DiffIndex); // tie-break chooses smaller diff index
        Assert.NotNull(table.GenerationPhaseTimings);
        Assert.True(table.GenerationPhaseTimings.BuildProfilesElapsedMs >= 0);
        Assert.True(table.GenerationPhaseTimings.BuildCanonicalCandidatesElapsedMs >= 0);
        Assert.True(table.GenerationPhaseTimings.MergeCanonicalCandidatesElapsedMs >= 0);
        Assert.True(table.GenerationPhaseTimings.BuildLegacyRowsElapsedMs >= 0);
        Assert.True(table.GenerationPhaseTimings.FinalizeCanonicalExportsElapsedMs >= 0);
        Assert.NotNull(table.GenerationPhaseTimings.CandidateBreakdown);
        Assert.True(table.GenerationPhaseTimings.CandidateBreakdown!.CandidateCount >= 0);
        Assert.True(table.GenerationPhaseTimings.CandidateBreakdown.CompensationStageTimings.TotalElapsedMs >= 0);
        Assert.NotNull(table.GenerationPhaseTimings.CandidateBreakdown.CompensationStageTimings.StageCBreakdown);
        Assert.True(table.GenerationPhaseTimings.CandidateBreakdown.CompensationStageTimings.StageCBreakdown!.TotalElapsedMs >= 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Generate_CadAllocationMode_ReportsOnlyExecutablePhases(bool hasCadProfile)
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = hasCadProfile
            ? CreateCadWithBounds(0, 0, 20, 10)
            : new CadPadSet(Array.Empty<CadPad>());
        var reports = new List<NotchGenerationProgress>();
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V21,
                    NotchAlgorithmVersion.V22,
                },
            },
        };

        _ = new NotchTableGenerator().Generate(
            cad,
            grid,
            settings,
            new CallbackProgress<NotchGenerationProgress>(reports.Add));

        var phases = reports
            .Where(static report => report.PhaseStepCount > 0)
            .Select(static report => $"{report.PhaseStep}/{report.PhaseStepCount}:{report.Phase}")
            .Distinct()
            .ToArray();

        Assert.Equal(ExpectedCadAllocationGenerationPhases, phases);
    }

    [Fact]
    public void Generate_V21_WorksFromOverlapMatchAndAutoDetectPipeline()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 20, 10); // exactly spans two regular pads on X axis

        var matchSettings = new MatchingSettings
        {
            Mode = MatchMode.LegacyOverlap,
            MatchThreshold = 0.35,
            FreeformAxisThreshold = 0.2,
            EnableAutoDetectXy = false
        };

        var matcher = new PadMatcher();
        var matchResult = PadMatcher.Match(cad, grid, matchSettings);
        FreeformDetector.AutoTagFreeforms(cad, grid, matchSettings, matchResult);

        var settings = new ProjectSettings
        {
            Matching = matchSettings,
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(NotchAlgorithmVersion.V21, row.Version));
        Assert.All(table.Rows, row => Assert.Equal(cad.Pads[0].Id, row.CadPadId));

        var diffSet = table.Rows.Select(r => r.DiffIndex).OrderBy(v => v).ToArray();
        Assert.Equal(ExpectedLegacyDiffSet, diffSet);
        Assert.All(table.Rows, row => Assert.Equal(row.DiffIndex, row.Values[0]));
    }

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

        var matcher = new PadMatcher();
        var matchResult = PadMatcher.Match(cad, grid, matchSettings);
        FreeformDetector.AutoTagFreeforms(cad, grid, matchSettings, matchResult);

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
        var generatorType = typeof(NotchTableGenerator);
        var diffLegType = generatorType.GetNestedType("V22DiffLeg", BindingFlags.NonPublic);
        var candidateType = generatorType.GetNestedType("V22CadCandidate", BindingFlags.NonPublic);
        Assert.NotNull(diffLegType);
        Assert.NotNull(candidateType);

        var coveredLeg = CreatePrivateRecord(
            diffLegType!,
            21,
            40,
            true);
        var missingLeg = CreatePrivateRecord(
            diffLegType!,
            22,
            30,
            true);
        var ignoredLeg = CreatePrivateRecord(
            diffLegType!,
            23,
            20,
            false);
        var legs = CreatePrivateList(diffLegType!, coveredLeg, missingLeg, ignoredLeg);
        var candidate = CreatePrivateRecord(
            candidateType!,
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
        var ic2CoveredLeg = CreatePrivateRecord(
            diffLegType!,
            41,
            25,
            true);
        var ic2MissingLeg = CreatePrivateRecord(
            diffLegType!,
            42,
            20,
            true);
        var ic2Legs = CreatePrivateList(diffLegType!, ic2CoveredLeg, ic2MissingLeg);
        var ic2Candidate = CreatePrivateRecord(
            candidateType!,
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
            candidateType!,
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

        var method = generatorType.GetMethod("BuildToFullCoverageAudit", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var audit = Assert.IsType<NotchToFullCoverageAudit>(method!.Invoke(null, [candidatesByDiff, canonicalRows, 65535]));

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
        Assert.Equal(CadPadIds200, firstGap.CadPadIds);

        var secondGap = audit.MissingGaps[1];
        Assert.Equal(1, secondGap.IcIndex);
        Assert.Equal(40, secondGap.SourceDiffIndex);
        Assert.Equal(42, secondGap.TargetDiffIndex);
        Assert.Equal(CadPadIds201, secondGap.CadPadIds);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_UsesUndoNfAndToFullRatios()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.NotNull(row.V22Node);
        Assert.Equal(row.Values[0], row.V22Node!.AnchorDiffIndex);
        Assert.Equal(row.Values[1], row.V22Node.CombinePercent);
        Assert.Equal(100, row.Values[1]); // Combined = 50% * 200% = 100%
        Assert.Equal(settings.Notch.NullValue, row.Values[2]); // No transfer leg in this simple case.
        Assert.Equal(0, row.Values[3]);
        Assert.Equal(settings.Notch.NullValue, row.Values[4]);
        Assert.Equal(0, row.Values[5]);
        Assert.Contains("R=50%", row.Comment);
        Assert.Contains("F=200%", row.Comment);
        Assert.Contains("C=100%", row.Comment);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_SnapshotStable_ForSimpleTriangle()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);
        var row = Assert.Single(table.Rows);

        var snapshot = BuildRowSnapshot(row);
        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,100,65535,0,65535,0,0|CAD=1 R=50% F=200% C=100% NT",
            snapshot);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_ConservativeModel_UsesTargetRegularSourceCoverage()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.ConservativeNoGain,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(50, row.Values[1]);
        Assert.Contains("R=50%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("F=200%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=50%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_CurrentGain_UsesTargetRegularStage3Coverage()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
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
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal([0, 200, 1, 100, settings.Notch.NullValue, 0, 0], row.Values);
        Assert.Contains("R=150%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("F=133%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=200%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_DisabledModel_ForcesUnityRatios()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;
        reg.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.Disabled,
                EnableToRegular = true,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(100, row.Values[1]);
        Assert.Contains("R=100%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("F=100%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=100%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_UsesActiveRegularPadSet_ForToFullBoundaryDecision()
    {
        var grid = CreateGrid(rows: 3, cols: 3);
        var center = grid.Pads.Single(pad => pad.Row == 1 && pad.Col == 1);
        var cadPolygon = new Polygon2(
            new[]
            {
                new Point2(10, 10),
                new Point2(20, 10),
                new Point2(10, 20),
            });
        var cad = new CadPadSet(new[]
        {
            new CadPad(1, "C1", "PAD", cadPolygon),
        });
        center.Freeform = FreeformType.XWay;
        center.MatchedCadPadId = 1;
        center.MatchScore = 1.0;

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
        var withoutActiveRegularFilter = generator.Generate(cad, grid, settings);
        var withActiveRegularFilter = generator.Generate(
            cad,
            grid,
            settings,
            activeRegularPadIds: new HashSet<int> { center.RegularPadId });

        var withoutRow = Assert.Single(withoutActiveRegularFilter.Rows);
        var withRow = Assert.Single(withActiveRegularFilter.Rows);

        Assert.Equal(100, withoutRow.Values[1]);
        Assert.Equal(100, withRow.Values[1]);
        Assert.Contains("F=200%", withoutRow.Comment, StringComparison.Ordinal);
        Assert.Contains("F=200%", withRow.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_V22_PreservesCompatibilityNeighborOrientation()
    {
        var grid = CreateGrid(rows: 2, cols: 1);
        var cad = CreateCadWithBounds(0, 5, 10, 15);

        var top = grid.Pads.Single(pad => pad.Row == 0);
        var bottom = grid.Pads.Single(pad => pad.Row == 1);
        top.Freeform = FreeformType.YWay;
        bottom.Freeform = FreeformType.YWay;
        top.MatchedCadPadId = cad.Pads[0].Id;
        bottom.MatchedCadPadId = cad.Pads[0].Id;
        top.MatchScore = 1.0;
        bottom.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var rows = new NotchTableGenerator().Generate(cad, grid, settings).Rows;
        var topRow = Assert.Single(rows, row => row.DiffIndex == top.DiffIndex);
        var bottomRow = Assert.Single(rows, row => row.DiffIndex == bottom.DiffIndex);

        Assert.All(rows, row => Assert.Equal(9, row.Values.Length));
        Assert.Equal(settings.Notch.NullValue, topRow.Values[3]);
        Assert.Equal(bottom.DiffIndex, topRow.Values[6]);
        Assert.Equal(top.DiffIndex, bottomRow.Values[3]);
        Assert.Equal(settings.Notch.NullValue, bottomRow.Values[6]);
    }

    [Fact]
    public void ProjectCadAllocationResolvedBatch_ValidatesCombinedPercentOnlyAfterFinalThresholdAdmission()
    {
        var grid = CreateGrid(rows: 1, cols: 3);
        var cad = CreateCadWithBounds(0, 0, 30, 10); // one CAD fully covers 3 regular pads
        foreach (var reg in grid.Pads)
        {
            reg.Freeform = FreeformType.XWay;
            reg.MatchedCadPadId = cad.Pads[0].Id;
            reg.MatchScore = 1.0;
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
                    NotchAlgorithmVersion.V22,
                },
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 50.0,
            }
        };

        var generator = new NotchTableGenerator();
        var generated = generator.GenerateCadAllocationResolvedBatch(cad, grid, settings);
        var rejected = generated.Table;

        Assert.Equal(1, rejected.GenerationPhaseTimings.CandidateBreakdown?.CandidateCount);
        Assert.Empty(rejected.Rows);
        Assert.False(rejected.ToFullCoverageAudit.HasExpectations);

        settings.Notch.ThresholdPercentV22 = 0.0;
        foreach (var reg in grid.Pads)
        {
            reg.Freeform = FreeformType.None;
        }

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings));
        Assert.Contains("Combine ratio exceeds 255%", ex.Message, StringComparison.Ordinal);

        settings.Notch.ThresholdPercentV22 = 80;
        var structurallyIneligible = generator.GenerateCadAllocationResolvedBatch(cad, grid, settings);
        settings.Notch.ThresholdPercentV22 = 0;
        var structurallyIneligibleEx = Assert.Throws<InvalidOperationException>(() =>
            NotchTableGenerator.ProjectCadAllocationResolvedBatch(structurallyIneligible.Batch, settings));
        Assert.Equal(ex.Message, structurallyIneligibleEx.Message);

        settings.Notch.EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 };
        settings.Notch.ThresholdQ7 = 64;
        Assert.Empty(NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings).Rows);
        settings.Notch.ThresholdQ7 = 0;
        var v21Ex = Assert.Throws<InvalidOperationException>(() =>
            NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings));
        Assert.Equal(ex.Message, v21Ex.Message);

        settings.Notch.EnabledVersions.Clear();
        settings.Notch.EnabledVersions.Add((NotchAlgorithmVersion)999);
        var unsupported = NotchTableGenerator.ProjectCadAllocationResolvedBatch(generated.Batch, settings);
        Assert.Empty(unsupported.Rows);
        Assert.False(unsupported.ToFullCoverageAudit.HasExpectations);
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_EmitsRowsPerIc_WhenCadOverlapsMultipleIcs()
    {
        var grid = CreateGrid(rows: 2, cols: 1);
        grid.Pads[0].IcIndex = 0;
        grid.Pads[0].DiffIndex = 0;
        grid.Pads[1].IcIndex = 1;
        grid.Pads[1].DiffIndex = 0;

        var cad = CreateCadWithBounds(0, 0, 10, 20);
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
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(CrossIcIndices, table.Rows.Select(row => row.IcIndex).ToArray());
        Assert.All(table.Rows, row => Assert.Contains("XIC=1/2", row.Comment, StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_CadAllocationMode_V22_EmitsToFullRow_WhenAnchorIsNotFreeform()
    {
        var grid = CreateGrid();
        var cad = CreateCadRightTriangleHalfCell();
        var regular = grid.Pads[0];
        regular.Freeform = FreeformType.None;
        regular.MatchedCadPadId = null;
        regular.MatchScore = 0.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableToFull = true,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(regular.DiffIndex, row.DiffIndex);
        Assert.Equal(cad.Pads[0].Id, row.CadPadId);
        Assert.Contains("F=200%", row.Comment, StringComparison.Ordinal);
        Assert.Contains("C=100%", row.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CadAllocationMode_WhenSubMicroPolygonChangeFlipsAllocation_UsesCurrentGeometry()
    {
        var grid = CreateGrid(rows: 1, cols: 2, cellWidth: 5, cellHeight: 10);
        grid.Pads[0].DiffIndex = 10;
        grid.Pads[1].DiffIndex = 20;
        foreach (var regular in grid.Pads)
        {
            regular.IcIndex = 0;
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = 7;
            regular.MatchScore = 1.0;
        }

        var leftAnchor = new CadPad(7, "LeftAnchor", "PAD", new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(5.0000004, 5),
            new Point2(0, 10),
        }));
        var rightAnchor = new CadPad(7, "RightAnchor", "PAD", new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(4.9999996, 5),
            new Point2(0, 10),
        }));
        Assert.Equal(75.0, leftAnchor.Area, 6);
        Assert.Equal(75.0, rightAnchor.Area, 6);
        Assert.Equal(leftAnchor.Bounds, rightAnchor.Bounds);
        Assert.Equal(
            CadPadGeometrySignature.Build(leftAnchor.Polygon),
            CadPadGeometrySignature.Build(rightAnchor.Polygon));
        Assert.Equal(37.500001, Polygon2.IntersectionAreaWithRect(leftAnchor.Polygon, grid.Pads[0].Bounds), 6);
        Assert.Equal(37.499999, Polygon2.IntersectionAreaWithRect(leftAnchor.Polygon, grid.Pads[1].Bounds), 6);
        Assert.Equal(37.499999, Polygon2.IntersectionAreaWithRect(rightAnchor.Polygon, grid.Pads[0].Bounds), 6);
        Assert.Equal(37.500001, Polygon2.IntersectionAreaWithRect(rightAnchor.Polygon, grid.Pads[1].Bounds), 6);

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = false,
                NullValue = 65535,
                ThresholdQ7 = 0,
                ThresholdPercentV22 = 0,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V21,
                    NotchAlgorithmVersion.V22,
                },
            },
        };

        AssertRefreshesMemo(leftAnchor, rightAnchor, ExpectedSubMicroRightAnchorRows);
        AssertRefreshesMemo(rightAnchor, leftAnchor, ExpectedSubMicroLeftAnchorRows);

        var vertices = leftAnchor.Polygon.Vertices;
        var cyclic = new CadPad(
            7,
            "Cyclic",
            "PAD",
            new Polygon2(vertices.Skip(2).Concat(vertices.Take(2))));
        var reversed = new CadPad(7, "Reversed", "PAD", new Polygon2(vertices.Reverse()));
        var canonicalGenerator = new NotchTableGenerator();
        var canonical = BuildPayloadSnapshots(
            canonicalGenerator.Generate(new CadPadSet(new[] { leftAnchor }), grid, settings));
        var cyclicActual = BuildPayloadSnapshots(
            canonicalGenerator.Generate(new CadPadSet(new[] { cyclic }), grid, settings));
        var reversedActual = BuildPayloadSnapshots(
            canonicalGenerator.Generate(new CadPadSet(new[] { reversed }), grid, settings));

        Assert.Equal(ExpectedSubMicroLeftAnchorRows, canonical);
        Assert.Equal(canonical, cyclicActual);
        Assert.Equal(canonical, reversedActual);

        AssertVersionProjection(NotchAlgorithmVersion.V21, ExpectedSubMicroRightAnchorRows[1]);
        AssertVersionProjection(NotchAlgorithmVersion.V22, ExpectedSubMicroRightAnchorRows[0]);

        void AssertRefreshesMemo(CadPad warm, CadPad current, string[] expectedCurrent)
        {
            var generator = new NotchTableGenerator();
            var warmRows = BuildPayloadSnapshots(generator.Generate(new CadPadSet(new[] { warm }), grid, settings));
            var actual = BuildPayloadSnapshots(generator.Generate(new CadPadSet(new[] { current }), grid, settings));
            var fresh = BuildPayloadSnapshots(
                new NotchTableGenerator().Generate(new CadPadSet(new[] { current }), grid, settings));

            Assert.Equal(expectedCurrent, actual);
            Assert.Equal(fresh, actual);
            Assert.False(warmRows.SequenceEqual(actual));
        }

        void AssertVersionProjection(NotchAlgorithmVersion version, string expectedCurrent)
        {
            settings.Notch.EnabledVersions = new HashSet<NotchAlgorithmVersion> { version };
            var generator = new NotchTableGenerator();
            _ = generator.Generate(new CadPadSet(new[] { leftAnchor }), grid, settings);
            var actual = Assert.Single(BuildPayloadSnapshots(
                generator.Generate(new CadPadSet(new[] { rightAnchor }), grid, settings)));

            Assert.Equal(expectedCurrent, actual);
        }
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

    private static object CreatePrivateRecord(Type type, params object?[] arguments)
    {
        var constructor = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == arguments.Length);
        var instance = constructor.Invoke(arguments);
        Assert.NotNull(instance);
        return instance!;
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
