// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// The generated v2.1 C file scales a Q7 ratio without a right shift of a signed operand (MISRA C:2012 Rule 10.1).
/// </summary>
public sealed class NotchExportQ7ScaleTests
{
    private const int NullDiff = 65534;
    private const string ScaleFunction = "static INT32 FUNC_NHC_ScaleQ7Floor(INT32 s32Product)";

    // The product of an INT16 source and a UINT8 ratio lies in [-8355840, 8355585].
    private const int LowestProduct = -8_355_840;
    private const int HighestProduct = 8_355_585;
    private const int AddType = 1;
    private const int SubType = 2;
    private const int NoTarget = 65535;

    private static readonly int[] _gridDiffIndices = [0];
    private static readonly int[] _v21Values = [0, 100, 110, 1, 1, 32, 2, 1, 32];
    private static readonly int[] _v22Values = [0, 110, 1, 25, 2, 25, 0];
    private static readonly int[] _probeProducts =
        [LowestProduct, -255, -129, -128, -127, -1, 0, 1, 127, 128, 255, HighestProduct];

    private static readonly int[] _legTypes = [AddType, SubType];

    // Source and ratio of the first leg, then of the second leg.
    private static readonly (int FirstSource, int FirstRatio, int SecondSource, int SecondRatio)[] _legCases =
    [
        (-1, 1, -1, 1),
        (-32768, 255, -32767, 255),
        (-128, 1, -129, 1),
        (-127, 127, 127, 127),
        (32767, 255, -32768, 255),
        (5, 100, -5, 100),
    ];

    [Fact]
    public void V21Export_UsesTheScaleFunctionAndHasNoShiftOrDeviationMarker()
    {
        var text = Export(NotchAlgorithmVersion.V21);

        Assert.DoesNotContain(">>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NHC-DEV-01", text, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(text, ScaleFunction));
        Assert.Equal(4, CountOccurrences(text, "FUNC_NHC_ScaleQ7Floor(((INT32)"));
        Assert.Equal(4, CountOccurrences(text, "(INT16)FUNC_NHC_ScaleQ7Floor("));
    }

