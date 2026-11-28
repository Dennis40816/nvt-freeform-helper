// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using System.Text;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

internal static class NotchFullTableBaseline
{
    private const string RecordVariable = "NFH_RECORD_NOTCH_BASELINE";

    public static string Serialize(NotchTable table)
    {
        var text = new StringBuilder();
        foreach (var row in table.Rows)
        {
            // Keep the literal snapshot prefix and comment field; append the independent typed node.
            text.Append(CultureInfo.InvariantCulture,
                $"{row.Version}|IC{row.IcIndex}|DIFF{row.DiffIndex}|REG{row.RegularPadIndex}|CAD{row.CadPadId}|");
            text.Append(Integers(row.Values)).Append('|').Append(Escape(row.Comment)).Append("|NODE=");
            text.Append(row.V22Node is { } node ? Node(node) : "null").Append('\n');
        }

        var audit = table.ToFullCoverageAudit;
        text.Append(CultureInfo.InvariantCulture,
            $"AUDIT|BUCKETS{audit.BucketCount}|EXPECTED{audit.ExpectedTargetDiffCount}|COVERED{audit.CoveredTargetDiffCount}");
        text.Append(CultureInfo.InvariantCulture,
            $"|MISSING{audit.MissingTargetDiffCount}|EXPECTATIONS{audit.HasExpectations}|GAPS{audit.HasMissingCoverage}\n");
        foreach (var gap in audit.MissingGaps)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"GAP|IC{gap.IcIndex}|SOURCE{gap.SourceDiffIndex}|TARGET{gap.TargetDiffIndex}|CAD");
            text.Append(Integers(gap.CadPadIds)).Append('\n');
        }

        return text.ToString();
    }

    public static void Match(string fileName, string actual)
    {
        var source = TestPaths.FromRepo("tests", "FreeformHelper.Tests", "Application", "Notch",
            nameof(NotchFullTableCharacterizationTests) + ".cs");
        Assert.True(File.Exists(source), $"Cannot locate characterization source: {source}");
        var directory = Path.Combine(Path.GetDirectoryName(source)!, "Baselines");
        var path = Path.Combine(directory, fileName);
        if (string.Equals(TestEnvironment.Get(RecordVariable), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(directory);
            TestFiles.WriteBytes(path, Encoding.UTF8.GetBytes(actual));
            Assert.Fail($"baseline recorded, rerun without {RecordVariable}: {path}");
        }

        Assert.True(File.Exists(path),
            $"Missing Notch baseline: {path}. Run this class on the unchanged generator with {RecordVariable}=1, " +
            "review and commit the resulting files, then unset the variable and rerun.");
        // The generated text never contains a raw CR (comments escape it), so the only line ending
        // the checked-in file may differ in is the checkout's CRLF conversion; undo exactly that.
        var expected = Encoding.UTF8.GetString(TestFiles.ReadBytes(path)).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(string.Equals(expected, actual, StringComparison.Ordinal), Difference(expected, actual, path));
    }

    public static void Equal(string expected, string actual, string route) =>
        Assert.True(string.Equals(expected, actual, StringComparison.Ordinal), Difference(expected, actual, route));

    private static string Node(NotchV22Node node) => Integers(
        [node.AnchorDiffIndex, node.CombinePercent, node.TargetDiffIndex1, node.TargetRatioPercent1,
         node.TargetDiffIndex2, node.TargetRatioPercent2, node.Flags]);

    private static string Integers(IEnumerable<int> values) =>
        string.Join(',', values.Select(static value => value.ToString(CultureInfo.InvariantCulture)));

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Difference(string expected, string actual, string context)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var index = 0;
        while (index < expectedLines.Length && index < actualLines.Length &&
               string.Equals(expectedLines[index], actualLines[index], StringComparison.Ordinal))
        {
            index++;
        }

        var expectedLine = index < expectedLines.Length ? Escape(expectedLines[index]) : "<EOF>";
        var actualLine = index < actualLines.Length ? Escape(actualLines[index]) : "<EOF>";
        var expectedCount = expected.Length == 0 ? 0 : expectedLines.Length - (expected.EndsWith('\n') ? 1 : 0);
        var actualCount = actual.Length == 0 ? 0 : actualLines.Length - (actual.EndsWith('\n') ? 1 : 0);
        return FormattableString.Invariant(
            $"{context}: first differing line {index + 1}; expected {expectedCount} lines, actual {actualCount} lines.\n") +
            $"Expected: {expectedLine}\nActual:   {actualLine}\n" +
            "Comparison is exact except that the file's CRLF line endings are read as LF; BOMs and the final LF count.";
    }
}
