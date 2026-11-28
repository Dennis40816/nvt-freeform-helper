using System.Collections.Immutable;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Presentation;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

public sealed class NotchDetailUseCase
{
    /// <summary>Explains the rows export writes for one CAD, from one uncached generation with the export inputs.</summary>
    public static NotchDetailExplanation BuildExplanation(CadPad selectedCad, CadPadSet cad, RegularGrid grid,
        ProjectSettings settings, IReadOnlySet<int>? activeRegularPadIds,
        IReadOnlyDictionary<int, int> cadOutputFwDiffByCadId, bool firmwareOutput = true)
    {
        if (settings.Notch.ComputationMode != NotchComputationMode.CadAllocation ||
            !settings.Notch.EnabledVersions.Contains(NotchAlgorithmVersion.V22))
        {
            return NotchDetailExplanation.WithoutRows("Row explanations require CadAllocation with V2.2 enabled.");
        }

        var service = new NotchExportService();
        var collector = new NotchV22ExplanationCollector(selectedCad.Id);
        var table = service.Generate(cad, grid, settings, activeRegularPadIds: activeRegularPadIds,
            cadOutputFwDiffIndexByCadId: cadOutputFwDiffByCadId, explanationCollector: collector);
        var projected = NotchCadOutputFwDiffProjectionService.Project(grid, table, cadOutputFwDiffByCadId);
        var selectedRows = NotchExportRowSelectionPolicy.Build(projected.Table.Rows.Select((row, ordinal) =>
            new NotchExportRowSelectionPolicy.RowChoice(ordinal, row, true)).ToArray(), NotchAlgorithmVersion.V22);
        IReadOnlyList<NotchTableRow> exportRows = firmwareOutput
            ? NotchV22ExportRows.Project(selectedRows.Rows, settings.Notch.NullValue, settings.Notch.ExportProfile)
            : selectedRows.Rows;
        var evidence = collector.Explanations;
        var won = evidence.Where(explanation => explanation.SelectedCadPadId == selectedCad.Id).ToImmutableArray();
        var sources = won.Select(explanation => NotchRowExplanationProjector.Build(explanation, exportRows,
                settings.Notch.CompensationModel, settings.Notch.EnabledVersions.Contains(NotchAlgorithmVersion.V21)))
            .Where(static source => source.Rows.Length > 0).ToImmutableArray();
        var cellIds = won.SelectMany(static explanation => explanation.Regulars)
            .Select(static regular => regular.RegularPadId).ToHashSet();
        var cells = grid.Pads.Where(pad => cellIds.Contains(pad.RegularPadId)).ToImmutableArray();
        return new NotchDetailExplanation(sources,
            sources.Length > 0 ? string.Empty : NoRowsReason(selectedCad, cad, grid, settings, evidence, service),
            cells,
            cad.Pads.Where(pad => pad.Id != selectedCad.Id && cells.Any(regular => regular.Bounds.Intersects(pad.Bounds)))
                .ToImmutableArray());
    }

