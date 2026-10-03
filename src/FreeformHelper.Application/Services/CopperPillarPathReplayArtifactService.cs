using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

public sealed record CopperPillarPathReplayRequest(
    Point2 Start,
    Point2 End,
    int StepCount,
    double Diameter,
    double PeakValue,
    double BaselineValue = 0d,
    int CircleSegmentCount = 96);

public sealed record CopperPillarPathReplayStep(
    int StepIndex,
    double CenterX,
    double CenterY,
    double MaxAfterValue,
    int EmsViolationCount,
    int? WorstRegularPadId,
    int? WorstIcIndex,
    int? WorstDiffIndex,
    int NetFlowResidualCount,
    int TargetCoverageRiskCount,
    string StatusText,
    IReadOnlyList<string> Diagnostics)
{
    [JsonIgnore]
    public bool IsSupported { get; init; } = true;

    [JsonIgnore]
    public bool HasGlobalFlowResidual { get; init; }

    public bool HasEmsViolations => EmsViolationCount > 0;
    public bool HasPhysicalAuditRisks =>
        HasGlobalFlowResidual || NetFlowResidualCount > 0 || TargetCoverageRiskCount > 0;
}

public sealed record CopperPillarPathReplayResult(IReadOnlyList<CopperPillarPathReplayStep> Steps)
{
    public static CopperPillarPathReplayResult Empty { get; } = new(Array.Empty<CopperPillarPathReplayStep>());

    public bool HasSteps => Steps.Count > 0;
    [JsonIgnore]
    public bool HasUnsupportedSteps => Steps.Any(static step => !step.IsSupported);

    public bool HasEmsViolations => Steps.Any(static step => step.HasEmsViolations);
    [JsonIgnore]
    public bool HasPhysicalAuditRisks => Steps.Any(static step => step.HasPhysicalAuditRisks);

    public int TotalEmsViolationCount => Steps.Sum(static step => step.EmsViolationCount);
    public int TotalNetFlowResidualCount => Steps.Sum(static step => step.NetFlowResidualCount);
    public int TotalTargetCoverageRiskCount => Steps.Sum(static step => step.TargetCoverageRiskCount);
    public CopperPillarPathReplayStep? WorstStep => Steps
        .OrderByDescending(static step => step.MaxAfterValue)
        .ThenBy(static step => step.StepIndex)
        .FirstOrDefault();
}

public sealed record CopperPillarPathReplayArtifactSnapshot(
    int StepCount,
    int TotalEmsViolationCount,
    int TotalNetFlowResidualCount,
    int TotalTargetCoverageRiskCount,
    int? WorstStepIndex,
    IReadOnlyList<CopperPillarPathReplayArtifactRow> Rows)
{
    public static CopperPillarPathReplayArtifactSnapshot Empty { get; } = new(
        0,
        0,
        0,
        0,
        null,
        Array.Empty<CopperPillarPathReplayArtifactRow>());
}

public sealed record CopperPillarPathReplayArtifactRow(
    int StepIndex,
    double CenterX,
    double CenterY,
    double MaxAfterValue,
    int EmsViolationCount,
    int? WorstRegularPadId,
    int? WorstIcIndex,
    int? WorstDiffIndex,
    int NetFlowResidualCount,
    int TargetCoverageRiskCount,
    string StatusText,
    string DiagnosticsText)
{
    [JsonIgnore]
    public bool HasGlobalFlowResidual { get; init; }

    [JsonIgnore]
    public string StepText => $"#{(StepIndex + 1).ToString(CultureInfo.InvariantCulture)}";

    [JsonIgnore]
    public string CenterText => string.Create(CultureInfo.InvariantCulture, $"({CenterX:0.###}, {CenterY:0.###})");

    [JsonIgnore]
    public string MaxAfterText => $"Max After {FormatNumber(MaxAfterValue)}";

    [JsonIgnore]
    public string WorstText => WorstRegularPadId.HasValue
        ? $"REG {WorstRegularPadId.Value.ToString(CultureInfo.InvariantCulture)} · IC {FormatNullableOneBased(WorstIcIndex)} · FW Diff {FormatNullable(WorstDiffIndex)}"
        : "REG -";

    [JsonIgnore]
    public string AuditText =>
        $"EMS {EmsViolationCount.ToString(CultureInfo.InvariantCulture)} · net-flow {NetFlowResidualCount.ToString(CultureInfo.InvariantCulture)} · coverage {TargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)}";

    [JsonIgnore]
    public bool HasEmsViolations => EmsViolationCount > 0;

    [JsonIgnore]
    public bool HasPhysicalAuditRisks =>
        HasGlobalFlowResidual || NetFlowResidualCount > 0 || TargetCoverageRiskCount > 0;

    private static string FormatNullable(int? value) =>
        value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : "-";

    private static string FormatNullableOneBased(int? value) =>
        value.HasValue
            ? (value.Value + 1).ToString(CultureInfo.InvariantCulture)
            : "-";

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}