    [Fact]
    public void V22Export_HasNoScaleFunctionAndNoShift()
    {
        var text = Export(NotchAlgorithmVersion.V22);

        Assert.DoesNotContain("FUNC_NHC_ScaleQ7Floor", text, StringComparison.Ordinal);
        Assert.DoesNotContain(">>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportedScaleFunction_RunWithGcc_FloorsEveryReachableProduct()
    {
        if (!NotchTableExporterTests.IsGccAvailable())
        {
            return;
        }

        var lines = new List<string>
        {
            "#include <stdio.h>",
            "int main(void)",
            "{",
            "    INT32 s32Mismatches = 0;",
            "    INT32 s32Product;",
            FormattableString.Invariant($$"""    for (s32Product = {{LowestProduct}}; s32Product <= {{HighestProduct}}; s32Product++)"""),
            "    {",
            "        INT32 s32Floor = s32Product / 128;",
            "        if ((s32Product % 128) < 0)",
            "        {",
            "            s32Floor--;",
            "        }",
            "        if (FUNC_NHC_ScaleQ7Floor(s32Product) != s32Floor)",
            "        {",
            "            s32Mismatches++;",
            "        }",
            "    }",
            """    printf("%d ", s32Mismatches);""",
        };
        lines.AddRange(_probeProducts.Select(static product =>
            FormattableString.Invariant($$"""    printf("%d ", FUNC_NHC_ScaleQ7Floor({{product}}));""")));
        lines.Add("    return 0;");
        lines.Add("}");
        var harness = string.Concat(
            NotchTableExporterTests.CreateGccHarnessSource(Export(NotchAlgorithmVersion.V21)),
            Environment.NewLine,
            string.Join(Environment.NewLine, lines),
            Environment.NewLine);

        var output = NotchTableExporterTests.RunCProgramWithGcc(harness);

        Assert.Equal(0d, output[0]);
        Assert.Equal(_probeProducts.Select(static product => Math.Floor(product / 128d)), output.Skip(1));
    }

    [Fact]
    public void ExportedCompensation_RunWithGcc_FloorsNegativeSourcesForBothLegsAndBothSigns()
    {
        if (!NotchTableExporterTests.IsGccAvailable())
        {
            return;
        }

        var scenarios = CreateScenarios();
        var grid = TestGeometryFactory.CreateLinearRegularGrid(Enumerable.Range(0, scenarios.Length * 3).ToArray());
        var table = new NotchTable(scenarios.SelectMany(static scenario => scenario.CreateRows()).ToArray());
        var generatedSource = NotchTableExporter.ExportAsCInitializer(
            table, CreateCad(), grid, CreateSettings(), NotchExportProfile.Debug);
        var lines = new List<string> { "#include <stdio.h>", "int main(void)", "{" };
        lines.AddRange(scenarios.SelectMany(static scenario => scenario.LoadLines()));
        lines.Add("    FUNC_NHC_DiffCompensationByIc(0u);");
        lines.AddRange(scenarios.Select(static scenario =>
            FormattableString.Invariant($$"""    printf("%d ", g_stNhcDiffAfter.as16Buf[{{scenario.TargetIndex}}]);""")));
        lines.Add("    return 0;");
        lines.Add("}");
        var harness = string.Concat(
            NotchTableExporterTests.CreateGccHarnessSource(generatedSource),
            Environment.NewLine,
            string.Join(Environment.NewLine, lines),
            Environment.NewLine);

        var firmware = NotchTableExporterTests.RunCProgramWithGcc(harness);

        Assert.Equal(scenarios.Select(static scenario => (double)scenario.ExpectedTarget()), firmware);
    }

    private static Scenario[] CreateScenarios() =>
        _legTypes
            .SelectMany(static first => _legTypes.Select(second => (first, second)))
            .SelectMany(static types => _legCases.Select(legCase => (types, legCase)))
            .Select(static (item, index) => new Scenario(index, item.types.first, item.types.second, item.legCase))
            .ToArray();

    private static CadPadSet CreateCad() =>
        new([TestGeometryFactory.CreateCadPad(42, "PAD", 0, 0, 1, 1)]);

    private static ProjectSettings CreateSettings() => new()
    {
        Notch = new NotchSettings { NullValue = NullDiff, ComputationMode = NotchComputationMode.CadAllocation },
    };

    private static string Export(NotchAlgorithmVersion version)
    {
        var values = version == NotchAlgorithmVersion.V21 ? _v21Values : _v22Values;
        var row = new NotchTableRow(version, 0, 17, 0, 42, values, "q7");
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_gridDiffIndices, _ => 0);
        return NotchTableExporter.ExportAsCInitializer(
            new NotchTable([row]), CreateCad(), grid, CreateSettings(), NotchExportProfile.Debug);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    // One destination cell that receives two legs from the two cells before it.
    private sealed record Scenario(
        int Index,
        int FirstType,
        int SecondType,
        (int FirstSource, int FirstRatio, int SecondSource, int SecondRatio) Legs)
    {
        public int FirstIndex => Index * 3;

        public int SecondIndex => (Index * 3) + 1;

        public int TargetIndex => (Index * 3) + 2;

        public NotchTableRow[] CreateRows() =>
        [
            CreateRow(FirstIndex, FirstType, Legs.FirstRatio),
            CreateRow(SecondIndex, SecondType, Legs.SecondRatio),
        ];

        public IEnumerable<string> LoadLines() =>
        [
            FormattableString.Invariant($"    g_stNhcDiffAfter.as16Buf[{FirstIndex}] = {Legs.FirstSource};"),
            FormattableString.Invariant($"    g_stNhcDiffAfter.as16Buf[{SecondIndex}] = {Legs.SecondSource};"),
            FormattableString.Invariant($"    g_stNhcDiffAfter.as16Buf[{TargetIndex}] = 0;"),
        ];

        // The firmware narrows each leg and the running sum to INT16; gcc wraps like an unchecked cast.
        public short ExpectedTarget()
        {
            var sum = ApplyLeg(0, FirstType, Legs.FirstSource, Legs.FirstRatio);
            return ApplyLeg(sum, SecondType, Legs.SecondSource, Legs.SecondRatio);
        }

        private static short ApplyLeg(short sum, int type, int source, int ratio)
        {
            var leg = unchecked((short)(int)Math.Floor(source * ratio / 128d));
            return unchecked((short)(type == AddType ? sum + leg : sum - leg));
        }

        private NotchTableRow CreateRow(int sourceIndex, int type, int ratio) =>
            new(
                NotchAlgorithmVersion.V21,
                0,
                sourceIndex,
                sourceIndex,
                42,
                [sourceIndex, 100, 100, TargetIndex, type, ratio, NoTarget, 0, 0],
                FormattableString.Invariant($"leg {sourceIndex}"));
    }
}
