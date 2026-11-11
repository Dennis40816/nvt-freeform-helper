// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class RegularMaskCharacterizationTests
{
    [AvaloniaFact]
    public async Task PickerAfterProjectReplacementOverwritesLoadedMaskAndPublishesOnceAsync()
    {
        using var files = new IoFailureScope();
        var saved = await CreateWorkspaceAsync(files);
        await ImportMaskAsync(saved, WriteMask(files, "saved.csv", "10,0"));
        saved.RegularVisibilityMaskUi.IsEnabled = false;
        var projectPath = await SaveEmbeddedProjectAsync(saved, files.PathFor("replacement.json"));
        var vm = await CreateWorkspaceAsync(files);
        var previousRegularPads = vm.RegularPads;
        var latePath = WriteMask(files, "late.csv", "0,10");
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickOpenRegularVisibilityMaskPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
        var pendingImport = vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        try
        {
            Assert.False(pendingImport.IsCompleted);
            Assert.True((await vm.LoadProjectFromPathAsync(projectPath)).IsLoaded);
            await WaitForGridRebuildAsync(vm);
            Assert.NotSame(previousRegularPads, vm.RegularPads);
            Assert.False(vm.RegularVisibilityMaskUi.IsEnabled);
            Assert.NotNull(vm.RegularVisibilityMaskForTests.EmbeddedMask);
            Assert.Equal(0, Assert.Single(vm.RegularVisibilityMaskForTests.Mask.ActiveRegularPadIds));
            var revision = vm.SimulationWorkspaceSourceRevision;
            var publications = ObservePublications(vm);

            release.SetResult(latePath);
            await pendingImport.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            // KNOWN DEFECT: A picker opened for the previous project replaces the newly loaded project's mask.
            Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
            Assert.Equal(latePath, vm.RegularVisibilityMaskForTests.SourcePath);
            Assert.Null(vm.RegularVisibilityMaskForTests.EmbeddedMask);
            Assert.Null(vm.RegularVisibilityMaskForTests.EmbeddedName);
            Assert.Equal(1, Assert.Single(vm.RegularVisibilityMaskForTests.Mask.ActiveRegularPadIds));
            Assert.Equal(1, ReadDiff(vm));
            Assert.Equal((revision + 1, true, 1), Assert.Single(publications));
            Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
        }
        finally
        {
            release.TrySetResult(null);
            await pendingImport.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task PickerAfterGridReplacementUsesNewDimensionsAndPublishesOnceAsync()
    {
        using var files = new IoFailureScope();
        var vm = await CreateWorkspaceAsync(files);
        var previousRegularPads = vm.RegularPads;
        var path = files.PathFor("three-columns.csv");
        TestFiles.WriteText(path, "Xch:\"3\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,0,10,0\n");
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickOpenRegularVisibilityMaskPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
        var pendingImport = vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        try
        {
            Assert.False(pendingImport.IsCompleted);
            Assert.Single(vm.CascadeIcSettings).XChannels = 3;
            await WaitForGridRebuildAsync(vm);
            await vm.MatchCommand.ExecuteAsync(null);
            Assert.NotSame(previousRegularPads, vm.RegularPads);
            Assert.Equal(3, vm.RegularPads.Count);
            Assert.False(vm.RegularVisibilityMaskUi.IsEnabled);
            var revision = vm.SimulationWorkspaceSourceRevision;
            var publications = ObservePublications(vm);

            release.SetResult(path);
            await pendingImport.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            // KNOWN DEFECT: A picker opened for the old grid is accepted against the replacement grid without an identity check.
            Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
            Assert.Equal(path, vm.RegularVisibilityMaskForTests.SourcePath);
            Assert.True(vm.RegularVisibilityMaskForTests.Mask.HasLoadedMask);
            Assert.Equal(1, Assert.Single(vm.RegularVisibilityMaskForTests.Mask.ActiveRegularPadIds));
            Assert.Equal(2, vm.RegularVisibilityMaskForTests.Mask.InactiveRegularCount);
            Assert.Equal((revision + 1, true, 1), Assert.Single(publications));
            Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
        }
        finally
        {
            release.TrySetResult(null);
            await pendingImport.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task FailedImportKeepsActiveEmbeddedMappingAndProvenanceWithoutPublicationAsync()
    {
        using var files = new IoFailureScope();
        var saver = await CreateWorkspaceAsync(files);
        var acceptedPath = WriteMask(files, "accepted.csv", "0,10");
        await ImportMaskAsync(saver, acceptedPath);
        var projectPath = await SaveEmbeddedProjectAsync(saver, files.PathFor("embedded.json"));
        File.Delete(acceptedPath);
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(projectPath)).IsLoaded);
        await WaitForGridRebuildAsync(vm);
        var accepted = vm.RegularVisibilityMaskForTests.Mask;
        var embedded = vm.RegularVisibilityMaskForTests.EmbeddedMask;
        var embeddedName = vm.RegularVisibilityMaskForTests.EmbeddedName;
        Assert.NotNull(embedded);
        Assert.True(accepted.HasLoadedMask);
        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.Equal(1, ReadDiff(vm));
        var revision = vm.SimulationWorkspaceSourceRevision;
        var publications = ObservePublications(vm);
        var rejectedPath = files.PathFor("empty.csv");
        TestFiles.WriteText(rejectedPath, string.Empty);

        await ImportMaskAsync(vm, rejectedPath);

        Assert.StartsWith("Regular visibility mask: failed to load empty.csv.", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(vm.StatusText, vm.RegularVisibilityMaskUi.Summary);
        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.Same(accepted, vm.RegularVisibilityMaskForTests.Mask);
        Assert.Equal(acceptedPath, vm.RegularVisibilityMaskForTests.SourcePath);
        Assert.Same(embedded, vm.RegularVisibilityMaskForTests.EmbeddedMask);
        Assert.Equal(embeddedName, vm.RegularVisibilityMaskForTests.EmbeddedName);
        Assert.Equal(1, ReadDiff(vm));
        Assert.Empty(publications);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        var resavedPath = await SaveEmbeddedProjectAsync(vm, files.PathFor("resaved.json"));
        var reloaded = files.CreateViewModel();
        Assert.True((await reloaded.LoadProjectFromPathAsync(resavedPath)).IsLoaded);
        await WaitForGridRebuildAsync(reloaded);
        Assert.True(reloaded.RegularVisibilityMaskUi.IsEnabled);
        Assert.Equal(acceptedPath, reloaded.RegularVisibilityMaskForTests.SourcePath);
        Assert.Equal(embeddedName, reloaded.RegularVisibilityMaskForTests.EmbeddedName);
        Assert.Equal(1, Assert.Single(reloaded.RegularVisibilityMaskForTests.Mask.ActiveRegularPadIds));
        Assert.Equal(1, ReadDiff(reloaded));
    }

    [AvaloniaFact]
    public async Task LoadProjectRestoresEmbeddedMaskAfterCadResetAndPublishesOnceAsync()
    {
        using var files = new IoFailureScope();
        var vm = await CreateWorkspaceAsync(files);
        var path = WriteMask(files, "embedded.csv", "0,10");
        await ImportMaskAsync(vm, path);
        var projectPath = await SaveEmbeddedProjectAsync(vm, files.PathFor("embedded.json"));
        File.Delete(path);
        vm.ClearRegularVisibilityMaskCommand.Execute(null);
        await ImportMaskAsync(vm, WriteMask(files, "current.csv", "10,0"));
        Assert.Equal(0, ReadDiff(vm));
        var enabledChanges = new List<(bool Enabled, bool Loaded)>();
        vm.RegularVisibilityMaskUi.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(RegularVisibilityMaskUiState.IsEnabled))
            {
                enabledChanges.Add((vm.RegularVisibilityMaskUi.IsEnabled, vm.RegularVisibilityMaskForTests.Mask.HasLoadedMask));
            }
        };
        var revision = vm.SimulationWorkspaceSourceRevision;
        var publications = ObservePublications(vm);

        Assert.True((await vm.LoadProjectFromPathAsync(projectPath)).IsLoaded);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(new[] { (false, false), (true, false) }, enabledChanges);
        Assert.NotNull(vm.RegularVisibilityMaskForTests.EmbeddedMask);
        Assert.Equal("embedded.csv", vm.RegularVisibilityMaskForTests.EmbeddedName);
        Assert.Equal(path, vm.RegularVisibilityMaskForTests.SourcePath);
        Assert.True(vm.RegularVisibilityMaskForTests.Mask.HasLoadedMask);
        Assert.Equal(1, Assert.Single(vm.RegularVisibilityMaskForTests.Mask.ActiveRegularPadIds));
        Assert.Equal(1, ReadDiff(vm));
        Assert.Equal((revision + 1, true, 1), Assert.Single(publications));
        Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
    }

    [AvaloniaFact]
    public async Task ImportNotifiesEnabledBeforeUpdatingMappingAndPublishesOnceAsync()
    {
        using var files = new IoFailureScope();
        var vm = await CreateWorkspaceAsync(files);
        var path = WriteMask(files, "mask.csv", "0,10");
        var initialSummary = vm.RegularVisibilityMaskUi.Summary;
        var revision = vm.SimulationWorkspaceSourceRevision;
        var notifications = ObserveMaskNotifications(vm);
        var disabledSummary = DisabledSummary("mask.csv");
        var enabledSummary = EnabledSummary("mask.csv");
        Assert.Equal(0, ReadDiff(vm));

        await ImportMaskAsync(vm, path);

        Assert.Equal(new[]
        {
            new MaskNotification("changing:Summary", false, initialSummary, 0, revision),
            new MaskNotification("changed:Summary", false, disabledSummary, 0, revision),
            new MaskNotification("changing:IsEnabled", false, disabledSummary, 0, revision),
            new MaskNotification("changing:Summary", true, disabledSummary, 0, revision),
            new MaskNotification("changed:Summary", true, enabledSummary, 0, revision),
            new MaskNotification("changed:IsEnabled", true, enabledSummary, 0, revision),
            new MaskNotification("source", true, enabledSummary, 1, revision + 1),
        }, notifications);
        Assert.Equal(1, ReadDiff(vm));
        Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
    }

    [AvaloniaTheory]
    [InlineData(false, 1, 0)]
    [InlineData(true, 0, 1)]
    public async Task TogglePublishesUpdatedMappingBeforeEnabledChangedAsync(bool enabled, int previousDiff, int nextDiff)
    {
        using var files = new IoFailureScope();
        var vm = await CreateWorkspaceAsync(files);
        await ImportMaskAsync(vm, WriteMask(files, "mask.csv", "0,10"));
        vm.RegularVisibilityMaskUi.IsEnabled = !enabled;
        var previousSummary = vm.RegularVisibilityMaskUi.Summary;
        var nextSummary = enabled ? EnabledSummary("mask.csv") : DisabledSummary("mask.csv");
        var revision = vm.SimulationWorkspaceSourceRevision;
        var notifications = ObserveMaskNotifications(vm);
        vm.NotchExportSummary = "Previous export completed.";
        Assert.Equal(previousDiff, ReadDiff(vm));

        vm.RegularVisibilityMaskUi.IsEnabled = enabled;

        Assert.Equal(new[]
        {
            new MaskNotification("changing:IsEnabled", !enabled, previousSummary, previousDiff, revision),
            new MaskNotification("changing:Summary", enabled, previousSummary, previousDiff, revision),
            new MaskNotification("changed:Summary", enabled, nextSummary, previousDiff, revision),
            new MaskNotification("source", enabled, nextSummary, nextDiff, revision + 1),
            new MaskNotification("changed:IsEnabled", enabled, nextSummary, nextDiff, revision + 1),
        }, notifications);
        Assert.Equal(nextDiff, ReadDiff(vm));
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
    }

    [AvaloniaFact]
    public async Task ClearNotifiesDisabledBeforeGeometryRestorationAndPublishesOnceAsync()
    {
        using var files = new IoFailureScope();
        var vm = await CreateWorkspaceAsync(files);
        var clearedSummary = vm.RegularVisibilityMaskUi.Summary;
        await ImportMaskAsync(vm, WriteMask(files, "mask.csv", "0,10"));
        var enabledSummary = vm.RegularVisibilityMaskUi.Summary;
        var revision = vm.SimulationWorkspaceSourceRevision;
        var notifications = ObserveMaskNotifications(vm);
        Assert.Equal(1, ReadDiff(vm));

        vm.ClearRegularVisibilityMaskCommand.Execute(null);

        Assert.Equal(new[]
        {
            new MaskNotification("changing:Summary", true, enabledSummary, 1, revision),
            new MaskNotification("changed:Summary", true, clearedSummary, 1, revision),
            new MaskNotification("changing:IsEnabled", true, clearedSummary, 1, revision),
            new MaskNotification("changed:IsEnabled", false, clearedSummary, 1, revision),
            new MaskNotification("source", false, clearedSummary, 0, revision + 1),
        }, notifications);
        Assert.Equal(0, ReadDiff(vm));
        Assert.False(vm.RegularVisibilityMaskForTests.Mask.HasLoadedMask);
        Assert.Null(vm.RegularVisibilityMaskForTests.SourcePath);
        Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
    }

    [AvaloniaFact]
    public async Task ImportWhileEnabledAcceptsMaskWithoutUpdatingMappingOrPublishingAsync()
    {
        using var files = new IoFailureScope();
        var vm = await CreateWorkspaceAsync(files);
        await ImportMaskAsync(vm, WriteMask(files, "previous.csv", "0,10"));
        Assert.Equal(1, ReadDiff(vm));
        var previousMask = vm.RegularVisibilityMaskForTests.Mask;
        var previousSummary = vm.RegularVisibilityMaskUi.Summary;
        var revision = vm.SimulationWorkspaceSourceRevision;
        var notifications = ObserveMaskNotifications(vm);
        vm.NotchExportSummary = "Previous export completed.";
        var path = WriteMask(files, "replacement.csv", "10,0");

        await ImportMaskAsync(vm, path);

        // KNOWN DEFECT: Replacing an enabled mask accepts new data but leaves the old mapping, export and simulation revision.
        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.NotSame(previousMask, vm.RegularVisibilityMaskForTests.Mask);
        Assert.Equal(path, vm.RegularVisibilityMaskForTests.SourcePath);
        Assert.Equal(0, Assert.Single(vm.RegularVisibilityMaskForTests.Mask.ActiveRegularPadIds));
        Assert.Equal(1, ReadDiff(vm));
        Assert.Equal("Previous export completed.", vm.NotchExportSummary);
        Assert.Equal(new[]
        {
            new MaskNotification("changing:Summary", true, previousSummary, 1, revision),
            new MaskNotification("changed:Summary", true, EnabledSummary("replacement.csv"), 1, revision),
        }, notifications);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        Assert.StartsWith("Regular visibility mask imported: replacement.csv.", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameEnabledValueLeavesMappingStatusAndNotificationsUnchangedAsync(bool enabled)
    {
        using var files = new IoFailureScope();
        var vm = await CreateWorkspaceAsync(files);
        await ImportMaskAsync(vm, WriteMask(files, "mask.csv", "0,10"));
        vm.RegularVisibilityMaskUi.IsEnabled = enabled;
        var status = vm.StatusText;
        var summary = vm.RegularVisibilityMaskUi.Summary;
        var diff = ReadDiff(vm);
        var revision = vm.SimulationWorkspaceSourceRevision;
        var notifications = ObserveMaskNotifications(vm);

        vm.RegularVisibilityMaskUi.IsEnabled = enabled;

        Assert.Empty(notifications);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(summary, vm.RegularVisibilityMaskUi.Summary);
        Assert.Equal(diff, ReadDiff(vm));
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
    }

    private static async Task<FreeformHelperViewModel> CreateWorkspaceAsync(IoFailureScope files)
    {
        var vm = files.CreateViewModel();
        vm.ActiveAreaWidth = 20;
        vm.ActiveAreaHeight = 10;
        vm.PanelBiasX = 0;
        vm.PanelBiasY = 0;
        vm.GridPaddingPercent = 0;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
        vm.AutoReplayStep2AfterProjectLoad = false;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        await WaitForGridRebuildAsync(vm);
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 15, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.Equal(0, ReadDiff(vm));
        return vm;
    }

    private static string WriteMask(IoFailureScope files, string name, string values)
    {
        var path = files.PathFor(name);
        TestFiles.WriteText(path, $"Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,{values}\n");
        return path;
    }

    private static Task ImportMaskAsync(FreeformHelperViewModel vm, string path)
    {
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(path);
        return vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
    }

    private static async Task<string> SaveEmbeddedProjectAsync(FreeformHelperViewModel vm, string path)
    {
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync());
        var project = JsonProjectStore.Load(path);
        Assert.NotNull(project.EmbeddedDxf);
        Assert.NotNull(project.EmbeddedRegularVisibilityMask);
        project.LastDxfPath = null;
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        return path;
    }

    private static int ReadDiff(FreeformHelperViewModel vm)
    {
        Assert.True(vm.TryGetCadOutputFwDiffIndex(Assert.Single(vm.CadPads).Id, out var diff));
        return diff;
    }

    private static List<(int Revision, bool Enabled, int Diff)> ObservePublications(FreeformHelperViewModel vm)
    {
        var publications = new List<(int Revision, bool Enabled, int Diff)>();
        vm.SimulationWorkspaceSourceChanged += (_, _) => publications.Add((
            vm.SimulationWorkspaceSourceRevision, vm.RegularVisibilityMaskUi.IsEnabled, ReadDiff(vm)));
        return publications;
    }

    private static List<MaskNotification> ObserveMaskNotifications(FreeformHelperViewModel vm)
    {
        var notifications = new List<MaskNotification>();
        void Record(string name) => notifications.Add(new MaskNotification(
            name, vm.RegularVisibilityMaskUi.IsEnabled, vm.RegularVisibilityMaskUi.Summary,
            ReadDiff(vm), vm.SimulationWorkspaceSourceRevision));
        vm.RegularVisibilityMaskUi.PropertyChanging += (_, change) => Record($"changing:{change.PropertyName}");
        vm.RegularVisibilityMaskUi.PropertyChanged += (_, change) => Record($"changed:{change.PropertyName}");
        vm.SimulationWorkspaceSourceChanged += (_, _) => Record("source");
        return notifications;
    }

    private static string DisabledSummary(string name) =>
        $"Regular visibility mask loaded but disabled: {name}. Step 4 uses pure geometry seed.";

    private static string EnabledSummary(string name) =>
        $"Regular visibility mask enabled: {name} loaded, active=1, inactive=1, frames=1. " +
        "Step 4 geometry seed only uses active regulars.";

    private sealed record MaskNotification(string Event, bool Enabled, string Summary, int Diff, int Revision);
}
