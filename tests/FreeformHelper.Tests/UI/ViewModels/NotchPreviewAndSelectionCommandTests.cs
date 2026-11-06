// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchPreviewAndSelectionCommandTests
{
    private static readonly Point2[] _padVertices =
    [
        new(0, 0),
        new(10, 0),
        new(10, 10),
        new(0, 10),
    ];
    private static readonly int[] _initialCadIds = [101];
    private static readonly int[] _initialRegularIds = [12];

    [Fact]
    public void FocusNotchValidationItemCommand_SharedSourceAndTarget_SelectsRegularOnce()
    {
        var vm = CreateValidationViewModel();
        vm.ApplyCanvasSelection(Array.Empty<int>(), _initialRegularIds);
        var item = vm.NotchValidationItems[0];
        Assert.Equal(item.SourceRegularPadId, item.TargetRegularPadId);
        Assert.Equal(12, Assert.Single(vm.SelectedRegularPadIndices));

        Assert.True(vm.FocusNotchValidationItemCommand.CanExecute(item));
        vm.FocusNotchValidationItemCommand.Execute(item);

        Assert.Equal(item.CadPadId, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(item.SourceRegularPadId, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Validation focus: DIRECT #1 (CAD=1, REG=1).", vm.StatusText);
    }

    [Fact]
    public void FocusNotchValidationItemCommand_NoTarget_PreservesSelectionAndShowsHint()
    {
        var vm = CreateValidationViewModel();
        vm.ApplyCanvasSelection(_initialCadIds, _initialRegularIds);
        var item = vm.NotchValidationItems[1];
        Assert.Equal(101, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(12, Assert.Single(vm.SelectedRegularPadIndices));

        Assert.True(vm.FocusNotchValidationItemCommand.CanExecute(item));
        vm.FocusNotchValidationItemCommand.Execute(item);

        Assert.Equal(101, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(12, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Validation focus skipped: no target pad available.", vm.StatusText);
    }

    private static FreeformHelperViewModel CreateValidationViewModel()
    {
        var polygon = new Polygon2(_padVertices);
        return new FreeformHelperViewModel
        {
            NotchPreviewAutoPlayEnabled = false,
            CadPads = new ObservableCollection<CadPad>
            {
                new(101, "CAD101", "signal", polygon),
            },
            RegularPads = new ObservableCollection<RegularPad>
            {
                new(0, 0, 11, polygon) { DiffIndex = 10 },
                new(0, 1, 12, polygon) { DiffIndex = 11 },
            },
            NotchValidationItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
            {
                CreateValidationItem(),
                CreateValidationItem() with
                {
                    SourceRegularPadId = -1,
                    TargetRegularPadId = -1,
                    CadPadId = -1,
                },
            },
        };
    }

    private static FreeformHelperViewModel.NotchValidationDisplayItem CreateValidationItem()
    {
        return new FreeformHelperViewModel.NotchValidationDisplayItem(
            KindText: "DIRECT",
            RowText: "#1",
            VersionText: "2.2",
            IcText: "IC 1",
            SourceText: "REG 11 / diff 10",
            TargetText: "REG 11 / diff 10",
            RatioText: "100%",
            AreaText: "100",
            ValuesText: string.Empty,
            CommentText: string.Empty,
            NoteText: string.Empty,
            SourceRegularPadId: 11,
            TargetRegularPadId: 11,
            CadPadId: 101,
            IcIndex: 0,
            SourceDiffIndex: 10,
            TargetDiffIndex: 10,
            IsDirect: true,
            IsIncoming: false,
            IsOutgoing: false);
    }
}
