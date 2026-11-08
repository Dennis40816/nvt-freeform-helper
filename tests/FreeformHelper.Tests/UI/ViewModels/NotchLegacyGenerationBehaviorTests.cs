// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchLegacyGenerationBehaviorTests
{
    [AvaloniaFact]
    public async Task CreateSimulationSession_RepeatedLegacyRequest_ReusesTableAndPreservesPreviewRowsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var projectPath = TestFiles.NewTempFilePath("legacy-generation-", ".json");
        try
        {
            await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile
            {
                Settings = new ProjectSettings
                {
                    Notch = new NotchSettings
                    {
                        ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                        EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                        LinkVersionThresholds = false,
                        LenScale = 7,
                        NullValue = 54321,
                    },
                },
            }, TestContext.Current.CancellationToken);
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath, projectPath);
            var first = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);
            Assert.NotNull(first);
            Assert.Equal(NotchComputationMode.LegacyRegularAnchor, first.ComputationMode);
            Assert.NotEmpty(first.Table.Rows);
            Assert.All(first.Table.Rows, static row => Assert.Equal(NotchAlgorithmVersion.V22, row.Version));
            var originalRows = vm.RegularPads
                .SelectMany(pad => vm.GetLastNotchRowsForRegularPad(pad.RegularPadId)).ToArray();
            Assert.NotEmpty(originalRows);
            Assert.All(originalRows, static row => Assert.NotEmpty(row.ValuesText));
            var originalMetrics = vm.GetNotchExportGenerationCacheMetrics();

            var repeated = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);

            Assert.NotNull(repeated);
            Assert.Equal(first.Table.Rows.Count, repeated.Table.Rows.Count);
            Assert.Equal(originalRows, vm.RegularPads
                .SelectMany(pad => vm.GetLastNotchRowsForRegularPad(pad.RegularPadId)).ToArray());
            var repeatedMetrics = vm.GetNotchExportGenerationCacheMetrics();
            Assert.Equal(originalMetrics.HitCount + 1, repeatedMetrics.HitCount);
            Assert.Equal(originalMetrics.StoreCount, repeatedMetrics.StoreCount);
            Assert.Equal(originalMetrics.MissCount, repeatedMetrics.MissCount);
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(projectPath);
        }
    }
}