    private static string NoRowsReason(CadPad selectedCad, CadPadSet cad, RegularGrid grid, ProjectSettings settings,
        ImmutableArray<NotchV22RowExplanation> explanations, NotchExportService service)
    {
        if (!cad.Pads.Any(pad => pad.Id == selectedCad.Id))
        {
            return "This CAD is outside the export CAD set.";
        }

        var lost = explanations.Where(explanation => explanation.SelectedCadPadId != selectedCad.Id &&
            explanation.Candidates.Any(candidate => candidate.CadPadId == selectedCad.Id)).ToArray();
        if (lost.Length > 0)
        {
            return string.Join(" ", lost.Select(static explanation =>
                $"CAD {explanation.SelectedCadPadId} wins IC {explanation.IcIndex + 1} diff {explanation.SourceDiffIndex}: " +
                NotchV22CandidateSelection.DecidingKey(explanation.Candidates) + "."));
        }

        if (explanations.Any(explanation => explanation.SelectedCadPadId == selectedCad.Id))
        {
            return "No V2.2 output row: the export profile omits this no-op row.";
        }

        var v22Settings = NotchComputationSettingsSnapshotFactory.Create(settings);
        v22Settings.Notch.EnabledVersions.Clear();
        v22Settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V22);
        var eligibility = service.EvaluateCadRowEligibility(selectedCad, grid, v22Settings);
        return "No V2.2 output row. " + (eligibility.EligibleVersions.Contains(NotchAlgorithmVersion.V22)
            ? "No freeform anchor or effective To Full influence on an active regular."
            : eligibility.Reason);
    }

    public static RegularGrid SnapshotGrid(RegularGrid grid) => new(grid.Rows, grid.Cols,
        grid.XEdges.ToArray(), grid.YEdges.ToArray(), grid.Pads.Select(static source =>
            new RegularPad(source.Row, source.Col, source.RegularPadId, source.Polygon)
            {
                IcIndex = source.IcIndex,
                DiffIndex = source.DiffIndex,
                MatchedCadPadId = source.MatchedCadPadId,
                MatchScore = source.MatchScore,
                Freeform = source.Freeform,
            }).ToArray());

    public static NotchV22CompensationResult BuildCompensation(
        CadPad cadPad,
        RegularGrid grid,
        ProjectSettings settings,
        IReadOnlyList<CadPad>? allCadPads = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        bool? enableToRegularOverride = null,
        bool? enableToFullOverride = null,
        double? strictOverlapRatioOverride = null,
        bool? enableToFullRuleEngineOverride = null,
        bool? enableToFullRuleTraceOverride = null)
    {
        var strictOverlapRatio = strictOverlapRatioOverride
            ?? Math.Clamp(settings.Notch.MultiOwnerStrictOverlapPercent / 100.0, 0.0, 1.0);
        var (resolvedEnableToRegular, resolvedEnableToFull) = settings.Notch.ResolveStep3Switches();
        var context = NotchV22CompensationService.CreateContext(
            cadPad,
            grid,
            enableToRegularOverride ?? resolvedEnableToRegular,
            enableToFullOverride ?? resolvedEnableToFull,
            allCadPads,
            activeRegularPadIds,
            strictOverlapRatio,
            enableToFullRuleEngineOverride ?? settings.Notch.EnableToFullRuleEngine,
            enableToFullRuleTraceOverride ?? settings.Notch.EnableToFullRuleTrace,
            settings.Notch.EnableBoundaryVirtualAreaCap,
            settings.Notch.BoundaryVirtualAreaCapRatio);
        var compensation = NotchV22CompensationService.Compute(context);
        var effectiveCombinedRatio = settings.Notch.ResolveCombinedRatio(
            compensation.ToRegularRatio,
            compensation.ToFullRatio);
        return Math.Abs(effectiveCombinedRatio - compensation.CombinedRatio) <= 1e-12
            ? compensation
            : compensation with { CombinedRatio = effectiveCombinedRatio };
    }

    public static NotchV22ResolvedResult BuildResolvedResult(
        CadPad cadPad,
        NotchV22CompensationResult compensation,
        double strictOverlapRatio,
        int? anchorIcIndex = null,
        int? anchorDiffIndex = null,
        NotchV22TargetAllocationAreaMode allocationAreaMode = NotchV22TargetAllocationAreaMode.SourceAreaDominant)
    {
        return NotchV22ResolvedResultService.Build(
            cadPad,
            compensation,
            strictOverlapRatio,
            anchorIcIndex,
            anchorDiffIndex,
            allocationAreaMode);
    }

    internal static NotchV22ResolvedResult BuildResolvedResult(NotchResolvedResultRequest request)
    {
        var compensation = request.PrecomputedCompensation ?? BuildCompensation(
            request.CadPad,
            request.Grid,
            request.Settings,
            request.AllCadPads,
            request.ActiveRegularPadIds,
            request.EnableToRegular,
            request.EnableToFull,
            request.StrictOverlapRatio,
            request.EnableToFullRuleEngine,
            request.EnableToFullRuleTrace);
        return NotchV22ResolvedResultService.Build(
            request.CadPad,
            compensation,
            request.StrictOverlapRatio,
            request.AnchorIcIndex,
            request.AnchorDiffIndex,
            request.AllocationAreaMode,
            CreateIdentity(request));
    }

    internal static bool MatchesRequest(NotchV22ResolvedResult result, NotchResolvedResultRequest request) =>
        result.Identity == CreateIdentity(request);

    private static NotchV22ResolvedResultIdentity CreateIdentity(NotchResolvedResultRequest request) =>
        NotchV22ResolvedResultService.CreateIdentity(
            request.CadPad,
            request.Grid,
            request.AllCadPads,
            request.ActiveRegularPadIds,
            request.CompensationModel,
            request.EnableToRegular,
            request.EnableToFull,
            request.EnableToFullRuleEngine,
            request.EnableToFullRuleTrace,
            request.Settings.Notch.EnableBoundaryVirtualAreaCap,
            request.Settings.Notch.BoundaryVirtualAreaCapRatio,
            request.StrictOverlapRatio,
            request.AnchorIcIndex,
            request.AnchorDiffIndex,
            request.AllocationAreaMode);

    public static NotchDetailViewModel Build(
        CadPad cadPad,
        int? dxfIndex,
        RegularGrid grid,
        ProjectSettings settings,
        IReadOnlyList<CadPad>? allCadPads = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        bool? enableToRegularOverride = null,
        bool? enableToFullOverride = null,
        bool? enableToFullRuleEngineOverride = null,
        bool? enableToFullRuleTraceOverride = null)
    {
        var compensation = BuildCompensation(
            cadPad,
            grid,
            settings,
            allCadPads,
            activeRegularPadIds,
            enableToRegularOverride,
            enableToFullOverride,
            enableToFullRuleEngineOverride: enableToFullRuleEngineOverride,
            enableToFullRuleTraceOverride: enableToFullRuleTraceOverride);

        var resolved = BuildResolvedResult(
            cadPad,
            compensation,
            Math.Clamp(settings.Notch.MultiOwnerStrictOverlapPercent / 100.0, 0.0, 1.0),
            anchorIcIndex: null,
            anchorDiffIndex: dxfIndex,
            allocationAreaMode: NotchV22TargetAllocationPolicy.ResolveAreaMode(settings.Notch.CompensationModel));

        return BuildFromResolvedResult(cadPad, dxfIndex, grid, settings, resolved);
    }

    internal static NotchDetailViewModel BuildFromResolvedResult(
        CadPad cadPad,
        int? dxfIndex,
        RegularGrid grid,
        ProjectSettings settings,
        NotchV22ResolvedResult resolved)
    {
        var allocations = NotchAllocationService.BuildAllocations(cadPad, grid);
        var freeform = NotchAllocationService.GetFreeformType(cadPad, grid);
        var compensation = resolved.Compensation;
        var items = allocations
            .Select(a => new NotchDetailAllocation(a.Pad.Index, a.Pad.Row, a.Pad.Col, a.Pad.IcIndex, a.Q7, a.Ratio))
            .ToList();

        var maxQ7 = items.Count == 0 ? 0 : items.Max(a => a.Q7);
        var thresholdQ7 = settings.Notch.ThresholdQ7;
        var thresholdSummary = thresholdQ7 > 0
            ? $"TH={thresholdQ7}/128, Max={maxQ7}/128"
            : "TH=0 (disabled)";
        var display = NotchDisplayProjector.Build(
            compensation,
            resolved.TargetAllocation,
            computationMode: settings.Notch.ComputationMode);

        return new NotchDetailViewModel(
            cadPad,
            dxfIndex,
            freeform,
            items,
            allocations.Select(a => a.Pad).ToList(),
            thresholdQ7,
            maxQ7,
            thresholdSummary,
            compensation.CombinedRatio,
            display.ToRegularRatioText,
            display.ToFullRatioText,
            display.CombinedRatioText,
            compensation.ToFullPolygons);
    }
}

internal sealed record NotchResolvedResultRequest(
    CadPad CadPad, RegularGrid Grid, ProjectSettings Settings,
    IReadOnlyList<CadPad> AllCadPads,
    IReadOnlySet<int>? ActiveRegularPadIds,
    NotchCompensationModel CompensationModel,
    bool EnableToRegular, bool EnableToFull,
    bool EnableToFullRuleEngine, bool EnableToFullRuleTrace,
    double StrictOverlapRatio, int? AnchorIcIndex, int? AnchorDiffIndex,
    NotchV22TargetAllocationAreaMode AllocationAreaMode,
    NotchV22CompensationResult? PrecomputedCompensation = null);
