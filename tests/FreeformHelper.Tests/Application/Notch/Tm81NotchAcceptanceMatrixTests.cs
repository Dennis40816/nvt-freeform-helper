using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class Tm81NotchAcceptanceMatrixTests
{
    private const double Stage3OverlapContactTolerance = 1e-4;

    private static readonly JsonSerializerOptions MatrixJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [ExampleDataFact]
    public async Task TM81_MatchesAcceptanceMatrixSnapshot()
    {
        var snapshotPath = GetSnapshotPath();
        var actual = await BuildActualMatrixAsync();
        var actualJson = JsonSerializer.Serialize(actual, MatrixJsonOptions);

        if (string.Equals(
                Environment.GetEnvironmentVariable("FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX"),
                "1",
                StringComparison.Ordinal))
        {
            File.WriteAllText(snapshotPath, actualJson);
            return;
        }

        Assert.True(File.Exists(snapshotPath), "The acceptance matrix snapshot file is missing.");
        var expected = JsonSerializer.Deserialize<Tm81NotchAcceptanceMatrixDocument>(
            File.ReadAllText(snapshotPath),
            MatrixJsonOptions);
        Assert.NotNull(expected);
        var expectedJson = JsonSerializer.Serialize(expected, MatrixJsonOptions);

        if (!string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
        {
            var actualDumpPath = Path.Combine(
                TestPaths.RepoRoot,
                "build",
                "tm81-notch-acceptance-matrix.actual.json");
            File.WriteAllText(actualDumpPath, actualJson);
        }

        GoldenAssert.TextEqual(expectedJson, actualJson, "TM8.1 acceptance matrix");
    }

    private static async Task<Tm81NotchAcceptanceMatrixDocument> BuildActualMatrixAsync()
    {
        var vm = new FreeformHelperViewModel();
        var repoRoot = TestPaths.RepoRoot;
        var projectPath = Path.Combine(repoRoot, "example", "TM 8.1", "TM8.1.json");
        var maskPath = Path.Combine(repoRoot, "example", "TM 8.1", "SeeRegular.csv");

        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(maskPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var (cad, grid, project) = ExtractRuntimeState(vm);
        project.Settings.Notch.EnabledVersions.Clear();
        project.Settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V21);
        project.Settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V22);

        var table = new NotchTableGenerator().Generate(cad, grid, project.Settings);
        var workflowTable = await NotchCurrentWorkflowTableTestHelper.GenerateRawTableAsync(
            vm,
            "TM81 acceptance matrix");
        var exportVm = new NotchExportSelectionViewModel(table);
        var orderedRows = table.Rows
            .OrderBy(static row => row.IcIndex)
            .ThenBy(static row => row.DiffIndex)
            .ThenBy(static row => (int)row.Version)
            .ToList();

        var v22Rows = orderedRows.Where(static row => row.Version == NotchAlgorithmVersion.V22).ToList();
        var v22NoOpRows = v22Rows.Where(IsNoOpV22Row).ToList();
        var warningRows = v22NoOpRows.Where(static row => row.CadPadId.HasValue).ToList();

        var caseContracts = BuildCaseContracts(vm);
        var diffRepairSummary = await BuildDiffRepairSummaryAsync(vm);

        return new Tm81NotchAcceptanceMatrixDocument
        {
            ProjectRelativePath = "example/TM 8.1/TM8.1.json",
            RowCount = orderedRows.Count,
            VersionCounts = BuildVersionCounts(orderedRows),
            ToFullCoverage = NotchToFullCoverageSnapshotBuilder.Build(workflowTable.ToFullCoverageAudit),
            Distribution = new Tm81NotchDistribution
            {
                Transfer = exportVm.TransferRowCount,
                Warning = exportVm.WarningRowCount,
                NoCad = exportVm.CadMissingRowCount,
                Legacy = exportVm.LegacyRowCount,
                Linked = exportVm.CadLinkedRowCount,
            },
            V22NoOp = new Tm81V22NoOpSummary
            {
                V22RowCount = v22Rows.Count,
                NoOpRowCount = v22NoOpRows.Count,
                NoOpRatio = v22Rows.Count == 0 ? 0.0 : (double)v22NoOpRows.Count / v22Rows.Count,
            },
            WarningTypeCounts = BuildWarningTypeCounts(warningRows),
            WarningCommentCounts = BuildWarningCommentCounts(warningRows),
            DiffRepairSummary = diffRepairSummary,
            CaseContracts = caseContracts,
        };
    }

    private static async Task<Tm81DiffRepairSummary> BuildDiffRepairSummaryAsync(FreeformHelperViewModel vm)
    {
        IndexMappingReportViewModel? openedReport = null;
        vm.OpenIndexMappingReportAsync = reportVm =>
        {
            openedReport = reportVm;
            return Task.CompletedTask;
        };

        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.NotNull(openedReport);

        var rows = openedReport!.MaskAuditRows.ToList();
        var repairRows = rows
            .Where(static row => row.RepairSuggestionDiffIndex.HasValue)
            .OrderBy(static row => row.CadPadId)
            .ToList();
        var passiveRows = rows
            .Where(static row => row.PassiveCompensationDiffIndex.HasValue)
            .OrderBy(static row => row.CadPadId)
            .ToList();
        var segmentOffsetRows = rows
            .Where(static row => row.DetectedOffset.HasValue)
            .OrderBy(static row => row.CadPadId)
            .ToList();

        return new Tm81DiffRepairSummary
        {
            MaskAuditRowCount = rows.Count,
            RepairSuggestionRowCount = repairRows.Count,
            PassiveCompensationRowCount = passiveRows.Count,
            SegmentOffsetDetectedRowCount = segmentOffsetRows.Count,
            RepairReasonCodeCounts = BuildCounts(repairRows.Select(static row => row.ReasonCodeText)),
            RepairModeCounts = BuildCounts(repairRows.Select(static row => row.ModeText)),
            SampleRepairRows = repairRows
                .Take(16)
                .Select(row => new Tm81DiffRepairSample
                {
                    CadPadId = row.CadPadId,
                    CurrentAssignedDiffIndex = row.CurrentAssignedDiffIndex,
                    SuggestedDiffIndex = row.RepairSuggestionDiffIndex,
                    PassiveCompensationDiffIndex = row.PassiveCompensationDiffIndex,
                    Mode = row.ModeText,
                    ReasonCode = row.ReasonCodeText,
                    DecisionSource = row.DecisionSourceText,
                    Segment = row.SegmentKeyText,
                })
                .ToList(),
        };
    }

    private static Tm81CaseContracts BuildCaseContracts(FreeformHelperViewModel vm)
    {
        var cad113Compensation = vm.GetCadV22CompensationResult(113);
        var cad113Overlay = vm.GetCadV22StageOverlays(113);
        Assert.NotNull(cad113Compensation);
        Assert.NotNull(cad113Overlay);
        var cad113MainInfo = Assert.Single(cad113Compensation!.RegularDebugInfos, static info => info.RegularIndex == 445);

        var cad364Compensation = vm.GetCadV22CompensationResult(364);
        var cad364Overlay = vm.GetCadV22StageOverlays(364);
        Assert.NotNull(cad364Compensation);
        Assert.NotNull(cad364Overlay);
        var reg291 = Assert.Single(vm.RegularPads, static pad => pad.Index == 291);
        var cad364Reg291Info = Assert.Single(cad364Compensation!.RegularDebugInfos, static info => info.RegularIndex == 291);
        var cad364Stage2Coverage = ComputeRectCoverage(cad364Overlay!.Value.Stage2Candidate, reg291.Bounds);
        var cad364Stage3Coverage = ComputeRectCoverage(cad364Overlay.Value.Stage3Final, reg291.Bounds);

        var cad402Compensation = vm.GetCadV22CompensationResult(402);
        Assert.NotNull(cad402Compensation);
        var reg624Info = Assert.Single(cad402Compensation!.RegularDebugInfos, static info => info.RegularIndex == 624);

        var cad490Compensation = vm.GetCadV22CompensationResult(490);
        var cad491Compensation = vm.GetCadV22CompensationResult(491);
        var cad490Overlay = vm.GetCadV22StageOverlays(490);
        var cad491Overlay = vm.GetCadV22StageOverlays(491);
        Assert.NotNull(cad490Compensation);
        Assert.NotNull(cad491Compensation);
        Assert.NotNull(cad490Overlay);
        Assert.NotNull(cad491Overlay);
        var cad490SharedInfo = Assert.Single(cad490Compensation!.RegularDebugInfos, static info => info.RegularIndex == 643);
        var cad491SharedInfo = Assert.Single(cad491Compensation!.RegularDebugInfos, static info => info.RegularIndex == 643);
        var stage3Overlap = ComputeRectangleIntersectionArea(cad490Overlay!.Value.Stage3Final, cad491Overlay!.Value.Stage3Final);

        var reg387Snapshot = vm.BuildRegularPadInspectorSnapshot(387);
        var reg388Snapshot = vm.BuildRegularPadInspectorSnapshot(388);
        var reg389Snapshot = vm.BuildRegularPadInspectorSnapshot(389);
        var reg387 = Assert.IsType<RegularPadInspectorSnapshot>(reg387Snapshot?.Regular);
        var reg388 = Assert.IsType<RegularPadInspectorSnapshot>(reg388Snapshot?.Regular);
        var reg389 = Assert.IsType<RegularPadInspectorSnapshot>(reg389Snapshot?.Regular);

        return new Tm81CaseContracts
        {
            Cad113 = new Tm81Cad113Case
            {
                IsToFullEnabled = cad113Overlay!.Value.IsToFullEnabled,
                ToFullRatio = cad113Compensation.ToFullRatio,
                RuleCode = cad113MainInfo.ToFullRuleCode,
                Stage1Count = cad113Overlay.Value.Stage1Seed.Count,
                Stage2Count = cad113Overlay.Value.Stage2Candidate.Count,
                Stage3Count = cad113Overlay.Value.Stage3Final.Count,
            },
            Cad364 = new Tm81Cad364Case
            {
                RuleCodeReg291 = cad364Reg291Info.ToFullRuleCode,
                IsBoundaryReg291 = cad364Reg291Info.IsBoundaryRegular,
                Stage2CoverageReg291 = cad364Stage2Coverage,
                Stage3CoverageReg291 = cad364Stage3Coverage,
                Reg291Area = reg291.Area,
            },
            Cad402 = new Tm81Cad402Case
            {
                Reg624OwnerCount = reg624Info.OwnerCadPadCount,
                Reg624OwnerCadPadIds = reg624Info.OwnerCadPadIds.OrderBy(static id => id).ToArray(),
                Reg624MatchedCadPadIds = vm.GetMatchedCadPadIds(reg624Info.RegularPadId).OrderBy(static id => id).ToArray(),
            },
            Cad490Cad491 = new Tm81Cad490491Case
            {
                Cad490RuleCodeReg643 = cad490SharedInfo.ToFullRuleCode,
                Cad491RuleCodeReg643 = cad491SharedInfo.ToFullRuleCode,
                Stage3OverlapArea = stage3Overlap,
            },
            FreeformTailLink = new Tm81FreeformTailLinkCase
            {
                Reg387Freeform = reg387.Freeform.ToString(),
                Reg387Source = reg387.FreeformSource,
                Reg388Freeform = reg388.Freeform.ToString(),
                Reg388Source = reg388.FreeformSource,
                Reg389Freeform = reg389.Freeform.ToString(),
                Reg389Source = reg389.FreeformSource,
            },
        };
    }

    private static (CadPadSet Cad, RegularGrid Grid, ProjectFile Project) ExtractRuntimeState(FreeformHelperViewModel vm)
    {
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        var projectField = typeof(FreeformHelperViewModel).GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);
        Assert.NotNull(projectField);

        var cad = Assert.IsType<CadPadSet>(cadField!.GetValue(vm));
        var grid = Assert.IsType<RegularGrid>(gridField!.GetValue(vm));
        var project = Assert.IsType<ProjectFile>(projectField!.GetValue(vm));
        return (cad, grid, project);
    }

    private static SortedDictionary<string, int> BuildVersionCounts(IEnumerable<NotchTableRow> rows)
    {
        return BuildCounts(rows.Select(static row => row.Version.ToString()));
    }

    private static SortedDictionary<string, int> BuildWarningTypeCounts(IEnumerable<NotchTableRow> warningRows)
    {
        return BuildCounts(warningRows.Select(static row => ExtractWarningType(row.Comment)));
    }

    private static SortedDictionary<string, int> BuildWarningCommentCounts(IEnumerable<NotchTableRow> warningRows)
    {
        return BuildCounts(warningRows.Select(static row => NormalizeWarningComment(row.Comment)));
    }

    private static SortedDictionary<string, int> BuildCounts(IEnumerable<string> keys)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in keys.Where(static key => !string.IsNullOrWhiteSpace(key)))
        {
            if (!result.TryGetValue(key, out var count))
            {
                count = 0;
            }

            result[key] = count + 1;
        }

        return result;
    }

    private static bool IsNoOpV22Row(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return false;
        }

        return node.CombinePercent == 100 &&
               node.TargetRatioPercent1 == 0 &&
               node.TargetRatioPercent2 == 0 &&
               node.TargetDiffIndex1 == node.TargetDiffIndex2 &&
               node.Flags == 0;
    }

    private static string ExtractWarningType(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return "NO_OP_UNSPECIFIED";
        }

        var upper = comment.ToUpperInvariant();
        var codeMatch = Regex.Match(upper, @"\b[A-Z0-9]+(?:_[A-Z0-9]+)+\b");
        if (codeMatch.Success)
        {
            return codeMatch.Value;
        }

        if (upper.Contains("NO-OP", StringComparison.Ordinal))
        {
            return "NO_OP_GENERIC";
        }

        return "NO_OP_OTHER";
    }

    private static string NormalizeWarningComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return "NO_COMMENT";
        }

        var normalized = comment.Trim().ToUpperInvariant();
        normalized = Regex.Replace(normalized, @"\d+(?:\.\d+)?", "#");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        const int maxLength = 120;
        if (normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return normalized;
    }

    private static double ComputeRectCoverage(IReadOnlyList<Polygon2> polygons, Rect2 rect)
    {
        var area = 0.0;
        foreach (var polygon in polygons)
        {
            area += Polygon2.IntersectionAreaWithRect(polygon, rect);
        }

        return area;
    }

    private static double ComputeRectangleIntersectionArea(
        IReadOnlyList<Polygon2> a,
        IReadOnlyList<Polygon2> b)
    {
        var area = 0.0;
        foreach (var left in a)
        {
            var leftBounds = left.Bounds;
            foreach (var right in b)
            {
                var rightBounds = right.Bounds;
                var overlapWidth = Math.Min(leftBounds.MaxX, rightBounds.MaxX) - Math.Max(leftBounds.MinX, rightBounds.MinX);
                var overlapHeight = Math.Min(leftBounds.MaxY, rightBounds.MaxY) - Math.Max(leftBounds.MinY, rightBounds.MinY);
                if (overlapWidth <= Stage3OverlapContactTolerance || overlapHeight <= Stage3OverlapContactTolerance)
                {
                    continue;
                }

                area += overlapWidth * overlapHeight;
            }
        }

        return area;
    }

    private static string GetSnapshotPath()
    {
        return Path.Combine(
            TestPaths.RepoRoot,
            "example",
            "golden-snapshots",
            "tm81-notch-acceptance-matrix.json");
    }

    private sealed class Tm81NotchAcceptanceMatrixDocument
    {
        public string ProjectRelativePath { get; set; } = string.Empty;

        public int RowCount { get; set; }

        public SortedDictionary<string, int> VersionCounts { get; set; } =
            new(StringComparer.Ordinal);

        public NotchToFullCoverageSnapshot ToFullCoverage { get; set; } = new();

        public Tm81NotchDistribution Distribution { get; set; } = new();

        public Tm81V22NoOpSummary V22NoOp { get; set; } = new();

        public SortedDictionary<string, int> WarningTypeCounts { get; set; } =
            new(StringComparer.Ordinal);

        public SortedDictionary<string, int> WarningCommentCounts { get; set; } =
            new(StringComparer.Ordinal);

        public Tm81DiffRepairSummary DiffRepairSummary { get; set; } = new();

        public Tm81CaseContracts CaseContracts { get; set; } = new();
    }

    private sealed class Tm81NotchDistribution
    {
        public int Transfer { get; set; }

        public int Warning { get; set; }

        public int NoCad { get; set; }

        public int Legacy { get; set; }

        public int Linked { get; set; }
    }

    private sealed class Tm81V22NoOpSummary
    {
        public int V22RowCount { get; set; }

        public int NoOpRowCount { get; set; }

        public double NoOpRatio { get; set; }
    }

    private sealed class Tm81DiffRepairSummary
    {
        public int MaskAuditRowCount { get; set; }

        public int RepairSuggestionRowCount { get; set; }

        public int PassiveCompensationRowCount { get; set; }

        public int SegmentOffsetDetectedRowCount { get; set; }

        public SortedDictionary<string, int> RepairReasonCodeCounts { get; set; } =
            new(StringComparer.Ordinal);

        public SortedDictionary<string, int> RepairModeCounts { get; set; } =
            new(StringComparer.Ordinal);

        public List<Tm81DiffRepairSample> SampleRepairRows { get; set; } = new();
    }

    private sealed class Tm81DiffRepairSample
    {
        public int CadPadId { get; set; }

        public int? CurrentAssignedDiffIndex { get; set; }

        public int? SuggestedDiffIndex { get; set; }

        public int? PassiveCompensationDiffIndex { get; set; }

        public string Mode { get; set; } = string.Empty;

        public string ReasonCode { get; set; } = string.Empty;

        public string DecisionSource { get; set; } = string.Empty;

        public string Segment { get; set; } = string.Empty;
    }

    private sealed class Tm81CaseContracts
    {
        public Tm81Cad113Case Cad113 { get; set; } = new();

        public Tm81Cad364Case Cad364 { get; set; } = new();

        public Tm81Cad402Case Cad402 { get; set; } = new();

        public Tm81Cad490491Case Cad490Cad491 { get; set; } = new();

        public Tm81FreeformTailLinkCase FreeformTailLink { get; set; } = new();
    }

    private sealed class Tm81Cad113Case
    {
        public bool IsToFullEnabled { get; set; }

        public double ToFullRatio { get; set; }

        public string RuleCode { get; set; } = string.Empty;

        public int Stage1Count { get; set; }

        public int Stage2Count { get; set; }

        public int Stage3Count { get; set; }
    }

    private sealed class Tm81Cad364Case
    {
        public string RuleCodeReg291 { get; set; } = string.Empty;

        public bool IsBoundaryReg291 { get; set; }

        public double Stage2CoverageReg291 { get; set; }

        public double Stage3CoverageReg291 { get; set; }

        public double Reg291Area { get; set; }
    }

    private sealed class Tm81Cad402Case
    {
        public int Reg624OwnerCount { get; set; }

        public int[] Reg624OwnerCadPadIds { get; set; } = Array.Empty<int>();

        public int[] Reg624MatchedCadPadIds { get; set; } = Array.Empty<int>();
    }

    private sealed class Tm81Cad490491Case
    {
        public string Cad490RuleCodeReg643 { get; set; } = string.Empty;

        public string Cad491RuleCodeReg643 { get; set; } = string.Empty;

        public double Stage3OverlapArea { get; set; }
    }

    private sealed class Tm81FreeformTailLinkCase
    {
        public string Reg387Freeform { get; set; } = string.Empty;

        public string Reg387Source { get; set; } = string.Empty;

        public string Reg388Freeform { get; set; } = string.Empty;

        public string Reg388Source { get; set; } = string.Empty;

        public string Reg389Freeform { get; set; } = string.Empty;

        public string Reg389Source { get; set; } = string.Empty;
    }
}
