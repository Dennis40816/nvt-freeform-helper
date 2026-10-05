using System.Collections;
using System.Globalization;
using System.Text.Json;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfManualOverrideIdentityContractTests
{
    private static readonly double[] OneCellEdges = { 0d, 1d };
    private static readonly double[] TwoCellEdges = { 0d, 1d, 2d };
    private static readonly double[] ExpectedProgress = { 0d, 0.6000000000000001, 0.6, 0.8, 0.9500000000000001, 1d };
    private static readonly int[] UnsortedIds = { 10, int.MinValue, 2, int.MaxValue, 0, -7 };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Analyze_TypedAndLegacyOverrides_PreserveIdentityAndDiagnostics(int scenario)
    {
        var (cad, grid, settings) = CreateFixture();
        var progress = new List<double>();
        var result = DxfRegularMappingAnalyzer.Analyze(
            cad, grid, settings, CreateOverrides(scenario), reportProgress: progress.Add);

        AssertContract(result, progress, scenario);

        var typedOverrides = CreateOverrides(scenario)?.ToDictionary(
            static entry => new CadPadId(entry.Key), static entry => new RegularPadId(entry.Value));
        var typedProgress = new List<double>();
        var typed = DxfRegularMappingAnalyzer.AnalyzeWithTypedOverrides(
            cad, grid, settings, typedOverrides, reportProgress: typedProgress.Add);

        AssertContract(typed, typedProgress, scenario);
        Assert.Equal(result.Pairs, typed.Pairs);
        AssertEquivalent(result.Report, typed.Report);
        Assert.Equal(progress, typedProgress);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(typed));
    }

    [Fact]
    public void Analyze_LegacyNullLiteralAndOmittedOverrides_PreserveContract()
    {
        var (cad, grid, settings) = CreateFixture();
        var progress = new List<double>();
        var explicitNull = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, null, null, progress.Add);
        AssertContract(explicitNull, progress, scenario: 0);

        progress.Clear();
        var omitted = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, reportProgress: progress.Add);
        AssertContract(omitted, progress, scenario: 0);
    }

    [Fact]
    public void Analyze_LegacyEmptyOverrides_OnlyReadsCountAndMatchesNull()
    {
        var (cad, grid, settings) = CreateFixture();
        var expectedProgress = new List<double>();
        var expected = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, null, reportProgress: expectedProgress.Add);
        var overrides = new CountGuardOverrides();
        var progress = new List<double>();
        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, overrides, reportProgress: progress.Add);

        Assert.Equal(expected.Pairs, result.Pairs);
        AssertEquivalent(expected.Report, result.Report);
        Assert.Equal(expectedProgress, progress);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result));
        Assert.False(overrides.WasEnumerated);
    }

    [Fact]
    public void Analyze_LegacyNonEmptyOverrides_EnumeratesAndApplies()
    {
        var (cad, grid, settings) = CreateFixture();
        var overrides = new CountGuardOverrides(new KeyValuePair<int, int>(2, -7));
        var progress = new List<double>();
        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, overrides, reportProgress: progress.Add);

        Assert.True(overrides.WasEnumerated);
        AssertContract(result, progress, scenario: 5);
    }

    [Theory]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(-1, -2)]
    [InlineData(0, 0)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void Analyze_TypedAndLegacyOverrides_DoNotRejectIntegerIds(int cadId, int regularId)
    {
        var left = Rectangle(0, 0, 1, 1);
        var right = Rectangle(1, 0, 2, 1);
        var cad = new CadPadSet(new[] { new CadPad(10, "C10", "L1", left), new CadPad(cadId, "C", "L1", right) });
        var grid = new RegularGrid(1, 2, TwoCellEdges, OneCellEdges, new[]
        {
            new RegularPad(0, 0, regularId, left) { IcIndex = 0, DiffIndex = 100 },
            new RegularPad(0, 1, 80, right) { IcIndex = 1, DiffIndex = 101 },
        });
        // IoU-only scoring automatically pairs each CAD with its overlapping cell at score 1.
        var automatic = DxfRegularMappingAnalyzer.Analyze(cad, grid, CreateSettings());
        Assert.Equal(new[]
        {
            new DxfRegularMappingPair(0, 10, regularId, 0, 100, 1),
            new DxfRegularMappingPair(1, cadId, 80, 1, 101, 1),
        }, automatic.Pairs);
        AssertEquivalent(EmptyReport(cadCount: 2, regularCount: 2, mappedCount: 2), automatic.Report);

        var progress = new List<double>();
        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, CreateSettings(),
            new Dictionary<int, int> { [cadId] = regularId }, reportProgress: progress.Add);

        // The override reserves the other cell at score 0, leaving the first CAD unmapped.
        Assert.Equal(new DxfRegularMappingPair(1, cadId, regularId, 0, 100, 0), Assert.Single(result.Pairs));
        Assert.Equal(1, result.Report.UnmappedCadCount);
        Assert.Equal(1, result.Report.UnmappedRegularCount);
        Assert.Equal(1, result.Report.LowConfidenceCount);
        Assert.Equal(ExpectedProgress.SkipLast(1).Append(0.99).Append(1d), progress);

        var typedProgress = new List<double>();
        var typed = DxfRegularMappingAnalyzer.AnalyzeWithTypedOverrides(cad, grid, CreateSettings(),
            new Dictionary<CadPadId, RegularPadId> { [new CadPadId(cadId)] = new RegularPadId(regularId) },
            reportProgress: typedProgress.Add);
        Assert.Equal(result.Pairs, typed.Pairs);
        AssertEquivalent(result.Report, typed.Report);
        Assert.Equal(progress, typedProgress);
    }

    [Fact]
    public void Analyze_LegacyOverrides_ReadDictionaryAfterCandidateProgress()
    {
        var (cad, grid, settings) = CreateFixture();
        var overrides = new Dictionary<int, int>();
        var progress = new List<double>();
        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, overrides, reportProgress: value =>
        {
            progress.Add(value);
            if (value == 0.6)
            {
                overrides[2] = -7;
            }
        });

        AssertContract(result, progress, scenario: 5);
    }

    [Fact]
    public void Analyze_TypedOverrides_ReadDictionaryAfterCandidateProgress()
    {
        var (cad, grid, settings) = CreateFixture();
        var overrides = new Dictionary<CadPadId, RegularPadId>();
        var progress = new List<double>();
        var result = DxfRegularMappingAnalyzer.AnalyzeWithTypedOverrides(cad, grid, settings, overrides,
            reportProgress: value =>
            {
                progress.Add(value);
                if (value == 0.6)
                {
                    overrides[new CadPadId(2)] = new RegularPadId(-7);
                }
            });

        AssertContract(result, progress, scenario: 5);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-7)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(int.MaxValue)]
    public void TypedIds_PreserveIntegerEqualityAndRepresentation(int value)
    {
        var cadId = new CadPadId(value);
        var regularId = new RegularPadId(value);
        Assert.Equal(value, cadId.Value);
        Assert.Equal(value, regularId.Value);
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), cadId.ToString());
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), regularId.ToString());
        Assert.Equal(new CadPadId(value), cadId);
        Assert.Equal(new RegularPadId(value), regularId);
        Assert.Equal(new CadPadId(value).GetHashCode(), cadId.GetHashCode());
        Assert.Equal(new RegularPadId(value).GetHashCode(), regularId.GetHashCode());
        Assert.True(cadId == new CadPadId(value));
        Assert.True(regularId == new RegularPadId(value));
        Assert.True(cadId != new CadPadId(value ^ 1));
        Assert.True(regularId != new RegularPadId(value ^ 1));
        Assert.False(cadId.Equals((object)regularId));
        Assert.False(regularId.Equals((object)cadId));
        Assert.Equal(default, new CadPadId(0));
        Assert.Equal(default, new RegularPadId(0));
    }

    [Fact]
    public void TypedIds_SortNumericallyIncludingIntegerExtremes()
    {
        var expected = UnsortedIds.Order().ToArray();
        Assert.Equal(expected, UnsortedIds.Select(static value => new CadPadId(value)).Order().Select(static id => id.Value));
        Assert.Equal(expected, UnsortedIds.Select(static value => new RegularPadId(value)).Order().Select(static id => id.Value));

        for (var i = 1; i < expected.Length; i++)
        {
            var cadLeft = new CadPadId(expected[i - 1]);
            var cadRight = new CadPadId(expected[i]);
            var regLeft = new RegularPadId(expected[i - 1]);
            var regRight = new RegularPadId(expected[i]);
            Assert.True(cadLeft < cadRight && cadLeft <= cadRight && cadRight > cadLeft && cadRight >= cadLeft);
            Assert.True(regLeft < regRight && regLeft <= regRight && regRight > regLeft && regRight >= regLeft);
            Assert.Equal(0, cadLeft.CompareTo(new CadPadId(cadLeft.Value)));
            Assert.Equal(0, regLeft.CompareTo(new RegularPadId(regLeft.Value)));
        }
    }

    [Fact]
    public void Analyze_TypedAndLegacyOverrides_KeepExternalIntegerIdsAndExpectedDiffMap()
    {
        var (cad, grid, settings) = CreateFixture();
        var expectedDiff = new Dictionary<int, int> { [10] = 501, [2] = 502 };
        var legacyProgress = new List<double>();
        var legacy = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, CreateOverrides(5), expectedDiff, legacyProgress.Add);
        var typedProgress = new List<double>();
        var typed = DxfRegularMappingAnalyzer.AnalyzeWithTypedOverrides(cad, grid, settings,
            new Dictionary<CadPadId, RegularPadId> { [new CadPadId(2)] = new RegularPadId(-7) }, expectedDiff, typedProgress.Add);

        Assert.Equal(new DxfRegularMappingPair(502, 2, -7, 1, 101, 0), Assert.Single(typed.Pairs));
        Assert.Equal(legacy.Pairs, typed.Pairs);
        AssertEquivalent(legacy.Report, typed.Report);
        Assert.Equal(legacyProgress, typedProgress);
        Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(typed));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(typed));
        var pair = json.RootElement.GetProperty("Pairs")[0];
        Assert.Equal(502, pair.GetProperty("DxfIndex").GetInt32());
        Assert.Equal(2, pair.GetProperty("CadPadId").GetInt32());
        Assert.Equal(-7, pair.GetProperty("RegularPadIndex").GetInt32());
        Assert.Equal(1, pair.GetProperty("IcIndex").GetInt32());
        Assert.Equal(101, pair.GetProperty("DiffIndex").GetInt32());
        var issue = json.RootElement.GetProperty("Report").GetProperty("Issues")[1];
        Assert.Equal(502, issue.GetProperty("DxfIndex").GetInt32());
        Assert.Equal(2, issue.GetProperty("CadPadId").GetInt32());
        Assert.Equal(-7, issue.GetProperty("RegularPadIndex").GetInt32());
        Assert.Equal(80, issue.GetProperty("TopCandidates")[0].GetProperty("RegularPadIndex").GetInt32());
    }

    private sealed class CountGuardOverrides(params KeyValuePair<int, int>[] entries) : IReadOnlyDictionary<int, int>
    {
        public int Count => entries.Length;
        public bool WasEnumerated { get; private set; }
        public int this[int key] => throw new InvalidOperationException("Lookup is not supported.");
        public IEnumerable<int> Keys => throw new InvalidOperationException("Key enumeration is not supported.");
        public IEnumerable<int> Values => throw new InvalidOperationException("Value enumeration is not supported.");
        public bool ContainsKey(int key) => throw new InvalidOperationException("Lookup is not supported.");
        public bool TryGetValue(int key, out int value) => throw new InvalidOperationException("Lookup is not supported.");

        public IEnumerator<KeyValuePair<int, int>> GetEnumerator()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Empty overrides must not be enumerated.");
            }

            WasEnumerated = true;
            return ((IEnumerable<KeyValuePair<int, int>>)entries).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static Dictionary<int, int>? CreateOverrides(int scenario)
    {
        return scenario switch
        {
            0 => null,
            1 => new Dictionary<int, int>(),
            2 => new Dictionary<int, int> { [999] = -7 },
            3 => new Dictionary<int, int> { [2] = 999 },
            4 => new Dictionary<int, int> { [0] = 0 },
            5 => new Dictionary<int, int> { [2] = -7 },
            6 => new Dictionary<int, int> { [2] = -7, [10] = 80 },
            7 => new Dictionary<int, int> { [2] = -7, [10] = -7 },
            8 => new Dictionary<int, int> { [10] = -7, [2] = -7 },
            9 => new Dictionary<int, int> { [int.MinValue] = -7, [2] = -7, [10] = int.MaxValue },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
    }

    private static void AssertContract(DxfRegularMappingResult result, IReadOnlyList<double> progress, int scenario)
    {
        // CAD list order is 10, 2; regular IDs 80, -7 differ from their list positions 0, 1.
        var forced = scenario >= 5;
        var swapped = scenario == 6;
        var expectedPairs = !forced
            ? new[] { new DxfRegularMappingPair(0, 10, -7, 1, 101, 1), new DxfRegularMappingPair(1, 2, 80, 0, 100, 1) }
            : swapped
                ? new[] { new DxfRegularMappingPair(0, 10, 80, 0, 100, 0), new DxfRegularMappingPair(1, 2, -7, 1, 101, 0) }
                : new[] { new DxfRegularMappingPair(1, 2, -7, 1, 101, 0) };
        Assert.Equal(expectedPairs, result.Pairs);

        var issues = new List<DxfRegularMappingIssue>();
        if (forced)
        {
            issues.Add(swapped
                ? new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence,
                    "Expected diff idx 0 (CAD id 10) -> diff100 (IC0, regId 80), score=0.",
                    DxfIndex: 0, CadPadId: 10, RegularPadIndex: 80, IcIndex: 0, DiffIndex: 100,
                    Score: 0, BestScore: 1, SecondScore: 0, Margin: 1,
                    TopCandidates: new[] { new DxfRegularMappingCandidate(-7, 1, 101, 1) })
                : new DxfRegularMappingIssue(DxfRegularMappingIssueKind.UnmappedCad,
                    "Expected diff idx 0 (CAD id 10).",
                    DxfIndex: 0, CadPadId: 10, BestScore: 1, SecondScore: 0, Margin: 1,
                    TopCandidates: new[] { new DxfRegularMappingCandidate(-7, 1, 101, 1) }));
            issues.Add(new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence,
                "Expected diff idx 1 (CAD id 2) -> diff101 (IC1, regId -7), score=0.",
                DxfIndex: 1, CadPadId: 2, RegularPadIndex: -7, IcIndex: 1, DiffIndex: 101,
                Score: 0, BestScore: 1, SecondScore: 0, Margin: 1,
                TopCandidates: new[] { new DxfRegularMappingCandidate(80, 0, 100, 1) }));
            if (!swapped)
            {
                issues.Add(new DxfRegularMappingIssue(DxfRegularMappingIssueKind.UnmappedRegular,
                    "diff100 (IC0, regId 80).", RegularPadIndex: 80, IcIndex: 0, DiffIndex: 100));
            }
        }

        var expectedReport = !forced
            ? EmptyReport(cadCount: 2, regularCount: 2, mappedCount: 2)
            : new DxfRegularMappingReport(2, 2, swapped ? 2 : 1, swapped ? 0 : 1, swapped ? 0 : 1,
                swapped ? 2 : 1, 0, true,
                swapped
                    ? "mapped=2, unmappedCad=0, unmappedRegular=0, lowConf=2, ambiguous=0, duplicateDiff=0"
                    : "mapped=1, unmappedCad=1, unmappedRegular=1, lowConf=1, ambiguous=0, duplicateDiff=0",
                issues.Count, false, issues, issues);
        AssertEquivalent(expectedReport, result.Report);
        Assert.Equal(ExpectedProgress, progress);
    }

    private static DxfRegularMappingReport EmptyReport(int cadCount, int regularCount, int mappedCount)
    {
        return new DxfRegularMappingReport(cadCount, regularCount, mappedCount, 0, 0, 0, 0, false,
            $"mapped={mappedCount}, unmappedCad=0, unmappedRegular=0, lowConf=0, ambiguous=0, duplicateDiff=0",
            0, false, Array.Empty<DxfRegularMappingIssue>(), Array.Empty<DxfRegularMappingIssue>());
    }

    private static void AssertEquivalent(DxfRegularMappingReport expected, DxfRegularMappingReport actual)
    {
        Assert.Equal(expected with { Issues = actual.Issues, SampleIssues = actual.SampleIssues }, actual);
        AssertIssues(expected.Issues, actual.Issues);
        AssertIssues(expected.SampleIssues, actual.SampleIssues);
    }

    private static void AssertIssues(IReadOnlyList<DxfRegularMappingIssue> expected, IReadOnlyList<DxfRegularMappingIssue> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i] with { TopCandidates = actual[i].TopCandidates }, actual[i]);
            Assert.Equal(expected[i].TopCandidates, actual[i].TopCandidates);
        }
    }

    private static (CadPadSet Cad, RegularGrid Grid, IndexMappingSettings Settings) CreateFixture()
    {
        var left = Rectangle(0, 0, 1, 1);
        var right = Rectangle(1, 0, 2, 1);
        var cad = new CadPadSet(new[] { new CadPad(10, "C10", "L1", right), new CadPad(2, "C2", "L1", left) });
        var grid = new RegularGrid(1, 2, TwoCellEdges, OneCellEdges, new[]
        {
            new RegularPad(0, 0, 80, left) { IcIndex = 0, DiffIndex = 100 },
            new RegularPad(0, 1, -7, right) { IcIndex = 1, DiffIndex = 101 },
        });
        return (cad, grid, CreateSettings());
    }

    private static IndexMappingSettings CreateSettings() => new()
    {
        CandidatePaddingCells = 0,
        WeightIou = 1,
        WeightCentroidDistance = 0,
        WeightAreaRatio = 0,
    };

    private static Polygon2 Rectangle(double minX, double minY, double maxX, double maxY) => new(new[]
    {
        new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY),
    });
}
