// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Verifies the empty and cancelled results of file picker path extraction.
/// Avalonia storage items cannot be implemented by user code, so the path cases are covered by the picker wiring only.
/// </summary>
public sealed class StoragePickerPathsTests
{
    [Fact]
    public void FirstLocalPath_EmptyList_ReturnsNull()
    {
        var path = StoragePickerPaths.FirstLocalPath([]);

        Assert.Null(path);
    }

    [Fact]
    public void LocalPath_NullSaveResult_ReturnsNull()
    {
        var path = StoragePickerPaths.LocalPath(null);

        Assert.Null(path);
    }
}
