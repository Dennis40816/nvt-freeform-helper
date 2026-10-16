using System.Runtime.CompilerServices;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SpinnerParentProcessTests
{
    [Theory]
    [InlineData("argument")]
    [InlineData("invalid-operation")]
    public void TryGet_WhenTheLookupFailsWithADocumentedException_ReturnsFalse(string kind)
    {
        var found = SpinnerParentProcess.TryGet(1, _ => throw Failure(kind), out var process);

        Assert.False(found);
        Assert.Null(process);
    }

    [Fact]
    public void TryGet_WhenTheLookupRunsOutOfMemory_Propagates()
    {
        var outOfMemory = (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

        Assert.Throws<OutOfMemoryException>(() => SpinnerParentProcess.TryGet(1, _ => throw outOfMemory, out _));
    }

    [Fact]
    public void TryGet_WhenTheLookupFailsWithAnUndocumentedException_Propagates()
    {
        Assert.Throws<FormatException>(() => SpinnerParentProcess.TryGet(1, _ => throw new FormatException("unexpected"), out _));
    }

    [Fact]
    public void TryGet_ForTheCurrentProcess_ReturnsIt()
    {
        var found = SpinnerParentProcess.TryGet(Environment.ProcessId, out var process);

        Assert.True(found);
        using (process)
        {
            Assert.Equal(Environment.ProcessId, process!.Id);
        }
    }

    [Fact]
    public void TryGet_ForAnIdWithoutAProcess_ReturnsFalse()
    {
        var found = SpinnerParentProcess.TryGet(int.MaxValue, out var process);

        Assert.False(found);
        Assert.Null(process);
    }

    private static Exception Failure(string kind) => kind switch
    {
        "argument" => new ArgumentException("no running process has this id"),
        "invalid-operation" => new InvalidOperationException("the process cannot be reached"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
