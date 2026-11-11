// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditChangeEntryBuilderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildWithNoEditsReturnsEmptyList(bool hasCad)
    {
        var current = hasCad ? new CadPadSet([CreatePad(1)]) : null;
        var edit = new CadEditState { BaselineCad = current };

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, new CombinedCadState(), static _ => false, PolygonsEquivalent);

        Assert.Empty(entries);
    }

    [Fact]
    public void BuildListsManualHideWithRestoreAction()
    {
        using var culture = TestCulture.UseEnUs();
        var current = new CadPadSet([CreatePad(1)]);
        var edit = new CadEditState();
        edit.HiddenCadPadIds.Add(1);

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, new CombinedCadState(), static _ => false, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Hidden, entry.Kind);
        Assert.Equal(1, entry.CadPadId);
        Assert.Equal(1, entry.FocusCadPadId);
        Assert.Equal("Pad 1", entry.CadName);
        Assert.Equal("Hidden", entry.StatusText);
        Assert.Equal("Layer A", entry.CurrentLayerText);
        Assert.Equal("Manual hide", entry.ListDetailText);
        Assert.Equal("Hidden manually from the current DXF edit session.", entry.DetailText);
        Assert.Equal(IconGlyphs.Undo, entry.ActionGlyph);
        Assert.Equal("Restore", entry.ActionLabel);
        Assert.Equal("Restore this hidden CAD pad.", entry.ActionHint);
        Assert.False(entry.CanFocus);
        Assert.True(entry.CanApply);
    }

    [Theory]
    [InlineData(true, "Combine group 7 hides this source pad. Source pads: 2.")]
    [InlineData(false, "Combine group 7 hides this source pad.")]
    public void BuildListsCombineHiddenSourceWithKnownOrMissingGroup(bool hasGroup, string expectedDetail)
    {
        using var culture = TestCulture.UseEnUs();
        var current = new CadPadSet([CreatePad(1)]);
        var edit = new CadEditState();
        edit.HiddenCadPadIds.Add(1);
        var combined = new CombinedCadState();
        combined.GroupIdBySourceCadId[1] = 7;
        if (hasGroup)
        {
            combined.Groups[7] = new CombinedCadGroupState([1, 2], [10]);
        }

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, combined, static _ => false, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Hidden, entry.Kind);
        Assert.Equal(1, entry.CadPadId);
        Assert.Equal(1, entry.FocusCadPadId);
        Assert.Equal("Pad 1", entry.CadName);
        Assert.Equal("Hidden by combine", entry.StatusText);
        Assert.Equal("Layer A", entry.CurrentLayerText);
        Assert.Equal("Group 7", entry.ListDetailText);
        Assert.Equal(expectedDetail, entry.DetailText);
        Assert.Equal(IconGlyphs.Remove, entry.ActionGlyph);
        Assert.Equal("Clear group", entry.ActionLabel);
        Assert.Equal("Clear this combine group and restore its source pads.", entry.ActionHint);
        Assert.False(entry.CanFocus);
        Assert.True(entry.CanApply);
    }

    [Fact]
    public void BuildListsBaselineOnlyHiddenPadAndSkipsUnknownHiddenId()
    {
        using var culture = TestCulture.UseEnUs();
        var edit = new CadEditState { BaselineCad = new CadPadSet([CreatePad(1, "Original")]) };
        edit.HiddenCadPadIds.UnionWith([99, 1]);

        var entries = DxfEditChangeEntryBuilder.Build(
            null, edit, new CombinedCadState(), static _ => false, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Hidden, entry.Kind);
        Assert.Equal(1, entry.CadPadId);
        Assert.Equal("Pad 1", entry.CadName);
        Assert.Equal("Layer Original", entry.CurrentLayerText);
        Assert.Equal("Hidden", entry.StatusText);
        Assert.Equal("Manual hide", entry.ListDetailText);
        Assert.False(entry.CanFocus);
        Assert.True(entry.CanApply);
    }

    [Theory]
    [InlineData(true, 1, "Auto-hidden duplicate", "Restore", "Restore this duplicate CAD pad into the active working set.")]
    [InlineData(false, 2, "Restored duplicate", "Hide again", "Auto-hide this duplicate CAD pad again.")]
    public void BuildListsDuplicateWithCanonicalFocusAndRestoreOrRehideAction(
        bool autoHidden, int expectedFocusId, string expectedStatus, string expectedAction, string expectedHint)
    {
        using var culture = TestCulture.UseEnUs();
        var current = new CadPadSet([CreatePad(2)]);
        var edit = new CadEditState { BaselineCad = new CadPadSet([CreatePad(1)]) };
        edit.DuplicateCadPadIdToCanonicalId[2] = 1;
        edit.DuplicateCadPadIdToCanonicalId[99] = 1;
        if (autoHidden)
        {
            edit.AutoHiddenDuplicateCadPadIds.Add(2);
        }

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, new CombinedCadState(), static _ => true, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Duplicate, entry.Kind);
        Assert.Equal(2, entry.CadPadId);
        Assert.Equal(expectedFocusId, entry.FocusCadPadId);
        Assert.Equal("Pad 2", entry.CadName);
        Assert.Equal(expectedStatus, entry.StatusText);
        Assert.Equal("Layer A · Canonical CAD 1", entry.CurrentLayerText);
        Assert.Equal("Canonical CAD 1", entry.ListDetailText);
        Assert.Equal("Exact same-layer duplicate of CAD 1 (Pad 1).", entry.DetailText);
        Assert.Equal(autoHidden ? IconGlyphs.Undo : IconGlyphs.VisibilityOff, entry.ActionGlyph);
        Assert.Equal(expectedAction, entry.ActionLabel);
        Assert.Equal(expectedHint, entry.ActionHint);
        Assert.True(entry.CanFocus);
        Assert.True(entry.CanApply);
    }

    [Fact]
    public void BuildListsExactDuplicateWhenCanonicalPadIsMissing()
    {
        using var culture = TestCulture.UseEnUs();
        var current = new CadPadSet([CreatePad(2)]);
        var edit = new CadEditState();
        edit.DuplicateCadPadIdToCanonicalId[2] = 99;
        edit.AutoHiddenDuplicateCadPadIds.Add(2);

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, new CombinedCadState(), static _ => false, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Duplicate, entry.Kind);
        Assert.Equal(2, entry.CadPadId);
        Assert.Equal(99, entry.FocusCadPadId);
        Assert.Equal("Auto-hidden duplicate", entry.StatusText);
        Assert.Equal("Layer A", entry.CurrentLayerText);
        Assert.Equal("Exact duplicate", entry.ListDetailText);
        Assert.Equal("Exact same-layer duplicate pad detected during DXF load.", entry.DetailText);
        Assert.True(entry.CanFocus);
        Assert.True(entry.CanApply);
    }

    [Theory]
    [InlineData(7, "Group 7 · 2 src", "Combine group 7 output. Source pads: 2.", true)]
    [InlineData(0, "Synthetic pad", "Synthetic CAD pad generated by combine.", false)]
    public void BuildListsCombinedOutputWithOrWithoutGroup(
        int groupId, string expectedListDetail, string expectedDetail, bool expectedCanApply)
    {
        using var culture = TestCulture.UseEnUs();
        var current = new CadPadSet([CreatePad(10)]);
        var combined = new CombinedCadState();
        combined.GroupIdByOutputCadId[10] = groupId;
        combined.GroupIdByOutputCadId[99] = 7;
        if (groupId > 0)
        {
            combined.Groups[7] = new CombinedCadGroupState([1, 2], [10]);
        }

        var entries = DxfEditChangeEntryBuilder.Build(
            current, new CadEditState(), combined, static _ => true, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Combined, entry.Kind);
        Assert.Equal(10, entry.CadPadId);
        Assert.Equal(10, entry.FocusCadPadId);
        Assert.Equal("Pad 10", entry.CadName);
        Assert.Equal("Synthetic", entry.StatusText);
        Assert.Equal("Layer A", entry.CurrentLayerText);
        Assert.Equal(expectedListDetail, entry.ListDetailText);
        Assert.Equal(expectedDetail, entry.DetailText);
        Assert.Equal(IconGlyphs.Remove, entry.ActionGlyph);
        Assert.Equal("Clear group", entry.ActionLabel);
        Assert.Equal("Clear this combine group and restore its source pads.", entry.ActionHint);
        Assert.True(entry.CanFocus);
        Assert.Equal(expectedCanApply, entry.CanApply);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildListsLayerMoveIgnoresCaseOnlyChangeAndUsesVisibilityDelegate(bool hidden)
    {
        using var culture = TestCulture.UseEnUs();
        var current = new CadPadSet([CreatePad(1, "B"), CreatePad(2, "a")]);
        var edit = new CadEditState { BaselineCad = new CadPadSet([CreatePad(1), CreatePad(2)]) };

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, new CombinedCadState(), id => id == 1 && hidden, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Moved, entry.Kind);
        Assert.Equal(1, entry.CadPadId);
        Assert.Equal(1, entry.FocusCadPadId);
        Assert.Equal("Pad 1", entry.CadName);
        Assert.Equal("Moved", entry.StatusText);
        Assert.Equal("Layer B", entry.CurrentLayerText);
        Assert.Equal("A -> B", entry.ListDetailText);
        Assert.Equal("Original layer: A -> Current layer: B.", entry.DetailText);
        Assert.Equal(IconGlyphs.Undo, entry.ActionGlyph);
        Assert.Equal("Restore layer", entry.ActionLabel);
        Assert.Equal("Move this CAD pad back to layer 'A'.", entry.ActionHint);
        Assert.Equal(!hidden, entry.CanFocus);
        Assert.True(entry.CanApply);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildListsRotatedPadSkipsEquivalentAndNewPadsAndUsesVisibilityDelegate(bool hidden)
    {
        using var culture = TestCulture.UseEnUs();
        var current = new CadPadSet([CreatePad(1, width: 1, height: 2), CreatePad(2), CreatePad(3)]);
        var edit = new CadEditState { BaselineCad = new CadPadSet([CreatePad(1), CreatePad(2), CreatePad(4)]) };

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, new CombinedCadState(), id => id == 1 && hidden, PolygonsEquivalent);

        var entry = Assert.Single(entries);
        Assert.Equal(DxfEditChangeKind.Rotated, entry.Kind);
        Assert.Equal(1, entry.CadPadId);
        Assert.Equal(1, entry.FocusCadPadId);
        Assert.Equal("Pad 1", entry.CadName);
        Assert.Equal("Rotated", entry.StatusText);
        Assert.Equal("Layer A", entry.CurrentLayerText);
        Assert.Equal("Geometry differs from DXF baseline", entry.ListDetailText);
        Assert.Equal(
            "Polygon vertices were rotated relative to the imported DXF baseline. Restore resets this pad geometry only.",
            entry.DetailText);
        Assert.Equal(IconGlyphs.Undo, entry.ActionGlyph);
        Assert.Equal("Restore geometry", entry.ActionLabel);
        Assert.Equal("Restore this CAD pad geometry back to DXF baseline.", entry.ActionHint);
        Assert.Equal(!hidden, entry.CanFocus);
        Assert.True(entry.CanApply);
    }

    [Fact]
    public void BuildOrdersSectionsHiddenDuplicateCombinedMovedRotatedAndIdsAscendingWithinEachSection()
    {
        var current = new CadPadSet(
        [
            CreatePad(50), CreatePad(49), CreatePad(40), CreatePad(39), CreatePad(30), CreatePad(29),
            CreatePad(20, "B"), CreatePad(19, "B"),
            CreatePad(10, width: 1, height: 2), CreatePad(9, width: 1, height: 2),
        ]);
        var edit = new CadEditState
        {
            BaselineCad = new CadPadSet([CreatePad(20), CreatePad(19), CreatePad(10), CreatePad(9)]),
        };
        edit.HiddenCadPadIds.UnionWith([50, 49]);
        edit.DuplicateCadPadIdToCanonicalId[40] = 1;
        edit.DuplicateCadPadIdToCanonicalId[39] = 1;
        var combined = new CombinedCadState();
        combined.GroupIdByOutputCadId[30] = 0;
        combined.GroupIdByOutputCadId[29] = 0;
        (DxfEditChangeKind Kind, int Id)[] expected =
        [
            (DxfEditChangeKind.Hidden, 49), (DxfEditChangeKind.Hidden, 50),
            (DxfEditChangeKind.Duplicate, 39), (DxfEditChangeKind.Duplicate, 40),
            (DxfEditChangeKind.Combined, 29), (DxfEditChangeKind.Combined, 30),
            (DxfEditChangeKind.Moved, 19), (DxfEditChangeKind.Moved, 20),
            (DxfEditChangeKind.Rotated, 9), (DxfEditChangeKind.Rotated, 10),
        ];

        var entries = DxfEditChangeEntryBuilder.Build(
            current, edit, combined, static _ => false, PolygonsEquivalent);

        Assert.Equal(expected, entries.Select(static entry => (entry.Kind, entry.CadPadId)));
    }

    private static CadPad CreatePad(int id, string layer = "A", double width = 2, double height = 1)
        => new(id, $"Pad {id}", layer, new Polygon2(
        [
            new Point2(0, 0), new Point2(width, 0), new Point2(width, height), new Point2(0, height),
        ]));

    private static bool PolygonsEquivalent(Polygon2 left, Polygon2 right)
        => left.Vertices.SequenceEqual(right.Vertices);
}
