using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfOverlapAnalyzerTests
{
    [Fact]
    public void Analyze_EmptyCad_ReportsCompletedProgress()
    {
        var analyzer = new DxfOverlapAnalyzer();
        var progress = new List<double>();

        var report = DxfOverlapAnalyzer.Analyze(new CadPadSet(new List<CadPad>()), reportProgress: progress.Add);

        Assert.Equal(0, report.PadCount);
        Assert.False(report.HasIssues);
        Assert.NotEmpty(progress);
        Assert.Equal(0.0, progress[0], 6);
        Assert.Equal(1.0, progress[^1], 6);
        AssertProgressIsMonotonic(progress);
    }

    [Fact]
    public void Analyze_DetectsDuplicateAndOverlapPairs()
    {
        var analyzer = new DxfOverlapAnalyzer();
        var pads = new List<CadPad>
        {
            CreatePad(1, "L1", 0, 0, 10, 10),
            CreatePad(2, "L2", 0, 0, 10, 10),
            CreatePad(3, "L3", 8, 8, 14, 14),
        };

        var report = DxfOverlapAnalyzer.Analyze(new CadPadSet(pads));

        Assert.Equal(3, report.PadCount);
        Assert.Equal(0, report.DuplicateSameLayerPairs);
        Assert.Equal(1, report.DuplicateCrossLayerPairs);
        Assert.Equal(2, report.OverlapPairs);
        Assert.True(report.HasIssues);
        Assert.NotEmpty(report.SampleIssues);
    }

    [Fact]
    public void Analyze_ReportsMonotonicProgress_ForNonEmptyInput()
    {
        var analyzer = new DxfOverlapAnalyzer();
        var progress = new List<double>();
        var pads = new List<CadPad>();

        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                var minX = col * 12.0;
                var minY = row * 12.0;
                pads.Add(CreatePad(row * 8 + col + 1, "L", minX, minY, minX + 10.0, minY + 10.0));
            }
        }

        var report = DxfOverlapAnalyzer.Analyze(new CadPadSet(pads), reportProgress: progress.Add);

        Assert.Equal(64, report.PadCount);
        Assert.NotEmpty(progress);
        Assert.Equal(0.0, progress[0], 6);
        Assert.Equal(1.0, progress[^1], 6);
        AssertProgressIsMonotonic(progress);
    }

    [Fact]
    public void Analyze_DetectsOverlap_WithSpatialAccelerationEnabled()
    {
        var analyzer = new DxfOverlapAnalyzer();
        var pads = new List<CadPad>();

        // Build enough non-overlap pads to force spatial acceleration path.
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var minX = col * 20.0;
                var minY = row * 20.0;
                pads.Add(CreatePad(row * 9 + col + 1, "L", minX, minY, minX + 8.0, minY + 8.0));
            }
        }

        pads.Add(CreatePad(9001, "L", 200.2, 100.2, 210.2, 110.2));
        pads.Add(CreatePad(9002, "L", 209.8, 100.5, 219.8, 110.5));

        var report = DxfOverlapAnalyzer.Analyze(new CadPadSet(pads));

        Assert.Equal(pads.Count, report.PadCount);
        Assert.Equal(0, report.DuplicateSameLayerPairs);
        Assert.Equal(0, report.DuplicateCrossLayerPairs);
        Assert.Equal(1, report.OverlapPairs);
    }

    [Fact]
    public void Analyze_ReportsTheSameProgressValues_ForDuplicateAndOverlapInput()
    {
        var progress = new List<double>();
        var pads = new List<CadPad>
        {
            CreatePad(1, "L1", 0, 0, 10, 10),
            CreatePad(2, "L2", 0, 0, 10, 10),
            CreatePad(3, "L3", 8, 8, 14, 14),
        };

        DxfOverlapAnalyzer.Analyze(new CadPadSet(pads), reportProgress: progress.Add);

        // start, end of the signature step (15%), end of the duplicate step (40%), end of the overlap step, done.
        var expected = new[] { 0.0, 0.15, 0.40, 1.0, 1.0 };
        Assert.Equal(expected, progress.Select(value => Math.Round(value, 6)));
    }

    [Fact]
    public void Analyze_ReportsTheSameProgressValues_ForSpatialAccelerationInput()
    {
        var progress = new List<double>();
        var pads = new List<CadPad>();
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                pads.Add(CreatePad(row * 8 + col + 1, "L", col * 12.0, row * 12.0, (col * 12.0) + 10.0, (row * 12.0) + 10.0));
            }
        }

        pads.Add(CreatePad(65, "L2", 0, 0, 10, 10));
        pads.Add(CreatePad(66, "L", 5, 5, 15, 15));

        DxfOverlapAnalyzer.Analyze(new CadPadSet(pads), reportProgress: progress.Add);

        // The signature step also reports once at pad 64 of 66. Only the last candidate pair reports in the overlap step.
        var expected = new[] { 0.0, 0.145455, 0.15, 0.40, 1.0, 1.0 };
        Assert.Equal(expected, progress.Select(value => Math.Round(value, 6)));
    }

    [Fact]
    public void Analyze_ReportsTheSameProgressValues_WhenTheOverlapStepPassesItsReportInterval()
    {
        var progress = new List<double>();
        var pads = new List<CadPad>();
        for (var index = 0; index < 70; index++)
        {
            // Every pad overlaps every other pad and none is a duplicate: 2415 pairs, above the interval of 2048.
            var shift = index * 0.01;
            pads.Add(CreatePad(index + 1, "L", shift, shift, 10.0 + shift, 10.0 + shift));
        }

        DxfOverlapAnalyzer.Analyze(new CadPadSet(pads), reportProgress: progress.Add);

        // The overlap step reports at pair 2048 and at the last pair.
        var expected = new[] { 0.0, 0.137143, 0.15, 0.40, 0.90882, 1.0, 1.0 };
        Assert.Equal(expected, progress.Select(value => Math.Round(value, 6)));
    }

    private static void AssertProgressIsMonotonic(List<double> progress)
    {
        for (var i = 0; i < progress.Count; i++)
        {
            Assert.InRange(progress[i], 0.0, 1.0);
            if (i == 0)
            {
                continue;
            }

            Assert.True(
                progress[i] >= progress[i - 1],
                $"progress regressed at index {i}: {progress[i - 1]} -> {progress[i]}");
        }
    }

    private static CadPad CreatePad(int id, string layer, double minX, double minY, double maxX, double maxY)
    {
        return new CadPad(id, $"C{id}", layer, new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        }));
    }
}
