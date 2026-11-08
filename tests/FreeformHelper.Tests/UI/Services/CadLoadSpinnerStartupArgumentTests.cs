// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadSpinnerStartupArgumentTests
{
    [Fact]
    public void TryInitializeFromArgs_IncompleteSpinnerLaunch_DoesNotEnterSpinnerMode()
    {
        string[][] incompleteLaunches =
        [
            [],
            ["--parent-pid", "222", "--pipe-name", "spinner.test"],
            ["--cad-load-spinner", "--pipe-name", "spinner.test"],
            ["--cad-load-spinner", "--parent-pid"],
            ["--cad-load-spinner", "--parent-pid", "not-a-pid", "--pipe-name", "spinner.test"],
            ["--cad-load-spinner", "--parent-pid", "222"],
            ["--cad-load-spinner", "--parent-pid", "222", "--pipe-name"],
            ["--cad-load-spinner", "--parent-pid", "222", "--pipe-name", "   "],
        ];

        foreach (var args in incompleteLaunches)
        {
            Assert.False(CadLoadSpinnerStartupContext.TryInitializeFromArgs(args));
            Assert.False(CadLoadSpinnerStartupContext.IsSpinnerMode);
        }
    }
}
