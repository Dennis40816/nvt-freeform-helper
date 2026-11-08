// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualSizingParsingBehaviorTests
{
    private static readonly int[] _expectedColumns = [0, 1, 2, 3];

    [Fact]
    public void TryParseColRange_WithoutBuiltGrid_ReturnsNoColumnsAndExplainsWhy()
    {
        var success = ManualSizingService.TryParseColRange("0-1", totalCols: 0, out var columns, out var error);

        Assert.False(success);
        Assert.Empty(columns);
        Assert.Equal("Grid not built.", error);
    }

    [Fact]
    public void TryParseColRange_InvalidInput_ReturnsDiagnosticWithoutPartiallySelectedColumns()
    {
        using var culture = TestCulture.UseEnUs();
        (string Input, string Error)[] cases =
        [
            ("1-2-3", "Invalid range format: '1-2-3'. Expected 'start-end'."),
            ("A-2", "Invalid numbers in range: 'A-2'."),
            ("0,2-4", "Index 4 in range '2-4' is out of bounds (0-3)."),
            ("2,pad", "Invalid index format: 'pad'."),
        ];

        foreach (var (input, expectedError) in cases)
        {
            var success = ManualSizingService.TryParseColRange(input, totalCols: 4, out var columns, out var error);

            Assert.False(success);
            Assert.Empty(columns);
            Assert.Equal(expectedError, error);
        }
    }

    [Fact]
    public void TryParseColRange_PastedListWithEmptySegments_ReturnsSortedUniqueColumns()
    {
        var success = ManualSizingService.TryParseColRange(" 3, , 1-2, 2, 0, ", totalCols: 4, out var columns, out var error);

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal(_expectedColumns, columns);
    }
}
