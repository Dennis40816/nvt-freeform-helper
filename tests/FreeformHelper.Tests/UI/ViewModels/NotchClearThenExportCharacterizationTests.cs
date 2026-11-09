// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchClearThenExportCharacterizationTests
{
    [AvaloniaFact]
    public async Task ClearStep3ThenExport_RegeneratesFromPreservedInputsInsteadOfReusingClearedTableAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        var before = await files.ExportAsync(vm);
        var previousTable = Assert.IsType<NotchTable>(vm.NotchCacheForTests.LastGeneratedTable);
        var misses = vm.GetNotchExportGenerationCacheMetrics().MissCount;

        vm.ClearStep3ResultCommand.Execute(null);

        Assert.Null(vm.NotchCacheForTests.LastGeneratedTable);
        Assert.False(vm.GetNotchExportGenerationCacheMetrics().HasEntry);

        var after = await files.ExportAsync(vm);

        var regenerated = Assert.IsType<NotchTable>(vm.NotchCacheForTests.LastGeneratedTable);
        Assert.NotSame(previousTable, regenerated);
        Assert.Equal(misses + 1, vm.GetNotchExportGenerationCacheMetrics().MissCount);
        Assert.Equal(2, regenerated.Rows.Count);
        // Clearing Step 3 keeps matching and Freeform tags, so a fresh export has the same rows.
        Assert.Equal(before, after);
        using var freshFiles = new NotchDataEditExportTestScope();
        var fresh = await freshFiles.CreateAsync();
        Assert.Equal(await freshFiles.ExportAsync(fresh), after);
    }
}
