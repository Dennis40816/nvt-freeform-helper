using System.Globalization;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using Xunit;
using static FreeformHelper.Tests.NotchEdgeCaseHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Edge cases of the Notch CSV and C table export that the generated table must get right: zero rows,
/// one row, the largest IC and node counts the firmware types can address, boundary and clamped
/// values, every enum value, and very long comments. A gcc test compiles the generated file as strict
/// C99 (<c>-std=c99 -pedantic -Wall -Wextra -Werror</c>) against the notch.h stub, and reads the table
/// back from the compiled object, so the assertions are on the stored data and not only on the text.
/// Inputs that the exporter takes without checking are pinned in
/// <see cref="NotchTableExporterEdgeCaseCharacterizationTests"/>.
/// </summary>
public sealed class NotchTableExporterEdgeCaseTests
{
    private const string CsvHeader =
        "row_number,version,version_code,ic_index,ic_number,fw_diff_idx,regular_pad_id,cad_pad_id,payload_width,payload_01,payload_02,payload_03,payload_04,payload_05,payload_06,payload_07,payload_08,payload_09,comment";

    private static readonly int[] _combineBoundaries =
        [int.MinValue, -1000, -1, 0, 1, 99, 100, 101, 254, 255, 256, 1000, int.MaxValue];

    private static readonly int[] _ratioBoundaries =
        [int.MinValue, -101, -100, -99, -1, 0, 1, 99, 100, 101, int.MaxValue];

    private static readonly string[] _noTypeLimits = ["-Wno-type-limits"];
    private static readonly int[] _v21Types = [0, 1, 2];
    private static readonly int[] _v21RegularBoundaries = [int.MinValue, -1, 0, 1, 254, 255, 256, int.MaxValue];
    private static readonly int[] _v21Q7Boundaries = [int.MinValue, -1, 0, 1, 127, 128, 254, 255, 256, int.MaxValue];
    private static readonly int[] _v21RoundTripDiffs = [10, 20];
    private static readonly int[] _v21RoundTripBefore = [100, 0];
    private static readonly int[] _v22SingleRowDiffs = [10, 20];
    private static readonly int[] _v22SingleRowBefore = [100, 0];
    private static readonly int[] _v22MaxDiffs = [65534, 0, 65533];
    private static readonly int[] _v22MaxDiffBefore = [100, 0, 0];
    private static readonly int[] _v22MaxDiffExpected = [25, 50, 25];
    private static readonly int[] _icDispatchDiffs = [5, 7];
    private static readonly int[] _icDispatchBefore = [200, 200];
    private static readonly int[] _icDispatchExpected = [200, 100];
    private static readonly int[] _v22SingleRowExpected = [90, 30];
    private static readonly int[] _v21SingleRowExpected = [90, 29];

    public static TheoryData<NotchExportProfile> ExportProfiles => new(Enum.GetValues<NotchExportProfile>());

    public static TheoryData<NotchAlgorithmVersion, NotchExportProfile, NotchComputationMode, NotchCompensationModel> EveryEnumCombination()
    {
        var data = new TheoryData<NotchAlgorithmVersion, NotchExportProfile, NotchComputationMode, NotchCompensationModel>();
        foreach (var version in Enum.GetValues<NotchAlgorithmVersion>())
        {
            foreach (var profile in Enum.GetValues<NotchExportProfile>())
            {
                foreach (var mode in Enum.GetValues<NotchComputationMode>())
                {
                    foreach (var model in Enum.GetValues<NotchCompensationModel>())
                    {
                        data.Add(version, profile, mode, model);
                    }
                }
            }
        }

        return data;
    }

    // ---- zero rows -------------------------------------------------------------------------

    [Fact]
    public void ExportAsCsv_ZeroRows_EmitsOnlyTheHeader()
    {
        var csv = NotchTableExporter.ExportAsCsv(new NotchTable([]));

        Assert.Equal(CsvHeader + Environment.NewLine, csv);
    }

