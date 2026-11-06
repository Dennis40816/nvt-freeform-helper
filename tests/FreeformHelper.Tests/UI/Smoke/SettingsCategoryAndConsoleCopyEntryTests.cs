// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.LogConsole;
using Nvt.Core.Threading;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsCategoryAndConsoleCopyEntryTests
{
    private const string OtherCategoryEntities =
        "0\nTEXT\n8\nlabels\n1\nnote\n10\n0\n20\n0\n40\n1\n0\nENDSEC\n" +
        "0\nSECTION\n2\nBLOCKS\n0\nLINE\n8\nblocks\n10\n0\n20\n0\n11\n1\n21\n1\n0\nENDSEC\n0\nEOF\n";

    private static readonly (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] _categoryPads =
    [
        ("poly-a", 0, 0, 10, 10),
        ("poly-b", 20, 0, 30, 10),
    ];

    [AvaloniaFact]
    public async Task EnableCategorySelectsOnlyItsLayersAndPreservesPendingDraftAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new LoadingModeTestScope();
        var path = CreateCategoryDxf();
        var vm = files.CreateViewModel();
        try
        {
            await OpenCategoryDxfAsync(vm, path);
            vm.DeselectAllLayers();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            vm.HasUnsavedChanges = false;
            var draft = vm.CreateSettingsWindowViewModel();
            var originalThreshold = vm.MatchThreshold;
            draft.MatchThreshold = originalThreshold + 0.03m;
            var otherLayers = CaptureOtherLayers(vm);
            var otherCategories = CaptureOtherCategories(draft);

            GetPolylineCategory(draft).EnableCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            AssertPolylineSelection(vm, draft, true);
            Assert.Equal(otherLayers, CaptureOtherLayers(vm));
            Assert.Equal(otherCategories, CaptureOtherCategories(draft));
            Assert.Equal(originalThreshold + 0.03m, draft.MatchThreshold);
            Assert.Equal(originalThreshold, vm.MatchThreshold);
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task DisableCategoryAppliesImmediatelyAcrossCancelAndSaveAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new LoadingModeTestScope();
        var path = CreateCategoryDxf();
        var vm = files.CreateViewModel();
        try
        {
            await OpenCategoryDxfAsync(vm, path);
            vm.SelectAllLayers();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            var draft = vm.CreateSettingsWindowViewModel();
            var originalThreshold = vm.MatchThreshold;
            draft.MatchThreshold = originalThreshold + 0.03m;
            var otherLayers = CaptureOtherLayers(vm);
            var otherCategories = CaptureOtherCategories(draft);
            var closeRequests = 0;
            draft.RequestClose += () => closeRequests++;

            GetPolylineCategory(draft).DisableCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            AssertPolylineSelection(vm, draft, false);
            Assert.Equal(originalThreshold, vm.MatchThreshold);
            Assert.Equal(originalThreshold + 0.03m, draft.MatchThreshold);
            // Category commands act on the owner immediately; Cancel only discards ordinary draft fields.
            draft.CancelCommand.Execute(null);

            Assert.Equal(1, closeRequests);
            AssertPolylineSelection(vm, vm.CreateSettingsWindowViewModel(), false);
            Assert.Equal(originalThreshold, vm.MatchThreshold);
            Assert.Equal(otherLayers, CaptureOtherLayers(vm));
            Assert.Equal(otherCategories, CaptureOtherCategories(draft));

            vm.SelectAllLayers();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            var savedDraft = vm.CreateSettingsWindowViewModel();
            savedDraft.MatchThreshold = originalThreshold + 0.03m;
            savedDraft.RequestClose += () => closeRequests++;
            GetPolylineCategory(savedDraft).DisableCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            AssertPolylineSelection(vm, savedDraft, false);
            Assert.Equal(originalThreshold, vm.MatchThreshold);

            savedDraft.SaveCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            Assert.Equal(2, closeRequests);
            Assert.Equal(savedDraft.MatchThreshold, vm.MatchThreshold);
            AssertPolylineSelection(vm, vm.CreateSettingsWindowViewModel(), false);
            Assert.Equal(otherLayers, CaptureOtherLayers(vm));
            Assert.Equal(otherCategories, CaptureOtherCategories(savedDraft));
        }
        finally
        {
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionCopiesOnlySelectedRowsAndPreservesClipboardWhenClearedAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var store = new LogStore();
        store.Add(LogLevel.Info, "selection-copy", "selected first\ncontinuation", DateTimeOffset.UnixEpoch);
        store.Add(LogLevel.Warn, "selection-copy", "unselected middle", DateTimeOffset.UnixEpoch);
        store.Add(LogLevel.Error, "selection-copy", "selected last", DateTimeOffset.UnixEpoch);
        using var session = new ConsoleSession(
            store, new UiEventRunner(static (_, _) => { }, static (_, _) => { }), new ConsoleLinkOpener());
        string? copied = null;
        var writes = 0;
        session.RequestSetClipboardTextAsync = text =>
        {
            copied = text;
            writes++;
            return Task.FromResult(true);
        };
        var controller = session.Controller;
        await HeadlessUiPump.WaitUntilAsync(
            () => controller.Projection.RowCount == 3, "The console did not publish the three input rows.");
        var rows = controller.Projection.Rows;
        var selection = controller.ViewState.Selection.Add(rows[0].MemberSequences[0]).Add(rows[2].MemberSequences[0]);
        controller.RequestViewState(controller.ViewState with { Selection = selection });
        await HeadlessUiPump.FlushQueueAsync();

        Assert.True(controller.CopySelectionCommand.CanExecute(null));
        controller.CopySelectionCommand.Execute(null);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(1, writes);
        Assert.NotNull(copied);
        Assert.Contains("selected first\ncontinuation", copied, StringComparison.Ordinal);
        Assert.Contains("selected last", copied, StringComparison.Ordinal);
        Assert.Contains("selection-copy", copied, StringComparison.Ordinal);
        Assert.DoesNotContain("unselected middle", copied, StringComparison.Ordinal);
        Assert.True(copied.IndexOf("selected first", StringComparison.Ordinal) < copied.IndexOf("selected last", StringComparison.Ordinal));
        var previousClipboard = copied;
        controller.RequestViewState(controller.ViewState with { Selection = selection.Clear() });
        await HeadlessUiPump.FlushQueueAsync();

        Assert.False(controller.CopySelectionCommand.CanExecute(null));
        var exception = Record.Exception(() => controller.CopySelectionCommand.Execute(null));
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Null(exception);
        Assert.Equal(1, writes);
        Assert.Equal(previousClipboard, copied);
    }

    private static string CreateCategoryDxf()
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(_categoryPads);
        var content = TestFiles.ReadContent(path).ReplaceLineEndings("\n");
        TestFiles.WriteText(path, content.Replace("0\nENDSEC\n0\nEOF\n", OtherCategoryEntities, StringComparison.Ordinal));
        return path;
    }

    private static async Task OpenCategoryDxfAsync(FreeformHelperViewModel vm, string path)
    {
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Equal(4, vm.LayerToggles.Count);
        Assert.Equal(3, vm.CreateSettingsWindowViewModel().LayerCategoryRows.Count);
    }

    private static SettingsWindowViewModel.LayerCategoryRow GetPolylineCategory(SettingsWindowViewModel draft)
        => Assert.Single(draft.LayerCategoryRows, static row => row.Category == FreeformHelperViewModel.DxfLayerCategory.Polyline);

    private static void AssertPolylineSelection(FreeformHelperViewModel vm, SettingsWindowViewModel draft, bool selected)
    {
        var row = GetPolylineCategory(draft);
        Assert.Equal(2, row.TotalCount);
        Assert.Equal(selected ? 2 : 0, row.SelectedCount);
        Assert.All(vm.LayerToggles.Where(static toggle => toggle.Name.StartsWith("poly-", StringComparison.Ordinal)),
            toggle => Assert.Equal(selected, toggle.IsSelected));
    }

    private static (string Name, bool Selected)[] CaptureOtherLayers(FreeformHelperViewModel vm)
        => vm.LayerToggles.Where(static toggle => !toggle.Name.StartsWith("poly-", StringComparison.Ordinal))
            .OrderBy(static toggle => toggle.Name, StringComparer.Ordinal)
            .Select(static toggle => (toggle.Name, toggle.IsSelected)).ToArray();

    private static (FreeformHelperViewModel.DxfLayerCategory Category, int Total, int Selected)[] CaptureOtherCategories(
        SettingsWindowViewModel draft)
        => draft.LayerCategoryRows.Where(static row => row.Category != FreeformHelperViewModel.DxfLayerCategory.Polyline)
            .OrderBy(static row => row.Category)
            .Select(static row => (row.Category, row.TotalCount, row.SelectedCount)).ToArray();
}
