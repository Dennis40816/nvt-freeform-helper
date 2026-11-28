// Copyright (c) 2026 Dennis Liu. All rights reserved.
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.ViewModels;
using Xunit;
using ProjectEditingSession = FreeformHelper.UI.Services.ProjectEditingSession;

namespace FreeformHelper.Tests;

public sealed class ProjectEditingSessionTests
{
    [Fact]
    public async Task LoadPublishesModeThenCommandsInRegistrationOrderAndReturnsItsValueAsync()
    {
        var events = new List<string>();
        var session = CreateObservedSession(events);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var load = session.RunLoadAsync(() => release.Task.WaitAsync(TestContext.Current.CancellationToken));
        try
        {
            AssertLoadingAvailability(events);
            Assert.False(session.IsProjectEditingEnabled);
        }
        finally
        {
            release.TrySetResult(42);
        }

        Assert.Equal(42, await load.WaitAsync(TestContext.Current.CancellationToken));
        AssertCompleteAvailabilityCycle(events);
        Assert.True(session.IsProjectEditingEnabled);
    }

    [Fact]
    public async Task CancelledLoadPreservesTheResultAndRestoresAvailabilityAsync()
    {
        var events = new List<string>();
        var session = CreateObservedSession(events);
        var cancelled = ProjectLoadCommandResult.Failed("CANCELLED", "Load cancelled.");

        var result = await session.RunLoadAsync(() => Task.FromResult(cancelled));

        Assert.Same(cancelled, result);
        AssertCompleteAvailabilityCycle(events);
        Assert.True(session.IsProjectEditingEnabled);
    }

    [Fact]
    public async Task FaultedLoadRestoresReadyAndCommandAvailabilityInOrderAsync()
    {
        var events = new List<string>();
        var session = CreateObservedSession(events);
        var command = session.Create(() => { });
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Load failed.");
        var load = session.RunLoadAsync(() => release.Task.WaitAsync(TestContext.Current.CancellationToken));
        try
        {
            AssertLoadingAvailability(events);
            Assert.False(command.CanExecute(null));
        }
        finally
        {
            release.TrySetException(failure);
        }

        InvalidOperationException? thrown = null;
        try
        {
            await load;
        }
        catch (InvalidOperationException ex)
        {
            thrown = ex;
        }

        Assert.Same(failure, thrown);
        AssertCompleteAvailabilityCycle(events);
        Assert.True(session.IsProjectEditingEnabled);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task SynchronousLoadExceptionAlsoRestoresReadyAsync()
    {
        var session = new ProjectEditingSession();
        var command = session.Create(() => { });
        var failure = new InvalidOperationException("Load delegate failed.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.RunLoadAsync<bool>(() => throw failure));

        Assert.Same(failure, thrown);
        Assert.True(session.IsProjectEditingEnabled);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task DirectExecutionOfEveryCommandShapeIsGuardedDuringLoadAsync()
    {
        var session = new ProjectEditingSession();
        var calls = new List<string?>();
        var sync = session.Create(() => calls.Add("sync"));
        var typed = session.Create<string>(calls.Add);
        var asyncCommand = session.CreateAsyncCommand(() =>
        {
            calls.Add("async");
            return Task.CompletedTask;
        });
        var resultCommand = session.CreateAsyncCommand(() =>
        {
            calls.Add("result");
            return Task.FromResult(true);
        });
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = session.RunLoadAsync(() => release.Task.WaitAsync(TestContext.Current.CancellationToken));
        try
        {
            Assert.False(sync.CanExecute(null));
            Assert.False(typed.CanExecute("blocked"));
            Assert.False(asyncCommand.CanExecute(null));
            Assert.False(resultCommand.CanExecute(null));
            sync.Execute(null);
            typed.Execute("blocked");
            await asyncCommand.ExecuteAsync(null);
            var result = resultCommand.ExecuteAsync(null);

            Assert.False(await Assert.IsAssignableFrom<Task<bool>>(result));
            Assert.Empty(calls);
        }
        finally
        {
            release.TrySetResult(true);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        sync.Execute(null);
        typed.Execute(null);
        await asyncCommand.ExecuteAsync(null);
        Assert.True(await Assert.IsAssignableFrom<Task<bool>>(resultCommand.ExecuteAsync(null)));
        Assert.Equal(["sync", null, "async", "result"], calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BooleanCommandReturnsTheDelegateResultAsync(bool result)
    {
        var session = new ProjectEditingSession();
        var command = session.CreateAsyncCommand(() => Task.FromResult(result));

        var execution = command.ExecuteAsync(null);

        Assert.Equal(result, await Assert.IsAssignableFrom<Task<bool>>(execution));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SynchronousCommandHonorsItsAdditionalPredicateOnDirectExecution(bool allowed)
    {
        var session = new ProjectEditingSession();
        var calls = 0;
        var command = session.Create(() => calls++, () => allowed);

        command.Execute(null);

        Assert.Equal(allowed, command.CanExecute(null));
        Assert.Equal(allowed ? 1 : 0, calls);
    }

    private static string ModeName(ProjectEditingSession session)
        => session.IsProjectEditingEnabled ? "Ready" : "LoadingProject";

    private static ProjectEditingSession CreateObservedSession(List<string> events)
    {
        ProjectEditingSession session = null!;
        session = new ProjectEditingSession(change =>
        {
            events.Add($"Changing:{ModeName(session)}:{session.IsProjectEditingEnabled}");
            change();
            events.Add($"AvailabilityChanged:{ModeName(session)}:{session.IsProjectEditingEnabled}");
        });
        Observe(session.Create(() => { }), "sync");
        Observe(session.Create<string>(_ => { }), "typed");
        Observe(session.CreateAsyncCommand(() => Task.CompletedTask), "async");
        Observe(session.CreateAsyncCommand(() => Task.FromResult(true)), "result");
        return session;

        void Observe(IRelayCommand command, string name)
        {
            command.CanExecuteChanged += (_, _) =>
                events.Add($"CanExecuteChanged:{name}:{ModeName(session)}:{session.IsProjectEditingEnabled}");
        }
    }

    private static void AssertLoadingAvailability(List<string> events)
    {
        Assert.Equal([
            "Changing:Ready:True",
            "AvailabilityChanged:LoadingProject:False",
            "CanExecuteChanged:sync:LoadingProject:False",
            "CanExecuteChanged:typed:LoadingProject:False",
            "CanExecuteChanged:async:LoadingProject:False",
            "CanExecuteChanged:result:LoadingProject:False",
        ], events);
    }

    private static void AssertCompleteAvailabilityCycle(List<string> events)
    {
        Assert.Equal([
            "Changing:Ready:True",
            "AvailabilityChanged:LoadingProject:False",
            "CanExecuteChanged:sync:LoadingProject:False",
            "CanExecuteChanged:typed:LoadingProject:False",
            "CanExecuteChanged:async:LoadingProject:False",
            "CanExecuteChanged:result:LoadingProject:False",
            "Changing:LoadingProject:False",
            "AvailabilityChanged:Ready:True",
            "CanExecuteChanged:sync:Ready:True",
            "CanExecuteChanged:typed:Ready:True",
            "CanExecuteChanged:async:Ready:True",
            "CanExecuteChanged:result:Ready:True",
        ], events);
    }
}
