// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

// UI thread only. Publication forwards notifications through the root, never this owner.
internal sealed class IndexMappingOwner
{
    private readonly IndexAssignmentInputs _inputs;
    private readonly IndexMappingPorts _ports;
    private readonly IndexMappingEffects _effects;
    private readonly IndexMappingNotifications _notifications;
    private MappingDiagnosticsResult _mappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun();

    public IndexMappingOwner(IndexAssignmentInputs inputs, IndexMappingPorts ports,
        IndexMappingEffects effects, IndexMappingNotifications notifications, IndexAssignmentOwner assignment)
    {
        _inputs = inputs;
        _ports = ports;
        _effects = effects;
        _notifications = notifications;
        Assignment = assignment;
    }

    public IndexAssignmentOwner Assignment { get; }

    public MappingDiagnosticsResult MappingDiagnosticsResult
    {
        get => _mappingDiagnosticsResult;
        set
        {
            if (EqualityComparer<MappingDiagnosticsResult>.Default.Equals(_mappingDiagnosticsResult, value)) return;
            var oldValue = _mappingDiagnosticsResult;
            _notifications.Changing(nameof(MappingDiagnosticsResult));
            _mappingDiagnosticsResult = value;
            ArgumentNullException.ThrowIfNull(oldValue);
            RaiseWhenChanged(oldValue.Summary, value.Summary, nameof(DxfRegularMappingSummary));
            RaiseWhenChanged(oldValue.Issues, value.Issues, nameof(DxfRegularMappingIssues));
            RaiseWhenChanged(oldValue.HasIssues, value.HasIssues, nameof(HasDxfRegularMappingIssues));
            _notifications.Changed(nameof(MappingDiagnosticsResult));
        }
    }

    public string DxfRegularMappingSummary => MappingDiagnosticsResult.Summary;
    public ObservableCollection<string> DxfRegularMappingIssues => MappingDiagnosticsResult.Issues;
    public bool HasDxfRegularMappingIssues => MappingDiagnosticsResult.HasIssues;

