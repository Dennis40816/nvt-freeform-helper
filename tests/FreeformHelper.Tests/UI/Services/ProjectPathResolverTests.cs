// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectPathResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void ResolveBlankPathRejectsBeforeProbing(string? path)
    {
        var result = FreeformHelper.UI.Services.ProjectPathResolver.Resolve(
            path, _ => throw new InvalidOperationException("Blank paths must not be probed."));

        Assert.Null(result.NormalizedPath);
        Assert.Equal("INVALID_ARGUMENTS", result.Code);
        Assert.Equal("Argument '--path' is required.", result.Message);
    }

    [Fact]
    public void ResolveInvalidCharacterRejectsWithTheOriginalPathBeforeProbing()
    {
        const string path = "  bad\0path.json  ";
        var error = Assert.Throws<ArgumentException>(() => Path.GetFullPath(path.Trim()));

        var result = FreeformHelper.UI.Services.ProjectPathResolver.Resolve(
            path, _ => throw new InvalidOperationException("Invalid paths must not be probed."));

        Assert.Null(result.NormalizedPath);
        Assert.Equal("INVALID_ARGUMENTS", result.Code);
        Assert.Equal($"Invalid project path '{path}': {error.Message}", result.Message);
    }

    [Theory]
    [InlineData("project.json")]
    [InlineData("  project.json \t")]
    public void ResolveAcceptedRelativePathProbesTheTrimmedFullPathOnce(string path)
    {
        var fullPath = Path.GetFullPath("project.json");
        var probedPaths = new List<string>();

        var result = FreeformHelper.UI.Services.ProjectPathResolver.Resolve(path, normalizedPath =>
        {
            probedPaths.Add(normalizedPath);
            return true;
        });

        Assert.Equal(fullPath, Assert.Single(probedPaths));
        Assert.Equal(fullPath, result.NormalizedPath);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    [Fact]
    public void ResolveMissingFileReturnsTheFullPathInTheResultAndMessage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, ".", "missing.json");
        var fullPath = Path.GetFullPath(path);
        var probedPaths = new List<string>();

        var result = FreeformHelper.UI.Services.ProjectPathResolver.Resolve(path, normalizedPath =>
        {
            probedPaths.Add(normalizedPath);
            return false;
        });

        Assert.Equal(fullPath, Assert.Single(probedPaths));
        Assert.Equal(fullPath, result.NormalizedPath);
        Assert.Equal("PATH_NOT_FOUND", result.Code);
        Assert.Equal($"Project file not found: {fullPath}", result.Message);
    }

    [Fact]
    public void ResolveProbeExceptionPropagatesWithoutBecomingAnInvalidPath()
    {
        var expected = new ArgumentException("Synthetic existence probe failure.");

        var actual = Assert.Throws<ArgumentException>(() => FreeformHelper.UI.Services.ProjectPathResolver.Resolve(
            "project.json", _ => throw expected));

        Assert.Same(expected, actual);
    }
}
