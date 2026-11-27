// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using NLog;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Explicitly opted-in tool host, not an example-data test. Normal test runs do no IO here.</summary>
[Collection("HeadlessUiSerial")]
public sealed class NotchAbReportHost
{
    [Fact]
    public async Task RunAsync()
    {
        var input = TestEnvironment.Get("NFH_NOTCH_AB_REQUEST");
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var request = JsonSerializer.Deserialize<NotchAbRequest>(input, JsonOptions())
            ?? throw new InvalidOperationException("Missing A/B request.");
        var output = ResolveBuildOutput(request.OutputPath);
        var cancellationToken = TestContext.Current.CancellationToken;
        var content = request.Action switch
        {
            "snapshot" => JsonSerializer.Serialize(await SnapshotAsync(request), JsonOptions()),
            "compare" => RenderComparison(await ReadSnapshotAsync(request.BeforePath, cancellationToken),
                await ReadSnapshotAsync(request.AfterPath, cancellationToken)),
            _ => throw new InvalidOperationException("Unknown A/B action."),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await TestFiles.WriteTextAsync(output, content + "\n", cancellationToken);
    }

    private static async Task<NotchAbSnapshot> SnapshotAsync(NotchAbRequest request)
    {
        // The assembly module initializer owns settings isolation. Fail before constructing the VM
        // if it was not applied. Suspend logging so project contents cannot enter NLog targets.
        if (string.IsNullOrWhiteSpace(TestEnvironment.Get("FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH")))
        {
            throw new InvalidOperationException("The A/B host requires test settings isolation.");
        }

        using var loggingScope = LogManager.SuspendLogging();
        var vm = new FreeformHelperViewModel();
        var loaded = await vm.LoadProjectFromPathAsync(Required(request.ProjectPath));
        if (!loaded.IsLoaded)
        {
            throw new InvalidOperationException("A/B project load failed; no snapshot was written.");
        }

        var cad = NotchCurrentWorkflowTableTestHelper.GetExportCadPadSet(vm);
        var grid = FreeformHelperViewModelPrivateAccess.Grid(vm)
            ?? throw new InvalidOperationException("A/B project has no regular grid.");
        var settings = vm.ProjectSession.Document.Settings;
        settings.Notch.ComputationMode = NotchComputationMode.CadAllocation;
        settings.Notch.EnabledVersions = [NotchAlgorithmVersion.V22];
        ApplyOverrides(settings.Notch, request);
        var workflow = FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm);
        var active = vm.WorkflowDataForTests.LatestPadMatch.RegularToCad.Keys.ToHashSet();
        var collector = new NotchV22ExplanationCollector();
        var table = new NotchTableGenerator().Generate(cad, grid, settings,
            activeRegularPadIds: active.Count == 0 ? null : active,
            cadOutputFwDiffIndexByCadId: workflow.CadOutputFwDiffIndexByCadId,
            explanationCollector: collector);
        if (table.Rows.Count != collector.Explanations.Sum(static source => source.Rows.Length))
        {
            throw new InvalidOperationException("A/B snapshot did not explain every generated row.");
        }

        return new NotchAbSnapshot(1, new NotchAbSettings(settings.Notch.CompensationModel,
            settings.Notch.EnableBoundaryVirtualAreaCap, settings.Notch.BoundaryVirtualAreaCapRatio,
            settings.Notch.EnableTargetCoverageGuard, settings.Notch.TargetCoverageCapPercent), collector.Explanations);
    }

    private static void ApplyOverrides(NotchSettings settings, NotchAbRequest request)
    {
        if (request.CompensationModel is { } model)
        {
            if (!Enum.IsDefined(model))
            {
                throw new InvalidOperationException("Unknown compensation model.");
            }

            settings.CompensationModel = model;
        }

        if (request.BoundaryVirtualAreaCap is { } enabled)
        {
            settings.EnableBoundaryVirtualAreaCap = enabled;
        }
    }

    private static async Task<NotchAbSnapshot> ReadSnapshotAsync(string? path, CancellationToken cancellationToken)
    {
        var snapshot = JsonSerializer.Deserialize<NotchAbSnapshot>(
            await TestFiles.ReadContentAsync(Required(path), cancellationToken), JsonOptions());
        if (snapshot is null || snapshot.SchemaVersion != 1 || snapshot.Explanations.IsDefault ||
            snapshot.Explanations.Any(static source => source.Rows.IsDefault || source.Rows.Any(
                static row => row.Ordinal < 0 || row.Values.IsDefault || row.Values.Length != 7)))
        {
            throw new InvalidOperationException("Unsupported or invalid A/B snapshot.");
        }

        return snapshot;
    }

    private static string RenderComparison(NotchAbSnapshot before, NotchAbSnapshot after)
    {
        var differences = NotchV22ExplanationComparer.Compare(before.Explanations, after.Explanations);
        var text = new StringBuilder("# Notch V22 A/B comparison\n\n");
        text.AppendLine("| IC | Source diff | Row | Change | Before (7 integers) | After (7 integers) | First step | Rules before | Rules after |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in differences)
        {
            var row = item.Key.Ordinal == 0 ? "MAIN" : $"CONT {item.Key.Ordinal}";
            text.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"| {item.Key.IcIndex} | {item.Key.SourceDiffIndex} | {row} | {item.Kind} | {Values(item.Before)} | {Values(item.After)} | {item.FirstStep} | {Rules(item.RuleCodesBefore)} | {Rules(item.RuleCodesAfter)} |");
        }

        text.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"\nDiffering rows: {differences.Length}. IC indices are zero-based.");
        return text.ToString();
    }

    private static string Values(ImmutableArray<int>? values) => values.HasValue ? string.Join(", ", values.Value) : "—";

    private static string Rules(ImmutableArray<string> rules) => string.Join("; ", rules)
        .Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Required(string? value) => !string.IsNullOrWhiteSpace(value)
        ? value : throw new InvalidOperationException("A required A/B path is missing.");

    private static string ResolveBuildOutput(string path)
    {
        var root = Path.GetFullPath(TestPaths.FromRepo("build"));
        var output = Path.GetFullPath(path);
        if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A/B output must be under this worktree's build directory.");
        }

        return output;
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };
}

internal sealed record NotchAbRequest(string Action, string OutputPath, string? ProjectPath = null,
    string? BeforePath = null, string? AfterPath = null, NotchCompensationModel? CompensationModel = null,
    bool? BoundaryVirtualAreaCap = null);

internal sealed record NotchAbSnapshot(int SchemaVersion, NotchAbSettings Settings,
    ImmutableArray<NotchV22RowExplanation> Explanations);

internal sealed record NotchAbSettings(NotchCompensationModel CompensationModel,
    bool BoundaryVirtualAreaCap, double BoundaryVirtualAreaCapRatio, bool TargetCoverageGuard, int TargetCoverageCapPercent);
