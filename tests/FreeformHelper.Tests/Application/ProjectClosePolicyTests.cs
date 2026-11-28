// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Verifies when project state requires a save prompt before closing.
/// </summary>
public sealed class ProjectClosePolicyTests
{
    [Theory]
    [InlineData(false, false, null, false)]
    [InlineData(false, true, null, true)]
    [InlineData(false, true, "", true)]
    [InlineData(false, true, " \t\r\n", true)]
    [InlineData(false, true, "project.json", false)]
    [InlineData(true, false, null, true)]
    [InlineData(true, true, "project.json", true)]
    public void ShouldPromptSaveUsesDirtyStateAndWhetherLoadedCadHasBeenSaved(
        bool hasUnsavedChanges,
        bool hasLoadedCad,
        string? lastSavedPath,
        bool expected)
    {
        var result = ProjectClosePolicy.ShouldPromptSave(hasUnsavedChanges, hasLoadedCad, lastSavedPath);

        Assert.Equal(expected, result);
    }
}
