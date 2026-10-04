using System.Collections.ObjectModel;
using System.Reflection;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceViewModelTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] SimulationDiffIndices = { 10, 11 };
    private static readonly int[] DuplicateDiffIndices = { 10, 10 };

    [Fact]
    public async Task Constructor_OffUiThread_DoesNotCreateDispatcherTimer()
    {
        var timerField = typeof(SimulationWorkspaceViewModel).GetField(
            "_playbackTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(timerField);

        var viewModel = await Task.Run(() => new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession()));

        Assert.Null(timerField.GetValue(viewModel));
    }

    [Fact]
    public void Collections_ExposeReadOnlyFacade()
    {
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession());

        Assert.IsType<ReadOnlyObservableCollection<NotchApplySimulationSourceFileItemViewModel>>(viewModel.CsvFiles);
        Assert.IsType<ReadOnlyObservableCollection<NotchApplySimulationFrameOption>>(viewModel.FrameOptions);
        Assert.IsType<ReadOnlyObservableCollection<NotchApplySimulationImpactItemViewModel>>(viewModel.SelectedImpactItems);
        Assert.IsType<ReadOnlyObservableCollection<SimulationSafetyRiskDiffViewModel>>(viewModel.SimulationHighRiskDiffs);
        Assert.IsType<ReadOnlyObservableCollection<CopperPillarPathReplayArtifactRow>>(viewModel.CopperPathReplayRows);
        Assert.Equal("Copper path replay: not run.", viewModel.CopperPathReplaySummaryText);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenPickerReturnsCsv_PopulatesCsvProjectionAndOverlay()
    {
        var session = BuildSession();
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(new[] { csvPath }),
        };

        await viewModel.ImportCsvAsync();

        Assert.True(viewModel.HasCsvFiles);
        Assert.True(viewModel.HasCsvData);
        Assert.True(viewModel.IsCsvSource);
        Assert.Equal(2, viewModel.SimulationOverlayItems.Count);
        Assert.Equal(0, viewModel.SelectedFrameSliderMaximum);
        Assert.Contains("frames 1", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.HasCsvInspectorData);
        Assert.Contains("Compatible frames 1/1", viewModel.CsvInspectorSummaryText, StringComparison.Ordinal);
        Assert.Contains("Expected grid 2 x 1", viewModel.CsvInspectorSummaryText, StringComparison.Ordinal);
        Assert.Contains("Declared Xch/Ych: 2 x 1", viewModel.CsvInspectorShapeSummaryText, StringComparison.Ordinal);
        Assert.Contains("CSV row 0", viewModel.CsvInspectorRowOriginText, StringComparison.Ordinal);
        Assert.Contains("range 50..100", viewModel.CsvInspectorBeforePreviewText, StringComparison.Ordinal);
        Assert.Contains("Xch/Ych 2 x 1", viewModel.CsvFiles[0].ShapeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScenarioSnapshot_WhenSourceChanges_TracksManualCsvAndCopperSources()
    {
        var session = BuildSession();
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(new[] { csvPath }),
        };

        Assert.Equal(SimulationScenarioKind.Manual, viewModel.GetSimulationScenarioSnapshot()?.Scenario.Kind);

        await viewModel.ImportCsvAsync();

        var csvSnapshot = Assert.IsType<SimulationScenarioSnapshot>(viewModel.GetSimulationScenarioSnapshot());
        Assert.Equal(SimulationScenarioKind.Csv, csvSnapshot.Scenario.Kind);
        Assert.Equal(csvSnapshot.Result.Cells.Count, viewModel.GetSimulationSafetyAuditSnapshot().CellCount);

        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);

        var copperSnapshot = Assert.IsType<SimulationScenarioSnapshot>(viewModel.GetSimulationScenarioSnapshot());
        Assert.Equal(SimulationScenarioKind.Copper, copperSnapshot.Scenario.Kind);
        Assert.Equal(copperSnapshot.SafetyAudit.CellCount, viewModel.GetSimulationSafetyAuditSnapshot().CellCount);
    }

    [Fact]
    public async Task SummaryTexts_WhenDisplaySettingsChange_TrackSimulationSnapshot()
    {
        var session = BuildSession();
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(new[] { csvPath }),
        };

        Assert.Equal("Source: Manual", viewModel.SourceModeSummaryText);
        Assert.StartsWith("Version:", viewModel.VersionSummaryText, StringComparison.Ordinal);
        Assert.StartsWith("View:", viewModel.ViewModeSummaryText, StringComparison.Ordinal);

        viewModel.SelectedCanvasColorOption = viewModel.CanvasColorOptions.Single(option => option.Value == NotchApplySimulationCanvasColorMode.Threshold);
        viewModel.ColorThresholdValue = 7.5d;
        Assert.Equal("Color: TH scale (7.5)", viewModel.ColorModeSummaryText);

        await viewModel.ImportCsvAsync();

        Assert.Equal("Source: CSV", viewModel.SourceModeSummaryText);
        Assert.Equal("Frame 1/1", viewModel.FrameSummaryText);
        Assert.Contains("frames 1", viewModel.CanvasStatusSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplySelectedOverride_WhenManualMode_UsesOverrideAsBeforeValue()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        viewModel.SelectRegularPad(0);
        viewModel.SelectedManualValue = 123d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);

        Assert.Equal("123", viewModel.SelectedBeforeText);
        Assert.Equal("62", viewModel.SelectedAfterText);
        Assert.True(viewModel.HasSelectedOverride);
    }

    [Fact]
    public void InputSummaryTexts_WhenManualMode_SplitBaselineAndOverrideCount()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        viewModel.SelectRegularPad(0);
        viewModel.SelectedManualValue = 88d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);

        Assert.Equal("Manual baseline 0", viewModel.InputPrimarySummaryText);
        Assert.Equal("Overrides 1", viewModel.InputSecondarySummaryText);
    }

    [Fact]
    public void InputContractTexts_WhenSourceChanges_DescribeSourceSemantics()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        Assert.Contains("regular/FW memory", viewModel.InputContractTitleText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not model CAD", viewModel.InputContractDetailText, StringComparison.OrdinalIgnoreCase);

        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Csv);
        Assert.Contains("FW/tool rectangular", viewModel.InputContractTitleText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("top row maps", viewModel.CsvProjectionContractText, StringComparison.OrdinalIgnoreCase);

        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);
        Assert.Contains("CAD physical overlap", viewModel.InputContractTitleText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CAD pad overlap", viewModel.InputContractDetailText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyManualPresetCommand_WhenManualMode_AppliesUniformAndClearsOverrides()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        viewModel.SelectRegularPad(0);
        viewModel.SelectedManualValue = 123d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);
        Assert.True(viewModel.HasManualOverrides);

        viewModel.ApplyManualPresetCommand.Execute("400");

        Assert.Equal(400d, viewModel.GlobalValue);
        Assert.False(viewModel.HasManualOverrides);
        Assert.Equal("400", viewModel.SelectedBeforeText);
        Assert.Contains("uniform 400", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildHoverTipText_WhenRegularExists_UsesSharedSimulationSnapshot()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 100d,
        };

        var text = viewModel.BuildHoverTipText(0);

        Assert.NotNull(text);
        Assert.Contains("REG 0", text, StringComparison.Ordinal);
        Assert.Contains("After 50", text, StringComparison.Ordinal);
        Assert.Contains("Delta -50", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedAreaOption_WhenChanged_FiltersOverlayAndClearsOutOfScopeSelection()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        viewModel.SelectRegularPad(1);
        Assert.Equal(2, viewModel.SimulationOverlayItems.Count);

        viewModel.SelectedAreaOption = viewModel.AreaOptions.Single(option => option.IcIndex == 0);

        Assert.Single(viewModel.SimulationOverlayItems);
        Assert.Equal(-1, viewModel.SelectedRegularPadId);
        Assert.Equal("IC 1", viewModel.AreaSummaryText);
    }

    [Fact]
    public void SelectedRegular_WhenManualBaselineApplied_ShowsNotchImpactBreakdown()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 100d,
        };

        viewModel.SelectRegularPad(0);

        Assert.True(viewModel.HasSelectedImpactItems);
        var impact = Assert.Single(viewModel.SelectedImpactItems);
        Assert.Equal("Anchor retain", impact.RoleText);
        Assert.Contains("keeps 50%", impact.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("-50", impact.DeltaText);
    }

    [Fact]
    public void SelectedRegular_WhenTouchedByNotchAction_ShowsStructuredFlowLegs()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 100d,
        };

        viewModel.SelectRegularPad(0);

        Assert.True(viewModel.HasSelectedFlowLegs);
        var leg = Assert.Single(viewModel.SelectedFlowLegs);
        Assert.Equal("Source retain", leg.RoleText);
        Assert.Contains("FW Diff 10", leg.SummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void RandomizeVisibleValues_WhenManualMode_FillsCurrentAreaOverrides()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        Assert.True(viewModel.CanRandomizeVisibleValues);
        Assert.False(viewModel.HasManualOverrides);

        viewModel.RandomizeVisibleValuesCommand.Execute(null);

        Assert.True(viewModel.HasManualOverrides);
        Assert.Contains("Randomized", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RandomizeVisibleValues_WhenDuplicateDiffExists_DoesNotCrash()
    {
        var session = BuildSessionWithDuplicateDiff();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        viewModel.RandomizeVisibleValuesCommand.Execute(null);

        Assert.True(viewModel.HasManualOverrides);
        Assert.Contains("Randomized", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, viewModel.SimulationOverlayItems.Count);
    }

    [Fact]
    public void CopperSource_WhenMoved_RegeneratesBeforeFrameThroughSharedSimulationPath()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);
        viewModel.CopperDiameter = 0.5d;
        viewModel.CopperPeakValue = 400d;
        viewModel.CopperBaselineValue = 0d;
        viewModel.MoveCopperToWorldPoint(new Point2(0.5d, 0.5d));

        // Full containment preserves the exact peak before firmware INT16 quantization.
        viewModel.SelectRegularPad(0);
        Assert.Equal("CAD overlap -> CAD Output FW Diff", viewModel.SelectedSourceText);
        Assert.Equal("400", viewModel.SelectedBeforeText);
        Assert.Equal("Source: Copper", viewModel.SourceModeSummaryText);
        Assert.True(viewModel.ShowSimulationDiagnostics);
        Assert.Contains("Physical audit", viewModel.SimulationPhysicalAuditSummaryText, StringComparison.Ordinal);

        viewModel.MoveCopperToWorldPoint(new Point2(1.5d, 0.5d));

        Assert.Equal("0", viewModel.SelectedBeforeText);
        viewModel.SelectRegularPad(1);
        Assert.Equal("400", viewModel.SelectedBeforeText);
    }

    [Fact]
    public void CopperContactModel_WhenFingerSelected_UsesLowerFullPadSignal()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);

        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);
        viewModel.SelectedCopperContactModelOption = viewModel.CopperContactModelOptions.Single(option => option.Value == SimulationContactModelMode.FingerPress);
        viewModel.CopperDiameter = 0.5d;
        viewModel.CopperBaselineValue = 0d;
        viewModel.MoveCopperToWorldPoint(new Point2(0.5d, 0.5d));

        viewModel.SelectRegularPad(0);

        // Full containment preserves the Finger press peak of 360 before firmware INT16 quantization.
        Assert.Equal("360", viewModel.SelectedBeforeText);
        Assert.Contains("Finger press", viewModel.CopperSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplayCopperPath_RecordsMaxAfterAndEmsViolationsThroughSharedSimulationPath()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            CopperDiameter = 0.5d,
            CopperPeakValue = 1000d,
            CopperBaselineValue = 0d,
        };

        var result = viewModel.ReplayCopperPath(
            new Point2(0.5d, 0.5d),
            new Point2(1.5d, 0.5d),
            stepCount: 2);

        Assert.Equal(2, result.Steps.Count);
        Assert.True(result.HasEmsViolations);
        Assert.NotNull(result.WorstStep);
        Assert.True(result.WorstStep!.WorstRegularPadId.HasValue);
        Assert.Equal(2, viewModel.CopperPathReplayRows.Count);
        Assert.Contains("Replay rows 2", viewModel.CopperPathReplayArtifactSummaryText, StringComparison.Ordinal);
        Assert.Contains("Copper path replay: 2 point", viewModel.CopperPathReplaySummaryText, StringComparison.Ordinal);
        Assert.Contains(
            $"EMS risk {result.TotalEmsViolationCount}",
            viewModel.CopperPathReplaySummaryText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayCopperPath_WhenPhysicalAuditWarningExists_AggregateUsesSameStatus()
    {
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession())
        {
            CopperDiameter = 0.5d,
            CopperPeakValue = 400d,
            CopperBaselineValue = 0d,
        };

        var result = viewModel.ReplayCopperPath(
            new Point2(0.5d, 0.5d),
            new Point2(1.5d, 0.5d),
            stepCount: 2);

        Assert.Equal(200d, result.Steps[0].MaxAfterValue);
        Assert.Equal(400d, result.Steps[1].MaxAfterValue);
        Assert.True(result.Steps[0].IsSupported);
        Assert.True(result.Steps[0].HasGlobalFlowResidual);
        Assert.True(result.Steps[0].HasPhysicalAuditRisks);
        Assert.Equal((0, 0, 0), (
            result.Steps[0].EmsViolationCount,
            result.Steps[0].NetFlowResidualCount,
            result.Steps[0].TargetCoverageRiskCount));
        Assert.False(result.HasUnsupportedSteps);
        Assert.True(result.HasPhysicalAuditRisks);
        Assert.Equal("audit warning", result.Steps[0].StatusText);
        Assert.Equal("audit warning", viewModel.CopperPathReplayRows[0].StatusText);
        Assert.True(viewModel.CopperPathReplayRows[0].HasPhysicalAuditRisks);
        Assert.Equal(
            "Copper path replay: 2 point(s) · Max After 400 at step 1 · audit warning.",
            viewModel.CopperPathReplaySummaryText);
    }

    [Fact]
    public async Task CopperPathReplayCommands_CopyAndExportArtifactRows()
    {
        var session = BuildSession();
        var copied = string.Empty;
        var exports = new List<SimulationWorkspaceViewModel.SimulationTextExportRequest>();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            RequestSetClipboardTextAsync = text =>
            {
                copied = text;
                return Task.FromResult(true);
            },
            RequestSaveTextFileAsync = request =>
            {
                exports.Add(request);
                return Task.FromResult(true);
            },
        };
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);
        viewModel.CopperDiameter = 0.5d;
        viewModel.CopperPeakValue = 1000d;
        viewModel.CopperPathStartX = 0.5m;
        viewModel.CopperPathStartY = 0.5m;
        viewModel.CopperPathEndX = 1.5m;
        viewModel.CopperPathEndY = 0.5m;
        viewModel.CopperPathStepCount = 2m;

        viewModel.ReplayCopperPathCommand.Execute(null);
        await viewModel.CopyCopperPathReplayCommand.ExecuteAsync(null);
        await viewModel.ExportCopperPathReplayCsvCommand.ExecuteAsync(null);
        await viewModel.ExportCopperPathReplayJsonCommand.ExecuteAsync(null);

        Assert.Contains("Step\tCenter\tMax After", copied, StringComparison.Ordinal);
        Assert.Equal(2, exports.Count);
        Assert.Equal("csv", exports[0].DefaultExtension);
        Assert.Contains("step_index,center_x", exports[0].Content, StringComparison.Ordinal);
        Assert.Equal("json", exports[1].DefaultExtension);
        Assert.Contains("\"stepCount\": 2", exports[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffViolationSummary_WhenNoMismatch_ShowsCleanState()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 100d,
        };

        Assert.False(viewModel.HasDiffViolationPads);
        Assert.Equal(0, viewModel.DiffViolationPadCount);
        Assert.Contains("No mismatch pads", viewModel.DiffViolationSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("audit warning", viewModel.SimulationSafetyStatusText);
    }

    [Fact]
    public void DiffViolationSummary_WhenDuplicateDiffValuesDiffer_ReportsDuplicateResolutionWithoutMismatch()
    {
        var session = BuildSessionWithDuplicateDiffDeltaMismatch();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 100d,
        };

        viewModel.ApplyRegularOverride(1, 20d);

        // Pads sharing one FW diff read the same firmware baseline, so their deltas cannot diverge.
        Assert.False(viewModel.HasDiffViolationPads);
        Assert.Equal(0, viewModel.DiffViolationPadCount);
        Assert.True(viewModel.HasDuplicateDiffResolutions);
        Assert.Equal(1, viewModel.DuplicateDiffResolutionCount);
    }

    [Fact]
    public void SimulationSafetySummary_WhenAfterExceedsEmsCap_ReportsRisk()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 1000d,
        };

        Assert.True(viewModel.HasSimulationSafetyViolations);
        Assert.Equal("EMS risk", viewModel.SimulationSafetyStatusText);
        Assert.Equal("EMS cap: After <= 480", viewModel.SimulationEmsCapText);
        Assert.Contains("EMS risk", viewModel.SimulationSafetySummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Max After", viewModel.SimulationMaxAfterText, StringComparison.Ordinal);
        Assert.Contains("Physical audit", viewModel.SimulationPhysicalAuditSummaryText, StringComparison.Ordinal);
        Assert.NotEmpty(viewModel.SimulationHighRiskDiffs);
        Assert.Contains(viewModel.SimulationHighRiskDiffs, item => item.IsViolation && item.StatusText == "EMS risk");
        viewModel.GlobalValue = 450d;
        Assert.Contains(viewModel.SimulationHighRiskDiffs, item => !item.IsViolation && item.StatusText == "Near cap");
    }

    [Theory]
    [InlineData(-1, "0")]
    [InlineData(0, "0")]
    [InlineData(7, "7")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(237, "99+")]
    public void FormatCountBadge_CapsLargeCountsAt99Plus(int count, string expected)
    {
        Assert.Equal(expected, SimulationWorkspaceViewModel.FormatCountBadge(count));
    }

    [Fact]
    public void SelectSimulationRiskDiffCommand_FocusesRiskPadAndMatchingIcArea()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 1000d,
        };
        var risk = viewModel.SimulationHighRiskDiffs.First(item => item.IcIndex == 1);

        viewModel.SelectSimulationRiskDiffCommand.Execute(risk);

        Assert.Equal(risk.RegularPadId, viewModel.SelectedRegularPadId);
        Assert.Equal(risk.IcIndex, viewModel.SelectedAreaOption.IcIndex);
        Assert.Contains("Focused", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void ColorLegend_WhenCanvasModeChanges_UsesSharedSnapshotRange()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 100d,
        };

        Assert.Equal("AUTO color scale", viewModel.ColorLegendTitleText);
        Assert.False(viewModel.ShowLegendCenterLabel);

        viewModel.SelectedCanvasViewOption = viewModel.CanvasViewOptions.Single(option => option.Value == NotchApplySimulationCanvasViewMode.Delta);

        Assert.True(viewModel.ShowLegendCenterLabel);
        Assert.Equal("0", viewModel.ColorLegendCenterText);
        Assert.StartsWith("-", viewModel.ColorLegendStartText, StringComparison.Ordinal);
        Assert.StartsWith("+", viewModel.ColorLegendEndText, StringComparison.Ordinal);
        Assert.Contains("green", viewModel.ColorLegendMeaningText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ColorLegend_WhenBeforeModeContainsNegativeValues_UsesSignedAutoScale()
    {
        var session = BuildSession();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 0d,
        };

        viewModel.SelectRegularPad(1);
        viewModel.SelectedManualValue = -15d;
        viewModel.ApplySelectedOverrideCommand.Execute(null);

        Assert.True(viewModel.ShowLegendCenterLabel);
        Assert.Equal("0", viewModel.ColorLegendCenterText);
        Assert.Contains("percentile-clamped", viewModel.ColorLegendMeaningText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("green", viewModel.ColorLegendMeaningText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlaybackCommands_WhenCsvLoaded_AdvanceAndClampFrames()
    {
        var session = BuildSession();
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            timestamp(mm:ss:nnn):"09:14:265",Frame index:"1",Break point index:"0",DiffData:
            200,80
            """);
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(new[] { csvPath }),
        };

        await viewModel.ImportCsvAsync();
        viewModel.IsPlaybackLoopEnabled = false;

        Assert.True(viewModel.CanStepFrames);
        Assert.Equal("Frame 1/2", viewModel.FrameSummaryText);

        viewModel.NextFrameCommand.Execute(null);
        Assert.Equal(1, viewModel.SelectedFrameSliderIndex);

        viewModel.NextFrameCommand.Execute(null);
        Assert.Equal(1, viewModel.SelectedFrameSliderIndex);

        viewModel.PreviousFrameCommand.Execute(null);
        Assert.Equal(0, viewModel.SelectedFrameSliderIndex);
    }

    private static SimulationWorkspaceSession BuildSession()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(
            SimulationDiffIndices,
            icIndexSelector: static col => col);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, NullDiffValue, 0, 0),
                comment: "sample")
        });

        return new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadPadsForCopperProjection: new[]
            {
                TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d),
                TestGeometryFactory.CreateCadPad(101, "SENSOR", 1d, 0d, 2d, 1d),
            },
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int>
            {
                [100] = 10,
                [101] = 11,
            },
            CadIcIndexByCadId: new Dictionary<int, int>
            {
                [100] = 0,
                [101] = 1,
            });
    }

    private static SimulationWorkspaceSession BuildSessionWithDuplicateDiff()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(DuplicateDiffIndices);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 100, NullDiffValue, 0, NullDiffValue, 0, 0),
                comment: "duplicate-diff-snapshot")
        });

        return new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
    }

    private static SimulationWorkspaceSession BuildSessionWithDuplicateDiffDeltaMismatch()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(DuplicateDiffIndices);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 50, NullDiffValue, 0, NullDiffValue, 0, 0),
                comment: "duplicate-diff-delta-mismatch")
        });

        return new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
    }
}
