// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchFreeformEditExportBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(FreeformType.None)]
    [InlineData(FreeformType.XWay)]
    [InlineData(FreeformType.XYWay)]
    public async Task ManualTagAfterExport_ClearsDownstreamAndExportsEditedRowsLikeFreshProjectAsync(FreeformType type)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        var cadId = NotchDataEditExportTestScope.FirstCadId(vm);
        if (type != FreeformType.None)
        {
            NotchDataEditExportTestScope.Tag(vm, cadId, FreeformType.None);
            await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);
        }
        var before = await files.ExportAsync(vm);
        var initialCount = type == FreeformType.None ? 2 : 1;
        Assert.Equal(initialCount, NotchDataEditExportTestScope.Rows(before).Length);

        NotchDataEditExportTestScope.Tag(vm, cadId, type);

        NotchDataEditExportTestScope.AssertDownstreamCleared(vm, hasMatch: true, hasFreeform: true);
        Assert.True(vm.ExportNotchCommand.CanExecute(null));
        // Step 2 is complete after a manual tag; export must regenerate immediately, without auto-detect.
        var immediate = await files.ExportAsync(vm);
        Assert.Equal($"CSV review notch exported. Rows={3 - initialCount}.", vm.StatusText);
        Assert.NotEqual(before, immediate);
        Assert.Equal(3 - initialCount, NotchDataEditExportTestScope.Rows(immediate).Length);
        var editedRows = NotchDataEditExportTestScope.Rows(immediate)
            .Where(row => row[7] == cadId.ToString(CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(type == FreeformType.None ? 0 : 1, editedRows.Length);

        using var freshFiles = new NotchDataEditExportTestScope();
        var fresh = await freshFiles.CreateAsync();
        var freshCadId = NotchDataEditExportTestScope.FirstCadId(fresh);
        if (type != FreeformType.None)
        {
            NotchDataEditExportTestScope.Tag(fresh, freshCadId, FreeformType.None);
        }
        NotchDataEditExportTestScope.Tag(fresh, freshCadId, type);
        Assert.Equal(immediate, await freshFiles.ExportAsync(fresh));
    }
}
