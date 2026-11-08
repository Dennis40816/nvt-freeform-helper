// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchValidationSelectedRegularBehaviorTests
{
    [AvaloniaFact]
    public async Task UseSelectedRegularForNotchValidationCommand_SelectedSecondReg_AnalyzesItsGeneratedRows()
    {
        using var culture = TestCulture.UseEnUs();
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = TestFiles.NewTempFilePath("notch-selected-reg-", ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            vm.NotchPreviewAutoPlayEnabled = false;
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            var selectedPad = vm.RegularPads[1];
            vm.NotchValidationRegularPadId = int.MaxValue;
            vm.ApplyCanvasSelection([], [selectedPad.RegularPadId]);

            vm.UseSelectedRegularForNotchValidationCommand.Execute(null);

            Assert.Equal((decimal)selectedPad.RegularPadId, vm.NotchValidationRegularPadId);
            Assert.Equal(selectedPad.RegularPadId, Assert.Single(vm.SelectedRegularPadIndices));
            Assert.NotEmpty(vm.NotchValidationItems);
            Assert.NotEmpty(vm.NotchValidationDirectItems);
            Assert.All(vm.NotchValidationDirectItems,
                item => Assert.Equal(selectedPad.RegularPadId, item.SourceRegularPadId));
            Assert.StartsWith($"REG {selectedPad.RegularPadId} | IC1/diff{selectedPad.DiffIndex} | rows=",
                vm.NotchValidationSummaryText, StringComparison.Ordinal);
            Assert.Equal(
                $"Validation updated: REG {selectedPad.RegularPadId}, rows={vm.NotchValidationDirectCount}, " +
                $"in={vm.NotchValidationIncomingCount}, out={vm.NotchValidationOutgoingCount}.",
                vm.StatusText);
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }
}
