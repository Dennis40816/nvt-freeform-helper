// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using Vm = FreeformHelper.UI.ViewModels.FreeformHelperViewModel;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CadImportLayerQualityCharacterizationTests
{
    private static readonly string[] _aggregateNames =
    [
        nameof(Vm.AreAllCadLayersSelected), nameof(Vm.AreNoCadLayersSelected), nameof(Vm.AreSomeCadLayersSelected),
        nameof(Vm.IsCadLayerBulkToggleChecked), nameof(Vm.CadLayerBulkToggleText), nameof(Vm.CadLayerBulkToggleToolTip),
    ];

    [AvaloniaFact]
    public async Task SuccessiveDxfLoadsReplaceSubscriptionsBeforeCollectionPublicationAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var oldToggles = vm.LayerToggles.ToArray();
        using (FreeformHelperViewModelPrivateAccess.UndoSuppression(vm).Enter())
        {
            oldToggles[1].IsSelected = false;
        }
        vm.HasUnsavedChanges = false;
        Assert.False(vm.CanUndo);
        using var probe = new SubscriptionProbe(vm, oldToggles);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.True(probe.SawReplacement);
        Assert.Empty(probe.OldEffects);
        Assert.Equal<IEnumerable<string?>>([nameof(Vm.CadPads), nameof(Vm.CanUndo), .. _aggregateNames], probe.NewEffects);
        Assert.NotSame(oldToggles[0], vm.LayerToggles[0]);
        Assert.NotSame(oldToggles[1], vm.LayerToggles[1]);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        // A retained old PropertyChanging handler would have captured beta=false before publication.
        Assert.All(vm.LayerToggles, static toggle => Assert.True(toggle.IsSelected));
        Assert.False(vm.CanUndo);
        Assert.Equal(2, vm.CadPads.Count);
        AssertDetachedToggles(vm, oldToggles);
        Assert.All(vm.LayerToggles, toggle => AssertSingleToggleCycle(vm, toggle));
    }

    [AvaloniaFact]
    public async Task DirectLayerAssignmentKeepsOldHandlersAndLeavesNewTogglesUnwiredAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var oldToggle = vm.LayerToggles[1];
        var originalPads = vm.CadPads;
        var assigned = new ObservableCollection<LayerToggle>([new("alpha"), new("beta")]);
        var changes = RecordNotifications(vm, [nameof(Vm.LayerToggles), nameof(Vm.CadPads), nameof(Vm.CanUndo), .. _aggregateNames]);
        vm.HasUnsavedChanges = false;

        vm.LayerToggles = assigned;
        vm.LayerToggles = assigned;
        assigned[0].IsSelected = false;

        // KNOWN DEFECT: Direct collection assignment leaves old toggles subscribed and new toggles unwired.
        Assert.Equal([nameof(Vm.LayerToggles)], changes);
        Assert.Same(assigned, vm.LayerToggles);
        Assert.Same(originalPads, vm.CadPads);
        Assert.True(vm.AreSomeCadLayersSelected);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        changes.Clear();
        oldToggle.IsSelected = false;
        Assert.Equal([nameof(Vm.CadPads), nameof(Vm.CanUndo), .. _aggregateNames], changes);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.False(assigned[0].IsSelected);
        Assert.True(assigned[1].IsSelected);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void DirectLayerAssignmentPublishesOnlyTheCollectionAndKeepsDependentGetters()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var before = PublicationState.Read(vm);
        var collectionValues = new List<ObservableCollection<LayerToggle>>();
        vm.PropertyChanging += (_, args) => RecordCollection(vm, args.PropertyName, collectionValues);
        vm.PropertyChanged += (_, args) => RecordCollection(vm, args.PropertyName, collectionValues);
        var changes = RecordNotifications(vm, [nameof(Vm.HasCadLayers), nameof(Vm.BoundLayerOptions),
            nameof(Vm.RegularSourceLayerOptions), nameof(Vm.DxfEditLayerOptions), .. _aggregateNames]);
        var original = vm.LayerToggles;
        var assigned = new ObservableCollection<LayerToggle>([new("assigned")]);

        vm.LayerToggles = assigned;

        // KNOWN DEFECT: Direct collection assignment also leaves HasCadLayers and the option lists stale.
        Assert.Equal([original, assigned], collectionValues);
        Assert.Empty(changes);
        Assert.Equal(before with { Toggles = "assigned:on", All = true, BulkText = "All on" }, PublicationState.Read(vm));
        Assert.False(vm.HasCadLayers);
    }

    [AvaloniaFact]
    public async Task LayerPublicationExposesOrderedIntermediateValuesAndSuppressedSelectionsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = await CreatePendingLayerWorkspaceAsync(files);
        vm.BoundLayerOptionSummary = "Before import.";
        vm.DxfRegularSourceHint = "Before import.";
        var initial = PublicationState.Read(vm);
        Assert.Empty(vm.LayerToggles);
        Assert.False(vm.HasCadLayers);
        Assert.Null(vm.SelectedBoundLayerOption.Name);
        Assert.Null(vm.SelectedRegularSourceLayerOption.Name);
        Assert.True(initial.GridIdle);
        using var probe = new PublicationProbe(vm);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(ExpectedPublication(initial), probe.Frames);
        Assert.False(vm.CanUndo);
        Assert.Equal("beta", Assert.Single(vm.CadPads).Layer);
        Assert.Single(vm.RegularPads);
        Assert.Equal("alpha", vm.SelectedRegularSourceLayerOption.Name);
    }

    [AvaloniaFact]
    public async Task LoadedLayerNamesAreConsumedBeforeTheNextExplicitLoadAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await CreatePendingLayerWorkspaceAsync(files);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal("beta", vm.SelectedBoundLayerOption.Name);
        Assert.Equal("alpha", vm.SelectedRegularSourceLayerOption.Name);
        vm.SelectedBoundLayerOption = vm.BoundLayerOptions.Single(static option => option.Name == "alpha");
        Assert.False(vm.WaitForGridRebuildIdleAsync().IsCompleted);
        await WaitForGridRebuildAsync(vm);
        vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(static option => option.Name == "beta");
        Assert.False(vm.LayerToggles.Single(static toggle => toggle.Name == "beta").IsSelected);
        await WaitForGridRebuildAsync(vm);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        // Consumed project names must not override the user's later selections on the next import.
        Assert.Equal("alpha", vm.SelectedBoundLayerOption.Name);
        Assert.Equal("beta", vm.SelectedRegularSourceLayerOption.Name);
        Assert.Equal("alpha", Assert.Single(vm.CadPads).Layer);
        Assert.False(vm.LayerToggles.Single(static toggle => toggle.Name == "beta").IsSelected);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedImportWritesIntoTheReplacementWorkspaceAsync(bool replaceProject)
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var latePath = WriteDxf(files, "late.dxf", ("late", 50, 0, 60, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(latePath);
        // The chosen path and options are captured before the overlay yields. Keep real import/publication pending.
        using var gate = new ContinuationGate(() => vm.IsCadLoadCanvasOverlayVisible);
        var pending = gate.StartAsync(() => vm.OpenDxfCommand.ExecuteAsync(null));
        ProjectFile? replacement = null;
        try
        {
            await gate.WaitUntilHeldAsync(pending);
            Assert.Equal(["alpha", "beta"], vm.CadPads.Select(static pad => pad.Layer).Order());
            await ReplaceWorkspaceAsync(vm, files, replaceProject);
            replacement = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
            Assert.Equal("replacement", Assert.Single(vm.CadPads).Layer);
            Assert.NotEqual(latePath, replacement.LastDxfPath);
        }
        finally
        {
            gate.Release();
            await pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await WaitForGridRebuildAsync(vm);
        }

        // KNOWN DEFECT: An older import overwrites the replacement CAD and writes its path into the current project.
        Assert.Same(replacement, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.Equal(latePath, FreeformHelperViewModelPrivateAccess.ProjectFile(vm).LastDxfPath);
        Assert.Equal("late", Assert.Single(vm.CadPads).Layer);
        Assert.Equal("late", Assert.Single(vm.LayerToggles).Name);
        Assert.True(vm.HasUnsavedChanges);
        Assert.False(vm.IsCadLoadCanvasOverlayVisible);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedOverlapCheckPublishesOldPadsAfterWorkspaceReplacementAsync(bool replaceProject)
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("old-signal", 0, 0, 10, 20), ("old-ground", 5, 5, 15, 25));
        var oldIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        // Check captures visible pads before awaiting the modal frame. No analyzer-speed assumption is needed.
        using var gate = new ContinuationGate(() => vm.IsModalLoadingSpinnerVisible);
        var pending = gate.StartAsync(() => vm.CheckDxfQualityCommand.ExecuteAsync(null));
        try
        {
            await gate.WaitUntilHeldAsync(pending);
            await ReplaceWorkspaceAsync(vm, files, replaceProject);
            await vm.CheckDxfQualityCommand.ExecuteAsync(null);
            Assert.Equal("DXF check: no overlaps detected.", vm.DxfOverlapSummary);
            Assert.Empty(vm.DxfOverlapHighlightedCadIds);
        }
        finally
        {
            gate.Release();
            await pending.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }

        // KNOWN DEFECT: An older overlap check replaces the newer source's clean result with stale issues and IDs.
        Assert.Equal("replacement", Assert.Single(vm.CadPads).Layer);
        Assert.StartsWith("DXF warnings:", vm.DxfOverlapSummary, StringComparison.Ordinal);
        Assert.Equal(vm.DxfOverlapSummary, vm.StatusText);
        Assert.NotEmpty(vm.DxfOverlapIssues);
        Assert.Equal(oldIds, vm.DxfOverlapHighlightedCadIds);
        Assert.Contains(vm.DxfOverlapHighlightedCadIds, id => vm.CadPads.All(pad => pad.Id != id));
        Assert.False(vm.IsCheckingDxfOverlap);
        Assert.Equal(1.0, vm.DxfOverlapProgress);
    }

    [AvaloniaFact]
    public async Task ExampleProjectReloadDetachesRetainedLayerTogglesAsync()
    {
        Assert.SkipWhen(!ExampleData.IsAvailable && !ExampleData.IsRequired, ExampleData.MissingMessage);
        using var example = ExampleProjectFixture.Create(Path.Combine(ExampleData.Root, "BOE36.35", "project_3635.json"));
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel(example.ProjectPath);
        Assert.True((await vm.LoadProjectFromPathAsync(example.ProjectPath)).IsLoaded);
        await WaitForGridRebuildAsync(vm);
        var oldToggles = vm.LayerToggles.ToArray();
        var visible = vm.CadPads.Select(static pad => (pad.Id, pad.Layer, pad.Bounds)).ToArray();
        Assert.NotEmpty(oldToggles);
        Assert.NotEmpty(visible);

        Assert.True((await vm.LoadProjectFromPathAsync(example.ProjectPath)).IsLoaded);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(visible, vm.CadPads.Select(static pad => (pad.Id, pad.Layer, pad.Bounds)));
        Assert.All(oldToggles, old => Assert.DoesNotContain(old, vm.LayerToggles));
        AssertDetachedToggles(vm, oldToggles);
    }

    private static void RecordCollection(FreeformHelperViewModel vm, string? name, List<ObservableCollection<LayerToggle>> values)
    {
        if (name == nameof(Vm.LayerToggles)) values.Add(vm.LayerToggles);
    }

    private static void AssertDetachedToggles(FreeformHelperViewModel vm, IEnumerable<LayerToggle> toggles)
    {
        var pads = vm.CadPads;
        var canUndo = vm.CanUndo;
        vm.HasUnsavedChanges = false;
        var changes = RecordNotifications(vm, [nameof(Vm.CadPads), nameof(Vm.CanUndo), .. _aggregateNames]);
        foreach (var toggle in toggles) toggle.IsSelected = !toggle.IsSelected;
        Assert.Empty(changes);
        Assert.Same(pads, vm.CadPads);
        Assert.Equal(canUndo, vm.CanUndo);
        Assert.False(vm.HasUnsavedChanges);
    }

    private static void AssertSingleToggleCycle(FreeformHelperViewModel vm, LayerToggle toggle)
    {
        var changes = RecordNotifications(vm, [nameof(Vm.CadPads), nameof(Vm.CanUndo), .. _aggregateNames]);
        Assert.True(toggle.IsSelected);
        toggle.IsSelected = false;
        Assert.Equal([nameof(Vm.CadPads), nameof(Vm.CanUndo), .. _aggregateNames], changes);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Layer == toggle.Name);
        vm.Undo();
        Assert.True(toggle.IsSelected);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.False(vm.CanUndo);
    }

    private static async Task<FreeformHelperViewModel> CreatePendingLayerWorkspaceAsync(LoadingModeTestScope files)
    {
        var project = new ProjectFile();
        project.Settings.Grid.BoundLayerName = "beta";
        project.Settings.Grid.RegularSourceLayerName = "alpha";
        project.Settings.Grid.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        project.Settings.Grid.AlignmentMode = GridAlignmentMode.FromCadBounds;
        project.Settings.Grid.XChannels = 1;
        project.Settings.Grid.YChannels = 1;
        project.Settings.Grid.RecalcBoundsOnLayerFilter = false;
        project.Settings.View.AutoReplayStep2AfterProjectLoad = false;
        await new JsonProjectStore().SaveAsync(files.PathFor("project.json"), project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        await WaitForGridRebuildAsync(vm);
        var path = WriteDxf(files, "layers.dxf", ("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        return vm;
    }

    private static async Task ReplaceWorkspaceAsync(FreeformHelperViewModel vm, LoadingModeTestScope files, bool replaceProject)
    {
        var previousProject = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        var path = WriteDxf(files, "replacement.dxf", ("replacement", 100, 0, 110, 10));
        if (replaceProject)
        {
            var project = new ProjectFile { LastDxfPath = path };
            project.Settings.View.AutoReplayStep2AfterProjectLoad = false;
            await new JsonProjectStore().SaveAsync(files.PathFor("replacement.json"), project, TestContext.Current.CancellationToken);
            Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("replacement.json"))).IsLoaded);
        }
        else
        {
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
        }
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(replaceProject, !ReferenceEquals(previousProject, FreeformHelperViewModelPrivateAccess.ProjectFile(vm)));
    }

    private static string WriteDxf(LoadingModeTestScope files, string name,
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var generated = CreateTempDxfFile(pads);
        try
        {
            var path = files.PathFor(name);
            TestFiles.CopyOverwriting(generated, path);
            return path;
        }
        finally
        {
            File.Delete(generated);
        }
    }

    private static List<PublicationFrame> ExpectedPublication(PublicationState initial)
    {
        var frames = new List<PublicationFrame>();
        var state = initial;
        Change(nameof(Vm.LayerToggles), state with { Toggles = "alpha:on,beta:on", All = true, BulkText = "All on" });
        Change(nameof(Vm.HasCadLayers), state with { HasLayers = true });
        Change(nameof(Vm.BoundLayerOptions), state with { Bounds = "<none>,alpha,beta" });
        Change(nameof(Vm.SelectedBoundLayerOption), state with { Bound = "beta" });
        Change(nameof(Vm.BoundLayerOptionSummary), state with { BoundSummary = "Hidden bounds layer is still valid." });
        Change(nameof(Vm.RegularSourceLayerOptions), state with { Sources = "<none>,alpha,beta" });
        Change(nameof(Vm.SelectedRegularSourceLayerOption), state with { Source = "alpha" });
        Change(nameof(Vm.DxfEditLayerOptions), state with { EditChoices = "alpha,beta" });
        Change(nameof(Vm.DxfEditTargetLayerName), state with { Target = "alpha" }, editCallback: true);
        Change(nameof(Vm.DxfEditRotationLayerName), state with { Rotation = "alpha" }, editCallback: true);
        frames.Add(new("changed", nameof(Vm.CanMoveSelectedCadPadsToLayer), state));
        Change("alpha.IsSelected", state with { Toggles = "alpha:off,beta:on", All = false, Some = true, BulkText = "Partly on" });
        // Auto-hide dirties the project after the suppressed toggle event, before the hint and aggregates.
        state = state with { Dirty = true };
        Change(nameof(Vm.DxfRegularSourceHint), state with { Hint = "Regular layer 'alpha': pads=1, matches X*Y=1*1." });
        frames.AddRange(_aggregateNames.Select(name => new PublicationFrame("changed", name, state)));
        Change(nameof(Vm.CadPads), state with { VisibleLayers = "beta" });
        return frames;

        void Change(string name, PublicationState next, bool editCallback = false)
        {
            frames.Add(new("changing", name, state));
            state = next;
            if (editCallback) frames.Add(new("changed", nameof(Vm.CanMoveSelectedCadPadsToLayer), state));
            frames.Add(new("changed", name, state));
        }
    }

    private sealed record PublicationFrame(string Phase, string Name, PublicationState State);

    private sealed record PublicationState(
        string Toggles, bool HasLayers, string Bounds, string? Bound, string BoundSummary,
        string Sources, string? Source, string EditChoices, string Target, string Rotation,
        string Hint, bool All, bool None, bool Some, string BulkText, string VisibleLayers,
        int RegularCount, bool CanUndo, bool Dirty, bool GridIdle, bool CanMove)
    {
        public static PublicationState Read(FreeformHelperViewModel vm) => new(
            string.Join(',', vm.LayerToggles.Select(static toggle => $"{toggle.Name}:{(toggle.IsSelected ? "on" : "off")}")),
            vm.HasCadLayers, string.Join(',', vm.BoundLayerOptions.Select(static option => option.Name ?? "<none>")),
            vm.SelectedBoundLayerOption.Name, vm.BoundLayerOptionSummary,
            string.Join(',', vm.RegularSourceLayerOptions.Select(static option => option.Name ?? "<none>")),
            vm.SelectedRegularSourceLayerOption.Name, string.Join(',', vm.DxfEditLayerOptions),
            vm.DxfEditTargetLayerName, vm.DxfEditRotationLayerName, vm.DxfRegularSourceHint,
            vm.AreAllCadLayersSelected, vm.AreNoCadLayersSelected, vm.AreSomeCadLayersSelected, vm.CadLayerBulkToggleText,
            string.Join(',', vm.CadPads.Select(static pad => pad.Layer).Order()), vm.RegularPads.Count,
            vm.CanUndo, vm.HasUnsavedChanges, vm.WaitForGridRebuildIdleAsync().IsCompletedSuccessfully,
            vm.CanMoveSelectedCadPadsToLayer);
    }

    private sealed class PublicationProbe : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly List<LayerToggle> _toggles = [];
        // All recording state is owned by the headless UI thread.
        private bool _recording;
        public List<PublicationFrame> Frames { get; } = [];

        public PublicationProbe(FreeformHelperViewModel vm)
        {
            _vm = vm;
            vm.PropertyChanging += OnChanging;
            vm.PropertyChanged += OnChanged;
        }

        private void OnChanging(object? sender, PropertyChangingEventArgs args)
        {
            if (args.PropertyName == nameof(Vm.LayerToggles)) _recording = true;
            Record("changing", args.PropertyName);
        }

        private void OnChanged(object? sender, PropertyChangedEventArgs args)
        {
            Record("changed", args.PropertyName);
            if (args.PropertyName == nameof(Vm.LayerToggles))
            {
                foreach (var toggle in _vm.LayerToggles)
                {
                    _toggles.Add(toggle);
                    toggle.PropertyChanging += OnToggleChanging;
                    toggle.PropertyChanged += OnToggleChanged;
                }
            }
            if (args.PropertyName == nameof(Vm.CadPads)) _recording = false;
        }

        private void OnToggleChanging(object? sender, PropertyChangingEventArgs args) => RecordToggle("changing", sender, args.PropertyName);
        private void OnToggleChanged(object? sender, PropertyChangedEventArgs args) => RecordToggle("changed", sender, args.PropertyName);

        private void RecordToggle(string phase, object? sender, string? name)
        {
            if (_recording && sender is LayerToggle toggle && name == nameof(LayerToggle.IsSelected))
                Frames.Add(new(phase, $"{toggle.Name}.{name}", PublicationState.Read(_vm)));
        }

        private void Record(string phase, string? name)
        {
            if (!_recording || name is null) return;
            if (name is nameof(Vm.LayerToggles) or nameof(Vm.HasCadLayers) or nameof(Vm.BoundLayerOptions)
                or nameof(Vm.SelectedBoundLayerOption) or nameof(Vm.BoundLayerOptionSummary) or nameof(Vm.RegularSourceLayerOptions)
                or nameof(Vm.SelectedRegularSourceLayerOption) or nameof(Vm.DxfEditLayerOptions) or nameof(Vm.DxfEditTargetLayerName)
                or nameof(Vm.DxfEditRotationLayerName) or nameof(Vm.CanMoveSelectedCadPadsToLayer) or nameof(Vm.DxfRegularSourceHint)
                or nameof(Vm.CadPads) or nameof(Vm.RegularPads) || _aggregateNames.Contains(name, StringComparer.Ordinal))
                Frames.Add(new(phase, name, PublicationState.Read(_vm)));
        }

        public void Dispose()
        {
            _vm.PropertyChanging -= OnChanging;
            _vm.PropertyChanged -= OnChanged;
            foreach (var toggle in _toggles)
            {
                toggle.PropertyChanging -= OnToggleChanging;
                toggle.PropertyChanged -= OnToggleChanged;
            }
        }
    }

    private sealed class SubscriptionProbe : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly IReadOnlyList<LayerToggle> _oldToggles;
        private readonly List<string?> _effects = [];
        // Mutable observations are owned by the headless UI thread.
        public string?[] OldEffects { get; private set; } = [];
        public string?[] NewEffects { get; private set; } = [];
        public bool SawReplacement { get; private set; }

        public SubscriptionProbe(FreeformHelperViewModel vm, IReadOnlyList<LayerToggle> oldToggles)
        {
            _vm = vm;
            _oldToggles = oldToggles;
            vm.PropertyChanging += OnChanging;
            vm.PropertyChanged += OnChanged;
        }

        private void OnChanging(object? sender, PropertyChangingEventArgs args)
        {
            if (args.PropertyName != nameof(Vm.LayerToggles)) return;
            var start = _effects.Count;
            foreach (var toggle in _oldToggles) toggle.IsSelected = !toggle.IsSelected;
            OldEffects = _effects.Skip(start).ToArray();
        }

        private void OnChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(Vm.CadPads) or nameof(Vm.CanUndo) || _aggregateNames.Contains(args.PropertyName))
                _effects.Add(args.PropertyName);
            if (args.PropertyName != nameof(Vm.LayerToggles)) return;
            SawReplacement = true;
            var start = _effects.Count;
            _vm.LayerToggles[0].IsSelected = false;
            NewEffects = _effects.Skip(start).ToArray();
        }

        public void Dispose()
        {
            _vm.PropertyChanging -= OnChanging;
            _vm.PropertyChanged -= OnChanged;
        }
    }

    // Only the selected command captures this context. Replacement commands use the normal headless context.
    private sealed class ContinuationGate(Func<bool> shouldHold) : SynchronizationContext, IDisposable
    {
        private readonly Dispatcher _dispatcher = Assert.IsAssignableFrom<Dispatcher>(Dispatcher.FromThread(Thread.CurrentThread));
        private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _pending = new();
        // Post marshals to the headless UI thread before accessing this queue or the release state.
        private bool _released;

        public Task StartAsync(Func<Task> action)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try { return action(); }
            finally { SetSynchronizationContext(previous); }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            _dispatcher.Post(() =>
            {
                if (!_released && shouldHold())
                {
                    _pending.Enqueue((d, state));
                    _held.TrySetResult();
                }
                else Run(d, state);
            });
        }

        public async Task WaitUntilHeldAsync(Task command)
        {
            await Task.WhenAny(_held.Task, command).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            if (command.IsCompleted) Assert.Null(command.Exception);
            Assert.True(_held.Task.IsCompletedSuccessfully, "The command finished without reaching the continuation gate.");
            Assert.False(command.IsCompleted);
        }

        private void Run(SendOrPostCallback callback, object? state)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try { callback(state); }
            finally { SetSynchronizationContext(previous); }
        }

        public void Release()
        {
            _released = true;
            while (_pending.TryDequeue(out var item)) Run(item.Callback, item.State);
        }

        public void Dispose() => Release();
    }
}
