using System.Globalization;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using Xunit;
using static FreeformHelper.Tests.NotchEdgeCaseHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the Notch CSV and C export do today with input it takes without checking. Each test
/// states the wanted behavior in its summary and asserts the current one, so a fix that rejects or
/// escapes the input changes the expected values here and must say so in its pull request (same
/// convention as <see cref="NotchNullValueCollisionCharacterizationTests"/>). The notch table is
/// firmware source and must follow MISRA C:2012; the rule numbers named below are the ones the
/// current output breaks. The findings are written up in <c>cloud-notes/notch-export-edges.md</c>.
/// </summary>
public sealed class NotchTableExporterEdgeCaseCharacterizationTests
{
    private static readonly string[] _noComment = ["-Wno-comment"];
    private static readonly string[] _noOverflow = ["-Wno-overflow"];
    private static readonly string[] _noTypeLimits = ["-Wno-type-limits"];
    private static readonly int[] _zeroedDiff = [0];
    private static readonly int[] _zeroedDiffBefore = [77];

    // ---- comments --------------------------------------------------------------------------

    /// <summary>
    /// Wanted: a comment never leaves its <c>//</c> comment (line breaks replaced or escaped).
    /// Current: the comment is written as is, so its second line becomes source code.
    /// </summary>
    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void Comment_WithALineBreak_BecomesSourceCodeOnItsOwnLine(NotchAlgorithmVersion version)
    {
        const string injected = "#error comment-injected-this-line";
        var text = ExportC(TableWithComment(version, "first\n" + injected), settings: LegacySettings());

        Assert.Contains(Lines(text), static line => line.StartsWith(injected, StringComparison.Ordinal));
        if (IsGccAvailable)
        {
            var result = CompileC99(text);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("comment-injected-this-line", result.StdErr, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Wanted: a comment cannot continue onto the next line (MISRA C:2012 Rule 3.2: no line splicing in
    /// <c>//</c> comments). Current: a trailing backslash splices the next table row into the comment.
    /// With the warning on, strict C99 rejects the file; with it off the compiler silently drops that
    /// row and fills the declared node with zeros (destination diff 0, ToFull 0).
    /// </summary>
    [Fact]
    public void Comment_EndingWithABackslash_SwallowsTheNextTableRow()
    {
        var table = new NotchTable([
            V21Row(0, 10, [10, 100, 100, 20, 1, 38, NullDiff, 0, 0], "tail\\"),
            V21Row(0, 11, [11, 100, 100, 21, 1, 38, NullDiff, 0, 0], "next"),
        ]);

        var text = ExportC(table, settings: LegacySettings());

        Assert.EndsWith("// tail\\", TableLine(text, "// tail"), StringComparison.Ordinal);
        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (2u)", text, StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            var strict = CompileC99(text);
            Assert.NotEqual(0, strict.ExitCode);
            Assert.Contains("multi-line comment", strict.StdErr, StringComparison.Ordinal);
            Assert.Equal(
                [[0, 10, 100, 100, 20, 1, 38, NullDiff, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]],
                DumpV21Table(text, _noComment));
        }
    }

    /// <summary>
    /// Wanted: no trigraph can form in a comment (MISRA C:2012 Rule 4.2). Current: <c>??/</c> at the end
    /// of a comment is a backslash in C99, so it splices the next row like a real backslash.
    /// </summary>
    [Fact]
    public void Comment_EndingWithATrigraphBackslash_IsRejectedByStrictC99()
    {
        RequireGcc();
        var table = new NotchTable([
            V21Row(0, 10, [10, 100, 100, 20, 1, 38, NullDiff, 0, 0], "x??/"),
            V21Row(0, 11, [11, 100, 100, 21, 1, 38, NullDiff, 0, 0], "next"),
        ]);

        var text = ExportC(table, settings: LegacySettings());
        var result = CompileC99(text);

        Assert.EndsWith("// x??/", TableLine(text, "// x??"), StringComparison.Ordinal);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("trigraph", result.StdErr, StringComparison.Ordinal);
    }

    /// <summary>
    /// Wanted: <c>//</c> and <c>/*</c> do not appear inside a comment (MISRA C:2012 Rule 3.1).
    /// Current: the comment text is copied unchanged.
    /// </summary>
    [Fact]
    public void Comment_WithCommentMarkers_IsCopiedUnchanged()
    {
        var table = TableWithComment(NotchAlgorithmVersion.V22, "a // b /* c */ d");

        var line = TableLine(ExportC(table), "NODE KEEP");

        Assert.Contains("// a // b /* c */ d; NODE KEEP=", line, StringComparison.Ordinal);
    }

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

    // ---- values the firmware field cannot hold ----------------------------------------------

    /// <summary>
    /// Wanted: a diff index outside [0, 65535] is rejected, as <c>NullValue</c> is. Current: the number
    /// is written as is into a UINT16 field. Strict C99 rejects the file (-Woverflow); without that
    /// warning the stored index wraps modulo 65536 and the node silently addresses another diff.
    /// </summary>
    [Theory]
    [InlineData("anchor", 65536)]
    [InlineData("anchor", int.MinValue)]
    [InlineData("anchor", int.MaxValue)]
    [InlineData("target", 65536)]
    [InlineData("target", int.MinValue)]
    [InlineData("target", int.MaxValue)]
    public void V22DiffIndexOutsideUint16_IsWrittenAsIsAndWrapsInTheCompiledTable(string slot, int value)
    {
        RequireGcc();
        var row = slot == "anchor"
            ? V22Row(0, value, 100, 20, 10, NullDiff, 0, 0, "oor")
            : V22Row(0, 10, 100, value, 10, NullDiff, 0, 0, "oor");
        var text = ExportC(new NotchTable([row]));

        var strict = CompileC99(text);
        var node = Assert.Single(DumpV22Table(text, _noOverflow));

        Assert.Contains(value.ToString(CultureInfo.InvariantCulture), TableLine(text, "// oor;"), StringComparison.Ordinal);
        Assert.NotEqual(0, strict.ExitCode);
        Assert.Contains("overflow", strict.StdErr, StringComparison.Ordinal);
        var wrapped = unchecked((ushort)value);
        Assert.Equal(wrapped, slot == "anchor" ? node[1] : node[3]);
    }

    /// <summary>
    /// Wanted: a negative diff index is rejected. Current: strict C99 does not see it (gcc has no
    /// -Woverflow for a negative constant in an unsigned field), so -1 is stored as 65535, which is
    /// also the default <c>NHC_DIFF_NONE</c>: a target of -1 becomes "no target" and an anchor of -1
    /// becomes diff 65535.
    /// </summary>
    [Theory]
    [InlineData("anchor")]
    [InlineData("target")]
    public void V22NegativeDiffIndex_PassesStrictC99AndBecomesUint16Max(string slot)
    {
        RequireGcc();
        var row = slot == "anchor"
            ? V22Row(0, -1, 100, 20, 10, NullDiff, 0, 0, "neg")
            : V22Row(0, 10, 100, -1, 10, NullDiff, 0, 0, "neg");
        var text = ExportC(new NotchTable([row]));

        var node = Assert.Single(DumpV22Table(text));

        Assert.Equal(ushort.MaxValue, slot == "anchor" ? node[1] : node[3]);
    }

    /// <summary>
    /// Wanted: the same rejection for the v2.1 destination and reference diff (legacy and CAD mode).
    /// Current: the index is written as is into a UINT16 field.
    /// </summary>
    [Fact]
    public void V21DiffIndexOutsideUint16_IsRejectedByStrictC99AndWrapsOtherwise()
    {
        RequireGcc();
        var table = new NotchTable([V21Row(0, 3, [65537, 100, 100, 65536, 1, 50, NullDiff, 0, 0], "oor")]);

        var text = ExportC(table, settings: LegacySettings());
        var strict = CompileC99(text);
        var node = Assert.Single(DumpV21Table(text, _noOverflow));

        Assert.NotEqual(0, strict.ExitCode);
        Assert.Contains("overflow", strict.StdErr, StringComparison.Ordinal);
        Assert.Equal(1, node[1]);
        Assert.Equal(0, node[4]);
    }

    /// <summary>
    /// Wanted: a v2.2 flag byte outside [0, 255] is rejected, as <c>NullValue</c> is. Current: the flags
    /// are masked with 0xFF, so 256 turns into "no flags" and -1 into every bit set.
    /// </summary>
    [Theory]
    [InlineData(256, 0, "NHC_FLAG_NONE")]
    [InlineData(257, 1, "NHC_FLAG_CONTINUATION")]
    [InlineData(-1, 255, "NHC_FLAG_CONTINUATION | NHC_FLAG_ANCHOR_OVERRIDE | 0xFCu")]
    [InlineData(int.MinValue, 0, "NHC_FLAG_NONE")]
    [InlineData(int.MaxValue, 255, "NHC_FLAG_CONTINUATION | NHC_FLAG_ANCHOR_OVERRIDE | 0xFCu")]
    public void V22FlagsOutsideAByte_AreMaskedSilently(int flags, int storedFlags, string literal)
    {
        var text = ExportC(new NotchTable([V22Row(0, 10, 100, NullDiff, 0, NullDiff, 0, flags, "mask")]));

        Assert.Contains($", {literal} }}, // mask;", TableLine(text, "// mask;"), StringComparison.Ordinal);
        if (IsGccAvailable)
        {
            Assert.Equal([[0, 10, 100, NullDiff, 0, NullDiff, 0, storedFlags]], DumpV22Table(text));
        }
    }

    /// <summary>
    /// Wanted: a row with fewer than 9 values is rejected or skipped (a v2.2 row has a defined legacy
    /// path for short payloads, a v2.1 row has none). Current: the missing values read as 0, so the node
    /// has destination diff 0, ToFull 0 and the firmware zeroes diff 0 on every frame.
    /// </summary>
    [Theory]
    [InlineData(NotchComputationMode.LegacyRegularAnchor)]
    [InlineData(NotchComputationMode.CadAllocation)]
    public void V21RowWithNoValues_ZeroesDiffZeroInTheFirmware(NotchComputationMode mode)
    {
        RequireGcc();
        var text = ExportC(
            new NotchTable([V21Row(0, 3, [], "empty")]),
            settings: CreateSettings(computationMode: mode));

        var node = Assert.Single(DumpV21Table(text));
        var after = RunCompensation(text, 0, _zeroedDiff, _zeroedDiffBefore);

        Assert.Equal(0, node[1]);
        Assert.Equal([0], after);
    }

    // ---- counts the firmware types cannot hold ---------------------------------------------

    /// <summary>
    /// Wanted: a table with 256 or more ICs either compiles as strict C99 or is rejected: the
    /// <c>u8Ic &lt; USER_NHC_IC_NUM</c> guard is always true for a UINT8 once there are 256 ICs
    /// (MISRA C:2012 Rule 14.3), and IC 257 and above cannot be addressed at all. Current: the file is
    /// written and strict C99 stops at the guard.
    /// </summary>
    [Theory]
    [InlineData(255, "256u")]
    [InlineData(256, "257u")]
    public void IcCountAboveUint8_IsWrittenAndTheDispatchGuardIsAlwaysTrue(int icIndex, string icNum)
    {
        RequireGcc();
        var text = ExportC(new NotchTable([V22Row(icIndex, 7, 50, NullDiff, 0, NullDiff, 0, 0, "last-ic")]));

        var result = CompileC99(text);

        Assert.Contains($"#define USER_NHC_IC_NUM          ({icNum})", text, StringComparison.Ordinal);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("type-limits", result.StdErr, StringComparison.Ordinal);
        Assert.Equal(0, CompileC99(text, _noTypeLimits).ExitCode);
    }

    /// <summary>
    /// Wanted: more than 65535 nodes in one IC is rejected (the node count is a UINT16). Current: the
    /// count is written as 65536u. Strict C99 rejects the file (-Woverflow); without that warning the
    /// stored count wraps to 0 and the IC silently gets no compensation at all.
    /// </summary>
    [Fact]
    public void NodeCountAboveUint16_WrapsToZeroInTheCompiledDispatchTable()
    {
        RequireGcc();
        var rows = Enumerable.Range(0, ushort.MaxValue + 1)
            .Select(static index => V22Row(0, index % ushort.MaxValue, 100, NullDiff, 0, NullDiff, 0, 0, "n"))
            .ToArray();

        var text = ExportC(new NotchTable(rows));
        var strict = CompileC99(text);

        Assert.Contains("#define USER_NHC_NODE_NUM_IC1 (65536u)", text, StringComparison.Ordinal);
        Assert.NotEqual(0, strict.ExitCode);
        Assert.Contains("overflow", strict.StdErr, StringComparison.Ordinal);
        Assert.Empty(DumpV22Table(text, _noOverflow));
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

    private static ProjectSettings LegacySettings() =>
        CreateSettings(computationMode: NotchComputationMode.LegacyRegularAnchor);
}
