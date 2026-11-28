// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingRepairEffectTests
{
    private static IndexMappingDecisionRowViewModel SelectRepair(IndexMappingReportViewModel report)
    {
        report.SetDecisionFilterModeCommand.Execute(IndexMappingDecisionFilterMode.ChangedByMask);
        Assert.Equal(4, report.DecisionRows.Count);
        var row = report.DecisionRows[0];
        Assert.True(row.CanApplyDiffOverride);
        report.SelectDecisionCommand.Execute(row);
        return row;
    }

    private static IndexMappingDecisionRowViewModel GetRepair(IndexMappingReportViewModel report, int cadId)
    {
        return Assert.Single(report.DecisionRows, row =>
            row.CadPadId == cadId && row.FilterMode == IndexMappingDecisionFilterMode.ChangedByMask);
    }

    private static Dictionary<int, PadInfoState> CapturePadInfo(FreeformHelperViewModel vm)
    {
        return vm.CadPads.ToDictionary(pad => pad.Id, pad =>
        {
            var snapshot = vm.BuildCadPadInspectorSnapshot(pad.Id,
                includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: false,
                includeMatchDetails: false, includeRuleTrace: false);
            Assert.NotNull(snapshot?.Cad);
            return new PadInfoState(snapshot.Cad.DxfIndex, snapshot.Cad.CadOutputFwDiffOverride);
        });
    }

    private static DecisionState CaptureDecision(IndexMappingDecisionRowViewModel row)
    {
        return new DecisionState(row.CurrentDisplay, row.DecisionReasonCodeText, row.DecisionSourceText);
    }

    private static void AssertOtherPadsUnchanged(
        FreeformHelperViewModel vm, IReadOnlyDictionary<int, PadInfoState> before, IEnumerable<int> changedCadIds)
    {
        var changed = changedCadIds.ToHashSet();
        var unchanged = before.Where(pair => !changed.Contains(pair.Key)).ToArray();
        Assert.NotEmpty(unchanged);
        var after = CapturePadInfo(vm);
        Assert.All(unchanged, pair => Assert.Equal(pair.Value, after[pair.Key]));
    }

    private static void AssertPadInfoEquals(
        IReadOnlyDictionary<int, PadInfoState> expected, IReadOnlyDictionary<int, PadInfoState> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        Assert.All(expected, pair => Assert.Equal(pair.Value, actual[pair.Key]));
    }

    private sealed record PadInfoState(int? DiffIndex, int? Override);
    private sealed record DecisionState(string Current, string Reason, string Source);

    private sealed class RepairWorkspace : IDisposable
    {
        private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
        private readonly string _dxfPath = CreateTempDxfFile(
            ("repair", 0, 0, 15, 10), ("repair", 20, 0, 40, 10),
            ("outside", 0, 10, 15, 20), ("outside", 20, 10, 40, 20));
        private readonly string _maskPath = TestFiles.WriteTempCsv(
            "Xch:\"4\",Ych:\"2\"\ntimestamp(0:0:0),DiffData,0,10,0,10\n0,10,0,10\n");

        public async Task<FreeformHelperViewModel> OpenAsync()
        {
            var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(Path.Combine(_directory.FullName, "app.json")))
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(_dxfPath),
                PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(_maskPath),
                ActiveAreaWidth = 40,
                ActiveAreaHeight = 20,
                GridPaddingPercent = 0,
                CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
            };
            var cascade = Assert.Single(vm.CascadeIcSettings);
            cascade.XChannels = 4;
            cascade.YChannels = 2;
            await WaitForGridRebuildAsync(vm);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            await vm.MatchCommand.ExecuteAsync(null);
            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
            Assert.Equal(4, vm.CadPads.Count);
            Assert.Equal(8, vm.RegularPads.Count);
            Assert.True(vm.RegularMask.IsEnabled);
            // Keep the four assignments deliberately wrong, so every proposed repair has an observable effect.
            foreach (var cadId in vm.CadPads.Select(pad => pad.Id).ToArray())
            {
                vm.SetCadOutputFwDiffIndexOverride(cadId, 100 + cadId);
            }

            return vm;
        }

        public static async Task<IndexMappingReportViewModel> AnalyzeAsync(FreeformHelperViewModel vm)
        {
            IndexMappingReportViewModel? opened = null;
            vm.OpenIndexMappingReportAsync = report =>
            {
                opened = report;
                return Task.CompletedTask;
            };
            await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);
            Assert.NotNull(opened);
            return opened;
        }

        public void Dispose()
        {
            File.Delete(_dxfPath);
            File.Delete(_maskPath);
            _directory.Delete(recursive: true);
        }
    }
}
