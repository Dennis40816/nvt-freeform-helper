using System.Reflection;
using System.Text.Json;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchGoldenBaselineTests
{
    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [ExampleDataFact]
    public async Task RealProjects_MatchGoldenBaseline()
    {
        var baselinePath = GetBaselinePath();
        var expected = LoadBaseline();
        var actual = new NotchGoldenBaselineDocument();

        foreach (var (projectKey, projectBaseline) in expected.Projects)
        {
            actual.Projects.Add(
                projectKey,
                await BuildActualBaselineAsync(projectBaseline.ProjectRelativePath));
        }

        var expectedJson = JsonSerializer.Serialize(expected, BaselineJsonOptions);
        var actualJson = JsonSerializer.Serialize(actual, BaselineJsonOptions);
        if (string.Equals(Environment.GetEnvironmentVariable("FREEFORMHELPER_UPDATE_NOTCH_BASELINE"), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(baselinePath, actualJson);
            return;
        }

        if (!string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
        {
            var actualDumpPath = Path.Combine(
                TestPaths.RepoRoot,
                "build",
                "notch-golden-baseline.actual.json");
            File.WriteAllText(actualDumpPath, actualJson);
        }

        GoldenAssert.TextEqual(expectedJson, actualJson, "notch golden baseline");
    }

    private static async Task<NotchGoldenProjectBaseline> BuildActualBaselineAsync(string projectRelativePath)
    {
        var projectPath = Path.Combine(
            TestPaths.RepoRoot,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        using var fixture = ExampleProjectFixture.Create(projectPath);

        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => Task.FromResult<string?>(fixture.ProjectPath),
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        var projectField = typeof(FreeformHelperViewModel).GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(cadField);
        Assert.NotNull(gridField);
        Assert.NotNull(projectField);

        var cad = Assert.IsType<CadPadSet>(cadField!.GetValue(vm));
        var grid = Assert.IsType<RegularGrid>(gridField!.GetValue(vm));
        var project = Assert.IsType<ProjectFile>(projectField!.GetValue(vm));

        project.Settings.Notch.EnabledVersions.Clear();
        project.Settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V21);
        project.Settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V22);

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, project.Settings);
        var workflowTable = await NotchCurrentWorkflowTableTestHelper.GenerateRawTableAsync(
            vm,
            $"Golden baseline {Path.GetFileName(projectRelativePath)}");
        var exportVm = new NotchExportSelectionViewModel(table);
        var orderedRows = table.Rows
            .OrderBy(static row => row.IcIndex)
            .ThenBy(static row => row.DiffIndex)
            .ThenBy(static row => (int)row.Version)
            .ToList();

        return new NotchGoldenProjectBaseline
        {
            ProjectRelativePath = projectRelativePath,
            RowCount = orderedRows.Count,
            VersionCounts = BuildVersionCounts(orderedRows),
            IcCounts = BuildIcCounts(orderedRows),
            ToFullCoverage = NotchToFullCoverageSnapshotBuilder.Build(workflowTable.ToFullCoverageAudit),
            Distribution = new NotchGoldenDistribution
            {
                Transfer = exportVm.TransferRowCount,
                Warning = exportVm.WarningRowCount,
                NoCad = exportVm.CadMissingRowCount,
                Legacy = exportVm.LegacyRowCount,
                Linked = exportVm.CadLinkedRowCount,
            },
            Samples = new NotchGoldenSamples
            {
                First = CreateSample(orderedRows.FirstOrDefault()),
                FirstV22Main = CreateSample(orderedRows.FirstOrDefault(static row =>
                    row.Version == NotchAlgorithmVersion.V22 &&
                    row.Comment.Contains("MAIN", StringComparison.OrdinalIgnoreCase))),
                FirstV22NoOp = CreateSample(orderedRows.FirstOrDefault(static row =>
                    row.Version == NotchAlgorithmVersion.V22 &&
                    row.Values.Length >= 7 &&
                    row.Values[1] == 100 &&
                    row.Values[2] == 65535 &&
                    row.Values[4] == 65535)),
                FirstV21Adjusted = CreateSample(orderedRows.FirstOrDefault(static row =>
                    row.Version == NotchAlgorithmVersion.V21 &&
                    row.Values.Length >= 4 &&
                    (row.Values[1] != 100 || row.Values[2] != 100 || row.Values[3] != 0))),
                Last = CreateSample(orderedRows.LastOrDefault()),
            },
        };
    }

    private static NotchGoldenSample? CreateSample(NotchTableRow? row)
    {
        return row is null
            ? null
            : new NotchGoldenSample
            {
                Version = row.Version.ToString(),
                Ic = row.IcIndex + 1,
                Diff = row.DiffIndex,
                Reg = row.RegularPadIndex,
                Cad = row.CadPadId,
                Values = row.Values.ToArray(),
                Comment = row.Comment,
            };
    }

    private static SortedDictionary<string, int> BuildVersionCounts(IEnumerable<NotchTableRow> rows)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var group in rows
                     .GroupBy(static row => row.Version.ToString())
                     .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            result[group.Key] = group.Count();
        }

        return result;
    }

    private static SortedDictionary<string, int> BuildIcCounts(IEnumerable<NotchTableRow> rows)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var group in rows
                     .GroupBy(static row => row.IcIndex)
                     .OrderBy(static group => group.Key))
        {
            result[$"IC{group.Key + 1}"] = group.Count();
        }

        return result;
    }

    private static NotchGoldenBaselineDocument LoadBaseline()
    {
        var baselinePath = GetBaselinePath();
        Assert.True(File.Exists(baselinePath), "The golden baseline file is missing.");

        using var stream = File.OpenRead(baselinePath);
        var baseline = JsonSerializer.Deserialize<NotchGoldenBaselineDocument>(stream, BaselineJsonOptions);
        Assert.NotNull(baseline);
        Assert.NotEmpty(baseline!.Projects);
        return baseline;
    }

    private static string GetBaselinePath()
    {
        return Path.Combine(
            TestPaths.RepoRoot,
            "example",
            "golden-snapshots",
            "notch-golden-baseline.json");
    }

    private sealed class NotchGoldenBaselineDocument
    {
        public SortedDictionary<string, NotchGoldenProjectBaseline> Projects { get; set; } =
            new(StringComparer.Ordinal);
    }

    private sealed class NotchGoldenProjectBaseline
    {
        public string ProjectRelativePath { get; set; } = string.Empty;

        public int RowCount { get; set; }

        public SortedDictionary<string, int> VersionCounts { get; set; } =
            new(StringComparer.Ordinal);

        public SortedDictionary<string, int> IcCounts { get; set; } =
            new(StringComparer.Ordinal);

        public NotchToFullCoverageSnapshot ToFullCoverage { get; set; } = new();

        public NotchGoldenDistribution Distribution { get; set; } = new();

        public NotchGoldenSamples Samples { get; set; } = new();
    }

    private sealed class NotchGoldenDistribution
    {
        public int Transfer { get; set; }

        public int Warning { get; set; }

        public int NoCad { get; set; }

        public int Legacy { get; set; }

        public int Linked { get; set; }
    }

    private sealed class NotchGoldenSamples
    {
        public NotchGoldenSample? First { get; set; }

        public NotchGoldenSample? FirstV22Main { get; set; }

        public NotchGoldenSample? FirstV22NoOp { get; set; }

        public NotchGoldenSample? FirstV21Adjusted { get; set; }

        public NotchGoldenSample? Last { get; set; }
    }

    private sealed class NotchGoldenSample
    {
        public string Version { get; set; } = string.Empty;

        public int Ic { get; set; }

        public int Diff { get; set; }

        public int Reg { get; set; }

        public int? Cad { get; set; }

        public int[] Values { get; set; } = Array.Empty<int>();

        public string Comment { get; set; } = string.Empty;
    }
}
