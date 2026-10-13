using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

// The generated firmware C must follow MISRA C:2012 Rule 15.5 (one exit per function) and
// Rule 11.9 (the NULL macro for null pointer constants). These tests pin the text shape;
// NotchTableExporterTests compares the same functions with gcc at runtime.
public sealed class NotchFirmwareCSingleExitTests
{
    [Theory]
    [InlineData(NotchExportProfile.Release)]
    [InlineData(NotchExportProfile.Debug)]
    public void ExportAsCInitializer_V22Table_EveryFunctionHasAtMostOneTrailingReturn(NotchExportProfile profile)
    {
        var text = ExportV22(profile);

        var functions = SplitIntoFunctionBodies(text);

        Assert.NotEmpty(functions);
        foreach (var body in functions)
        {
            var returns = body.Where(static line => line.TrimStart().StartsWith("return", StringComparison.Ordinal)).ToArray();
            Assert.True(returns.Length <= 1, $"more than one return in:{Environment.NewLine}{string.Join(Environment.NewLine, body)}");
            if (returns.Length == 1)
            {
                Assert.Equal(returns[0], body.Last(static line => line.Trim().Length > 0));
            }
        }
    }

    [Fact]
    public void ExportAsCInitializer_V22DebugProfile_GuardsBodiesInsteadOfReturningEarly()
    {
        var text = ExportV22(NotchExportProfile.Debug);

        Assert.DoesNotContain("return;", text, StringComparison.Ordinal);
        Assert.Contains("if (pstTable != NULL)", text, StringComparison.Ordinal);
        Assert.Contains("if (u8Ic < USER_NHC_IC_NUM)", text, StringComparison.Ordinal);
        Assert.Contains("if (pstMaskInfo->pu16ActiveDiffs != NULL)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_V21ReleaseProfile_GuardsBodiesInsteadOfReturningEarly()
    {
        var text = NotchTableExporter.ExportAsCInitializer(
            new NotchTable(
            [
                new NotchTableRow(
                    NotchAlgorithmVersion.V21,
                    icIndex: 0,
                    diffIndex: 0,
                    regularPadIndex: 0,
                    cadPadId: 7,
                    values: [0, 100, 90, 1, 1, 30, 65535, 0, 0],
                    comment: "single exit"),
            ]),
            CreateCadSet(),
            CreateGrid(),
            CreateSettings());

        Assert.DoesNotContain("return;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("*)0)", text, StringComparison.Ordinal);
        Assert.Contains("if (pstTable != NULL)", text, StringComparison.Ordinal);
        Assert.Contains("if (u8Ic < USER_NHC_IC_NUM)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportAsCInitializer_V22Table_NeverWritesATypedZeroAsNullPointer()
    {
        var text = ExportV22(NotchExportProfile.Debug);

        Assert.DoesNotContain("*)0)", text, StringComparison.Ordinal);
        Assert.Contains("NULL", text, StringComparison.Ordinal);
        Assert.Contains("// Requires the FW platform NULL macro (typedef.h) for null pointer constants.", text, StringComparison.Ordinal);
    }

    private static string ExportV22(NotchExportProfile profile) =>
        NotchTableExporter.ExportAsCInitializer(
            new NotchTable(
            [
                new NotchTableRow(
                    icIndex: 0,
                    diffIndex: 0,
                    regularPadIndex: 0,
                    cadPadId: 7,
                    v22Node: new NotchV22Node(0, 80, 1, 25, 65535, 0, 0),
                    comment: "first ic"),
                new NotchTableRow(
                    icIndex: 1,
                    diffIndex: 0,
                    regularPadIndex: 1,
                    cadPadId: 8,
                    v22Node: new NotchV22Node(0, 70, 1, -15, 65535, 0, 0),
                    comment: "second ic"),
            ]),
            CreateCadSet(),
            CreateGrid(),
            CreateSettings(),
            profile,
            new HashSet<int> { 0, 1 });

    private static CadPadSet CreateCadSet() => new(
    [
        TestGeometryFactory.CreateCadPad(7, "L", 0, 0, 1, 1),
        TestGeometryFactory.CreateCadPad(8, "L", 0, 1, 1, 2),
    ]);

    private static RegularGrid CreateGrid() => TestGeometryFactory.CreateRegularGrid(
        rows: 2,
        cols: 1,
        diffIndexSelector: static (_, _) => 0,
        icIndexSelector: static (row, _) => row);

    private static ProjectSettings CreateSettings() => new()
    {
        Notch = new NotchSettings { NullValue = 65535, ComputationMode = NotchComputationMode.CadAllocation },
    };

    // A function body is the run of lines between a "{" and a "}" that start in column 0.
    private static List<string[]> SplitIntoFunctionBodies(string text)
    {
        var bodies = new List<string[]>();
        List<string>? current = null;
        foreach (var line in text.Split('\n').Select(static line => line.TrimEnd('\r')))
        {
            if (line == "{")
            {
                current = [];
            }
            else if (line == "}" && current is not null)
            {
                bodies.Add([.. current]);
                current = null;
            }
            else
            {
                current?.Add(line);
            }
        }

        return bodies;
    }
}
