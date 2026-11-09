// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using FreeformHelper.Application.Export;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchPreviewExportHandoffBehaviorTests
{
    [Fact]
    public void ExportSelection_ChangesHandoffTypeProfileAndVersionAndNotifiesBindings()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel { NotchPreviewAutoPlayEnabled = false };
        vm.SelectedNotchExportFileTypeOption = vm.NotchExportFileTypeOptions.Single(
            static option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        var changed = new ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, args) => changed.Enqueue(args.PropertyName);

        vm.SelectedNotchExportFileTypeOption = vm.NotchExportFileTypeOptions.Single(
            static option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv21);
        vm.SelectedNotchExportProfileOption = vm.NotchExportProfileOptions.Single(
            static option => option.Value == NotchExportProfile.Debug);

        Assert.Equal("C v2.1 (.c)", vm.NotchExportHandoffFileText);
        Assert.Equal("v2.1", vm.NotchExportHandoffVersionText);
        Assert.Equal("Debug (trace + sim mask)", vm.NotchExportHandoffProfileText);
        Assert.Contains(nameof(vm.NotchExportHandoffFileText), changed);
        Assert.Contains(nameof(vm.NotchExportHandoffVersionText), changed);
        Assert.Contains(nameof(vm.NotchExportHandoffProfileText), changed);

        vm.SelectedNotchExportFileTypeOption = vm.NotchExportFileTypeOptions.Single(
            static option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
        vm.SelectedNotchExportProfileOption = vm.NotchExportProfileOptions.Single(
            static option => option.Value == NotchExportProfile.Release);

        Assert.Equal("CSV review (.csv)", vm.NotchExportHandoffFileText);
        Assert.Equal("Review window selection", vm.NotchExportHandoffVersionText);
        Assert.Equal("Release (minimal FW)", vm.NotchExportHandoffProfileText);

        vm.SelectedNotchExportFileTypeOption = vm.NotchExportFileTypeOptions.Single(
            static option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);

        Assert.Equal("C v2.2 (.c)", vm.NotchExportHandoffFileText);
        Assert.Equal("v2.2", vm.NotchExportHandoffVersionText);
    }
}
