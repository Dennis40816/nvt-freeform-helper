using System.Globalization;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using Xunit.Sdk;

namespace FreeformHelper.Tests;

/// <summary>Result of one gcc or generated-program run: exit code 0 when it succeeded, 1 when it failed (the failure text is in StdErr).</summary>
internal readonly record struct NotchGccResult(int ExitCode, string StdOut, string StdErr);

/// <summary>
/// Shared pieces of the Notch C table export edge case tests: row builders, export entry point,
/// a C99 compile/run harness around the notch.h stub, and a dump program that prints the table
/// data the C compiler actually stored, so a test can assert the data and not only the text.
/// </summary>
internal static class NotchEdgeCaseHarness
{
    /// <summary>The default reserved "no target" diff value (NHC_DIFF_NONE).</summary>
    public const int NullDiff = ushort.MaxValue;

    /// <summary>Strict C99: pedantic ISO C, warnings as errors. The generated file is firmware source.</summary>
    private static readonly string[] _c99Flags =
        ["-std=c99", "-pedantic", "-Wall", "-Wextra", "-Werror", "-include", "stddef.h"];

    private const string StubHeader = """
#ifndef NOTCH_H_
#define NOTCH_H_
#include <stdint.h>
#define FUNC_ENABLE 1
#define USER_SWITCH_NOTCH_COMPENSATION FUNC_ENABLE
typedef uint8_t UINT8;
typedef uint16_t UINT16;
typedef int8_t INT8;
typedef int16_t INT16;
typedef struct
{
    INT16 as16Buf[65536];
} NHC_DIFF_BUFFER;
extern NHC_DIFF_BUFFER g_stNhcDiffAfter;
#define S_2D_DIFFAFTER (&g_stNhcDiffAfter)
#endif
""";

    private const string V22DumpMain = """

#include <stdio.h>

int main(void)
{
    unsigned int u32Ic;
    unsigned int u32Node;
    for (u32Ic = 0u; u32Ic < (unsigned int)USER_NHC_IC_NUM; u32Ic++)
    {
        for (u32Node = 0u; u32Node < (unsigned int)castNHC_TABLE_BY_IC[u32Ic].u16NodeNum; u32Node++)
        {
            const ST_PRI_NHC_TABLE_NODE_INFO* pstNode = &castNHC_TABLE_BY_IC[u32Ic].pstTable[u32Node];
            printf("%u %u %u %u %d %u %d %u\n",
                u32Ic,
                (unsigned int)pstNode->NHC_IDX,
                (unsigned int)pstNode->NHC_COMBINE,
                (unsigned int)pstNode->NHC_1ST_IDX,
                (int)pstNode->NHC_1ST_RATIO,
                (unsigned int)pstNode->NHC_2ND_IDX,
                (int)pstNode->NHC_2ND_RATIO,
                (unsigned int)pstNode->NHC_FLAGS);
        }
    }

    return 0;
}
""";

    private const string V21DumpMain = """

#include <stdio.h>

int main(void)
{
    unsigned int u32Ic;
    unsigned int u32Node;
    for (u32Ic = 0u; u32Ic < (unsigned int)USER_NHC_IC_NUM; u32Ic++)
    {
        for (u32Node = 0u; u32Node < (unsigned int)castNHC_TABLE_BY_IC[u32Ic].u16NodeNum; u32Node++)
        {
            const ST_PRI_NHC_TABLE_NODE_INFO* pstNode = &castNHC_TABLE_BY_IC[u32Ic].pstTable[u32Node];
            printf("%u %u %u %u %u %u %u %u %u %u\n",
                u32Ic,
                (unsigned int)pstNode->NHC_IDX,
                (unsigned int)pstNode->NHC_REGULAR,
                (unsigned int)pstNode->NHC_REGU_TO_FULL,
                (unsigned int)pstNode->NHC_1ST_IDX,
                (unsigned int)pstNode->NHC_1ST_TYPE,
                (unsigned int)pstNode->NHC_1ST_RATIO,
                (unsigned int)pstNode->NHC_2ND_IDX,
                (unsigned int)pstNode->NHC_2ND_TYPE,
                (unsigned int)pstNode->NHC_2ND_RATIO);
        }
    }

    return 0;
}
""";

