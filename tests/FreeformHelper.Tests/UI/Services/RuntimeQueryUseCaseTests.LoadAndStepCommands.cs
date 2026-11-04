using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_QueryLoadProject_WithoutPath_ReturnsInvalidArguments()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };

        var response = await ExecuteQueryAsync(shell, "load-project");

        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
        Assert.Contains("--path", response.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_QueryLoadProject_WithUnreadableProjectFile_ReturnsLoadFailedAndLeavesTheShellEmpty()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var directory = TestFiles.CreateTempDirectory();
        var path = Path.Combine(directory.FullName, "broken-project.json");
        try
        {
            await using (var writer = new StreamWriter(path, append: false))
            {
                await writer.WriteAsync("{");
            }

            var response = await ExecuteQueryAsync(shell, "load-project", ("path", path));

            Assert.False(response.Ok);
            Assert.Equal("LOAD_FAILED", response.Error?.Code);
            Assert.Empty(shell.FreeformHelper.CadPads);
            Assert.Empty(shell.FreeformHelper.RegularPads);
            Assert.Equal(ProjectSessionMode.Ready, shell.FreeformHelper.SessionMode);
        }
        finally
        {
            TestDirectoryCleanup.DeleteBestEffort(directory.FullName);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("5")]
    [InlineData("x")]
    public async Task ExecuteAsync_QueryRunStep_WithMissingOrOutOfRangeStep_ReturnsInvalidArguments(string? step)
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var args = step is null ? Array.Empty<(string, string)>() : [("step", step)];

        var response = await ExecuteQueryAsync(shell, "run-step", args);

        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("4")]
    public async Task ExecuteAsync_QueryRunStep_WithoutAProjectLoaded_ReturnsStepNotReady(string step)
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };

        var response = await ExecuteQueryAsync(shell, "run-step", ("step", step));

        Assert.False(response.Ok);
        Assert.Equal("STEP_NOT_READY", response.Error?.Code);
        Assert.False(string.IsNullOrWhiteSpace(response.Error?.Message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("6")]
    public async Task ExecuteAsync_QueryClearStep_WithMissingOrOutOfRangeStep_ReturnsInvalidArguments(string? step)
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var args = step is null ? Array.Empty<(string, string)>() : [("step", step)];

        var response = await ExecuteQueryAsync(shell, "clear-step", args);

        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    public async Task ExecuteAsync_QueryClearStep_WithoutAProjectLoaded_SucceedsAndReportsAnEmptyWorkflow(string step)
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };

        var response = await ExecuteQueryAsync(shell, "clear-step", ("step", step));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal(int.Parse(step, System.Globalization.CultureInfo.InvariantCulture), root.GetProperty("step").GetInt32());
        var workflow = root.GetProperty("workflow");
        Assert.False(workflow.GetProperty("hasCad").GetBoolean());
        Assert.False(workflow.GetProperty("hasGrid").GetBoolean());
        Assert.False(workflow.GetProperty("hasStep1Result").GetBoolean());
        Assert.False(workflow.GetProperty("hasStep4Result").GetBoolean());
    }
}
