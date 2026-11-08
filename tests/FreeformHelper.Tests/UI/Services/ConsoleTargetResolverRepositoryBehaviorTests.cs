// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("AppContextBaseDirectorySerial")]
public sealed class ConsoleTargetResolverRepositoryBehaviorTests
{
    [Theory]
    [InlineData("exports/run/report.csv")]
    [InlineData(@"exports\run\report.csv")]
    public void ResolveFindsRepositoryRelativeReportFromNestedWorkingDirectory(string target)
    {
        using var scope = new RepositoryScope();
        var expected = WriteReport(Path.Combine(scope.RepositoryPath, "exports", "run"));

        var result = ConsoleTargetResolver.Resolve(target);

        Assert.True(result.Exists);
        Assert.False(result.IsDirectory);
        Assert.Equal(expected, result.FullPath);
    }

    [Theory]
    [InlineData("report.csv", true)]
    [InlineData("missing.csv", false)]
    public void ResolveBareFileNameChoosesShortestExistingRepositoryMatch(string fileName, bool expectedExists)
    {
        using var scope = new RepositoryScope();
        WriteReport(Path.Combine(scope.RepositoryPath, "a", "archived", "run"));
        var expected = WriteReport(Path.Combine(scope.RepositoryPath, "z"));

        var result = ConsoleTargetResolver.Resolve(fileName);

        Assert.Equal(expectedExists, result.Exists);
        Assert.False(result.IsDirectory);
        Assert.Equal(expectedExists ? expected : null, result.FullPath);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveUsesRepositoryMarkerAboveApplicationFolderForRepositoryRelativeReport(bool hasRepositoryMarker)
    {
        using var scope = new RepositoryScope(hasRepositoryMarker);
        var expected = WriteReport(Path.Combine(scope.RepositoryPath, "exports"));
        Environment.CurrentDirectory = scope.OutsideRepositoryPath;

        var result = ConsoleTargetResolver.Resolve("exports/report.csv");

        Assert.Equal(hasRepositoryMarker, result.Exists);
        Assert.False(result.IsDirectory);
        Assert.Equal(hasRepositoryMarker ? expected : null, result.FullPath);
    }

    private static string WriteReport(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "report.csv");
        TestFiles.WriteText(path, "PAD_ID,X,Y\r\n7,10,20\r\n");
        return path;
    }

    private sealed class RepositoryScope : IDisposable
    {
        private const string BaseDirectoryKey = "APP_CONTEXT_BASE_DIRECTORY";
        private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
        private readonly (string WorkingDirectory, object? BaseDirectory) _previousState =
            (Environment.CurrentDirectory, AppContext.GetData(BaseDirectoryKey));

        public RepositoryScope(bool hasRepositoryMarker = true)
        {
            if (hasRepositoryMarker)
            {
                Directory.CreateDirectory(Path.Combine(RepositoryPath, ".git"));
            }

            var applicationDirectory = Directory.CreateDirectory(Path.Combine(RepositoryPath, "application"));
            var workingDirectory = Directory.CreateDirectory(Path.Combine(RepositoryPath, "work", "reports"));
            Directory.CreateDirectory(OutsideRepositoryPath);
            Environment.CurrentDirectory = workingDirectory.FullName;
            AppContext.SetData(BaseDirectoryKey, applicationDirectory.FullName + Path.DirectorySeparatorChar);
        }

        public string RepositoryPath => Path.Combine(_directory.FullName, "repository");
        public string OutsideRepositoryPath => Path.Combine(_directory.FullName, "outside");

        public void Dispose()
        {
            AppContext.SetData(BaseDirectoryKey, _previousState.BaseDirectory);
            Environment.CurrentDirectory = _previousState.WorkingDirectory;
            _directory.Delete(recursive: true);
        }
    }
}
