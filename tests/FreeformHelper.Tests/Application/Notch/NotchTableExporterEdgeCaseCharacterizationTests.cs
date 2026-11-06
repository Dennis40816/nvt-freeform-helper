using System.Globalization;
using FreeformHelper.Application.Export;
using FreeformHelper.Domain.Notch;
using Xunit;
using static FreeformHelper.Tests.NotchEdgeCaseHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the Notch CSV and C export still do today with input it takes without checking. Each
/// test states the wanted behavior in its summary and asserts the current one, so a fix that rejects
/// or escapes the input changes the expected values here and must say so in its pull request (same
/// convention as <see cref="NotchNullValueCollisionCharacterizationTests"/>). The notch table is
/// firmware source and must follow MISRA C:2012; the rule numbers named below are the ones the
/// current output breaks. Input that the export now rejects (out-of-range numbers, line breaks in
/// comments) is covered by <c>NotchExportRangeCheckTests</c> and <c>NotchExportCommentTests</c>.
/// </summary>
public sealed class NotchTableExporterEdgeCaseCharacterizationTests
{
    private static readonly string[] _noTypeLimits = ["-Wno-type-limits"];

    // ---- comments --------------------------------------------------------------------------

    /// <summary>
    /// Wanted: the generated file is plain ASCII, or its encoding is a decided contract.
    /// Current: non-ASCII characters (for example pad names in a CJK project) pass into the C file.
    /// </summary>
    [Fact]
    public void Comment_WithNonAsciiText_IsCopiedUnchanged()
    {
        const string comment = "焊盤 pad 😀";
        var text = ExportC(TableWithComment(NotchAlgorithmVersion.V22, comment));

        Assert.Contains($"// {comment}; NODE KEEP=", text, StringComparison.Ordinal);
        Assert.Contains(text, static character => character > 127);
        if (IsGccAvailable)
        {
            var result = CompileC99(text);
            Assert.True(result.ExitCode == 0, result.StdErr);
        }
    }

    // ---- counts the firmware types cannot hold ---------------------------------------------

    /// <summary>
    /// Wanted: a table with 256 ICs either compiles as strict C99 or is rejected: the
    /// <c>u8Ic &lt; USER_NHC_IC_NUM</c> guard is always true for a UINT8 once there are 256 ICs
    /// (MISRA C:2012 Rule 14.3). Current: the file is written and strict C99 stops at the guard. More
    /// than 256 ICs are rejected by the export range check.
    /// </summary>
    [Theory]
    [InlineData(255, "256u")]
    public void IcCountOf256_IsWrittenAndTheDispatchGuardIsAlwaysTrue(int icIndex, string icNum)
    {
        RequireGcc();
        var text = ExportC(new NotchTable([V22Row(icIndex, 7, 50, NullDiff, 0, NullDiff, 0, 0, "last-ic")]));

        var result = CompileC99(text);

        Assert.Contains($"#define USER_NHC_IC_NUM          ({icNum})", text, StringComparison.Ordinal);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("type-limits", result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, CompileC99(text, _noTypeLimits).ExitCode);
    }

    // ---- CSV -------------------------------------------------------------------------------

    /// <summary>
    /// Wanted: the CSV carries every payload value, or fails when it cannot. Current: only the first 9
    /// values get a column while <c>payload_width</c> still reports the real length, so values 10 and
    /// above are lost.
    /// </summary>
    [Fact]
    public void ExportAsCsv_PayloadLongerThanNineValues_KeepsTheWidthAndDropsTheTail()
    {
        var table = new NotchTable([V21Row(0, 1, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], "wide")]);

        var columns = Lines(NotchTableExporter.ExportAsCsv(table))[1].Split(',');

        Assert.Equal(19, columns.Length);
        Assert.Equal("12", columns[8]);
        Assert.Equal(["1", "2", "3", "4", "5", "6", "7", "8", "9"], columns[9..18]);
    }

    /// <summary>
    /// Wanted: <c>ic_number</c> is IC index + 1 for every index. Current: <c>int.MaxValue + 1</c> wraps.
    /// </summary>
    [Fact]
    public void ExportAsCsv_IcIndexIntMax_WrapsTheIcNumber()
    {
        var table = new NotchTable([V22Row(int.MaxValue, 5, 100, NullDiff, 0, NullDiff, 0, 0, "max-ic")]);

        var columns = Lines(NotchTableExporter.ExportAsCsv(table))[1].Split(',');

        Assert.Equal(int.MaxValue.ToString(CultureInfo.InvariantCulture), columns[3]);
        Assert.Equal(int.MinValue.ToString(CultureInfo.InvariantCulture), columns[4]);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static NotchTable TableWithComment(NotchAlgorithmVersion version, string comment)
    {
        return new NotchTable([
            version == NotchAlgorithmVersion.V22
                ? V22Row(0, 10, 120, 20, 30, NullDiff, 0, 0, comment)
                : V21Row(0, 10, [10, 100, 120, 20, 1, 38, NullDiff, 0, 0], comment),
        ]);
    }
}
