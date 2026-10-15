using System.Runtime.CompilerServices;
using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ExceptionPolicyTests
{
    public static TheoryData<Exception> RecoverableExceptions() => new()
    {
        new InvalidOperationException("invalid"),
        new ArgumentException("argument"),
        new ArgumentNullException("value"),
        new InvalidCastException("cast"),
        new IOException("io"),
        new UnauthorizedAccessException("access"),
        new FormatException("format"),
        new TimeoutException("timeout"),
        new NotSupportedException("not supported"),
        new OperationCanceledException("canceled"),
        new AggregateException(new InvalidOperationException("inner")),
        new AggregateException(CreateOutOfMemory()),
    };

    public static TheoryData<Exception> NonRecoverableExceptions() => new()
    {
        CreateOutOfMemory(),
        new InsufficientMemoryException("insufficient memory"),
    };

    [Theory]
    [MemberData(nameof(RecoverableExceptions))]
    public void IsRecoverable_ForOrdinaryFailures_ReturnsTrue(Exception exception)
    {
        Assert.True(ExceptionPolicy.IsRecoverable(exception));
    }

    [Theory]
    [MemberData(nameof(NonRecoverableExceptions))]
    public void IsRecoverable_ForOutOfMemory_ReturnsFalse(Exception exception)
    {
        Assert.False(ExceptionPolicy.IsRecoverable(exception));
    }

    [Fact]
    public void IsRecoverable_ForNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionPolicy.IsRecoverable(null!));
    }

    // The runtime reserves this type (CA2201), so the instance is created without running a constructor.
    private static OutOfMemoryException CreateOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));
}
