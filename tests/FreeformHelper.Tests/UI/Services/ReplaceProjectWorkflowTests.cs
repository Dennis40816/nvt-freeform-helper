// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Verifies replacement plans, awaited step order and recovery deletion after a successful save.</summary>
public sealed class ReplaceProjectWorkflowTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task CancelledPickerStopsBeforeCheckingUnsavedWorkAsync(string? path)
    {
        var steps = new PreparationSteps { Path = path };

        var plan = await steps.PrepareAsync();

        Assert.Equal(FreeformHelper.UI.Services.ReplaceProjectOutcome.Cancel, plan.Outcome);
        Assert.Null(plan.Path);
        Assert.Null(plan.BackupPath);
        Assert.Equal("Load cancelled.", plan.Message);
        Assert.Equal(["pick"], steps.Calls);
    }

    [Fact]
    public async Task NoUnsavedWorkPlansLoadWithoutBackupOrPromptAsync()
    {
        var steps = new PreparationSteps { HasUnsavedChanges = false };

        var plan = await steps.PrepareAsync();

        Assert.Equal(FreeformHelper.UI.Services.ReplaceProjectOutcome.Load, plan.Outcome);
        Assert.Equal("replacement.json", plan.Path);
        Assert.Null(plan.BackupPath);
        Assert.Equal(["pick", "check-unsaved"], steps.Calls);
    }

    [Theory]
    [InlineData(null, "Cancel", "Load cancelled: unsaved changes were kept.")]
    [InlineData(false, "Load", "")]
    [InlineData(true, "SaveThenLoad", "Saving before load.")]
    public async Task UnsavedWorkBacksUpBeforePromptAndReturnsTheChosenPlanAsync(
        bool? choice, string expected, string message)
    {
        var steps = new PreparationSteps { Choice = choice };

        var plan = await steps.PrepareAsync();

        Assert.Equal(expected, plan.Outcome.ToString());
        Assert.Equal("replacement.json", plan.Path);
        Assert.Equal("backup.json", plan.BackupPath);
        Assert.Equal(message, plan.Message);
        Assert.Equal(["pick", "check-unsaved", "backup", "get-prompt", "prompt:backup.json"], steps.Calls);
    }

    [Fact]
    public async Task UnwiredPromptPlansLoadAfterBackupAsync()
    {
        var steps = new PreparationSteps { PromptWired = false };

        var plan = await steps.PrepareAsync();

        Assert.Equal(FreeformHelper.UI.Services.ReplaceProjectOutcome.Load, plan.Outcome);
        Assert.Equal("replacement.json", plan.Path);
        Assert.Equal("backup.json", plan.BackupPath);
        Assert.Equal(["pick", "check-unsaved", "backup", "get-prompt"], steps.Calls);
    }

    [Theory]
    [InlineData(null, "Cancel")]
    [InlineData(false, "Load")]
    [InlineData(true, "SaveThenLoad")]
    public async Task RejectedBackupStillPromptsWithNullPathAsync(
        bool? choice, string expected)
    {
        var steps = new PreparationSteps { BackupPath = null, Choice = choice };

        var plan = await steps.PrepareAsync();

        Assert.Equal(expected, plan.Outcome.ToString());
        Assert.Equal("replacement.json", plan.Path);
        Assert.Null(plan.BackupPath);
        Assert.Equal(["pick", "check-unsaved", "backup", "get-prompt", "prompt:"], steps.Calls);
    }

    [Fact]
    public async Task PreparationWaitsForPickerThenBackupThenPromptAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var calls = new ConcurrentQueue<string>();
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backup = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompt = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var promptEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparing = FreeformHelper.UI.Services.ReplaceProjectWorkflow.PrepareAsync(
            () =>
            {
                calls.Enqueue("pick");
                return picker.Task.WaitAsync(token);
            },
            () =>
            {
                calls.Enqueue("check-unsaved");
                return true;
            },
            () =>
            {
                calls.Enqueue("backup");
                backupEntered.TrySetResult();
                return backup.Task.WaitAsync(token);
            },
            () =>
            {
                calls.Enqueue("get-prompt");
                return path =>
                {
                    calls.Enqueue($"prompt:{path}");
                    promptEntered.TrySetResult();
                    return prompt.Task.WaitAsync(token);
                };
            });
        try
        {
            Assert.Equal(["pick"], calls);
            Assert.False(preparing.IsCompleted);
            picker.SetResult("replacement.json");
            await backupEntered.Task.WaitAsync(token);
            Assert.Equal(["pick", "check-unsaved", "backup"], calls);
            Assert.False(preparing.IsCompleted);
            backup.SetResult("backup.json");
            await promptEntered.Task.WaitAsync(token);
            Assert.Equal(["pick", "check-unsaved", "backup", "get-prompt", "prompt:backup.json"], calls);
            Assert.False(preparing.IsCompleted);
        }
        finally
        {
            picker.TrySetResult(null);
            backup.TrySetResult(null);
            prompt.TrySetResult(true);
        }

        var plan = await preparing.WaitAsync(token);
        Assert.Equal(FreeformHelper.UI.Services.ReplaceProjectOutcome.SaveThenLoad, plan.Outcome);
        Assert.Equal("replacement.json", plan.Path);
        Assert.Equal("backup.json", plan.BackupPath);
    }

    [Fact]
    public async Task PromptIsResolvedAfterBackupCanWireItAsync()
    {
        Func<string?, Task<bool?>>? prompt = null;

        var plan = await FreeformHelper.UI.Services.ReplaceProjectWorkflow.PrepareAsync(
            () => Task.FromResult<string?>("replacement.json"),
            () => true,
            () =>
            {
                prompt = _ => Task.FromResult<bool?>(true);
                return Task.FromResult<string?>("backup.json");
            },
            () => prompt);

        Assert.Equal(FreeformHelper.UI.Services.ReplaceProjectOutcome.SaveThenLoad, plan.Outcome);
    }

    [Fact]
    public async Task CancelledOrFailedSaveStopsBeforeDeletingBackupOrLoadingAsync()
    {
        var calls = new List<string>();

        var saved = await FreeformHelper.UI.Services.ReplaceProjectWorkflow.SaveThenLoadAsync(
            "replacement.json", "backup.json",
            () =>
            {
                calls.Add("save");
                return Task.FromResult(false);
            },
            _ => calls.Add("delete"),
            _ =>
            {
                calls.Add("load");
                return Task.CompletedTask;
            });

        Assert.False(saved);
        Assert.Equal(["save"], calls);
    }

    [Theory]
    [InlineData(true, "backup.json")]
    [InlineData(false, "backup.json")]
    [InlineData(true, null)]
    public async Task SuccessfulSaveDeletesBackupBeforeReplacementRegardlessOfLoadResultAsync(
        bool replacementSucceeded, string? backupPath)
    {
        var steps = new PreparationSteps { Choice = true, BackupPath = backupPath };
        var plan = await steps.PrepareAsync();
        Assert.Equal(FreeformHelper.UI.Services.ReplaceProjectOutcome.SaveThenLoad, plan.Outcome);

        var saved = await FreeformHelper.UI.Services.ReplaceProjectWorkflow.SaveThenLoadAsync(
            plan.Path!, plan.BackupPath,
            () =>
            {
                steps.Calls.Enqueue("save");
                return Task.FromResult(true);
            },
            path => steps.Calls.Enqueue($"delete:{path}"),
            path =>
            {
                steps.Calls.Enqueue($"load:{path}");
                return Task.FromResult(replacementSucceeded);
            });

        Assert.True(saved);
        Assert.Equal(new[] { "pick", "check-unsaved", "backup", "get-prompt", $"prompt:{backupPath}",
            "save", $"delete:{backupPath}", "load:replacement.json" }, steps.Calls);
    }

    [Fact]
    public async Task SaveCompletionPrecedesDeleteAndLoadCompletionIsAwaitedAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var calls = new ConcurrentQueue<string>();
        var save = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacing = FreeformHelper.UI.Services.ReplaceProjectWorkflow.SaveThenLoadAsync(
            "replacement.json", "backup.json",
            () =>
            {
                calls.Enqueue("save");
                return save.Task.WaitAsync(token);
            },
            path => calls.Enqueue($"delete:{path}"),
            path =>
            {
                calls.Enqueue($"load:{path}");
                loadEntered.TrySetResult();
                return load.Task.WaitAsync(token);
            });
        try
        {
            Assert.Equal(["save"], calls);
            Assert.False(replacing.IsCompleted);
            save.SetResult(true);
            await loadEntered.Task.WaitAsync(token);
            Assert.Equal(["save", "delete:backup.json", "load:replacement.json"], calls);
            Assert.False(replacing.IsCompleted);
        }
        finally
        {
            save.TrySetResult(false);
            load.TrySetResult();
        }

        Assert.True(await replacing.WaitAsync(token));
    }

    [Fact]
    public async Task SaveExceptionPropagatesBeforeDeleteOrLoadAsync()
    {
        var failure = new InvalidOperationException("save failed");
        var calls = new List<string>();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FreeformHelper.UI.Services.ReplaceProjectWorkflow.SaveThenLoadAsync(
                "replacement.json", "backup.json",
                () => Task.FromException<bool>(failure),
                _ => calls.Add("delete"),
                _ =>
                {
                    calls.Add("load");
                    return Task.CompletedTask;
                }));

        Assert.Same(failure, actual);
        Assert.Empty(calls);
    }

    private sealed class PreparationSteps
    {
        // Configuration is set before execution; observations use a concurrent queue.
        internal ConcurrentQueue<string> Calls { get; } = new();
        internal string? Path { get; init; } = "replacement.json";
        internal bool HasUnsavedChanges { get; init; } = true;
        internal string? BackupPath { get; init; } = "backup.json";
        internal bool PromptWired { get; init; } = true;
        internal bool? Choice { get; init; }

        internal Task<FreeformHelper.UI.Services.ReplaceProjectPlan> PrepareAsync()
        {
            return FreeformHelper.UI.Services.ReplaceProjectWorkflow.PrepareAsync(
                PickPathAsync, HasUnsavedWork, BackUpAsync, GetPrompt);
        }

        private Task<string?> PickPathAsync()
        {
            Calls.Enqueue("pick");
            return Task.FromResult(Path);
        }

        private bool HasUnsavedWork()
        {
            Calls.Enqueue("check-unsaved");
            return HasUnsavedChanges;
        }

        private Task<string?> BackUpAsync()
        {
            Calls.Enqueue("backup");
            return Task.FromResult(BackupPath);
        }

        private Func<string?, Task<bool?>>? GetPrompt()
        {
            Calls.Enqueue("get-prompt");
            return PromptWired ? PromptAsync : null;
        }

        private Task<bool?> PromptAsync(string? path)
        {
            Calls.Enqueue($"prompt:{path}");
            return Task.FromResult(Choice);
        }
    }
}
