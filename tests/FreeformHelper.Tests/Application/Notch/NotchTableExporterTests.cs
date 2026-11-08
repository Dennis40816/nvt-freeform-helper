using System.ComponentModel;
using System.Diagnostics;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableExporterTests
{
    private static readonly double[] _unitEdges = [0d, 10d];
    private static readonly string[] _expectedV21PayloadPrefix = ["1", "2", "3", "4"];
    private static readonly string[] _expectedV22PayloadPrefix = ["7", "8"];
    private static readonly int[] _v21OnlyPayload = [10, 120, 120, 20, 1, 38, 65535, 0, 0];
    private static readonly double[] _runtimeParityBeforeValues = [100d, 50d, 25d, 80d, 40d, 30d];
    private static readonly int[] _v21Q7BoundaryMagnitudes = [0, 1, 127, 128, 129, 255];
    private static readonly int[] _v21Q7BoundaryTypes = [1, 2];
    private static readonly int[] _v21MultiTermWrapDiffs = [10, 11, 12, 20];
    private static readonly int[] _v21MultiTermWrapSourceDiffs = [12, 10, 11];
    private static readonly int[] _expectedV21Q7BoundarySignedPercents =
        [1, -1, 99, -99, 100, -100, 101, -101, 199, -199];
    private static readonly double[] _expectedV21Q7BoundaryLegDeltas =
        [1d, -1d, 127d, -127d, 128d, -128d, 129d, -129d, 255d, -255d];
    private static readonly double[] _expectedV21Q7BoundaryRuntimeValues =
    [
        128d, 0d,
        128d, 0d,
        127d, 1d,
        129d, -1d,
        1d, 127d,
        255d, -127d,
        0d, 128d,
        256d, -128d,
        0d, 129d,
        257d, -129d,
        0d, 255d,
        383d, -255d,
    ];

    private static CadPadSet CreateCadSet()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var pads = new[] { new CadPad(1, "L", "P", polygon) };
        return new CadPadSet(pads);
    }

    private static CadPadSet CreateCadSetWithIcCount(int icCount)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var pads = Enumerable.Range(0, Math.Max(1, icCount))
            .Select(index =>
            {
                return new CadPad(index + 1, $"L{index}", $"P{index}", polygon);
            })
            .ToArray();
        return new CadPadSet(pads);
    }

    private static RegularGrid CreateGrid()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var regular = new RegularPad(0, 0, 0, polygon)
        {
            DiffIndex = 0,
            IcIndex = 0
        };
        var regularPads = new[] { regular };
        return new RegularGrid(1, 1, _unitEdges, _unitEdges, regularPads);
    }

    private static RegularGrid CreateGridWithIcCount(int icCount)
    {
        var cols = Math.Max(1, icCount);
        var xEdges = Enumerable.Range(0, cols + 1).Select(static index => (double)(index * 10)).ToArray();
        var yEdges = new[] { 0d, 10d };
        var pads = new List<RegularPad>(cols);
        for (var col = 0; col < cols; col++)
        {
            var polygon = new Polygon2(new[]
            {
                new Point2(col * 10, 0),
                new Point2((col + 1) * 10, 0),
                new Point2((col + 1) * 10, 10),
                new Point2(col * 10, 10),
            });
            var pad = new RegularPad(0, col, col, polygon)
            {
                DiffIndex = col,
                IcIndex = col,
            };
            pads.Add(pad);
        }

        return new RegularGrid(1, cols, xEdges, yEdges, pads);
    }

    private static ProjectSettings CreateProjectSettings(
        NotchComputationMode computationMode = NotchComputationMode.CadAllocation)
    {
        return new ProjectSettings
        {
            Notch = new NotchSettings
            {
                NullValue = 65535,
                ComputationMode = computationMode,
            }
        };
    }

    private static RegularGrid CreateRuntimeParityGrid()
    {
        const int rows = 2;
        const int cols = 3;
        var xEdges = Enumerable.Range(0, cols + 1).Select(static index => (double)index).ToArray();
        var yEdges = Enumerable.Range(0, rows + 1).Select(static index => (double)index).ToArray();
        var pads = new List<RegularPad>(rows * cols);
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var regularPadId = (row * cols) + col;
                var polygon = new Polygon2(new[]
                {
                    new Point2(col, row),
                    new Point2(col + 1, row),
                    new Point2(col + 1, row + 1),
                    new Point2(col, row + 1),
                });
                pads.Add(new RegularPad(row, col, regularPadId, polygon)
                {
                    IcIndex = row,
                    DiffIndex = col,
                });
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    private static RegularGrid CreateV21Q7BoundaryGrid()
    {
        var cols = _v21Q7BoundaryMagnitudes.Length * _v21Q7BoundaryTypes.Length * 2;
        var xEdges = Enumerable.Range(0, cols + 1).Select(static index => (double)index).ToArray();
        var yEdges = new[] { 0d, 1d };
        var pads = Enumerable.Range(0, cols)
            .Select(col => new RegularPad(
                row: 0,
                col,
                index: col,
                new Polygon2(new[]
                {
                    new Point2(col, 0),
                    new Point2(col + 1, 0),
                    new Point2(col + 1, 1),
                    new Point2(col, 1),
                }))
            {
                IcIndex = 0,
                DiffIndex = col,
            })
            .ToArray();
        return new RegularGrid(1, cols, xEdges, yEdges, pads);
    }

    private static NotchTable CreateV21Q7BoundaryTable()
    {
        var rows = new List<NotchTableRow>();
        var pairIndex = 0;
        foreach (var magnitudeQ7 in _v21Q7BoundaryMagnitudes)
        {
            foreach (var type in _v21Q7BoundaryTypes)
            {
                var sourceDiffIndex = pairIndex * 2;
                var targetDiffIndex = sourceDiffIndex + 1;
                rows.Add(new NotchTableRow(
                    NotchAlgorithmVersion.V21,
                    icIndex: 0,
                    diffIndex: sourceDiffIndex,
                    regularPadIndex: sourceDiffIndex,
                    cadPadId: 1000 + pairIndex,
                    values: [sourceDiffIndex, 100, 100, targetDiffIndex, type, magnitudeQ7, 65535, 0, 0],
                    comment: $"Q7={magnitudeQ7} type={type}"));
                pairIndex++;
            }
        }

        return new NotchTable(rows);
    }

    private static DiffFrameGridProjectionResult CreateV21Q7BoundaryProjection(RegularGrid grid)
    {
        var cells = grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select(static pad => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: pad.DiffIndex % 2 == 0 ? 128d : 0d))
            .ToArray();
        return new DiffFrameGridProjectionResult(true, null, cells);
    }

    private static NotchTable CreateRuntimeParityV22Table()
    {
        return new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 0,
                regularPadIndex: 0,
                cadPadId: 100,
                v22Node: new NotchV22Node(0, 94, 1, 44, 65535, 0, 0),
                comment: "ic0 move 44"),
            new(
                icIndex: 0,
                diffIndex: 2,
                regularPadIndex: 2,
                cadPadId: 102,
                v22Node: new NotchV22Node(2, 120, 1, 60, 65535, 0, 0),
                comment: "ic0 amplify to diff1"),
            new(
                icIndex: 1,
                diffIndex: 0,
                regularPadIndex: 3,
                cadPadId: 200,
                v22Node: new NotchV22Node(0, 90, 2, -20, 65535, 0, 0),
                comment: "ic1 negative leg"),
        });
    }

    private static NotchTable CreateRuntimeParityV21Table()
    {
        return new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 0,
                regularPadIndex: 0,
                cadPadId: 100,
                values: [0, 100, 94, 1, 1, 56, 65535, 0, 0],
                comment: "ic0 move 44"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 2,
                regularPadIndex: 2,
                cadPadId: 102,
                values: [2, 100, 120, 1, 1, 77, 65535, 0, 0],
                comment: "ic0 amplify to diff1"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 1,
                diffIndex: 0,
                regularPadIndex: 3,
                cadPadId: 200,
                values: [0, 100, 90, 2, 2, 26, 65535, 0, 0],
                comment: "ic1 negative leg"),
        });
    }

    private static NotchTable CreateRuntimeParityLegacyV21Table()
    {
        return new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 0,
                regularPadIndex: 0,
                cadPadId: 100,
                values: [0, 100, 50, 1, 1, 64, 65535, 0, 0],
                comment: "ic0 destination scale and add"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 2,
                regularPadIndex: 2,
                cadPadId: 102,
                values: [2, 100, 100, 0, 2, 32, 65535, 0, 0],
                comment: "ic0 destination subtract"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 1,
                diffIndex: 1,
                regularPadIndex: 4,
                cadPadId: 201,
                values: [1, 100, 120, 0, 1, -128, 2, 2, 300],
                comment: "ic1 malformed magnitudes normalize at UINT8 boundary"),
        });
    }

    private static double[] SimulateWithCSharp(
        RegularGrid grid,
        NotchTable table,
        NotchAlgorithmVersion version,
        NotchComputationMode computationMode = NotchComputationMode.CadAllocation,
        IReadOnlyList<double>? beforeValues = null,
        int nullDiffValue = 65535)
    {
        var projection = CreateRuntimeParityProjection(grid, beforeValues ?? _runtimeParityBeforeValues);
        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            version,
            nullDiffValue,
            ComputationMode: computationMode));

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        return result.Cells
            .OrderBy(static cell => cell.RegularRow)
            .ThenBy(static cell => cell.RegularCol)
            .Select(static cell => cell.AfterValue)
            .ToArray();
    }

    private static DiffFrameGridProjectionResult CreateRuntimeParityProjection(
        RegularGrid grid,
        IReadOnlyList<double> beforeValues)
    {
        Assert.Equal(grid.Pads.Count, beforeValues.Count);
        var cells = grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select((pad, index) => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: beforeValues[index]))
            .ToList();
        return new DiffFrameGridProjectionResult(true, null, cells);
    }

    private static string ExtractSingleTableRowLine(string text, string rowPrefix)
    {
        var line = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static l => l.TrimEnd('\r'))
            .Single(l => l.Contains(rowPrefix, StringComparison.Ordinal));
        return line;
    }

    internal static bool IsGccAvailable()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gcc",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("--version");

        try
        {
            if (!process.Start())
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal static double[] RunCProgramWithGcc(string source)
    {
        var tempDirectory = TestFiles.NewTempFilePath("notch-runtime-", string.Empty);
        NotchGccTestDirectory.Create(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, "notch_runtime.c");
        var executablePath = Path.Combine(tempDirectory, "notch_runtime.exe");
        TestFiles.WriteText(sourcePath, source);
        WriteNotchHeaderStub(tempDirectory);

        try
        {
            RunProcess(
                "gcc",
                [
                    "-std=c11",
                    "-include",
                    "stddef.h",
                    "-Wall",
                    "-Wextra",
                    "-Werror",
                    sourcePath,
                    "-o",
                    executablePath,
                ],
                "gcc failed to build generated notch runtime harness.");
            var output = RunProcess(executablePath, [], "generated notch runtime harness failed.");
            return output
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(static value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
        }
        finally
        {
            TestDirectoryCleanup.DeleteBestEffort(tempDirectory);
        }
    }

    internal static string RunProcess(string fileName, IEnumerable<string> arguments, string failureMessage)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), $"{fileName} did not start.");
        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(15000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
        }

        Assert.True(
            exited && process.ExitCode == 0,
            $"{failureMessage}{Environment.NewLine}stdout:{Environment.NewLine}{stdOut}{Environment.NewLine}stderr:{Environment.NewLine}{stdErr}");
        return stdOut;
    }

    private static void AssertCCompilesWithGcc(string source)
    {
        var tempDirectory = TestFiles.NewTempFilePath("notch-compile-", string.Empty);
        NotchGccTestDirectory.Create(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, "notch_export.c");
        var objectPath = Path.Combine(tempDirectory, "notch_export.o");
        TestFiles.WriteText(sourcePath, CreateGccHarnessSource(source));
        WriteNotchHeaderStub(tempDirectory);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gcc",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        string[] gccFlags = ["-std=c11", "-include", "stddef.h", "-Wall", "-Wextra", "-Werror", "-c", sourcePath, "-o", objectPath];
        foreach (var flag in gccFlags)
        {
            process.StartInfo.ArgumentList.Add(flag);
        }

        try
        {
            Assert.True(process.Start(), "gcc did not start.");
            var stdOut = process.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            Assert.True(
                process.ExitCode == 0,
                $"gcc failed to compile generated notch export.{Environment.NewLine}stdout:{Environment.NewLine}{stdOut}{Environment.NewLine}stderr:{Environment.NewLine}{stdErr}");
        }
        finally
        {
            TestDirectoryCleanup.DeleteBestEffort(tempDirectory);
        }
    }

    private static void WriteNotchHeaderStub(string tempDirectory)
    {
        var headerPath = Path.Combine(tempDirectory, "notch.h");
        TestFiles.WriteText(
            headerPath,
            """
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
""");
    }

    internal static string CreateGccHarnessSource(string generatedSource) =>
        string.Concat(
            "#include <stdint.h>\ntypedef int32_t INT32;\n",
            generatedSource,
            "\nNHC_DIFF_BUFFER g_stNhcDiffAfter;\n");

    private static string CreateV22RuntimeParityHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            CreateRuntimeMainSource());

    private static string CreateSingleIcRuntimeParityHarnessSource(
        string generatedSource,
        int[] diffIndices,
        int[] beforeValues)
    {
        Assert.Equal(diffIndices.Length, beforeValues.Length);
        var loadLines = string.Join(
            Environment.NewLine,
            diffIndices.Select((diffIndex, index) =>
                $"    g_stNhcDiffAfter.as16Buf[{diffIndex}] = {beforeValues[index]};"));
        var printLines = string.Join(
            Environment.NewLine,
            diffIndices.Select(static diffIndex =>
                $"    printf(\"%d\\n\", g_stNhcDiffAfter.as16Buf[{diffIndex}]);"));
        return string.Concat(
            CreateGccHarnessSource(generatedSource),
            "\n#include <stdio.h>\n\nint main(void)\n{\n",
            loadLines,
            "\n\n    FUNC_NHC_DiffCompensationByIc(0u);\n",
            printLines,
            "\n\n    return 0;\n}\n");
    }

    private static string CreateV21RuntimeParityHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            CreateRuntimeMainSource());

    private static string CreateV21Q7BoundaryRuntimeHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            """

#include <stdio.h>

int main(void)
{
    for (int index = 0; index < 24; ++index)
    {
        g_stNhcDiffAfter.as16Buf[index] = ((index % 2) == 0) ? 128 : 0;
    }

    FUNC_NHC_DiffCompensationByIc(0u);
    for (int index = 0; index < 24; ++index)
    {
        printf("%d\n", g_stNhcDiffAfter.as16Buf[index]);
    }

    return 0;
}
""");

    private static string CreateV21MultiTermWrapHarnessSource(string generatedSource) =>
        string.Concat(
            CreateGccHarnessSource(generatedSource),
            """

#include <stdio.h>

int main(void)
{
    g_stNhcDiffAfter.as16Buf[10] = 32767;
    g_stNhcDiffAfter.as16Buf[11] = 32767;
    g_stNhcDiffAfter.as16Buf[12] = 32767;
    g_stNhcDiffAfter.as16Buf[20] = 0;

    FUNC_NHC_DiffCompensationByIc(0u);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[10]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[11]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[12]);
    printf("%d\n", g_stNhcDiffAfter.as16Buf[20]);
    return 0;
}
""");

    private static string CreateRuntimeMainSource() =>
        """

#include <stdio.h>

static void LoadIc0(void)
{
    g_stNhcDiffAfter.as16Buf[0] = 100;
    g_stNhcDiffAfter.as16Buf[1] = 50;
    g_stNhcDiffAfter.as16Buf[2] = 25;
}

static void LoadIc1(void)
{
    g_stNhcDiffAfter.as16Buf[0] = 80;
    g_stNhcDiffAfter.as16Buf[1] = 40;
    g_stNhcDiffAfter.as16Buf[2] = 30;
}

int main(void)
{
    INT16 values[6];
    LoadIc0();
    FUNC_NHC_DiffCompensationByIc(0u);
    values[0] = g_stNhcDiffAfter.as16Buf[0];
    values[1] = g_stNhcDiffAfter.as16Buf[1];
    values[2] = g_stNhcDiffAfter.as16Buf[2];

    LoadIc1();
    FUNC_NHC_DiffCompensationByIc(1u);
    values[3] = g_stNhcDiffAfter.as16Buf[0];
    values[4] = g_stNhcDiffAfter.as16Buf[1];
    values[5] = g_stNhcDiffAfter.as16Buf[2];

    for (int index = 0; index < 6; ++index)
    {
        printf("%d\n", values[index]);
    }

    return 0;
}
""";

    private sealed class MutatingReadOnlySet(IEnumerable<int> values, Action mutation)
        : SortedSet<int>(values), IReadOnlySet<int>, IEnumerable<int>
    {
        private Action? _mutation = mutation;

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            System.Threading.Interlocked.Exchange(ref _mutation, null)?.Invoke();
            return base.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            ((IEnumerable<int>)this).GetEnumerator();
    }
}
