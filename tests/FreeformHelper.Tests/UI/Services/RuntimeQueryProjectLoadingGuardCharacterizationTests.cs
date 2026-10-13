using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the runtime query entry does while a project load is in progress (issue #64).
/// The UI commands are disabled during a load because <c>IsProjectEditingEnabled</c> is false.
/// The runtime query mutation handlers do not check that state, so a query such as
/// <c>set-tofull</c> changes the project while the load still owns it. These tests describe the
/// current result, not the wanted one. A fix puts one guard at the use-case boundary and returns a
/// busy error, which changes the expected values here and must say so in its pull request.
/// </summary>
[Collection("RuntimeQueryUseCaseSerial")]
public sealed class RuntimeQueryProjectLoadingGuardCharacterizationTests
{
    [Fact]
    public async Task SetToFull_WhileProjectLoadIsPending_ChangesTheSettingWithoutBusyError()
    {
        using var shell = new ShellViewModel();
        var helper = shell.FreeformHelper;
        using var gate = new SemaphoreSlim(0, 1);
        helper.PickLoadProjectPathAsync = () => WaitForGateAsync(gate);
        var before = helper.EnableToFull;
        var requested = !before;
        var useCase = new RuntimeQueryUseCase(shell);

        var load = helper.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.Equal(ProjectSessionMode.LoadingProject, helper.SessionMode);
            Assert.False(helper.IsProjectEditingEnabled);

            var args = new Dictionary<string, string>
            {
                ["enable"] = requested ? "true" : "false",
            };
            var response = await useCase.ExecuteAsync(
                new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "set-tofull", args));

            Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
            Assert.Equal(requested, helper.EnableToFull);
        }
        finally
        {
            gate.Release();
            await load;
        }
    }

    private static async Task<string?> WaitForGateAsync(SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        return null;
    }
}