    public static bool IsGccAvailable => NotchTableExporterTests.IsGccAvailable();

    public static void RequireGcc() => Assert.SkipUnless(IsGccAvailable, "gcc is not available on this machine.");

    public static ProjectSettings CreateSettings(
        int nullValue = NullDiff,
        NotchComputationMode computationMode = NotchComputationMode.CadAllocation,
        NotchCompensationModel compensationModel = NotchCompensationModel.CurrentGain)
    {
        return new ProjectSettings
        {
            Notch = new NotchSettings
            {
                NullValue = nullValue,
                ComputationMode = computationMode,
                CompensationModel = compensationModel,
            },
        };
    }

    public static string ExportC(
        NotchTable table,
        NotchExportProfile profile = NotchExportProfile.Debug,
        ProjectSettings? settings = null)
    {
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "L", 0, 0, 10, 10)]);
        var grid = TestGeometryFactory.CreateLinearRegularGrid([0]);
        return NotchTableExporter.ExportAsCInitializer(table, cad, grid, settings ?? CreateSettings(), profile);
    }

    public static NotchTableRow V22Row(
        int icIndex,
        int anchor,
        int combine,
        int target1,
        int ratio1,
        int target2,
        int ratio2,
        int flags,
        string comment)
    {
        return new NotchTableRow(
            icIndex,
            diffIndex: anchor,
            regularPadIndex: anchor,
            cadPadId: 1,
            v22Node: new NotchV22Node(anchor, combine, target1, ratio1, target2, ratio2, flags),
            comment: comment);
    }

    public static NotchTableRow V21Row(int icIndex, int diffIndex, int[] values, string comment)
    {
        return new NotchTableRow(
            NotchAlgorithmVersion.V21,
            icIndex,
            diffIndex,
            regularPadIndex: diffIndex,
            cadPadId: 1,
            values,
            comment);
    }

    /// <summary>Splits generated text into lines without the line terminators.</summary>
    public static string[] Lines(string text)
    {
        return text.Split('\n').Select(static line => line.TrimEnd('\r')).ToArray();
    }

    /// <summary>The single generated table line that contains <paramref name="marker"/>.</summary>
    public static string TableLine(string text, string marker)
    {
        return Lines(text).Single(line => line.Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>Compiles the generated source as C99 only (no link, no run).</summary>
    public static NotchGccResult CompileC99(string generatedSource, IReadOnlyList<string>? extraFlags = null)
    {
        var directory = NewDirectory();
        try
        {
            var sourcePath = WriteSources(directory, WrapGenerated(generatedSource));
            var objectPath = Path.Combine(directory, "notch_edge.o");
            return Run("gcc", [.. _c99Flags, .. extraFlags ?? [], "-c", sourcePath, "-o", objectPath]);
        }
        finally
        {
            TestDirectoryCleanup.DeleteBestEffort(directory);
        }
    }

    /// <summary>Compiles the generated source with <paramref name="mainSource"/> as C99, runs it, and returns stdout.</summary>
    public static string BuildAndRunC99(
        string generatedSource,
        string mainSource,
        IReadOnlyList<string>? extraFlags = null)
    {
        var directory = NewDirectory();
        try
        {
            var sourcePath = WriteSources(directory, WrapGenerated(generatedSource) + mainSource);
            var executablePath = Path.Combine(directory, "notch_edge.exe");
            var build = Run("gcc", [.. _c99Flags, .. extraFlags ?? [], sourcePath, "-o", executablePath]);
            Assert.True(
                build.ExitCode == 0,
                $"gcc rejected the generated notch export as C99.{Environment.NewLine}{build.StdErr}");
            var run = Run(executablePath, []);
            Assert.True(run.ExitCode == 0, $"generated notch program failed.{Environment.NewLine}{run.StdErr}");
            return run.StdOut;
        }
        finally
        {
            TestDirectoryCleanup.DeleteBestEffort(directory);
        }
    }

    /// <summary>
    /// Compiles the v2.2 export as C99 and returns, per node, the fields the compiler stored:
    /// ic, NHC_IDX, NHC_COMBINE, NHC_1ST_IDX, NHC_1ST_RATIO, NHC_2ND_IDX, NHC_2ND_RATIO, NHC_FLAGS.
    /// </summary>
    public static int[][] DumpV22Table(string generatedSource, IReadOnlyList<string>? extraFlags = null) =>
        ParseDump(BuildAndRunC99(generatedSource, V22DumpMain, extraFlags));

    /// <summary>
    /// Compiles the v2.1 export as C99 and returns, per node, the fields the compiler stored:
    /// ic, NHC_IDX, NHC_REGULAR, NHC_REGU_TO_FULL, NHC_1ST_IDX, NHC_1ST_TYPE, NHC_1ST_RATIO,
    /// NHC_2ND_IDX, NHC_2ND_TYPE, NHC_2ND_RATIO.
    /// </summary>
    public static int[][] DumpV21Table(string generatedSource, IReadOnlyList<string>? extraFlags = null) =>
        ParseDump(BuildAndRunC99(generatedSource, V21DumpMain, extraFlags));

    /// <summary>
    /// Runs the dispatch function of one IC over the INT16 buffer and returns the buffer values at
    /// <paramref name="diffIndices"/> afterwards.
    /// </summary>
    public static int[] RunCompensation(
        string generatedSource,
        int icIndex,
        int[] diffIndices,
        int[] beforeValues,
        IReadOnlyList<string>? extraFlags = null)
    {
        Assert.Equal(diffIndices.Length, beforeValues.Length);
        var load = string.Join(
            Environment.NewLine,
            diffIndices.Select((diffIndex, index) =>
                $"    g_stNhcDiffAfter.as16Buf[{diffIndex}] = {beforeValues[index]};"));
        var print = string.Join(
            Environment.NewLine,
            diffIndices.Select(static diffIndex =>
                $"    printf(\"%d\\n\", g_stNhcDiffAfter.as16Buf[{diffIndex}]);"));
        var main = string.Concat(
            "\n#include <stdio.h>\n\nint main(void)\n{\n",
            load,
            $"\n    FUNC_NHC_DiffCompensationByIc({icIndex}u);\n",
            print,
            "\n    return 0;\n}\n");
        return BuildAndRunC99(generatedSource, main, extraFlags)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(static value => int.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
    }

    /// <summary>
    /// Runs the C# apply simulation of a one-row linear grid with the given diff indices (IC 0) and
    /// returns the values after compensation, in grid order.
    /// </summary>
    public static double[] Simulate(
        NotchTable table,
        NotchAlgorithmVersion version,
        int[] diffIndices,
        double[] beforeValues)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
        var cells = grid.Pads
            .OrderBy(static pad => pad.Col)
            .Select((pad, index) => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: beforeValues[index]))
            .ToList();
        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [new DiffFrameGridProjectionResult(true, null, cells)],
            table,
            version,
            NullDiff));
        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        return result.Cells
            .OrderBy(static cell => cell.RegularCol)
            .Select(static cell => cell.AfterValue)
            .ToArray();
    }

    private static int[][] ParseDump(string stdout)
    {
        return stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static value => int.Parse(value, CultureInfo.InvariantCulture))
                .ToArray())
            .ToArray();
    }

    private static string WrapGenerated(string generatedSource) =>
        string.Concat(
            "#include <stdint.h>\ntypedef int32_t INT32;\n",
            generatedSource,
            "\nNHC_DIFF_BUFFER g_stNhcDiffAfter;\n");

    private static string NewDirectory()
    {
        var directory = TestFiles.NewTempFilePath("notch-edge-", string.Empty);
        NotchGccTestDirectory.Create(directory);
        return directory;
    }

    private static string WriteSources(string directory, string source)
    {
        TestFiles.WriteText(Path.Combine(directory, "notch.h"), StubHeader);
        var sourcePath = Path.Combine(directory, "notch_edge.c");
        TestFiles.WriteText(sourcePath, source);
        return sourcePath;
    }

    // The shared helper in NotchTableExporterTests starts the process and fails with the captured
    // output when the exit code is not 0. A failure is turned into a result here so a test can assert
    // that gcc rejected the source.
    private static NotchGccResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            return new NotchGccResult(0, NotchTableExporterTests.RunProcess(fileName, arguments, $"{fileName} failed."), string.Empty);
        }
        catch (XunitException failure)
        {
            return new NotchGccResult(1, string.Empty, failure.Message);
        }
    }
}
