using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
    [Fact]
    public void Generate_Skips_When_Freeform_None()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_V22_BuildsCompatibilityGeometryRow()
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,50,100,54321,70,0,54321,0,54321|Freeform=XWay CadPadId=1 Case=0",
            BuildRowSnapshot(row));
    }

    [Theory]
    [InlineData(FreeformType.XWay)]
    [InlineData(FreeformType.YWay)]
    [InlineData(FreeformType.XYWay)]
    public void Generate_LegacyRegularAnchor_VersionDispatchMatchesEligibility(FreeformType freeform)
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();
        grid.Pads[0].Freeform = freeform;
        settings.Notch.EnabledVersions =
        [
            NotchAlgorithmVersion.V22,
            NotchAlgorithmVersion.V21,
        ];
        var expectedVersions = freeform == FreeformType.XYWay
            ? new[] { NotchAlgorithmVersion.V21 }
            : new[] { NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22 };
        var generator = new NotchTableGenerator();

        var eligibility = generator.EvaluateCadRowEligibility(cad.Pads[0], grid, settings);
        var table = generator.Generate(cad, grid, settings);

        Assert.Equal(expectedVersions, eligibility.EligibleVersions);
        Assert.Equal(expectedVersions.Length, eligibility.EstimatedRowCount);
        Assert.Equal(grid.Pads[0].RegularPadId, eligibility.AnchorRegularPadId);
        Assert.Equal(expectedVersions, table.Rows.Select(static row => row.Version));
        Assert.All(table.Rows, static row => Assert.Equal(9, row.Values.Length));
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_FreezesRequestBeforeInitialProgressCallback()
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.ProcessedCount == 0)
            {
                settings.Notch.ComputationMode = NotchComputationMode.CadAllocation;
            }
        });

        var row = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings, progress).Rows);

        Assert.Equal(NotchComputationMode.CadAllocation, settings.Notch.ComputationMode);
        Assert.Equal(
            "V22|IC0|DIFF0|REG0|CAD1|0,50,100,54321,70,0,54321,0,54321|" +
            "Freeform=XWay CadPadId=1 Case=0",
            BuildRowSnapshot(row));
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_OwnsEnabledVersionsBeforeInitialProgressCallback()
    {
        var (grid, cad, settings) = CreateLegacyTriangleScenario();
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.ProcessedCount != 0)
            {
                return;
            }

            settings.Notch.EnabledVersions.Clear();
            settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V21);
        });

        var current = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings, progress).Rows);
        var next = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings).Rows);

        Assert.Equal(NotchAlgorithmVersion.V22, current.Version);
        Assert.Equal(NotchAlgorithmVersion.V21, next.Version);
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_FreezesRowSettingsForUnbuiltRowsAndNextCallObservesChanges()
    {
        var (grid, cad) = CreateTwoRegularLegacyScenario();

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 0,
                LenScale = 7,
                NullValue = 54321,
            },
        };
        var reports = new List<NotchGenerationProgress>();
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            reports.Add(snapshot);
            if (snapshot.ProcessedCount != 1)
            {
                return;
            }

            settings.Notch.ThresholdPercentV22 = 75;
            settings.Notch.LenScale = 9;
            settings.Notch.NullValue = 12345;
        });
        var generator = new NotchTableGenerator();

        var currentRows = generator.Generate(cad, grid, settings, progress).Rows;

        Assert.Equal(2, currentRows.Count);
        var currentLaterRow = Assert.Single(currentRows, row => row.DiffIndex == 1);
        Assert.Contains(54321, currentLaterRow.Values);
        Assert.Contains(70, currentLaterRow.Values);
        Assert.Equal(
            new[]
            {
                (Processed: 0, Total: 2, Rows: 0, Cad: (int?)null, Regular: (int?)null),
                (Processed: 1, Total: 2, Rows: 1, Cad: (int?)1, Regular: (int?)0),
                (Processed: 2, Total: 2, Rows: 2, Cad: (int?)1, Regular: (int?)1),
                (Processed: 2, Total: 2, Rows: 2, Cad: (int?)null, Regular: (int?)null),
            },
            reports.Select(report =>
                (report.ProcessedCount,
                 report.TotalCount,
                 report.GeneratedRowCount,
                 report.CurrentCadPadId,
                 report.CurrentRegularPadId)));
        Assert.Empty(generator.Generate(cad, grid, settings).Rows);

        settings.Notch.ThresholdPercentV22 = 0;
        var nextLaterRow = Assert.Single(
            generator.Generate(cad, grid, settings).Rows,
            row => row.DiffIndex == 1);
        Assert.Contains(12345, nextLaterRow.Values);
        Assert.Contains(90, nextLaterRow.Values);
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_FreezesLinkedThresholdForUnbuiltRows()
    {
        var (grid, cad) = CreateTwoRegularLegacyScenario();

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>
                {
                    NotchAlgorithmVersion.V22,
                    NotchAlgorithmVersion.V21,
                },
                LinkVersionThresholds = true,
                ThresholdQ7 = 0,
            },
        };
        var progress = new CallbackProgress<NotchGenerationProgress>(snapshot =>
        {
            if (snapshot.ProcessedCount == 1)
            {
                settings.Notch.ThresholdQ7 = 128;
            }
        });
        var generator = new NotchTableGenerator();

        var current = generator.Generate(cad, grid, settings, progress);

        Assert.Equal(
            new[]
            {
                NotchAlgorithmVersion.V21,
                NotchAlgorithmVersion.V22,
                NotchAlgorithmVersion.V21,
                NotchAlgorithmVersion.V22,
            },
            current.Rows.Select(static row => row.Version));
        Assert.Empty(generator.Generate(cad, grid, settings).Rows);
    }

    [Fact]
    public void Generate_V21_Adds_Row_When_Cad_Matched()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);

        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = cad.Pads[0].Id;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Single(table.Rows);
        Assert.Equal(NotchAlgorithmVersion.V21, table.Rows[0].Version);
    }

    [Fact]
    public void Generate_V21_YWay_PreservesLegacyNeighborOrientation()
    {
        var grid = CreateGrid(rows: 2, cols: 1);
        var cad = CreateCadWithBounds(0, 5, 10, 15); // centered across the row boundary

        var top = grid.Pads.Single(pad => pad.Row == 0);
        var bottom = grid.Pads.Single(pad => pad.Row == 1);
        top.Freeform = FreeformType.YWay;
        bottom.Freeform = FreeformType.YWay;
        top.MatchedCadPadId = cad.Pads[0].Id;
        bottom.MatchedCadPadId = cad.Pads[0].Id;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var rows = new NotchTableGenerator().Generate(cad, grid, settings).Rows;
        var topRow = Assert.Single(rows, row => row.DiffIndex == top.DiffIndex);
        var bottomRow = Assert.Single(rows, row => row.DiffIndex == bottom.DiffIndex);

        Assert.Equal(bottom.DiffIndex, topRow.Values[3]);
        Assert.Equal(top.DiffIndex, bottomRow.Values[6]);
    }

    [Fact]
    public void Generate_V21_EncodesNeighborLegMagnitudeAsQ7()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 19, 10); // extends 90% into the right neighbor
        var left = grid.Pads.Single(pad => pad.Row == 0 && pad.Col == 0);
        left.Freeform = FreeformType.XWay;
        left.MatchedCadPadId = cad.Pads[0].Id;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var row = Assert.Single(new NotchTableGenerator().Generate(cad, grid, settings).Rows);

        Assert.Equal(grid.Pads[1].DiffIndex, row.Values[6]);
        Assert.Equal(1, row.Values[7]);
        Assert.Equal(115, row.Values[8]); // Round(0.9 * 128)
    }

    [Fact]
    public void Generate_Skips_When_Cad_Not_Matched()
    {
        var grid = CreateGrid();
        var cad = CreateCad(grid);

        var reg = grid.Pads[0];
        reg.Freeform = FreeformType.XWay;
        reg.MatchedCadPadId = null;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public void Generate_V21_WorksFromOverlapMatchAndAutoDetectPipeline()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 20, 10); // exactly spans two regular pads on X axis

        var matchSettings = new MatchingSettings
        {
            Mode = MatchMode.LegacyOverlap,
            MatchThreshold = 0.35,
            FreeformAxisThreshold = 0.2,
            EnableAutoDetectXy = false
        };

        var matchResult = PadMatcher.Match(cad, grid);
        TestInfrastructure.FreeformDetectorTestSupport.AutoTag(cad, grid, matchSettings, matchResult);

        var settings = new ProjectSettings
        {
            Matching = matchSettings,
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V21 }
            }
        };

        var generator = new NotchTableGenerator();
        var table = generator.Generate(cad, grid, settings);

        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(NotchAlgorithmVersion.V21, row.Version));
        Assert.All(table.Rows, row => Assert.Equal(cad.Pads[0].Id, row.CadPadId));

        var diffSet = table.Rows.Select(r => r.DiffIndex).OrderBy(v => v).ToArray();
        Assert.Equal(_expectedLegacyDiffSet, diffSet);
        Assert.All(table.Rows, row => Assert.Equal(row.DiffIndex, row.Values[0]));
    }

    [Fact]
    public void Generate_LegacyRegularAnchor_V22_PreservesCompatibilityNeighborOrientation()
    {
        var grid = CreateGrid(rows: 2, cols: 1);
        var cad = CreateCadWithBounds(0, 5, 10, 15);

        var top = grid.Pads.Single(pad => pad.Row == 0);
        var bottom = grid.Pads.Single(pad => pad.Row == 1);
        top.Freeform = FreeformType.YWay;
        bottom.Freeform = FreeformType.YWay;
        top.MatchedCadPadId = cad.Pads[0].Id;
        bottom.MatchedCadPadId = cad.Pads[0].Id;
        top.MatchScore = 1.0;
        bottom.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 }
            }
        };

        var rows = new NotchTableGenerator().Generate(cad, grid, settings).Rows;
        var topRow = Assert.Single(rows, row => row.DiffIndex == top.DiffIndex);
        var bottomRow = Assert.Single(rows, row => row.DiffIndex == bottom.DiffIndex);

        Assert.All(rows, row => Assert.Equal(9, row.Values.Length));
        Assert.Equal(settings.Notch.NullValue, topRow.Values[3]);
        Assert.Equal(bottom.DiffIndex, topRow.Values[6]);
        Assert.Equal(top.DiffIndex, bottomRow.Values[3]);
        Assert.Equal(settings.Notch.NullValue, bottomRow.Values[6]);
    }
}
