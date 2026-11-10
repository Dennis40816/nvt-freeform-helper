// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowBindingSmokeTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public async Task LazyWorkspacePanelsAndPopupsReportNoBindingErrorsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var capture = new AvaloniaBindingLogCapture();
        try
        {
            await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
            var activation = new MainWindowBindingActivation(fixture.Window, capture);
            try
            {
                await activation.RunAsync();
            }
            finally
            {
                output.WriteLine("Supplemental synthetic project; this is not the real-input coverage phase.");
                output.WriteLine(activation.Format());
            }
        }
        finally
        {
            await HeadlessUiPump.FlushQueueAsync();
            output.WriteLine(capture.Format(BindingDocuments.ReadRepository()));
        }
        Assert.Empty(capture.Errors);
    }

    [AvaloniaFact]
    public async Task RealProjectLoadKeepsOwningChildrenInitializedAndActivatesBindingsAsync()
    {
        Assert.SkipWhen(!ExampleData.IsAvailable && !ExampleData.IsRequired,
            "Uncovered real-input activation: " + ExampleData.MissingMessage);
        HeadlessAppBootstrap.EnsureInitialized();
        using var capture = new AvaloniaBindingLogCapture();
        try
        {
            await ActivateRealProjectAsync(capture);
        }
        finally
        {
            await HeadlessUiPump.FlushQueueAsync();
            output.WriteLine(capture.Format(BindingDocuments.ReadRepository()));
        }
        Assert.Empty(capture.Errors);
    }

    private async Task ActivateRealProjectAsync(AvaloniaBindingLogCapture capture)
    {
        using var project = ExampleProjectFixture.Create(TestPaths.FromRepo("example", "BOE36.35", "project_3635.json"));
        var probe = new UiEventProbe();
        var helper = new FreeformHelperViewModel(null, probe.Runner) { NotchPreviewAutoPlayEnabled = false };
        using var shell = new ShellViewModel(helper) { IsDevPageAvailable = true };
        shell.Simulation.RequestPrewarmWorkspaceAsync = null;
        shell.CoordinatePlanner.RequestPrewarmWorkspaceAsync = null;
        var window = new MainWindow { Width = 1280, Height = 900, WindowState = WindowState.Normal };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        var activation = new MainWindowBindingActivation(window, capture);
        try
        {
            window.SetShellViewModel(shell);
            MainWindowBindingCatalog.RequireOwningChildren(shell, helper.CreateSettingsWindowViewModel());
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            capture.Phase = "real project load";
            helper.PickLoadProjectPathAsync = () => Task.FromResult<string?>(project.ProjectPath);
            await helper.LoadProjectCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            await helper.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.NotEmpty(helper.CadPads);
            Assert.NotEmpty(helper.RegularPads);
            MainWindowBindingCatalog.RequireOwningChildren(shell, helper.CreateSettingsWindowViewModel());
            await activation.RunAsync();
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            MainWindowBindingActivation.CloseOwnedWindows(window);
            helper.HasUnsavedChanges = false;
            window.Close();
            await helper.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();
            output.WriteLine(activation.Format());
        }
    }
}
