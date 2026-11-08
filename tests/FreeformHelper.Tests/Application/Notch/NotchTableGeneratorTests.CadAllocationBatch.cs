using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
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
}
