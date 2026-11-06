using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveProjectAsync_EmbedConfirmation_WritesChosenDxfContentAsync(bool embed)
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        var confirmations = 0;
        vm.ConfirmEmbedDxfAsync = () =>
        {
            confirmations++;
            return Task.FromResult(embed);
        };

        Assert.True(await vm.SaveProjectAsync());

        var saved = JsonProjectStore.Load(files.PathFor("project.json"));
        Assert.Equal(1, confirmations);
        Assert.False(vm.HasUnsavedChanges);
        Assert.NotNull(saved.LastDxfPath);
        if (embed)
        {
            Assert.Equal(Path.GetFileName(saved.LastDxfPath), saved.EmbeddedDxfName);
            Assert.Equal(await TestFiles.ReadBytesAsync(saved.LastDxfPath, TestContext.Current.CancellationToken), saved.EmbeddedDxf);
        }
        else
        {
            Assert.Null(saved.EmbeddedDxf);
            Assert.Null(saved.EmbeddedDxfName);
        }
    }

    [Fact]
    public async Task SaveProjectAsync_DecliningEmbedAfterEmbedding_RemovesSavedDxfContentAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync());
        Assert.NotEmpty(Assert.IsType<byte[]>(JsonProjectStore.Load(files.PathFor("project.json")).EmbeddedDxf));
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);

        Assert.True(await vm.SaveProjectAsync());

        var saved = JsonProjectStore.Load(files.PathFor("project.json"));
        Assert.Null(saved.EmbeddedDxf);
        Assert.Null(saved.EmbeddedDxfName);
        Assert.NotNull(saved.LastDxfPath);
    }

    [Fact]
    public void ShouldPromptSaveOnExit_WhenEmptyAndUnchanged_ReturnsFalse()
    {
        var vm = new FreeformHelperViewModel();

        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.ShouldPromptSaveOnExit());
    }


    [Fact]
    public async Task ShouldPromptSaveOnExit_WhenCadLoadedButNeverSaved_ReturnsTrue()
    {
        var dxfPath = CreateTempDxfFile(("L1", 0, 0, 10, 10));
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            Assert.NotEmpty(vm.CadPads);
            // Isolate never-saved content from dirty-state tracking.
            vm.HasUnsavedChanges = false;

            Assert.True(vm.ShouldPromptSaveOnExit());
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }


    [Fact]
    public async Task ShouldPromptSaveOnExit_WhenCadLoadedAndSavedWithoutChanges_ReturnsFalse()
    {
        var dxfPath = CreateTempDxfFile(("L1", 0, 0, 10, 10));
        var projectPath = TestFiles.NewTempFilePath("freeform-helper-", ".json");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
                PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync = () => Task.FromResult(false),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            Assert.NotEmpty(vm.CadPads);
            Assert.True(await vm.SaveProjectAsync());

            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.ShouldPromptSaveOnExit());
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(projectPath);
        }
    }


    [Fact]
    public void ShouldPromptSaveOnExit_WhenUnsavedChangesExistWithoutLoadedContent_ReturnsTrue()
    {
        var vm = new FreeformHelperViewModel
        {
            HasUnsavedChanges = true,
        };

        Assert.True(vm.ShouldPromptSaveOnExit());
    }


    [Fact]
    public async Task SaveProjectAsync_WhenDialogHandlerMissing_ReturnsFalse()
    {
        var vm = new FreeformHelperViewModel();

        var result = await vm.SaveProjectAsync();

        Assert.False(result);
        Assert.Equal("Save project: dialog handler not wired.", vm.StatusText);
    }


    [Fact]
    public async Task SaveProjectAsync_WhenUserCancels_KeepsUnsavedState()
    {
        var vm = new FreeformHelperViewModel
        {
            HasUnsavedChanges = true,
            PickSaveProjectPathAsync = () => Task.FromResult<string?>(null),
        };

        var result = await vm.SaveProjectAsync();

        Assert.False(result);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.ShouldPromptSaveOnExit());
    }


    [Fact]
    public async Task SaveProjectAsync_WhenSucceeded_ClearsUnsavedState()
    {
        var vm = new FreeformHelperViewModel
        {
            HasUnsavedChanges = true,
        };

        var path = TestFiles.NewTempFilePath("freeform-helper-", ".json");
        try
        {
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);

            var result = await vm.SaveProjectAsync();

            Assert.True(result);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.ShouldPromptSaveOnExit());
            Assert.StartsWith("Project saved:", vm.StatusText, StringComparison.Ordinal);
            Assert.True(File.Exists(path));
            var saved = JsonProjectStore.Load(path);
            Assert.Equal(MatchMode.LegacyOverlap, saved.Settings.Matching.Mode);
            Assert.True(saved.Settings.Matching.EnableCentroidFallback);
            Assert.Equal(5, saved.Settings.Matching.NearestK);
            Assert.Equal("Overlap", saved.UiSnapshot.Matching.MatchMode);
            Assert.True(saved.UiSnapshot.Matching.EnableCentroidFallback);
            Assert.Equal(5, saved.UiSnapshot.Matching.NearestK);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }


    [Fact]
    public async Task SaveProjectAsync_WhenIoError_ReturnsFalseAndKeepsUnsavedState()
    {
        var vm = new FreeformHelperViewModel
        {
            HasUnsavedChanges = true,
        };

        var directoryPath = TestFiles.NewTempFilePath("freeform-helper-dir-", "");
        Directory.CreateDirectory(directoryPath);
        try
        {
            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(directoryPath);
            vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);

            var result = await vm.SaveProjectAsync();

            Assert.False(result);
            Assert.True(vm.HasUnsavedChanges);
            Assert.True(vm.ShouldPromptSaveOnExit());
            Assert.StartsWith("Save project failed:", vm.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
