using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchExportSelectionViewModelTests
{
    private static readonly int[] WorkspaceCad201 = [201];
    private static readonly int[] WorkspaceCad200 = [200];



    [Fact]
    public void Ctor_DefaultRowDisplayMode_TransferOnly_HidesNoOpRows()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Equal(NotchExportRowDisplayMode.TransferOnly, vm.SelectedRowDisplayModeOption.Value);
        Assert.Single(vm.Rows);
        Assert.Equal(1, vm.TotalCount);
        Assert.Equal(2, vm.SelectedCount);
        Assert.True(vm.Rows[0].IsTransferRow);
    }


    [Fact]
    public void RowDisplayMode_AllRows_ShowsNoOpRows_ButViewFilterDoesNotChangeExportSelectionScope()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);

        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        Assert.Equal(2, vm.TotalCount);
        Assert.Equal(2, vm.SelectedCount);

        var selected = vm.BuildSelectedTable();
        Assert.Equal(2, selected.Rows.Count);

        var transferOnlyOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.TransferOnly);
        vm.SelectedRowDisplayModeOption = transferOnlyOption;

        Assert.Single(vm.Rows);
        selected = vm.BuildSelectedTable();
        Assert.Equal(2, selected.Rows.Count);
    }


    [Fact]
    public void ViewFilter_DefaultTransferOnly_DoesNotDropHiddenSelectionsFromExport()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Single(vm.Rows);
        var selected = vm.BuildSelectedTable();

        Assert.Equal(2, selected.Rows.Count);
    }


    [Fact]
    public void UseShownOnlyCommand_ReplacesSelectionWithVisibleRows()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);

        vm.UseShownOnlyCommand.Execute(null);
        var selected = vm.BuildSelectedTable();

        Assert.Single(vm.Rows);
        Assert.Single(selected.Rows);
        Assert.Equal(9, selected.Rows[0].DiffIndex);
    }


    [Fact]
    public void ApplyWorkspaceSelection_BuildsLinkedRows_AndToggleCommandsAffectThoseRows()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);
        var selectedCadIds = WorkspaceCad201;

        vm.ApplyWorkspaceSelection(selectedCadIds, Array.Empty<int>());

        Assert.True(vm.HasWorkspaceLinkedRows);
        Assert.Single(vm.WorkspaceLinkedRows);
        Assert.Equal(vm.Rows[1], vm.WorkspaceLinkedRows[0]);
        Assert.Equal(vm.Rows[1], vm.SelectedRow);

        vm.DisableWorkspaceLinkedRowsCommand.Execute(null);
        Assert.False(vm.Rows[1].IsSelected);
        Assert.Equal(2, vm.SelectedCount);

        vm.EnableWorkspaceLinkedRowsCommand.Execute(null);
        Assert.True(vm.Rows[1].IsSelected);
        Assert.Equal(3, vm.SelectedCount);
    }


    [Fact]
    public void ApplyWorkspaceSelection_DoesNotInvokePreviewCallback_UntilUserClicksRow()
    {
        var table = BuildSampleTable();
        var callbackCount = 0;
        var vm = new NotchExportSelectionViewModel(table, _ => callbackCount++);

        vm.ApplyWorkspaceSelection(WorkspaceCad201, Array.Empty<int>());

        Assert.Equal(0, callbackCount);
        vm.SelectPreviewRowCommand.Execute(vm.Rows[1]);
        Assert.Equal(1, callbackCount);
    }


    [Fact]
    public void ApplyWorkspaceSelection_RespectsTransferOnlyVisibleRows()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var selectedCadIds = WorkspaceCad200;

        vm.ApplyWorkspaceSelection(selectedCadIds, Array.Empty<int>());
        Assert.False(vm.HasWorkspaceLinkedRows);

        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        Assert.True(vm.HasWorkspaceLinkedRows);
        Assert.Single(vm.WorkspaceLinkedRows);
        Assert.Equal("1 linked row(s) · CAD 1 · REG 0", vm.WorkspaceSelectionSummaryText);
    }


    [Fact]
    public void Rows_ExposeCompactTableStatusAndMappingFields()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        Assert.Equal("No-op", vm.Rows[0].StatusText);
        Assert.True(vm.Rows[0].IsStatusWarning);
        Assert.Equal("REG 100 -> CAD 200", vm.Rows[0].MappingText);
        Assert.Equal("100%", vm.Rows[0].MatchSummaryText);

        Assert.Equal("Linked", vm.Rows[1].StatusText);
        Assert.True(vm.Rows[1].IsStatusLinked);
        Assert.Equal("REG 101 -> CAD 201", vm.Rows[1].MappingText);
    }


    [Fact]
    public void RowStatus_NoCad_WhenCadMissing()
    {
        var rows = new NotchTableRow[]
        {
            new NotchTableRow(
                icIndex: 2,
                diffIndex: 30,
                regularPadIndex: 888,
                cadPadId: null,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 30,
                    CombinePercent: 92,
                    TargetDiffIndex1: 31,
                    TargetRatioPercent1: 8,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "no-cad"),
        };
        var table = new NotchTable(rows);
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        Assert.Equal("No CAD", vm.Rows[0].StatusText);
        Assert.True(vm.Rows[0].IsStatusNoCad);
        Assert.Equal("REG 888 -> CAD -", vm.Rows[0].MappingText);
    }


    [Fact]
    public void HidePanelForInspectCommand_InvokesAttachedAction()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);
        var called = false;

        Assert.False(vm.CanHidePanelForInspect);
        vm.AttachWindowActions(() => called = true);

        Assert.True(vm.CanHidePanelForInspect);
        vm.HidePanelForInspectCommand.Execute(null);

        Assert.True(called);
    }
}