    private void RaiseWhenChanged<T>(T oldValue, T newValue, string propertyName)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue)) _notifications.Changed(propertyName);
    }

    public async Task AnalyzeIndexMappingAsync()
    {
        var input = _ports.PrepareInputs(() =>
        {
            MappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun() with
            {
                Summary = "Diagnostics: no CAD output pads.",
            };
            _effects.Status(DxfRegularMappingSummary);
        });
        if (input is not { } context) return;

        await _ports.RunAsync(
            () =>
            {
                _ports.ApplyUiSettings();
                _effects.InvalidateDownstream();
                MappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun() with
                {
                    Summary = "Diagnostics: running...",
                };
                _effects.Status("Running mapping diagnostics...");
                _effects.Info("Analyzing DXF/Regular mapping (cadVisible={0}, regular={1}).", context.Cad.Pads.Count, context.Grid.Pads.Count);
            },
            AnalyzeMappingAsync);

        async Task AnalyzeMappingAsync(Action<double> reportProgress)
        {
            var result = await _ports.AnalyzeAsync(context.Cad, context.Grid, reportProgress);
            var report = result.Report;
            ApplyMappingDiagnosticsReport(report);
            if (_ports.CanOpenReport() && (report.TotalIssueCount > 0 || (report.MaskAuditRows?.Count ?? 0) > 0))
            {
                await _ports.OpenReportAsync(report);
            }
            LogMappingDiagnosticsReport(report);
            _ports.CompleteWorkflow();
        }
    }

    private void ApplyMappingDiagnosticsReport(DxfRegularMappingReport report)
    {
        var issueMessages = report.SampleIssues.Select(i => $"[{i.Kind}] {i.Message}").ToList();

        MappingDiagnosticsResult = new MappingDiagnosticsResult(
            report.HasIssues
                ? $"Diagnostics warnings: {report.Summary}"
                : $"Diagnostics OK: {report.Summary}",
            new ObservableCollection<string>(issueMessages),
            HasIssues: (report.HasIssues && report.SampleIssues.Count > 0) ||
                       report.MaskChangedCount > 0 ||
                       report.MaskRemovedCount > 0);

        _effects.Status(BuildMappingStatusForWorkspaceHeader(report));
    }

    private void LogMappingDiagnosticsReport(DxfRegularMappingReport report)
    {
        if (report.HasIssues)
        {
            _effects.Warn("DXF/Regular mapping warnings: {0}", report.Summary);
            foreach (var issue in report.SampleIssues.Take(10))
            {
                _effects.Warn("Mapping issue: {0}", issue.Message);
            }
        }
        else
        {
            _effects.Info("DXF/Regular mapping OK: {0}", report.Summary);
        }
    }

    public bool LocateMappingTargetFromReport(int? cadPadId, int? regularPadIndex)
    {
        var cadIds = new List<int>(1);
        var regularIndices = new List<int>(1);

        if (cadPadId is int cadId && _inputs.CadPads().Any(p => p.Id == cadId))
        {
            cadIds.Add(cadId);
        }

        if (regularPadIndex is int regularIndex && _inputs.RegularPads().Any(p => p.RegularPadId == regularIndex))
        {
            regularIndices.Add(regularIndex);
        }

        if (cadIds.Count == 0 && regularIndices.Count == 0)
        {
            return false;
        }

        return _effects.Locate(
            cadIds,
            regularIndices,
            focusSelection: true,
            notVisibleStatus: "Mapping issue locate: target not visible.",
            successStatusBuilder: (visibleCadCount, visibleRegularCount) =>
                $"Mapping diagnostics focused: CAD={visibleCadCount}, Regular={visibleRegularCount}.");
    }

    public bool ApplyMappingOverrideFromReport(int cadId, int regularIndex)
    {
        if (_inputs.RegularPads().All(p => p.RegularPadId != regularIndex))
        {
            _effects.Status("Mapping override: target regular pad not in current grid.");
            return false;
        }

        _inputs.Project().DxfRegularMappingOverrides[cadId] = regularIndex;
        _effects.InvalidateDownstream();
        _effects.MarkUnsaved();
        _effects.Status($"Mapping override saved: CAD {cadId} -> regular id {regularIndex}.");
        _effects.Info("Mapping override saved: CAD {0} -> regular id {1}.", cadId, regularIndex);
        return true;
    }

    public bool ClearMappingOverrideFromReport(int cadId)
    {
        if (!_inputs.Project().DxfRegularMappingOverrides.Remove(cadId))
        {
            _effects.Status($"Mapping override clear: CAD {cadId} has no saved override.");
            return false;
        }

        _effects.InvalidateDownstream();
        _effects.MarkUnsaved();
        _effects.Status($"Mapping override cleared: CAD {cadId}.");
        _effects.Info("Mapping override cleared for CAD {0}.", cadId);
        return true;
    }

    public bool ApplyCadOutputFwDiffOverrideFromReport(int cadId, int diffIndex)
    {
        if (_inputs.CadPads().All(pad => pad.Id != cadId))
        {
            _effects.Status($"Diff repair override ignored: CAD {cadId} is not visible.");
            return false;
        }

        Assignment.SetCadOutputFwDiffIndexOverride(cadId, diffIndex);
        _effects.Info("Diff repair override applied: CAD {0} -> diff {1}.", cadId, diffIndex);
        return true;
    }

    public int? GetMappingOverrideRegularIndex(int cadPadId)
    {
        return _inputs.Project().DxfRegularMappingOverrides.TryGetValue(cadPadId, out var regularIndex)
            ? regularIndex
            : null;
    }

    private static string BuildMappingStatusForWorkspaceHeader(DxfRegularMappingReport report)
    {
        if (!report.HasIssues)
        {
            var okText = $"Diagnostics OK: map={report.MappedCount}, low={report.LowConfidenceCount}, amb={report.AmbiguousCount}, dup={report.DuplicateDiffCount}.";
            return ClampWorkspaceHeaderStatus(okText);
        }

        var warningText =
            $"Diagnostics warn: map={report.MappedCount}, unmapCad={report.UnmappedCadCount}, unmapReg={report.UnmappedRegularCount}, " +
            $"low={report.LowConfidenceCount}, amb={report.AmbiguousCount}, dup={report.DuplicateDiffCount}, mask={report.MaskChangedCount}/{report.MaskRemovedCount}.";
        return ClampWorkspaceHeaderStatus(warningText);
    }

    private static string ClampWorkspaceHeaderStatus(string statusText)
    {
        const int maxChars = 100;
        if (statusText.Length <= maxChars)
        {
            return statusText;
        }

        return string.Concat(statusText.AsSpan(0, maxChars - 1), "…");
    }

}
