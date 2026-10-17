// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SettingsSynchronizationBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SuppressedWorkspaceBehaviorTests
{
    [Fact]
    public async Task DeselectAllLayers_BatchChange_SuppressesIntermediateFilteringAndRecordsOneUndo()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var padCounts = new List<int>();
        vm.LayerToggles[0].PropertyChanged += (_, _) => padCounts.Add(vm.CadPads.Count);
        vm.LayerToggles[1].PropertyChanged += (_, _) => padCounts.Add(vm.CadPads.Count);
        var changes = Observe(vm, nameof(vm.CanUndo), nameof(vm.AreAllCadLayersSelected));

        vm.DeselectAllLayers();

        Assert.Equal([2, 2], padCounts);
        Assert.Empty(vm.CadPads);
        Assert.Equal<string?>([nameof(vm.CanUndo), nameof(vm.AreAllCadLayersSelected)], changes);
        vm.Undo();
        Assert.Equal(2, vm.CadPads.Count);
        Assert.False(vm.CanUndo);
        vm.LayerToggles[0].IsSelected = false;
        Assert.Single(vm.CadPads);
        Assert.True(vm.CanUndo);
        await WaitForGridRebuildAsync(vm);
    }

    [Fact]
    public async Task SelectAllLayers_AlreadySelected_DoesNotCreateUndo()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();

        vm.SelectAllLayers();

        Assert.True(vm.AreAllCadLayersSelected);
        Assert.False(vm.CanUndo);
        Assert.Equal(2, vm.CadPads.Count);
    }

    [Fact]
    public async Task ApplyCanvasSelection_RegularPad_UpdatesInputsWithoutApplyingSizingOrUndo()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var pad = vm.RegularPads[0];
        var changes = Observe(vm, nameof(vm.ManualRowsRange), nameof(vm.ManualColsRange),
            nameof(vm.PendingColumnWidth), nameof(vm.PendingRowHeight));

        vm.ApplyCanvasSelection([], [pad.Index]);

        Assert.Equal((decimal)pad.Bounds.Width, vm.PendingColumnWidth);
        Assert.Equal((decimal)pad.Bounds.Height, vm.PendingRowHeight);
        Assert.False(vm.CanUndo);
        Assert.Equal<string?>([nameof(vm.ManualRowsRange), nameof(vm.ManualColsRange),
            nameof(vm.PendingColumnWidth), nameof(vm.PendingRowHeight)], changes);
        vm.PendingColumnWidth += 1;
        await WaitForGridRebuildAsync(vm);
        Assert.True(vm.CanUndo);
        Assert.Equal(pad.Bounds.Width + 1, vm.RegularPads[0].Bounds.Width, 5);
    }

    [Fact]
    public async Task ApplySelectionToManualRange_NoSelection_DoesNotCreateUndoOrChangeInput()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();

        vm.ApplySelectionToManualRange();

        Assert.Equal(string.Empty, vm.ManualRowsRange);
        Assert.False(vm.CanUndo);
        Assert.Equal("Manual sizing: select regular pads first.", vm.StatusText);
    }

    [Fact]
    public async Task OpenDxfCommand_LayerOptions_InitializesSelectionsBeforePublicHandlersResume()
    {
        using var fixture = new WorkspaceFixture();
        var vm = fixture.CreateViewModel();
        var changes = Observe(vm, nameof(vm.BoundLayerOptions), nameof(vm.SelectedBoundLayerOption),
            nameof(vm.BoundLayerOptionSummary), nameof(vm.RegularSourceLayerOptions), nameof(vm.SelectedRegularSourceLayerOption));

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal<string?>([nameof(vm.BoundLayerOptions), nameof(vm.RegularSourceLayerOptions)], changes);
        Assert.False(vm.CanUndo);
        vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(option => option.Name == "alpha");
        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        Assert.False(vm.LayerToggles.Single(toggle => toggle.Name == "alpha").IsSelected);
        Assert.Single(vm.CadPads);
        vm.RegularSourceMode = RegularSourceMode.GeneratedGrid;
        vm.GridAlignmentMode = GridAlignmentMode.FromCadBounds;
        vm.SelectedBoundLayerOption = vm.BoundLayerOptions.Single(option => option.Name == "beta");
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(20d, vm.RegularPads.Min(static pad => pad.Bounds.MinX), 5);
    }

    [Fact]
    public async Task OpenDxfCommand_EnabledMask_ResetAvoidsToggleStatusAndRestoresPublicHandler()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.RegularVisibilityMaskUi.IsEnabled = true;
        var statuses = new List<string>();
        vm.PropertyChanged += (_, args) => RecordStatus(vm, args.PropertyName, statuses);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.False(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.DoesNotContain("Regular visibility mask disabled. Step 4 uses pure geometry seed.", statuses);
        vm.RegularVisibilityMaskUi.IsEnabled = true;
        Assert.Equal("Regular visibility mask enabled.", vm.StatusText);
    }

    [Fact]
    public async Task SaveProjectAsync_ChangedSettings_RoundTripsWithoutLoadUndoOrDirtyNotifications()
    {
        using var fixture = new WorkspaceFixture();
        var saved = await fixture.OpenAsync();
        saved.SelectedScanOrderOption = saved.ScanOrderOptions[1];
        saved.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.StrictUnique;
        saved.NotchThresholdQ7 = 65;
        saved.IsWidthRowLocal = false;
        saved.LayerToggles[0].IsSelected = false;
        await WaitForGridRebuildAsync(saved);
        Assert.True(await saved.SaveProjectAsync());
        var loaded = fixture.CreateViewModel();
        var changes = Observe(loaded, nameof(loaded.CanUndo), nameof(loaded.HasUnsavedChanges));

        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);

        Assert.Equal(saved.SelectedScanOrder, loaded.SelectedScanOrder);
        Assert.Equal(CadOutputFwDiffAutoMode.StrictUnique, loaded.CadOutputFwDiffAutoMode);
        Assert.Equal(65m, loaded.NotchThresholdQ7);
        Assert.Equal(50.78m, loaded.NotchThresholdPercent);
        Assert.False(loaded.IsWidthRowLocal);
        Assert.True(loaded.IsHeightColumnLocal);
        Assert.False(loaded.UseLocalSizing);
        Assert.False(loaded.LayerToggles[0].IsSelected);
        Assert.False(loaded.CanUndo);
        Assert.False(loaded.HasUnsavedChanges);
        Assert.Empty(changes);
    }

    [Fact]
    public async Task Undo_LayersThresholdAndManualSizing_RestoresCompleteSequenceWithoutRecordingUndo()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var originalThreshold = vm.NotchThresholdQ7;
        var originalWidth = vm.RegularPads[0].Bounds.Width;
        vm.DeselectAllLayers();
        vm.SelectAllLayers();
        await WaitForGridRebuildAsync(vm);
        vm.NotchThresholdQ7 = 65;
        vm.ApplySizingToCells([0], [0], (decimal)originalWidth + 1, null);
        await WaitForGridRebuildAsync(vm);

        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(originalWidth, vm.RegularPads[0].Bounds.Width, 5);
        Assert.Equal(65m, vm.NotchThresholdQ7);
        vm.Undo();
        Assert.Equal(originalThreshold, vm.NotchThresholdQ7);
        Assert.Equal(Math.Round(originalThreshold * 100m / 128m, 2), vm.NotchThresholdPercent);
        vm.Undo();
        Assert.Equal(originalThreshold, vm.NotchThresholdQ7);
        vm.Undo();
        Assert.Empty(vm.CadPads);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.False(vm.CanUndo);
    }

    private static void RecordStatus(FreeformHelperViewModel vm, string? name, List<string> statuses)
    {
        if (string.Equals(name, nameof(vm.StatusText), StringComparison.Ordinal))
        {
            statuses.Add(vm.StatusText);
        }
    }

    internal sealed class WorkspaceFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
        private readonly string _dxfPath = CreateTempDxfFile(("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10));

        public FreeformHelperViewModel CreateViewModel() => new(new AppGeneralSettingsStore(Path.Combine(_directory.FullName, "app.json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(_dxfPath),
            PickSaveProjectPathAsync = () => Task.FromResult<string?>(Path.Combine(_directory.FullName, "project.json")),
            PickLoadProjectPathAsync = () => Task.FromResult<string?>(Path.Combine(_directory.FullName, "project.json")),
            ConfirmEmbedDxfAsync = () => Task.FromResult(false),
        };

        public async Task<FreeformHelperViewModel> OpenAsync()
        {
            var vm = CreateViewModel();
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            return vm;
        }

        public void Dispose()
        {
            File.Delete(_dxfPath);
            _directory.Delete(recursive: true);
        }
    }
}
