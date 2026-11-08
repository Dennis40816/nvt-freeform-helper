using System.Reflection;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
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
        // Each accessor binds to the helper by its exact parameter list. A helper that takes settings again,
        // or drops the context, makes the call throw MissingMethodException before it reads the null context.
        Assert.Throws<NullReferenceException>(() =>
            NotchTableGeneratorPrivateAccess.BuildCanonicalCandidatesByDiff(null, null, null, 0, 0, 0));
        Assert.Throws<NullReferenceException>(() =>
            NotchTableGeneratorPrivateAccess.BuildCanonicalCandidatesForProfile(null, null, null, null, out _));
        Assert.Throws<NullReferenceException>(() =>
            NotchTableGeneratorPrivateAccess.BuildV22CadCandidate(null, null, 0, null, null, null, out _));

        var contextType = NotchTableGeneratorPrivateAccess.GenerationContextClrType;
        Assert.NotNull(contextType);
        var contextFields = contextType!.GetFields(BindingFlags.Instance | BindingFlags.Public);
        var contextProperties = contextType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
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

        var matchResult = PadMatcher.Match(cad, grid);
        TestInfrastructure.FreeformDetectorTestSupport.AutoTag(cad, grid, matchSettings, matchResult);

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
        Assert.NotNull(table.GenerationPhaseTimings.CandidateBreakdown);
        Assert.Equal(1, table.GenerationPhaseTimings.CandidateBreakdown!.CandidateCount);
        Assert.NotNull(table.GenerationPhaseTimings.CandidateBreakdown.CompensationStageTimings);
        Assert.NotNull(table.GenerationPhaseTimings.CandidateBreakdown.CompensationStageTimings.StageCBreakdown);
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

        Assert.Equal(_expectedCadAllocationGenerationPhases, phases);
    }
}
