// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
[Trait("Category", "Characterization")]
public sealed class SettingsWindowMixedLayoutApplyCharacterizationTests
{
    [AvaloniaFact]
    public async Task SettingsWindow_MixedLayoutEdits_ApplyOneRebuildAndPreserveNavigationPolicy()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            var setup = vm.CreateSettingsWindowViewModel();
            var initialIc = Assert.Single(setup.CascadeIcSettings);
            initialIc.XChannels = 2m;
            initialIc.YChannels = 2m;
            setup.SelectedGridAlignmentOption = setup.GridAlignmentOptions.Single(
                static option => option.Value == GridAlignmentMode.FromCadBounds);
            setup.GridPaddingPercent = 0m;
            setup.PanelBiasX = 0m;
            setup.PanelBiasY = 0m;
            setup.SaveCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            // The scope supplies a closed rectangle on "signal", with bounds (0, 0) to (10, 20).
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            var cad = Assert.Single(vm.CadPads);
            Assert.Equal(4, vm.RegularPads.Count);
            var originalPads = vm.RegularPads;
            var host = new SelectionLayerCanvasProbe();
            vm.CanvasHost = host;
            var regularIndex = vm.RegularPads[0].Index;
            host.SetSelection([cad.Id], [regularIndex]);
            vm.ApplyCanvasSelection([cad.Id], [regularIndex]);

            var draft = vm.CreateSettingsWindowViewModel();
            draft.GridPaddingPercent = 10m;
            draft.PanelBiasX = 7m;
            draft.PanelBiasY = -3m;
            Assert.Single(draft.CascadeIcSettings).XChannels = 3m;
            var rebuildCount = 0;
            vm.PropertyChanged += (_, change) =>
            {
                // Count completed grid publications through the same observable property the canvas binds.
                if (change.PropertyName == nameof(vm.RegularPads))
                {
                    rebuildCount++;
                }
            };

            draft.SaveCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            // Current policy: one mixed Apply rebuild fits the content and clears both canvas and VM selection.
            Assert.Equal(1, rebuildCount);
            Assert.Equal(1, host.FitToContentCount);
            Assert.Equal(0, host.ResetViewCount);
            Assert.Equal(0, host.FocusSelectionCount);
            Assert.Equal(0, host.FocusWithMinZoomCount);
            Assert.Empty(host.SelectedCadIds);
            Assert.Empty(host.SelectedRegularIds);
            Assert.Empty(vm.SelectedCadPadIds);
            Assert.Empty(vm.SelectedRegularPadIndices);
            Assert.NotSame(originalPads, vm.RegularPads);
            Assert.Equal(6, vm.RegularPads.Count);
            Assert.Equal(3m, vm.XChannels);
            Assert.Equal(2m, vm.YChannels);
            Assert.Equal(10m, vm.GridPaddingPercent);
            Assert.Equal(7m, vm.PanelBiasX);
            Assert.Equal(-3m, vm.PanelBiasY);

            // CAD-bounds alignment keeps the saved panel bias out of the geometry; 10% padding expands each side.
            Assert.Equal(-1d, vm.RegularPads.Min(static pad => pad.Bounds.MinX), 6);
            Assert.Equal(11d, vm.RegularPads.Max(static pad => pad.Bounds.MaxX), 6);
            Assert.Equal(-2d, vm.RegularPads.Min(static pad => pad.Bounds.MinY), 6);
            Assert.Equal(22d, vm.RegularPads.Max(static pad => pad.Bounds.MaxY), 6);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }
}
