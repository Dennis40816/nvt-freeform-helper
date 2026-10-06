using System.Runtime.CompilerServices;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Locates the private panel example data under <c>example/</c>. The data is confidential and lives in a
/// separate repository, so a checkout without it must skip the tests that read it instead of failing them.
/// </summary>
internal static class ExampleData
{
    /// <summary>
    /// When set to <c>1</c>, missing data is an error instead of a skip. Gates that are expected to have
    /// the data (owner machine, CI with access) set this so a missing checkout cannot pass silently.
    /// </summary>
    public const string RequireEnvironmentVariable = "FREEFORMHELPER_REQUIRE_EXAMPLE_DATA";

    public const string MissingMessage =
        "Private example data is not checked out under 'example/'. " +
        "Run 'git submodule update --init example' if you have access.";

    public static string Root { get; } = TestPaths.FromRepo("example");

    public static bool IsAvailable { get; } =
        Directory.Exists(Root) && Directory.EnumerateFileSystemEntries(Root).Any();

    public static bool IsRequired { get; } = string.Equals(
        Environment.GetEnvironmentVariable(RequireEnvironmentVariable),
        "1",
        StringComparison.Ordinal);

    public static string? SkipReason { get; } = IsAvailable || IsRequired ? null : MissingMessage;
}

/// <summary>A fact that reads the private example data; skipped when the data is not checked out.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExampleDataFactAttribute : FactAttribute
{
    public ExampleDataFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = ExampleData.SkipReason;
    }
}

/// <summary>A theory that reads the private example data; skipped when the data is not checked out.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExampleDataTheoryAttribute : TheoryAttribute
{
    public ExampleDataTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = ExampleData.SkipReason;
    }
}
