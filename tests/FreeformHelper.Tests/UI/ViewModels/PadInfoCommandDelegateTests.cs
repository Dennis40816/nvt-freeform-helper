// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInfoCommandDelegateTests
{
    private static readonly Point2[] _padVertices =
    [
        new(0, 0),
        new(10, 0),
        new(10, 8),
        new(0, 8),
    ];

    [Theory]
    [InlineData("-")]
    [InlineData("274")]
    public void CadPadInfo_HighlightAllNotchOwnersCommand_WithoutOtherOwners_DoesNotHighlight(string owners)
    {
        var highlightedGroups = new List<IReadOnlyCollection<int>>();
        var vm = CreateCadPadViewModel(owners, highlightedGroups.Add);
        Assert.True(vm.HasToFullDiagnosticsText);
        Assert.Empty(vm.NotchOwnerLinks);
        Assert.True(vm.HighlightAllNotchOwnersCommand.CanExecute(null));

        vm.HighlightAllNotchOwnersCommand.Execute(null);

        Assert.Empty(highlightedGroups);
        Assert.Empty(vm.NotchOwnerLinks);
        Assert.True(vm.HasToFullDiagnosticsText);
        Assert.False(vm.HasPendingChanges);
    }

    private static CadPadInfoViewModel CreateCadPadViewModel(
        string owners,
        Action<IReadOnlyCollection<int>> highlightCadOwnerPads)
    {
        var pad = new CadPad(274, "CAD274", "AA", new Polygon2(_padVertices));
        var diagnostics =
            "IC3/diff112 reg4808: ov=10, src=10, blk=0, reach=10, blocker=0, owners=" + owners +
            ", ownerShare=-, boundary=N, candidate=Y, tofull=Y (GATE_NOT_BOUNDARY)";

        return new CadPadInfoViewModel(
            pads: new List<CadPad> { pad },
            getDxfIndex: static _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null,
            highlightCadOwnerPads: highlightCadOwnerPads,
            getCadV22CompensationPreview: static _ => (1.0, 1.0, 1.0, true),
            getCadV22CompensationDiagnostics: _ => diagnostics);
    }
}
