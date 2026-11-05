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
    private const int LowestProduct = -8_400_000;
    private const int HighestProduct = 8_400_000;

    private static readonly int[] _gridDiffIndices = [0];
    private static readonly int[] _v21Values = [0, 100, 110, 1, 1, 32, 2, 1, 32];
    private static readonly int[] _v22Values = [0, 110, 1, 25, 2, 25, 0];

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
    public void ScaleFunction_EqualsAnArithmeticShiftForEveryInt16TimesUint8Product()
    {
        var text = Export(NotchAlgorithmVersion.V21);
        Assert.Contains("s32Bias = 127;", text, StringComparison.Ordinal);
        Assert.Contains("return (s32Product - s32Bias) / 128;", text, StringComparison.Ordinal);

        // C integer division truncates toward zero, like C# integer division.
        var mismatches = 0;
        for (var product = LowestProduct; product <= HighestProduct; product++)
        {
            var bias = product < 0 ? 127 : 0;
            if ((product - bias) / 128 != product >> 7)
            {
                mismatches++;
            }
        }

        Assert.Equal(0, mismatches);
    }

    private static string Export(NotchAlgorithmVersion version)
    {
        var values = version == NotchAlgorithmVersion.V21 ? _v21Values : _v22Values;
        var row = new NotchTableRow(version, 0, 17, 0, 42, values, "q7");
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_gridDiffIndices, _ => 0);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "PAD", 0, 0, 1, 1)]);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings { NullValue = NullDiff, ComputationMode = NotchComputationMode.CadAllocation },
        };
        return NotchTableExporter.ExportAsCInitializer(new NotchTable([row]), cad, grid, settings, NotchExportProfile.Debug);
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
}
