using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [AvaloniaFact]
    public Task ExportNotchCommand_WhenReleaseChangesToDebug_ReusesGenerationAndFormatsDebugProfile()
    {
        return AssertProfileChangeReusesGenerationAsync(NotchExportProfile.Release, NotchExportProfile.Debug);
    }

    [AvaloniaFact]
    public Task ExportNotchCommand_WhenDebugChangesToRelease_ReusesGenerationAndFormatsReleaseProfile()
    {
        return AssertProfileChangeReusesGenerationAsync(NotchExportProfile.Debug, NotchExportProfile.Release);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_LegacyRegularAnchor_FinalSettingChangesRemainRequestSpecificTableMisses()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var projectPath = TestFiles.NewTempFilePath("notch-legacy-cache-", ".json");
        var outputPath = TestFiles.NewTempFilePath("notch-legacy-cache-", ".c");
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
                        LenScale = 7,
                        NullValue = 54321,
                        LinkVersionThresholds = false,
                    },
                },
            }, CancellationToken.None);

            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath, projectPath);
            var metrics = new List<NotchExportGenerationCacheMetricsSnapshot>();

            var initialPayload = await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            Assert.Contains("//   ComputationMode: LegacyRegularAnchor", initialPayload, StringComparison.Ordinal);
            Assert.Contains("//   Sections: v2.1=empty, v2.2=present", initialPayload, StringComparison.Ordinal);
            metrics.Add(vm.GetNotchExportGenerationCacheMetrics());

            ConfigureNotchVersion(vm, NotchAlgorithmVersion.V21);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            metrics.Add(vm.GetNotchExportGenerationCacheMetrics());

            vm.NotchThresholdQ7 = 1;
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            metrics.Add(vm.GetNotchExportGenerationCacheMetrics());

            vm.LenScale += 1;
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            metrics.Add(vm.GetNotchExportGenerationCacheMetrics());

            vm.NullValue -= 1;
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            metrics.Add(vm.GetNotchExportGenerationCacheMetrics());

            for (var index = 0; index < metrics.Count; index++)
            {
                var snapshot = metrics[index];
                var expectedRequestCount = index + 1;
                Assert.True(snapshot.HasEntry);
                Assert.True(snapshot.EntryRowCount > 0);
                Assert.Equal(0, snapshot.HitCount);
                Assert.Equal(expectedRequestCount, snapshot.MissCount);
                Assert.Equal(expectedRequestCount, snapshot.StoreCount);
                if (index > 0)
                {
                    Assert.NotEqual(metrics[index - 1].SettingsFingerprint, snapshot.SettingsFingerprint);
                }
            }
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(projectPath);
            File.Delete(outputPath);
        }
    }

    [AvaloniaTheory]
    [InlineData(NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22)]
    public Task ExportNotchCommand_WhenVersionChanges_ReusesResolvedBatchAndMatchesFreshProjection(
        NotchAlgorithmVersion warmVersion,
        NotchAlgorithmVersion requestedVersion)
    {
        return AssertVersionChangeReusesResolvedBatchAsync(warmVersion, requestedVersion);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_ThresholdReprojectsButResolutionSettingRebuildsBatch()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var rejectedPath = TestFiles.NewTempFilePath("notch-cache-rejected-", ".c");
        var cachedPath = TestFiles.NewTempFilePath("notch-cache-admitted-", ".c");
        var freshPath = TestFiles.NewTempFilePath("notch-cache-fresh-", ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            ConfigureNotchVersion(vm, NotchAlgorithmVersion.V21);
            vm.NotchThresholdQ7 = 128;

            await ExecuteNotchProfileCommandAsync(vm, NotchExportProfile.Release, rejectedPath);
            var afterRejected = vm.GetNotchExportGenerationCacheMetrics();
            Assert.False(File.Exists(rejectedPath));
            Assert.Contains("no rows generated", vm.StatusText, StringComparison.OrdinalIgnoreCase);

            vm.NotchThresholdQ7 = 0;
            var cachedPayload = await ExportNotchProfileAsync(vm, NotchExportProfile.Release, cachedPath);
            var afterAdmitted = vm.GetNotchExportGenerationCacheMetrics();

            var freshVm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            ConfigureNotchVersion(freshVm, NotchAlgorithmVersion.V21);
            freshVm.NotchThresholdQ7 = 0;
            await ExportNotchProfileAsync(freshVm, NotchExportProfile.Release, freshPath);

            Assert.Contains("//   Sections: v2.1=present, v2.2=empty", cachedPayload, StringComparison.Ordinal);
            Assert.Equal(await TestFiles.ReadBytesAsync(freshPath, TestContext.Current.CancellationToken), await TestFiles.ReadBytesAsync(cachedPath, TestContext.Current.CancellationToken));

            Assert.True(afterRejected.HasEntry);
            Assert.Equal(0, afterRejected.EntryRowCount);
            Assert.Equal(0, afterRejected.HitCount);
            Assert.Equal(1, afterRejected.MissCount);
            Assert.Equal(1, afterRejected.StoreCount);
            Assert.True(afterAdmitted.HasEntry);
            Assert.True(afterAdmitted.EntryRowCount > 0);
            Assert.Equal(1, afterAdmitted.HitCount);
            Assert.Equal(1, afterAdmitted.MissCount);
            Assert.Equal(1, afterAdmitted.StoreCount);

            vm.NullValue -= 1;
            var nullPayload = await ExportNotchProfileAsync(vm, NotchExportProfile.Release, cachedPath);
            var afterNullProjection = vm.GetNotchExportGenerationCacheMetrics();
            var nullFreshVm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            ConfigureNotchVersion(nullFreshVm, NotchAlgorithmVersion.V21);
            nullFreshVm.NotchThresholdQ7 = 0;
            nullFreshVm.NullValue = vm.NullValue;
            await ExportNotchProfileAsync(nullFreshVm, NotchExportProfile.Release, freshPath);

            Assert.NotEqual(cachedPayload, nullPayload);
            Assert.Equal(await TestFiles.ReadBytesAsync(freshPath, TestContext.Current.CancellationToken), await TestFiles.ReadBytesAsync(cachedPath, TestContext.Current.CancellationToken));
            Assert.Equal(2, afterNullProjection.HitCount);
            Assert.Equal(1, afterNullProjection.MissCount);
            Assert.Equal(1, afterNullProjection.StoreCount);
            Assert.Equal(afterAdmitted.SettingsFingerprint, afterNullProjection.SettingsFingerprint);

            vm.SelectedNotchCompensationModelOption = Assert.Single(
                vm.NotchCompensationModelOptions,
                static option => option.Value == NotchCompensationModel.ConservativeNoGain);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, cachedPath);
            var afterResolutionChange = vm.GetNotchExportGenerationCacheMetrics();
            Assert.Equal(2, afterResolutionChange.HitCount);
            Assert.Equal(2, afterResolutionChange.MissCount);
            Assert.Equal(2, afterResolutionChange.StoreCount);
            Assert.NotEqual(afterAdmitted.SettingsFingerprint, afterResolutionChange.SettingsFingerprint);
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(rejectedPath);
            File.Delete(cachedPath);
            File.Delete(freshPath);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportNotchCommand_TargetCoverageRequestReusesResolvedBatchAndMatchesFreshProjection(
        bool changeGuard)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var warmPath = TestFiles.NewTempFilePath("notch-target-warm-", ".c");
        var cachedPath = TestFiles.NewTempFilePath("notch-target-cached-", ".c");
        var freshPath = TestFiles.NewTempFilePath("notch-target-fresh-", ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            vm.EnableTargetCoverageGuard = !changeGuard;
            vm.TargetCoverageCapPercent = 50m;
            var cad = Assert.Single(vm.CadPads);
            var revision = vm.GetNotchStep3Revision();
            var simulationRevision = vm.SimulationWorkspaceSourceRevision;
            var resolved = vm.GetCadV22ResolvedResult(cad.Id);
            Assert.NotNull(resolved);

            var warmPayload = await ExportNotchProfileAsync(vm, NotchExportProfile.Release, warmPath);
            var afterWarm = vm.GetNotchExportGenerationCacheMetrics();
            var regularPadId = vm.RegularPads[0].RegularPadId;
            Assert.NotEqual("Notch export: not run.", vm.NotchExportSummary);
            Assert.True(vm.TryGetNotchValidationReportForRegularPad(
                regularPadId,
                out _,
                out _,
                out _,
                out _));

            if (changeGuard)
            {
                vm.EnableTargetCoverageGuard = true;
            }
            else
            {
                vm.TargetCoverageCapPercent = 255m;
            }

            var afterRequestChange = vm.GetNotchExportGenerationCacheMetrics();
            Assert.Equal(revision, vm.GetNotchStep3Revision());
            Assert.Equal(simulationRevision + 1, vm.SimulationWorkspaceSourceRevision);
            Assert.Same(resolved, vm.GetCadV22ResolvedResult(cad.Id));
            Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
            Assert.False(vm.TryGetNotchValidationReportForRegularPad(
                regularPadId,
                out _,
                out _,
                out var validationFailureCode,
                out _));
            Assert.Equal("NOT_READY", validationFailureCode);
            Assert.True(afterRequestChange.HasEntry);
            Assert.Equal(afterWarm.ClearCount, afterRequestChange.ClearCount);

            var cachedPayload = await ExportNotchProfileAsync(vm, NotchExportProfile.Release, cachedPath);
            var afterProjection = vm.GetNotchExportGenerationCacheMetrics();
            var freshVm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            freshVm.EnableTargetCoverageGuard = true;
            freshVm.TargetCoverageCapPercent = changeGuard ? 50m : 255m;
            await ExportNotchProfileAsync(freshVm, NotchExportProfile.Release, freshPath);

            Assert.NotEqual(warmPayload, cachedPayload);
            Assert.Equal(await TestFiles.ReadBytesAsync(freshPath, TestContext.Current.CancellationToken), await TestFiles.ReadBytesAsync(cachedPath, TestContext.Current.CancellationToken));
            Assert.Equal(afterWarm.HitCount + 1, afterProjection.HitCount);
            Assert.Equal(afterWarm.MissCount, afterProjection.MissCount);
            Assert.Equal(afterWarm.StoreCount, afterProjection.StoreCount);
            Assert.Equal(afterWarm.ClearCount, afterProjection.ClearCount);
            Assert.Equal(afterWarm.SettingsFingerprint, afterProjection.SettingsFingerprint);
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(warmPath);
            File.Delete(cachedPath);
            File.Delete(freshPath);
        }
    }

    private static async Task AssertVersionChangeReusesResolvedBatchAsync(
        NotchAlgorithmVersion warmVersion,
        NotchAlgorithmVersion requestedVersion)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var cachedPath = TestFiles.NewTempFilePath("notch-version-cached-", ".c");
        var freshPath = TestFiles.NewTempFilePath("notch-version-fresh-", ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            ConfigureNotchVersion(vm, warmVersion);
            var warmPayload = await ExportNotchProfileAsync(vm, NotchExportProfile.Release, cachedPath);
            var afterWarm = vm.GetNotchExportGenerationCacheMetrics();

            var warmProjectionProgress = new System.Collections.Concurrent.ConcurrentQueue<string>();
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FreeformHelperViewModel.NotchExportProgressText))
                {
                    warmProjectionProgress.Enqueue(vm.NotchExportProgressText);
                }
            };

            ConfigureNotchVersion(vm, requestedVersion);
            var cachedPayload = await ExportNotchProfileAsync(vm, NotchExportProfile.Release, cachedPath);
            var afterVersionChange = vm.GetNotchExportGenerationCacheMetrics();

            var freshVm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            ConfigureNotchVersion(freshVm, requestedVersion);
            await ExportNotchProfileAsync(freshVm, NotchExportProfile.Release, freshPath);

            AssertVersionSections(cachedPayload, requestedVersion);
            Assert.Equal(await TestFiles.ReadBytesAsync(freshPath, TestContext.Current.CancellationToken), await TestFiles.ReadBytesAsync(cachedPath, TestContext.Current.CancellationToken));
            Assert.NotEqual(warmPayload, cachedPayload);
            Assert.True(afterVersionChange.HasEntry);
            Assert.True(afterVersionChange.EntryRowCount > 0);
            Assert.Equal(1, afterVersionChange.HitCount);
            Assert.Equal(1, afterVersionChange.MissCount);
            Assert.Equal(1, afterVersionChange.StoreCount);
            Assert.Equal(afterWarm.SettingsFingerprint, afterVersionChange.SettingsFingerprint);
            Assert.Contains(
                warmProjectionProgress,
                static message => message.Contains("Finalize canonical exports", StringComparison.Ordinal));
            Assert.DoesNotContain(
                warmProjectionProgress,
                static message => message.Contains("Build allocation profiles", StringComparison.Ordinal) ||
                                  message.Contains("Build canonical candidates", StringComparison.Ordinal) ||
                                  message.Contains("Merge candidates by diff", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(cachedPath);
            File.Delete(freshPath);
        }
    }

    private static async Task AssertProfileChangeReusesGenerationAsync(
        NotchExportProfile warmProfile,
        NotchExportProfile requestedProfile)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        var warmPath = TestFiles.NewTempFilePath("notch-profile-warm-", ".c");
        var cachedPath = TestFiles.NewTempFilePath("notch-profile-cached-", ".c");
        var freshPath = TestFiles.NewTempFilePath("notch-profile-fresh-", ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            var warmPayload = await ExportNotchProfileAsync(vm, warmProfile, warmPath);
            var afterWarm = vm.GetNotchExportGenerationCacheMetrics();

            Assert.True(afterWarm.HasEntry);
            Assert.True(afterWarm.EntryRowCount > 0);
            Assert.Equal(0, afterWarm.HitCount);
            Assert.Equal(1, afterWarm.MissCount);
            Assert.Equal(1, afterWarm.StoreCount);

            var cachedPayload = await ExportNotchProfileAsync(vm, requestedProfile, cachedPath);
            var afterProfileChange = vm.GetNotchExportGenerationCacheMetrics();
            var freshVm = await CreateNotchExportCacheViewModelAsync(dxfPath);
            await ExportNotchProfileAsync(freshVm, requestedProfile, freshPath);

            Assert.Equal(1, afterProfileChange.HitCount);
            Assert.Equal(1, afterProfileChange.MissCount);
            Assert.Equal(1, afterProfileChange.StoreCount);
            Assert.Equal(afterWarm.SettingsFingerprint, afterProfileChange.SettingsFingerprint);
            Assert.Equal(await TestFiles.ReadBytesAsync(freshPath, TestContext.Current.CancellationToken), await TestFiles.ReadBytesAsync(cachedPath, TestContext.Current.CancellationToken));
            Assert.NotEqual(warmPayload, cachedPayload);
            Assert.Contains($"//   ExportProfile: {requestedProfile}", cachedPayload, StringComparison.Ordinal);
            Assert.DoesNotContain("FwBaseMask", cachedPayload, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(warmPath);
            File.Delete(cachedPath);
            File.Delete(freshPath);
        }
    }

    internal static async Task<FreeformHelperViewModel> CreateNotchExportCacheViewModelAsync(
        string dxfPath,
        string? projectPath = null)
    {
        var vm = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
        };

        if (projectPath is not null)
        {
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            await vm.LoadProjectCommand.ExecuteAsync(null);
        }

        vm.ActiveAreaWidth = 20;
        vm.ActiveAreaHeight = 10;
        vm.GridPaddingPercent = 0;

        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        await WaitForGridRebuildAsync(vm);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.RegularPads.Count);
        Assert.All(vm.RegularPads, static pad => Assert.Equal(FreeformType.XWay, pad.Freeform));
        ConfigureNotchVersion(vm, NotchAlgorithmVersion.V22);
        return vm;
    }

    private static void ConfigureNotchVersion(
        FreeformHelperViewModel vm,
        NotchAlgorithmVersion version)
    {
        vm.EnableV21 = version == NotchAlgorithmVersion.V21;
        vm.EnableV22 = version == NotchAlgorithmVersion.V22;
        var fileType = version switch
        {
            NotchAlgorithmVersion.V21 => FreeformHelperViewModel.NotchExportFileType.Cv21,
            NotchAlgorithmVersion.V22 => FreeformHelperViewModel.NotchExportFileType.Cv22,
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported notch version."),
        };
        vm.SelectedNotchExportFileTypeOption = Assert.Single(
            vm.NotchExportFileTypeOptions,
            option => option.Value == fileType);
    }

    private static void AssertVersionSections(string payload, NotchAlgorithmVersion version)
    {
        var expected = version switch
        {
            NotchAlgorithmVersion.V21 => "//   Sections: v2.1=present, v2.2=empty",
            NotchAlgorithmVersion.V22 => "//   Sections: v2.1=empty, v2.2=present",
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported notch version."),
        };
        Assert.Contains(expected, payload, StringComparison.Ordinal);
    }

    internal static async Task<string> ExportNotchProfileAsync(
        FreeformHelperViewModel vm,
        NotchExportProfile profile,
        string outputPath)
    {
        await ExecuteNotchProfileCommandAsync(vm, profile, outputPath);

        Assert.True(File.Exists(outputPath), vm.StatusText);
        return await TestFiles.ReadContentAsync(outputPath, TestContext.Current.CancellationToken);
    }

    private static async Task ExecuteNotchProfileCommandAsync(
        FreeformHelperViewModel vm,
        NotchExportProfile profile,
        string outputPath)
    {
        vm.SelectedNotchExportProfileOption = Assert.Single(
            vm.NotchExportProfileOptions,
            option => option.Value == profile);
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(outputPath);
        vm.OpenNotchExportSelectionAsync = null;

        await vm.ExportNotchCommand.ExecuteAsync(null);
    }

    private static readonly int[] _suggestedBaseNameV22Payload = [21, 94, 22, 12, 65535, 0, 0];
    private static readonly int[] _suggestedBaseNameV21Payload = [10, 100, 100, 11, 1, 12, 65535, 0, 0];
    private static readonly int[] _suggestedBaseNameMixedV22Payload = [11, 94, 12, 22, 65535, 0, 0];

    [Fact]
    public void ResolveSuggestedNotchExportBaseName_WhenSingleCVersion_UsesVersionLabel()
    {
        var table = new NotchTable(new[]
        {
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 21, 21, 1021, _suggestedBaseNameV22Payload, "v22"),
        });

        var result = FreeformHelperViewModel.ResolveSuggestedNotchExportBaseName("notch_table", "c", table);

        Assert.Equal("notch_v2.2", result);
    }

    [Fact]
    public void ResolveSuggestedNotchExportBaseName_WhenMixedVersionsOrNonC_KeepsRequestedName()
    {
        var mixedTable = new NotchTable(new NotchTableRow[]
        {
            new(NotchAlgorithmVersion.V21, 0, 10, 10, 1010, _suggestedBaseNameV21Payload, "v21"),
            new(NotchAlgorithmVersion.V22, 0, 11, 11, 1011, _suggestedBaseNameMixedV22Payload, "v22"),
        });

        Assert.Equal(
            "notch_table",
            FreeformHelperViewModel.ResolveSuggestedNotchExportBaseName("notch_table", "c", mixedTable));
        Assert.Equal(
            "notch_table",
            FreeformHelperViewModel.ResolveSuggestedNotchExportBaseName("notch_table", "csv", mixedTable));
    }
}
