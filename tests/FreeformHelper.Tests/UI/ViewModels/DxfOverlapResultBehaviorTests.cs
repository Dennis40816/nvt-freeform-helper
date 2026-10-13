using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfOverlapResultBehaviorTests
{
    private static readonly (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] _overlappingPads =
    [
        ("signal", 0, 0, 10, 20),
        ("ground", 5, 5, 15, 25),
    ];

    private static readonly (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] _separatePads =
    [
        ("signal", 0, 0, 10, 20),
        ("signal", 30, 0, 40, 20),
    ];

    [Fact]
    public void NewViewModel_BeforeAnyCheck_ShowsNotRunResult()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal("DXF check: not run.", vm.DxfOverlapSummary);
        Assert.Empty(vm.DxfOverlapIssues);
        Assert.Equal(0, vm.DxfOverlapIssueCount);
        Assert.False(vm.HasDxfOverlapIssues);
        Assert.Empty(vm.DxfOverlapHighlightedCadIds);
        Assert.False(vm.HasDxfOverlapHighlights);
    }

    [Fact]
    public async Task CheckDxfQuality_NoDxfLoaded_ReportsNoDxfInSummaryAndStatus()
    {
        var vm = new FreeformHelperViewModel();

        await vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.Equal("DXF check: no DXF loaded.", vm.DxfOverlapSummary);
        Assert.Equal(vm.DxfOverlapSummary, vm.StatusText);
        Assert.Empty(vm.DxfOverlapIssues);
        Assert.False(vm.HasDxfOverlapIssues);
        Assert.False(vm.HasDxfOverlapHighlights);
    }

    [Fact]
    public async Task CheckDxfQuality_OverlappingPads_ShowsWarningIssuesAndBothPadsHighlighted()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);

        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.StartsWith("DXF warnings:", dxf.Vm.DxfOverlapSummary, StringComparison.Ordinal);
        Assert.Equal(dxf.Vm.DxfOverlapSummary, dxf.Vm.StatusText);
        Assert.NotEmpty(dxf.Vm.DxfOverlapIssues);
        Assert.Equal(dxf.Vm.DxfOverlapIssues.Count, dxf.Vm.DxfOverlapIssueCount);
        Assert.True(dxf.Vm.HasDxfOverlapIssues);
        Assert.Equal(dxf.Vm.CadPads.Select(static pad => pad.Id).Order(), dxf.Vm.DxfOverlapHighlightedCadIds);
        Assert.True(dxf.Vm.HasDxfOverlapHighlights);
    }

    [Fact]
    public async Task CheckDxfQuality_SeparatePads_ShowsNoOverlapsAndNoHighlights()
    {
        using var dxf = await DxfFixture.OpenAsync(_separatePads);

        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.Equal("DXF check: no overlaps detected.", dxf.Vm.DxfOverlapSummary);
        Assert.Empty(dxf.Vm.DxfOverlapIssues);
        Assert.Equal(0, dxf.Vm.DxfOverlapIssueCount);
        Assert.False(dxf.Vm.HasDxfOverlapIssues);
        Assert.Empty(dxf.Vm.DxfOverlapHighlightedCadIds);
        Assert.False(dxf.Vm.HasDxfOverlapHighlights);
    }

    [Fact]
    public async Task CheckDxfQuality_RunTwice_ReplacesTheResultWithTheSameContent()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);
        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);
        var firstIssues = dxf.Vm.DxfOverlapIssues.ToArray();

        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.Equal(firstIssues, dxf.Vm.DxfOverlapIssues);
        Assert.Equal(firstIssues.Length, dxf.Vm.DxfOverlapIssueCount);
    }

    [Fact]
    public async Task ClearDxfOverlapHighlights_AfterCheck_ClearsHighlightsAndKeepsIssues()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);
        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);
        var issueCount = dxf.Vm.DxfOverlapIssueCount;

        dxf.Vm.ClearDxfOverlapHighlightsCommand.Execute(null);

        Assert.Empty(dxf.Vm.DxfOverlapHighlightedCadIds);
        Assert.False(dxf.Vm.HasDxfOverlapHighlights);
        Assert.Equal(issueCount, dxf.Vm.DxfOverlapIssueCount);
        Assert.True(dxf.Vm.HasDxfOverlapIssues);
        Assert.StartsWith("DXF warnings:", dxf.Vm.DxfOverlapSummary, StringComparison.Ordinal);
        Assert.Equal("DXF check: highlight cleared.", dxf.Vm.StatusText);
    }

    [Fact]
    public async Task SelectDxfOverlapPads_AfterCheck_SelectsBothOverlappingPads()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);
        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);

        dxf.Vm.SelectDxfOverlapPadsCommand.Execute(null);

        Assert.Equal("DXF check: highlight + select (2 pads).", dxf.Vm.StatusText);
        Assert.Equal("Selected: CAD=2.", dxf.Vm.SelectionSummary);
    }

    [Fact]
    public async Task SelectDxfOverlapPads_AfterHighlightsCleared_RestoresHighlightsFromTheLastResult()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);
        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);
        var highlighted = dxf.Vm.DxfOverlapHighlightedCadIds.ToArray();
        dxf.Vm.ClearDxfOverlapHighlightsCommand.Execute(null);

        dxf.Vm.SelectDxfOverlapPadsCommand.Execute(null);

        Assert.Equal(highlighted, dxf.Vm.DxfOverlapHighlightedCadIds);
        Assert.True(dxf.Vm.HasDxfOverlapHighlights);
        Assert.Equal("Selected: CAD=2.", dxf.Vm.SelectionSummary);
    }

    [Fact]
    public void SelectDxfOverlapPads_WithoutAnyResult_ReportsNoOverlapResult()
    {
        var vm = new FreeformHelperViewModel();

        vm.SelectDxfOverlapPadsCommand.Execute(null);

        Assert.Equal("DXF check: no overlap result.", vm.StatusText);
        Assert.Empty(vm.DxfOverlapHighlightedCadIds);
        Assert.False(vm.HasDxfOverlapHighlights);
    }

    [Fact]
    public async Task CheckDxfQuality_FirstOverlappingCheck_RaisesTheSummaryTheCountAndTheFlags()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);
        var changes = RecordNotifications(
            dxf.Vm,
            nameof(FreeformHelperViewModel.DxfOverlapSummary),
            nameof(FreeformHelperViewModel.DxfOverlapIssueCount),
            nameof(FreeformHelperViewModel.HasDxfOverlapIssues),
            nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds),
            nameof(FreeformHelperViewModel.HasDxfOverlapHighlights));

        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.Contains(nameof(FreeformHelperViewModel.DxfOverlapSummary), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.DxfOverlapIssueCount), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.HasDxfOverlapIssues), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.HasDxfOverlapHighlights), changes);
    }

    [Fact]
    public async Task CheckDxfQuality_SecondCheckWithTheSameContent_RaisesNoCountOrFlagNotification()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);
        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);
        var changes = RecordNotifications(
            dxf.Vm,
            nameof(FreeformHelperViewModel.DxfOverlapSummary),
            nameof(FreeformHelperViewModel.DxfOverlapIssueCount),
            nameof(FreeformHelperViewModel.HasDxfOverlapIssues),
            nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds),
            nameof(FreeformHelperViewModel.HasDxfOverlapHighlights));

        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.Contains(nameof(FreeformHelperViewModel.DxfOverlapSummary), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.DxfOverlapIssueCount), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.HasDxfOverlapIssues), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.HasDxfOverlapHighlights), changes);
    }

    [Fact]
    public async Task ClearDxfOverlapHighlights_AfterCheck_RaisesOnlyTheHighlightProperties()
    {
        using var dxf = await DxfFixture.OpenAsync(_overlappingPads);
        await dxf.Vm.CheckDxfQualityCommand.ExecuteAsync(null);
        var changes = RecordNotifications(
            dxf.Vm,
            nameof(FreeformHelperViewModel.DxfOverlapSummary),
            nameof(FreeformHelperViewModel.DxfOverlapIssueCount),
            nameof(FreeformHelperViewModel.HasDxfOverlapIssues),
            nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds),
            nameof(FreeformHelperViewModel.HasDxfOverlapHighlights));

        dxf.Vm.ClearDxfOverlapHighlightsCommand.Execute(null);

        Assert.Contains(nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds), changes);
        Assert.Contains(nameof(FreeformHelperViewModel.HasDxfOverlapHighlights), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.DxfOverlapSummary), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.DxfOverlapIssueCount), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.HasDxfOverlapIssues), changes);
    }

    [Fact]
    public void ClearDxfOverlapHighlights_WithoutResult_RaisesOnlyTheCollectionProperty()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(
            vm,
            nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds),
            nameof(FreeformHelperViewModel.HasDxfOverlapHighlights));

        vm.ClearDxfOverlapHighlightsCommand.Execute(null);

        Assert.Contains(nameof(FreeformHelperViewModel.DxfOverlapHighlightedCadIds), changes);
        Assert.DoesNotContain(nameof(FreeformHelperViewModel.HasDxfOverlapHighlights), changes);
    }

    private sealed class DxfFixture : IDisposable
    {
        private readonly string _path;

        private DxfFixture(string path, FreeformHelperViewModel vm)
        {
            _path = path;
            Vm = vm;
        }

        public FreeformHelperViewModel Vm { get; }

        public static async Task<DxfFixture> OpenAsync(params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
        {
            var path = CreateTempDxfFile(pads);
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await vm.WaitForGridRebuildIdleAsync();
            return new DxfFixture(path, vm);
        }

        public void Dispose()
        {
            File.Delete(_path);
        }
    }
}
