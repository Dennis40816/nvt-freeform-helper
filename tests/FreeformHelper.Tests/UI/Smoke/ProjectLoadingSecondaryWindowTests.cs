using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class ProjectLoadingSecondaryWindowTests
{
    private static readonly int[] _hiddenIds = [10];
    private static readonly int[] _autoHiddenIds = [11];

    [AvaloniaTheory]
    [InlineData(DxfEditChangeKind.Hidden)]
    [InlineData(DxfEditChangeKind.Duplicate)]
    public async Task LoadPickerAwait_BlocksOpenDxfChangeListWithoutChangingHiddenUndoOrDirtyState(DxfEditChangeKind kind)
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel();
        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(new[] { CreateCad(10), CreateCad(11), CreateCad(12) });
        var hidden = FreeformHelperViewModelPrivateAccess.CadEdit(vm).HiddenCadPadIds;
        hidden.Add(10);
        var hiddenUndo = FreeformHelperViewModelPrivateAccess.CadEdit(vm).HiddenUndoStack;
        hiddenUndo.Push([10]);
        var duplicateIds = FreeformHelperViewModelPrivateAccess.CadEdit(vm).DuplicateCadPadIdToCanonicalId;
        duplicateIds[11] = 10;
        duplicateIds[12] = 10;
        var autoHidden = FreeformHelperViewModelPrivateAccess.CadEdit(vm).AutoHiddenDuplicateCadPadIds;
        autoHidden.Add(11);
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view };
        DxfEditChangeListWindow? window = null;
        Task? load = null;
        try
        {
            owner.Show();
            await (kind == DxfEditChangeKind.Hidden
                ? vm.OpenHiddenDxfEditChangeListCommand
                : vm.OpenDuplicateDxfEditChangeListCommand).ExecuteAsync(null);
            window = Assert.IsType<DxfEditChangeListWindow>(FreeformHelperViewPrivateAccess.DxfEditChangeListWindow(view));
            var changes = Assert.IsType<DxfEditChangeListViewModel>(window.DataContext);
            changes.AreAllVisibleRowsSelected = true;
            var entry = Assert.IsType<DxfEditChangeListEntry>(changes.SelectedEntry);
            IRelayCommand[] commands = [changes.ApplySelectedEntriesCommand, changes.ApplySelectedEntryCommand,
                changes.RestoreSelectedDuplicateEntriesCommand, changes.RehideSelectedDuplicateEntriesCommand];
            var before = commands.Select(command => command.CanExecute(entry)).ToArray();
            Assert.True(before[1]);
            Assert.True(kind == DxfEditChangeKind.Hidden ? before[0] : before[2] && before[3]);
            var notifications = new int[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                var index = i;
                commands[i].CanExecuteChanged += (_, _) => notifications[index]++;
            }

            vm.PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
            load = vm.LoadProjectCommand.ExecuteAsync(null);
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            foreach (var command in commands) Assert.False(command.CanExecute(entry));
            Assert.All(notifications, count => Assert.True(count > 0));
            Assert.False(window.IsEnabled);
            Assert.All(window.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEffectivelyEnabled));
            changes.AreAllVisibleRowsSelected = false;
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Assert.False(changes.AreAllVisibleRowsSelected);
            changes.AreAllVisibleRowsSelected = true;
            foreach (var command in commands) command.Execute(entry);
            Assert.Equal(_hiddenIds, hidden.Order().ToArray());
            Assert.Equal(_autoHiddenIds, autoHidden.Order().ToArray());
            Assert.Equal(_hiddenIds, Assert.Single(hiddenUndo));
            Assert.False(FreeformHelperViewModelPrivateAccess.UndoService(vm).CanUndo);
            Assert.False(vm.HasUnsavedChanges);

            release.SetResult(null);
            await load;
            Assert.True(window.IsEnabled);
            Assert.Equal(before, commands.Select(command => command.CanExecute(entry)).ToArray());
            Assert.All(notifications, count => Assert.True(count >= 2));
            if (kind == DxfEditChangeKind.Hidden) changes.ApplySelectedEntriesCommand.Execute(null);
            else changes.RestoreSelectedDuplicateEntriesCommand.Execute(null);
            Assert.Empty(kind == DxfEditChangeKind.Hidden ? hidden : autoHidden);
            Assert.True(vm.CanUndo);
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            release.TrySetResult(null);
            if (load is not null) await load;
            window?.Close();
            owner.Close();
        }
    }

    private static CadPad CreateCad(int id) => new(id, $"CAD{id}", "L1", new Polygon2(new[]
    {
        new Point2(0, 0), new Point2(10, 0), new Point2(10, 10), new Point2(0, 10)
    }));

    [AvaloniaFact]
    public async Task ShowArtifactDetailCommand_LoadedProject_OpensAndClosesDetailAsync()
    {
        using var session = await LoadedSecondaryWindowSession.CreateAsync();
        var source = Assert.IsType<CoordinatePlannerWorkspaceSession>(
            await session.Helper.CreateCoordinatePlannerWorkspaceSessionAsync());
        var model = new CoordinatePlannerWorkspaceViewModel(new CoordinatePlannerWorkspaceUseCase(), source);
        session.Owner.Content = new CoordinatePlannerWorkspaceView { DataContext = model };
        await HeadlessUiPump.FlushQueueAsync();
        var row = model.ArtifactRows.First();

        var show = model.ShowArtifactDetailCommand.ExecuteAsync(row);
        var dialog = await HeadlessUiPump.WaitForOwnedWindowAsync<CoordinateArtifactDetailWindow>(
            session.Owner, nameof(CoordinateArtifactDetailWindow));
        try
        {
            Assert.Same(row, dialog.DataContext);
            Assert.Same(row, model.SelectedArtifactRow);
            UiEventProbe.Click(dialog, static button => button.Classes.Contains("actionNeutral"));
            await HeadlessUiPump.WaitForConditionAsync(() => show.IsCompleted,
                "Closing the coordinate detail window did not complete its command.");
            await show;
            Assert.False(dialog.IsVisible);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExportDxfLayerImageCommand_LoadedProject_OpensAndCancelsWindowAsync()
    {
        using var session = await LoadedSecondaryWindowSession.CreateAsync();
        var picks = 0;
        session.Helper.PickSaveExportPathAsync = (_, _) =>
        {
            picks++;
            return Task.FromResult<string?>(null);
        };
        var dirty = session.Helper.HasUnsavedChanges;

        var export = session.Helper.ExportDxfLayerImageCommand.ExecuteAsync(null);
        var dialog = await HeadlessUiPump.WaitForOwnedWindowAsync<LayerImageExportWindow>(
            session.Owner, nameof(LayerImageExportWindow));
        try
        {
            var model = Assert.IsType<LayerImageExportViewModel>(dialog.DataContext);
            Assert.Equal("L1", Assert.Single(model.VisibleRows).Name);
            Assert.True(model.VisibleRows[0].IsChecked);
            model.CancelCommand.Execute(null);
            await HeadlessUiPump.WaitForConditionAsync(() => export.IsCompleted,
                "Cancelling the layer image window did not complete the export command.");
            await export;

            Assert.False(dialog.IsVisible);
            Assert.Null(model.CompletedRequest);
            Assert.Equal(0, picks);
            Assert.Equal(dirty, session.Helper.HasUnsavedChanges);
        }
        finally
        {
            dialog.Close();
        }
    }
}

internal sealed class LoadedSecondaryWindowSession : IDisposable
{
    private readonly DirectoryInfo _directory;

    private LoadedSecondaryWindowSession(DirectoryInfo directory, FreeformHelperViewModel helper)
    {
        _directory = directory;
        Helper = helper;
        View = new FreeformHelperView { DataContext = helper };
        Owner = new Window { Content = View, Width = 1200, Height = 800 };
        Owner.Show();
    }

    public FreeformHelperViewModel Helper { get; }
    public FreeformHelperView View { get; }
    public Window Owner { get; }

    public static async Task<LoadedSecondaryWindowSession> CreateAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var directory = TestFiles.CreateTempDirectory();
        LoadedSecondaryWindowSession? session = null;
        try
        {
            var path = Path.Combine(directory.FullName, "project.json");
            await new JsonProjectStore().SaveAsync(path, CreateProject(), TestContext.Current.CancellationToken);
            var helper = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(path),
                ApplyAppVisualPreferencesOnProjectLoad = false,
            };
            await helper.LoadProjectCommand.ExecuteAsync(null);
            await helper.WaitForGridRebuildIdleAsync();
            Assert.NotEmpty(helper.CadPads);
            Assert.NotEmpty(helper.RegularPads);
            session = new LoadedSecondaryWindowSession(directory, helper);
            await HeadlessUiPump.FlushQueueAsync();
            return session;
        }
        catch
        {
            if (session is not null) session.Dispose();
            else directory.Delete(recursive: true);
            throw;
        }
    }

    private static ProjectFile CreateProject() => new()
    {
        EmbeddedDxfName = "entry-coverage.dxf",
        EmbeddedDxf = Encoding.UTF8.GetBytes(SyntheticDxf),
        Settings = new ProjectSettings { Grid = new GridSettings { XChannels = 2, YChannels = 1 } },
        UiSnapshot = new ProjectUiSnapshot
        {
            Grid = new UiGridSnapshot
            {
                CascadeCount = 1, IcX = [2], IcY = [1], TotalX = 2, TotalY = 1,
                ActiveAreaWidthMm = 20, ActiveAreaHeightMm = 10,
            },
        },
    };

    public void Dispose()
    {
        foreach (var child in Owner.OwnedWindows.ToArray()) child.Close();
        Owner.Close();
        _directory.Delete(recursive: true);
    }

    private const string SyntheticDxf = """
        0
        SECTION
        2
        ENTITIES
        0
        LWPOLYLINE
        8
        L1
        70
        1
        90
        4
        10
        0
        20
        0
        10
        20
        20
        0
        10
        20
        20
        10
        10
        0
        20
        10
        0
        ENDSEC
        0
        EOF
        """;
}
