// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportCommentTests
{
    private const int NullDiff = 65534;
    private static readonly int[] _gridDiffIndices = [0];
    private static readonly int[] _v21Values = [0, 100, 110, 1, 1, 32, 2, 1, 32];
    private static readonly int[] _v22Values = [0, 110, 1, 25, 2, 25, 0];

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, "hostile first\r\nnewline tail")]
    [InlineData(NotchAlgorithmVersion.V21, "hostile */ /* tail // nested")]
    [InlineData(NotchAlgorithmVersion.V21, "hostile C:\\pads tail\\")]
    [InlineData(NotchAlgorithmVersion.V22, "hostile first\nnewline tail")]
    [InlineData(NotchAlgorithmVersion.V22, "hostile */ /* tail // nested")]
    [InlineData(NotchAlgorithmVersion.V22, "hostile C:\\pads tail\\")]
    public void ExportAsCInitializer_HostileRowComment_StaysOnOneSafeCommentLine(
        NotchAlgorithmVersion version,
        string comment)
    {
        var values = version == NotchAlgorithmVersion.V21 ? _v21Values : _v22Values;
        var row = new NotchTableRow(version, 0, 17, 0, 42, values, comment);

        var text = Export(row);

        var commentLines = text
            .Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Where(static line => line.Contains("hostile", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(commentLines);
        foreach (var line in commentLines)
        {
            Assert.Contains("tail", line, StringComparison.Ordinal);
            var commentText = line[(line.IndexOf("// ", StringComparison.Ordinal) + 3)..];
            Assert.DoesNotContain("/*", commentText, StringComparison.Ordinal);
            Assert.DoesNotContain("*/", commentText, StringComparison.Ordinal);
            Assert.DoesNotContain("//", commentText, StringComparison.Ordinal);
            Assert.False(line.EndsWith('\\'), line);
        }
    }

    [Theory]
    [InlineData("a\rb\nc", "a b c")]
    [InlineData("*/ /* // ///", "* / / * / / / / /")]
    [InlineData("C:\\pads\\", "C:\\pads")]
    [InlineData("tail\\ \\\n", "tail")]
    [InlineData("row??/", "row? ?/")]
    [InlineData("what??", "what? ?")]
    public void SanitizeLineComment_BreaksMarkersNewlinesAndTrailingBackslash(string comment, string expected)
    {
        Assert.Equal(expected, NotchFirmwareCExporter.SanitizeLineComment(comment));
    }

    private static string Export(NotchTableRow row)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_gridDiffIndices, _ => 0);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "PAD", 0, 0, 1, 1)]);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings { NullValue = NullDiff, ComputationMode = NotchComputationMode.CadAllocation },
        };
        return NotchTableExporter.ExportAsCInitializer(new NotchTable([row]), cad, grid, settings, NotchExportProfile.Debug);
    }
}