public static class CopperPillarPathReplayArtifactService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static CopperPillarPathReplayArtifactSnapshot BuildSnapshot(CopperPillarPathReplayResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var rows = result.Steps
            .Select(static step => new CopperPillarPathReplayArtifactRow(
                step.StepIndex,
                step.CenterX,
                step.CenterY,
                step.MaxAfterValue,
                step.EmsViolationCount,
                step.WorstRegularPadId,
                step.WorstIcIndex,
                step.WorstDiffIndex,
                step.NetFlowResidualCount,
                step.TargetCoverageRiskCount,
                step.StatusText,
                step.Diagnostics.Count == 0
                    ? string.Empty
                    : string.Join(" | ", step.Diagnostics))
            {
                HasGlobalFlowResidual = step.HasGlobalFlowResidual,
            })
            .ToArray();
        return new CopperPillarPathReplayArtifactSnapshot(
            result.Steps.Count,
            result.TotalEmsViolationCount,
            result.TotalNetFlowResidualCount,
            result.TotalTargetCoverageRiskCount,
            result.WorstStep?.StepIndex,
            rows);
    }

    public static string ExportCsv(CopperPillarPathReplayArtifactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return ExportCsv(snapshot.Rows);
    }

    public static string ExportCsv(IEnumerable<CopperPillarPathReplayArtifactRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.AppendLine("step_index,center_x,center_y,max_after,ems_violations,worst_regular_pad,worst_ic,worst_diff,net_flow_residuals,target_coverage_risks,status,diagnostics");
        foreach (var row in rows)
        {
            AppendCsvCell(builder, row.StepIndex);
            AppendCsvCell(builder, row.CenterX);
            AppendCsvCell(builder, row.CenterY);
            AppendCsvCell(builder, row.MaxAfterValue);
            AppendCsvCell(builder, row.EmsViolationCount);
            AppendCsvCell(builder, row.WorstRegularPadId);
            AppendCsvCell(builder, row.WorstIcIndex);
            AppendCsvCell(builder, row.WorstDiffIndex);
            AppendCsvCell(builder, row.NetFlowResidualCount);
            AppendCsvCell(builder, row.TargetCoverageRiskCount);
            AppendCsvCell(builder, row.StatusText);
            AppendCsvCell(builder, row.DiagnosticsText, endOfRow: true);
        }

        return builder.ToString();
    }

    public static string ExportJson(CopperPillarPathReplayArtifactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static string ExportClipboardTable(IEnumerable<CopperPillarPathReplayArtifactRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.AppendLine("Step\tCenter\tMax After\tWorst\tAudit\tStatus\tDiagnostics");
        foreach (var row in rows)
        {
            builder
                .Append(row.StepText).Append('\t')
                .Append(row.CenterText).Append('\t')
                .Append(row.MaxAfterText).Append('\t')
                .Append(row.WorstText).Append('\t')
                .Append(row.AuditText).Append('\t')
                .Append(row.StatusText).Append('\t')
                .Append(row.DiagnosticsText)
                .AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendCsvCell(StringBuilder builder, string value, bool endOfRow = false)
    {
        builder.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
        if (endOfRow)
        {
            builder.AppendLine();
            return;
        }

        builder.Append(',');
    }

    private static void AppendCsvCell(StringBuilder builder, int value, bool endOfRow = false) =>
        AppendCsvCell(builder, value.ToString(CultureInfo.InvariantCulture), endOfRow);

    private static void AppendCsvCell(StringBuilder builder, int? value, bool endOfRow = false) =>
        AppendCsvCell(builder, value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty, endOfRow);

    private static void AppendCsvCell(StringBuilder builder, double value, bool endOfRow = false) =>
        AppendCsvCell(builder, value.ToString("0.######", CultureInfo.InvariantCulture), endOfRow);
}
