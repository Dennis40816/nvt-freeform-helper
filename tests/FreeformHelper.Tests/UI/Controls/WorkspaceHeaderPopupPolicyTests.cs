// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class WorkspaceHeaderPopupPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    public void PopupIsOpenWhenPinnedOrHovered(bool pinned, bool buttonHovered, bool popupHovered, bool expected)
    {
        Assert.Equal(expected, WorkspaceHeaderPopupPolicy.ShouldBeOpen(pinned, buttonHovered, popupHovered));
    }

    [Fact]
    public void MaxSizeLeavesAMarginToTheWindow()
    {
        Assert.Equal(new Size(970, 650), WorkspaceHeaderPopupPolicy.MaxSize(new Size(1000, 680)));
    }

    [Fact]
    public void MaxSizeNeverGoesBelowTheMinimum()
    {
        Assert.Equal(new Size(260, 200), WorkspaceHeaderPopupPolicy.MaxSize(new Size(100, 90)));
    }

    [Fact]
    public void MaxSizeKeepsTheMinimumAtItsExactEdge()
    {
        Assert.Equal(new Size(260, 200), WorkspaceHeaderPopupPolicy.MaxSize(new Size(290, 230)));
        Assert.Equal(new Size(261, 201), WorkspaceHeaderPopupPolicy.MaxSize(new Size(291, 231)));
    }
}
