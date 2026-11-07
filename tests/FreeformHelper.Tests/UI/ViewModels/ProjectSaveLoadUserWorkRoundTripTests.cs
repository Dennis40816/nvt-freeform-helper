// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectSaveLoadUserWorkRoundTripTests
{
    [AvaloniaFact]
    public async Task SaveLoadPreservesEditedDxfExportAndManualRegularWorkAsync()
    {
        using var files = new LoadingModeTestScope();
        var original = await OpenAsync(files,
            ("signal", 0, 0, 8, 20), ("signal", 10, 0, 20, 20),
            ("signal", 18, 0, 28, 20), ("signal", 30, 0, 40, 20), ("aux", 0, 25, 8, 30));
        await EditDxfAsync(original);
        await ApplyManualRegularWorkAsync(original);
        var expectedPads = CaptureVisibleCad(original);
        var expectedGrid = CaptureRegularWork(original);
        var expectedCounts = CaptureEditCounts(original);
        var overrideId = original.CadPads[0].Id;
        var expectedDxf = await ExportDxfAsync(original, files.PathFor("before.dxf"));
        Assert.Equal(2, expectedPads.Length);
        Assert.Equal((3, 1, 1, 1), expectedCounts);
        Assert.Contains("LWPOLYLINE", expectedDxf, StringComparison.Ordinal);
        Assert.DoesNotContain("\naux\n", expectedDxf.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);

        Assert.True(await original.SaveProjectAsync(), original.StatusText);
        var loaded = await LoadFreshAsync(files);

        Assert.Equal(expectedPads, CaptureVisibleCad(loaded));
        Assert.Equal(expectedGrid, CaptureRegularWork(loaded));
        Assert.Equal(expectedCounts, CaptureEditCounts(loaded));
        Assert.True(loaded.TryGetCadOutputFwDiffIndex(overrideId, out var diff));
        Assert.Equal(37, diff);
        Assert.Equal(expectedDxf, await ExportDxfAsync(loaded, files.PathFor("after.dxf")));
    }

    [AvaloniaFact]
    public async Task SaveLoadManualSizesTagsAndFwDiffProduceTheSameNotchFileAsync()
    {
        using var files = new LoadingModeTestScope();
        var original = await OpenAsync(files, ("signal", 7, 3.5, 13, 6.5));
        await ApplyManualRegularWorkAsync(original);
        var expectedGrid = CaptureRegularWork(original);
        var cadId = Assert.Single(original.CadPads).Id;
        var expected = await ExportNotchProfileAsync(original, NotchExportProfile.Release, files.PathFor("before.c"));
        Assert.Contains("//   Sections: v2.1=empty, v2.2=present", expected, StringComparison.Ordinal);

        Assert.True(await original.SaveProjectAsync(), original.StatusText);
        var loaded = await LoadFreshAsync(files);
        var actual = await ExportNotchProfileAsync(loaded, NotchExportProfile.Release, files.PathFor("after.c"));

        Assert.Equal(expectedGrid, CaptureRegularWork(loaded));
        Assert.Equal(37, loaded.GetCadOutputFwDiffIndexOverride(cadId));
        Assert.True(loaded.TryGetCadOutputFwDiffIndex(cadId, out var diff));
        Assert.Equal(37, diff);
        Assert.Equal(expected, actual);
    }

    [AvaloniaFact]
    public async Task SaveLoadWithReplayRedetectsManualNoneButKeepsXAndYAndXYAsync()
    {
        using var files = new LoadingModeTestScope();
        var original = await OpenAsync(files, ("signal", 0, 0, 40, 20));
        original.AutoReplayStep2AfterProjectLoad = true;
        original.EnableAutoDetectXy = true;
        await original.MatchCommand.ExecuteAsync(null);
        await original.AutoDetectFreeformsCommand.ExecuteAsync(null);
        Assert.All(original.RegularPads, static pad => Assert.Equal(FreeformType.XYWay, pad.Freeform));
        ApplyManualTags(original);
        Assert.Equal([FreeformType.None, FreeformType.XWay, FreeformType.YWay, FreeformType.XYWay],
            original.RegularPads.OrderBy(static pad => pad.Index).Select(static pad => pad.Freeform));

        Assert.True(await original.SaveProjectAsync(), original.StatusText);
        var loaded = await LoadFreshAsync(files);

        // None removes its override today, so replay detects that pad again.
        Assert.Equal([FreeformType.XYWay, FreeformType.XWay, FreeformType.YWay, FreeformType.XYWay],
            loaded.RegularPads.OrderBy(static pad => pad.Index).Select(static pad => pad.Freeform));
        Assert.Contains("Step 2 freeform restored", loaded.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SaveTwiceWithoutEditsPreservesAllContentExceptSaveTimestampAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenAsync(files, ("signal", 0, 0, 40, 20));
        await ApplyManualRegularWorkAsync(vm);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var first = await ReadProjectWithoutSaveTimestampAsync(files);

        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var second = await ReadProjectWithoutSaveTimestampAsync(files);

        Assert.Equal(first, second);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task MissingUnembeddedDxfLoadsManualRegularWorkButResavingDropsDxfEditsAsync()
    {
        using var files = new LoadingModeTestScope();
        var original = await OpenAsync(files, ("signal", 0, 0, 40, 20), ("signal", 50, 0, 60, 20));
        await SelectCadPadsAsync(original, [original.CadPads[1].Id]);
        original.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(original);
        await ApplyManualRegularWorkAsync(original);
        var expectedGrid = CaptureRegularWork(original);
        var cadId = Assert.Single(original.CadPads).Id;
        Assert.True(await original.SaveProjectAsync(), original.StatusText);
        var before = JsonProjectStore.Load(files.PathFor("project.json"));
        Assert.Single(before.HiddenCadPadIds);
        Assert.Null(before.EmbeddedDxf);
        File.Delete(files.PathFor("input.dxf"));

        var loaded = await LoadFreshAsync(files);

        Assert.Empty(loaded.CadPads);
        Assert.Equal(expectedGrid, CaptureRegularWork(loaded));
        Assert.Equal(37, loaded.GetCadOutputFwDiffIndexOverride(cadId));
        // A missing external DXF currently leaves a success/ready status rather than a missing-file warning.
        Assert.Equal("Project loaded: no CAD output pads; Step 1 auto replay skipped.", loaded.StatusText);
        Assert.True(await loaded.SaveProjectAsync(), loaded.StatusText);
        var after = JsonProjectStore.Load(files.PathFor("project.json"));
        Assert.Equal(before.LastDxfPath, after.LastDxfPath);
        Assert.Equal(before.FreeformOverrides.OrderBy(static pair => pair.Key),
            after.FreeformOverrides.OrderBy(static pair => pair.Key));
        Assert.Equal(37, after.CadOutputFwDiffIndexOverrides[cadId]);
        Assert.Empty(after.HiddenCadPadIds);
    }

    private static async Task<FreeformHelperViewModel> OpenAsync(LoadingModeTestScope files,
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var generatedPath = CreateTempDxfFile(pads);
        try
        {
            TestFiles.CopyOverwriting(generatedPath, files.PathFor("input.dxf"));
        }
        finally
        {
            File.Delete(generatedPath);
        }

        var vm = files.CreateViewModel();
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(files.PathFor("input.dxf"));
        vm.ActiveAreaWidth = 40;
        vm.ActiveAreaHeight = 20;
        vm.GridPaddingPercent = 0;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.AutoReplayStep2AfterProjectLoad = false;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 2;
        vm.EnableV21 = false;
        vm.EnableV22 = true;
        vm.SelectedNotchExportFileTypeOption = Assert.Single(vm.NotchExportFileTypeOptions,
            static option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(pads.Length, vm.CadPads.Count);
        Assert.Equal(4, vm.RegularPads.Count);
        return vm;
    }

    private static async Task<FreeformHelperViewModel> LoadFreshAsync(LoadingModeTestScope files)
    {
        var loaded = files.CreateViewModel();
        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);
        Assert.Equal(ProjectSessionMode.Ready, loaded.SessionMode);
        Assert.False(loaded.HasUnsavedChanges);
        return loaded;
    }

    private static async Task EditDxfAsync(FreeformHelperViewModel vm)
    {
        var ids = vm.CadPads.Where(static pad => pad.Layer == "signal")
            .OrderBy(static pad => pad.Bounds.MinX).Select(static pad => pad.Id).ToArray();
        await SelectCadPadsAsync(vm, [ids[0]]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [ids[1], ids[2]]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [ids[3]]);
        vm.DxfEditRotationDegrees = 30m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [ids[3]]);
        vm.DxfEditNewLayerName = "manual";
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Single(vm.LayerToggles, static layer => layer.Name == "aux").IsSelected = false;
        await WaitForGridRebuildAsync(vm);
    }

    private static async Task ApplyManualRegularWorkAsync(FreeformHelperViewModel vm)
    {
        vm.ApplySizingToCells([0], [0], 6m, 7m);
        await WaitForGridRebuildAsync(vm);
        var resized = Assert.Single(vm.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        Assert.Equal(6, resized.Bounds.Width, 6);
        Assert.Equal(7, resized.Bounds.Height, 6);
        await vm.MatchCommand.ExecuteAsync(null);
        ApplyManualTags(vm);
        vm.SetCadOutputFwDiffIndexOverride(vm.CadPads[0].Id, 37);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(vm.CadPads[0].Id, out var diff));
        Assert.Equal(37, diff);
    }

    private static void ApplyManualTags(FreeformHelperViewModel vm)
    {
        var pads = vm.RegularPads.OrderBy(static pad => pad.Index).ToArray();
        vm.ApplyCanvasSelection([], [pads[0].Index]);
        vm.SetFreeformXYCommand.Execute(null);
        vm.SetFreeformNoneCommand.Execute(null);
        vm.ApplyCanvasSelection([], [pads[1].Index]);
        vm.SetFreeformXCommand.Execute(null);
        vm.ApplyCanvasSelection([], [pads[2].Index]);
        vm.SetFreeformYCommand.Execute(null);
        vm.ApplyCanvasSelection([], [pads[3].Index]);
        vm.SetFreeformXYCommand.Execute(null);
        Assert.Equal([FreeformType.None, FreeformType.XWay, FreeformType.YWay, FreeformType.XYWay],
            pads.Select(static pad => pad.Freeform));
    }

    private static string[] CaptureVisibleCad(FreeformHelperViewModel vm) => vm.CadPads.OrderBy(static pad => pad.Id)
        .Select(static pad => FormattableString.Invariant($"{pad.Id}:{pad.Layer}:{CaptureVertices(pad.Polygon)}"))
        .ToArray();

    private static string[] CaptureRegularWork(FreeformHelperViewModel vm) => vm.RegularPads.OrderBy(static pad => pad.Index)
        .Select(static pad => FormattableString.Invariant(
            $"{pad.Index}:{pad.Row}:{pad.Col}:{pad.Freeform}:{CaptureVertices(pad.Polygon)}"))
        .ToArray();

    private static string CaptureVertices(Polygon2 polygon) => string.Join(";",
        polygon.Vertices.Select(static point => FormattableString.Invariant($"{point.X:R},{point.Y:R}")));

    private static (int Hidden, int Combined, int Relayered, int Rotated) CaptureEditCounts(FreeformHelperViewModel vm) =>
        (vm.DeletedCadPadCount, vm.CombinedCadPadCount, vm.RelayeredCadPadCount, vm.RotatedCadPadCount);

    private static async Task<string> ExportDxfAsync(FreeformHelperViewModel vm, string path)
    {
        vm.PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(path);
        await vm.ExportDxfVisibleCommand.ExecuteAsync(null);
        Assert.True(File.Exists(path), vm.StatusText);
        return await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadProjectWithoutSaveTimestampAsync(LoadingModeTestScope files)
    {
        var text = await TestFiles.ReadContentAsync(files.PathFor("project.json"), TestContext.Current.CancellationToken);
        // JsonProjectStore updates savedAt on every save; compare every other character as written.
        const string key = "\"savedAt\": \"";
        var valueStart = text.IndexOf(key, StringComparison.Ordinal);
        Assert.True(valueStart >= 0, text);
        valueStart += key.Length;
        var valueEnd = text.IndexOf('"', valueStart);
        Assert.True(valueEnd > valueStart, text);
        return string.Concat(text.AsSpan(0, valueStart), "<savedAt>", text.AsSpan(valueEnd));
    }
}