    [Theory]
    [MemberData(nameof(ExportProfiles))]
    public void ExportAsCInitializer_ZeroRows_EmitsTheEmptyMarkerOnly(NotchExportProfile profile)
    {
        var text = ExportC(new NotchTable([]), profile);

        Assert.Equal("// Notch table is empty.\n", text);
    }

    [Fact]
    public void ExportAsCInitializer_ZeroRows_CompilesAsC99()
    {
        RequireGcc();

        var result = CompileC99("#include \"notch.h\"\n" + ExportC(new NotchTable([])));

        Assert.True(result.ExitCode == 0, result.StdErr);
    }

    [Fact]
    public void Export_RowsOfNoSupportedVersion_AreNamedInCsvAndLeftOutOfC()
    {
        var unsupported = (NotchAlgorithmVersion)99;
        var table = new NotchTable([
            new NotchTableRow(unsupported, 0, 4, 4, 7, [1, 2, 3], "unsupported")
        ]);

        var csvRow = Lines(NotchTableExporter.ExportAsCsv(table))[1];

        Assert.Equal("1,99,99,0,1,4,4,7,3,1,2,3,,,,,,,\"unsupported\"", csvRow);
        Assert.Equal("// Notch table contains no supported v2.1/v2.2 rows.\n", ExportC(table));
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void ExportAsCInitializer_OnlyRowsOfAnInvalidIc_EmitsAnEmptyCompilableTable(NotchAlgorithmVersion version)
    {
        var row = version == NotchAlgorithmVersion.V22
            ? V22Row(-1, 10, 120, 20, 30, NullDiff, 0, 0, "dropped")
            : V21Row(-1, 10, [10, 100, 100, 20, 1, 38, NullDiff, 0, 0], "dropped");

        var text = ExportC(new NotchTable([row]));

        Assert.Contains("#define USER_NHC_IC_NUM          (1u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM        (0u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_MAX    (1u)", text, StringComparison.Ordinal);
        Assert.Contains("{ NULL, USER_NHC_NODE_NUM_IC1 }, // IC1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped", text, StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            Assert.Empty(version == NotchAlgorithmVersion.V22 ? DumpV22Table(text) : DumpV21Table(text));
        }
    }

    // ---- one row ---------------------------------------------------------------------------

    [Fact]
    public void ExportAsCsv_OneRow_EmitsTheExactRow()
    {
        var table = new NotchTable([V22Row(0, 5, 100, NullDiff, 0, NullDiff, 0, 0, "only")]);

        var lines = Lines(NotchTableExporter.ExportAsCsv(table));

        Assert.Equal(CsvHeader, lines[0]);
        Assert.Equal("1,2.2,30,0,1,5,5,1,7,5,100,65535,0,65535,0,0,,,\"only\"", lines[1]);
        Assert.Equal(string.Empty, lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Theory]
    [MemberData(nameof(ExportProfiles))]
    public void ExportAsCInitializer_OneV22Row_IsExact(NotchExportProfile profile)
    {
        var table = new NotchTable([V22Row(0, 10, 120, 20, 30, NullDiff, 0, 0, "only")]);

        var text = ExportC(table, profile);

        Assert.Contains("#define USER_NHC_IC_NUM          (1u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM        (1u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_MAX    (1u)", text, StringComparison.Ordinal);
        Assert.Contains("#define NHC_DIFF_NONE            (0xFFFFu)", text, StringComparison.Ordinal);
        Assert.Equal(
            "    {    10, 120,            20,   30, NHC_DIFF_NONE,    0, NHC_FLAG_NONE }, // only; NODE KEEP=90% MOVE=30%",
            TableLine(text, "// only;"));
        if (IsGccAvailable)
        {
            Assert.Equal([[0, 10, 120, 20, 30, NullDiff, 0, 0]], DumpV22Table(text));
            Assert.Equal(
                _v22SingleRowExpected,
                RunCompensation(text, 0, _v22SingleRowDiffs, _v22SingleRowBefore));
        }
    }

    [Fact]
    public void ExportAsCInitializer_OneV21LegacyRow_IsExact()
    {
        var table = new NotchTable([V21Row(0, 10, [10, 100, 120, 20, 1, 38, NullDiff, 0, 0], "only")]);

        var text = ExportC(table, settings: CreateSettings(computationMode: NotchComputationMode.LegacyRegularAnchor));

        Assert.Contains("#define USER_NHC_NODE_NUM        (1u)", text, StringComparison.Ordinal);
        Assert.Equal(
            "    {    10, 100, 120,            20, NHC_TYPE_ADD,   38, NHC_DIFF_NONE, NHC_TYPE_NONE,    0 }, // only",
            TableLine(text, "// only"));
        if (IsGccAvailable)
        {
            Assert.Equal([[0, 10, 100, 120, 20, 1, 38, NullDiff, 0, 0]], DumpV21Table(text));
        }
    }

    [Fact]
    public void ExportAsCInitializer_OneV21CadAllocationRow_SplitsIntoSelfAndLegNodes()
    {
        // 120% combine with one +38/128 leg: the leg is 30%, so the source keeps 90% (SUB 13/128).
        var table = new NotchTable([V21Row(0, 10, [10, 100, 120, 20, 1, 38, NullDiff, 0, 0], "only")]);

        var text = ExportC(table);

        Assert.Contains("#define USER_NHC_NODE_NUM        (2u)", text, StringComparison.Ordinal);
        Assert.Equal(
            "    {    10, 100, 100,            10, NHC_TYPE_SUB,   13, NHC_DIFF_NONE, NHC_TYPE_NONE,    0 }, // SELF only srcD=10",
            TableLine(text, "// SELF only"));
        Assert.Equal(
            "    {    20, 100, 100,            10, NHC_TYPE_ADD,   38, NHC_DIFF_NONE, NHC_TYPE_NONE,    0 }, // LEG only srcD=10",
            TableLine(text, "// LEG only"));
        if (IsGccAvailable)
        {
            Assert.Equal(
                [[0, 10, 100, 100, 10, 2, 13, NullDiff, 0, 0], [0, 20, 100, 100, 10, 1, 38, NullDiff, 0, 0]],
                DumpV21Table(text));
            Assert.Equal(_v21SingleRowExpected, RunCompensation(text, 0, _v21RoundTripDiffs, _v21RoundTripBefore));
        }
    }

    // ---- largest counts the firmware types can address -------------------------------------

    [Fact]
    public void ExportAsCInitializer_IcIndex254_CompilesAsStrictC99WithAllIcsReachableThroughUint8()
    {
        RequireGcc();
        var table = new NotchTable([
            V22Row(0, 5, 100, NullDiff, 0, NullDiff, 0, 0, "first-ic"),
            V22Row(254, 7, 50, NullDiff, 0, NullDiff, 0, 0, "last-ic"),
        ]);

        var text = ExportC(table);

        Assert.Contains("#define USER_NHC_IC_NUM          (255u)", text, StringComparison.Ordinal);
        Assert.Equal(
            [[0, 5, 100, NullDiff, 0, NullDiff, 0, 0], [254, 7, 50, NullDiff, 0, NullDiff, 0, 0]],
            DumpV22Table(text));
        Assert.Equal(_icDispatchExpected, RunCompensation(text, 254, _icDispatchDiffs, _icDispatchBefore));
    }

    /// <summary>
    /// IC index 255 is the last one a UINT8 <c>u8Ic</c> can address. The table data is exact; the strict
    /// compile of that file is pinned in the characterization tests, hence <c>-Wno-type-limits</c> here.
    /// </summary>
    [Fact]
    public void ExportAsCInitializer_IcIndex255_AddressesAll256IcsThroughUint8()
    {
        var table = new NotchTable([
            V22Row(0, 5, 100, NullDiff, 0, NullDiff, 0, 0, "first-ic"),
            V22Row(255, 7, 50, NullDiff, 0, NullDiff, 0, 0, "last-ic"),
        ]);

        var text = ExportC(table);
        var emptyDispatchEntries = Lines(text).Count(static line => line.StartsWith("    { NULL,", StringComparison.Ordinal));

        Assert.Contains("#define USER_NHC_IC_NUM          (256u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (1u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC256 (1u)", text, StringComparison.Ordinal);
        Assert.Equal(254, emptyDispatchEntries);
        Assert.Contains("    { castNHC_TABLE_IC256, USER_NHC_NODE_NUM_IC256 }, // IC256", text, StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            Assert.Equal(
                [[0, 5, 100, NullDiff, 0, NullDiff, 0, 0], [255, 7, 50, NullDiff, 0, NullDiff, 0, 0]],
                DumpV22Table(text, _noTypeLimits));
            Assert.Equal(
                _icDispatchExpected,
                RunCompensation(text, 255, _icDispatchDiffs, _icDispatchBefore, _noTypeLimits));
        }
    }

    [Fact]
    public void ExportAsCInitializer_65535NodesInOneIc_FitsTheUint16NodeCount()
    {
        RequireGcc();
        var rows = Enumerable.Range(0, ushort.MaxValue)
            .Select(static index => V22Row(0, index, 100, NullDiff, 0, NullDiff, 0, 0, "n"))
            .ToArray();

        var text = ExportC(new NotchTable(rows));
        var dump = DumpV22Table(text);

        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (65535u)", text, StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_MAX    (65535u)", text, StringComparison.Ordinal);
        Assert.Equal(ushort.MaxValue, dump.Length);
        Assert.Equal([0, 0, 100, NullDiff, 0, NullDiff, 0, 0], dump[0]);
        Assert.Equal([0, ushort.MaxValue - 1, 100, NullDiff, 0, NullDiff, 0, 0], dump[^1]);
    }

    [Fact]
    public void ExportAsCInitializer_LargestDiffIndexBelowTheNullValue_IsStoredAndApplied()
    {
        var table = new NotchTable([V22Row(0, 65534, 100, 0, 50, 65533, 25, 0, "max-diff")]);

        var text = ExportC(table);

        Assert.Equal(
            "    { 65534, 100,             0,   50,         65533,   25, NHC_FLAG_NONE }, // max-diff; NODE KEEP=25% MOVE=75%",
            TableLine(text, "// max-diff;"));
        if (IsGccAvailable)
        {
            Assert.Equal([[0, 65534, 100, 0, 50, 65533, 25, 0]], DumpV22Table(text));
            Assert.Equal(_v22MaxDiffExpected, RunCompensation(text, 0, _v22MaxDiffs, _v22MaxDiffBefore));
        }
    }

    // ---- boundary and clamped values -------------------------------------------------------

    [Fact]
    public void ExportAsCInitializer_V22CombineAndRatioBoundaries_AreClampedToTheFieldRange()
    {
        RequireGcc();
        var rows = new List<NotchTableRow>();
        var expected = new List<int[]>();
        foreach (var combine in _combineBoundaries)
        {
            foreach (var ratio in _ratioBoundaries)
            {
                var anchor = rows.Count;
                rows.Add(V22Row(0, anchor, combine, 100, ratio, 200, ratio, 0, $"c{anchor:D3}"));
                var clampedRatio = Math.Clamp(ratio, -100, 100);
                expected.Add([0, anchor, Math.Clamp(combine, 0, 255), 100, clampedRatio, 200, clampedRatio, 0]);
            }
        }

        var text = ExportC(new NotchTable(rows));

        Assert.Equal(expected, DumpV22Table(text));
    }

    [Fact]
    public void ExportAsCInitializer_V22ExtremeInput_WritesTheClampedLiteralsAndMatchingKeepMove()
    {
        var table = new NotchTable([V22Row(0, 10, int.MaxValue, 20, int.MaxValue, 30, int.MinValue, 0, "ext")]);

        var line = TableLine(ExportC(table), "// ext;");

        Assert.Equal(
            "    {    10, 255,            20,  100,            30, -100, NHC_FLAG_NONE }, // ext; NODE KEEP=255% MOVE=0%",
            line);
    }

    /// <summary>
    /// The exporter clamps combine to [0, 255] and ratios to [-100, 100] before it writes the UINT8 and
    /// INT8 fields. The C# simulation must apply the same clamp, so it and the compiled firmware agree.
    /// </summary>
    [Theory]
    [InlineData(300, 50, 205, 50)]
    [InlineData(150, 150, 50, 100)]
    [InlineData(-50, 0, 0, 0)]
    [InlineData(100, -150, 200, -100)]
    public void ExportAndSimulation_ClampedV22Input_GiveTheSameCompensatedValues(
        int combine,
        int ratio,
        int expectedSource,
        int expectedTarget)
    {
        RequireGcc();
        int[] diffs = [10, 11];
        var table = new NotchTable([V22Row(0, 10, combine, 11, ratio, NullDiff, 0, 0, "clamp")]);

        var simulated = Simulate(table, NotchAlgorithmVersion.V22, diffs, [100d, 0d]);
        var firmware = RunCompensation(ExportC(table), 0, diffs, [100, 0]);

        Assert.Equal([expectedSource, expectedTarget], firmware);
        Assert.Equal([(double)expectedSource, expectedTarget], simulated);
    }

    [Fact]
    public void ExportAsCInitializer_ReleaseWithOnlyNoOpV22Rows_EmitsAnEmptyCompilableTable()
    {
        var table = new NotchTable([V22Row(0, 10, 100, NullDiff, 0, NullDiff, 0, 0, "no-op")]);

        var text = ExportC(table, NotchExportProfile.Release);

        Assert.Contains("#define USER_NHC_NODE_NUM        (0u)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("no-op", text, StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            Assert.Empty(DumpV22Table(text));
        }
    }

    [Fact]
    public void ExportAsCInitializer_V22EveryFlagByte_IsWrittenAsItsNamedBits()
    {
        var rows = Enumerable.Range(0, 256)
            .Select(static flags => V22Row(0, flags, 100, NullDiff, 0, NullDiff, 0, flags, $"flag-{flags:D3}"))
            .ToArray();

        var text = ExportC(new NotchTable(rows));

        for (var flags = 0; flags < 256; flags++)
        {
            Assert.Contains(
                $", {ExpectedFlagLiteral(flags)} }}, // flag-{flags:D3};",
                TableLine(text, $"// flag-{flags:D3};"),
                StringComparison.Ordinal);
        }

        if (IsGccAvailable)
        {
            Assert.Equal(
                Enumerable.Range(0, 256).Select(static flags => new[] { 0, flags, 100, NullDiff, 0, NullDiff, 0, flags }),
                DumpV22Table(text));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32768)]
    [InlineData(65534)]
    [InlineData(65535)]
    public void ExportAsCInitializer_NullValueBoundaries_AreWrittenAsTheNoTargetLiteral(int nullValue)
    {
        var realTarget = nullValue == 7 ? 8 : 7;
        var table = new NotchTable([V22Row(0, 3, 100, nullValue, 0, realTarget, 40, 0, "null-edge")]);

        var text = ExportC(table, settings: CreateSettings(nullValue));

        Assert.Contains(
            $"#define NHC_DIFF_NONE            (0x{nullValue:X4}u)",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            $"{{     3, 100, NHC_DIFF_NONE,    0,{realTarget,14},   40, NHC_FLAG_NONE }}, // null-edge;",
            text,
            StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            Assert.Equal([[0, 3, 100, nullValue, 0, realTarget, 40, 0]], DumpV22Table(text));
        }
    }

    [Fact]
    public void ExportAsCInitializer_V21LegacyBoundaries_AreClampedAndTypesNormalized()
    {
        RequireGcc();
        var rows = new List<NotchTableRow>();
        var expected = new List<int[]>();
        foreach (var percent in _v21RegularBoundaries)
        {
            foreach (var q7 in _v21Q7Boundaries)
            {
                var destination = rows.Count;
                rows.Add(V21Row(0, destination, [destination, percent, percent, 40, 1, q7, 50, 2, q7], $"b{destination:D3}"));
                var clampedPercent = Math.Clamp(percent, 0, 255);
                var clampedQ7 = Math.Clamp(q7, 0, 255);
                expected.Add([0, destination, clampedPercent, clampedPercent, 40, 1, clampedQ7, 50, 2, clampedQ7]);
            }
        }

        var text = ExportC(
            new NotchTable(rows),
            settings: CreateSettings(computationMode: NotchComputationMode.LegacyRegularAnchor));

        Assert.Equal(expected, DumpV21Table(text));
    }

    [Fact]
    public void ExportAsCInitializer_V21LegacyBoundaries_OutOfRangeTypesBecomeNone()
    {
        RequireGcc();
        int[] types = [int.MinValue, -1, 0, 1, 2, 3, 255, int.MaxValue];
        var rows = types
            .Select(static (type, index) => V21Row(0, index, [index, 100, 100, 40, type, 50, 60, type, 70], $"t{index}"))
            .ToArray();

        var dump = DumpV21Table(ExportC(
            new NotchTable(rows),
            settings: CreateSettings(computationMode: NotchComputationMode.LegacyRegularAnchor)));

        var normalized = types.Select(static type => type is 1 or 2 ? type : 0).ToArray();
        Assert.Equal(
            normalized.Select(static (type, index) => new[] { 0, index, 100, 100, 40, type, 50, 60, type, 70 }),
            dump);
    }

    [Fact]
    public void ExportAsCInitializer_V21EveryTypeInBothSlots_IsWrittenAsItsNamedConstant()
    {
        var rows = new List<NotchTableRow>();
        foreach (var first in _v21Types)
        {
            foreach (var second in _v21Types)
            {
                var destination = rows.Count;
                rows.Add(V21Row(0, destination, [destination, 100, 100, 40, first, 50, 60, second, 70], $"slot{destination}"));
            }
        }

        var text = ExportC(
            new NotchTable(rows),
            settings: CreateSettings(computationMode: NotchComputationMode.LegacyRegularAnchor));

        string[] names = ["NHC_TYPE_NONE", "NHC_TYPE_ADD", "NHC_TYPE_SUB"];
        for (var index = 0; index < rows.Count; index++)
        {
            var line = TableLine(text, $"// slot{index}");
            Assert.Contains(
                $", {names[index / 3]},   50,            60, {names[index % 3]},   70 }}",
                line,
                StringComparison.Ordinal);
        }

        if (IsGccAvailable)
        {
            Assert.Equal(
                Enumerable.Range(0, 9).Select(static index => new[] { 0, index, 100, 100, 40, index / 3, 50, 60, index % 3, 70 }),
                DumpV21Table(text));
        }
    }

    [Fact]
    public void ExportAsCInitializer_V21CadAllocationBoundaries_FollowTheQ7AndPercentRules()
    {
        RequireGcc();

        // combine above 255 saturates, so the source gains 155% (ADD 198/128).
        int[][] saturated =
        [
            [0, 10, 100, 100, 10, 1, 198, NullDiff, 0, 0],
        ];
        Assert.Equal(saturated, DumpCad([10, 100, 255, 20, 1, 0, NullDiff, 0, 0]));
        Assert.Equal(saturated, DumpCad([10, 100, 256, 20, 1, 0, NullDiff, 0, 0]));
        Assert.Equal(saturated, DumpCad([10, 100, int.MaxValue, 20, 1, 0, NullDiff, 0, 0]));

        // combine below 0 floors at 0, so the source gives up everything (SUB 128/128).
        int[][] floored =
        [
            [0, 10, 100, 100, 10, 2, 128, NullDiff, 0, 0],
        ];
        Assert.Equal(floored, DumpCad([10, 100, -1, 20, 1, 0, NullDiff, 0, 0]));
        Assert.Equal(floored, DumpCad([10, 100, int.MinValue, 20, 1, 0, NullDiff, 0, 0]));

        // magnitude 256 and above saturates at 255 (199%), so the source keeps nothing.
        int[][] oversizeAdd =
        [
            [0, 10, 100, 100, 10, 2, 128, NullDiff, 0, 0],
            [0, 20, 100, 100, 10, 1, 255, NullDiff, 0, 0],
        ];
        Assert.Equal(oversizeAdd, DumpCad([10, 100, 100, 20, 1, 256, NullDiff, 0, 0]));
        Assert.Equal(oversizeAdd, DumpCad([10, 100, 100, 20, 1, int.MaxValue, NullDiff, 0, 0]));

        // a SUB leg of 199% returns that share to the source (ADD 255/128).
        Assert.Equal(
            [
                [0, 10, 100, 100, 10, 1, 255, NullDiff, 0, 0],
                [0, 20, 100, 100, 10, 2, 255, NullDiff, 0, 0],
            ],
            DumpCad([10, 100, 100, 20, 2, 256, NullDiff, 0, 0]));

        // a negative magnitude floors at 0, so the leg has no effect and no node is written.
        Assert.Empty(DumpCad([10, 100, 100, 20, 1, -1, NullDiff, 0, 0]));

        // types other than ADD and SUB drop the leg.
        Assert.Empty(DumpCad([10, 100, 100, 20, 3, 50, NullDiff, 0, 0]));
        Assert.Empty(DumpCad([10, 100, 100, 20, -1, 50, NullDiff, 0, 0]));
        Assert.Empty(DumpCad([10, 100, 100, 20, 255, 50, NullDiff, 0, 0]));
    }

    // ---- every enum value ------------------------------------------------------------------

    [Fact]
    public void ExportAsCsv_EveryAlgorithmVersion_IsLabelledAndCoded()
    {
        var expected = new Dictionary<NotchAlgorithmVersion, (string Label, string Code)>
        {
            [NotchAlgorithmVersion.V21] = ("2.1", "21"),
            [NotchAlgorithmVersion.V22] = ("2.2", "30"),
        };

        foreach (var version in Enum.GetValues<NotchAlgorithmVersion>())
        {
            var (label, code) = Assert.Contains(version, expected);
            var table = new NotchTable([new NotchTableRow(version, 0, 4, 4, 7, [4], "enum")]);

            var columns = Lines(NotchTableExporter.ExportAsCsv(table))[1].Split(',');

            Assert.Equal(label, columns[1]);
            Assert.Equal(code, columns[2]);
        }
    }

    [Theory]
    [MemberData(nameof(EveryEnumCombination))]
    public void ExportAsCInitializer_EveryEnumCombination_CompilesAsC99AndNamesTheSettings(
        NotchAlgorithmVersion version,
        NotchExportProfile profile,
        NotchComputationMode mode,
        NotchCompensationModel model)
    {
        var row = version == NotchAlgorithmVersion.V22
            ? V22Row(0, 10, 120, 20, 30, NullDiff, 0, 0, "enum")
            : V21Row(0, 10, [10, 100, 120, 20, 1, 38, NullDiff, 0, 0], "enum");

        var text = ExportC(new NotchTable([row]), profile, CreateSettings(computationMode: mode, compensationModel: model));

        Assert.Contains($"//   ExportProfile: {profile}", text, StringComparison.Ordinal);
        Assert.Contains($"//   ComputationMode: {mode}", text, StringComparison.Ordinal);
        Assert.Contains($"//   CompensationModel: {model}", text, StringComparison.Ordinal);
        var v21Section = version == NotchAlgorithmVersion.V21 ? "present" : "empty";
        var v22Section = version == NotchAlgorithmVersion.V22 ? "present" : "empty";
        Assert.Contains($"//   Sections: v2.1={v21Section}, v2.2={v22Section}", text, StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            var result = CompileC99(text);
            Assert.True(result.ExitCode == 0, result.StdErr);
        }
    }

    // ---- very long and awkward comments ----------------------------------------------------

    [Fact]
    public void Export_100000CharacterComment_StaysOnOneIntactLineInCsvAndC()
    {
        var comment = new string('x', 100_000);
        var v22 = new NotchTable([V22Row(0, 10, 120, 20, 30, NullDiff, 0, 0, comment)]);
        var v21 = new NotchTable([V21Row(0, 10, [10, 100, 120, 20, 1, 38, NullDiff, 0, 0], comment)]);

        var csvRow = Lines(NotchTableExporter.ExportAsCsv(v22))[1];
        var v22Text = ExportC(v22);
        var v21Text = ExportC(v21, settings: CreateSettings(computationMode: NotchComputationMode.LegacyRegularAnchor));

        Assert.EndsWith($",\"{comment}\"", csvRow, StringComparison.Ordinal);
        Assert.EndsWith($" }}, // {comment}; NODE KEEP=90% MOVE=30%", TableLine(v22Text, "// xxx"), StringComparison.Ordinal);
        Assert.EndsWith($" }}, // {comment}", TableLine(v21Text, "// xxx"), StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            Assert.Equal([[0, 10, 120, 20, 30, NullDiff, 0, 0]], DumpV22Table(v22Text));
            Assert.Equal([[0, 10, 100, 120, 20, 1, 38, NullDiff, 0, 0]], DumpV21Table(v21Text));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ExportAsCInitializer_BlankComment_IsWrittenAsADash(string comment)
    {
        var v22 = new NotchTable([V22Row(0, 10, 120, 20, 30, NullDiff, 0, 0, comment)]);
        var v21 = new NotchTable([V21Row(0, 10, [10, 100, 120, 20, 1, 38, NullDiff, 0, 0], comment)]);

        Assert.EndsWith(" }, // -; NODE KEEP=90% MOVE=30%", TableLine(ExportC(v22), "NODE KEEP"), StringComparison.Ordinal);
        Assert.EndsWith(
            " }, // -",
            TableLine(
                ExportC(v21, settings: CreateSettings(computationMode: NotchComputationMode.LegacyRegularAnchor)),
                "NHC_TYPE_ADD,   38"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCsv_CommentWithQuotesAndCommas_IsQuotedWithDoubledQuotes()
    {
        var table = new NotchTable([V22Row(0, 5, 100, NullDiff, 0, NullDiff, 0, 0, "a,\"b\",c")]);

        var csvRow = Lines(NotchTableExporter.ExportAsCsv(table))[1];

        Assert.EndsWith(",\"a,\"\"b\"\",c\"", csvRow, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCsv_NullCadPadIdAndExtremeValues_AreWrittenExactly()
    {
        var table = new NotchTable([
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: -1,
                diffIndex: int.MinValue,
                regularPadIndex: int.MaxValue,
                cadPadId: null,
                values: [int.MinValue, int.MaxValue, -1],
                comment: "extreme")
        ]);

        var csvRow = Lines(NotchTableExporter.ExportAsCsv(table))[1];

        Assert.Equal(
            string.Create(
                CultureInfo.InvariantCulture,
                $"1,2.2,30,-1,0,{int.MinValue},{int.MaxValue},,3,{int.MinValue},{int.MaxValue},-1,,,,,,,\"extreme\""),
            csvRow);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static int[][] DumpCad(int[] values)
    {
        var table = new NotchTable([V21Row(0, values[0], values, "cad")]);
        return DumpV21Table(ExportC(table));
    }

    private static string ExpectedFlagLiteral(int flags)
    {
        if (flags == 0)
        {
            return "NHC_FLAG_NONE";
        }

        var parts = new List<string>();
        if ((flags & 1) != 0)
        {
            parts.Add("NHC_FLAG_CONTINUATION");
        }

        if ((flags & 2) != 0)
        {
            parts.Add("NHC_FLAG_ANCHOR_OVERRIDE");
        }

        var rest = flags & ~3;
        if (rest != 0)
        {
            parts.Add($"0x{rest:X2}u");
        }

        return string.Join(" | ", parts);
    }
}
