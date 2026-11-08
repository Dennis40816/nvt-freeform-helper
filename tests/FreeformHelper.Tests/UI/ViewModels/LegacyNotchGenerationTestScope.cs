// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

internal sealed class LegacyNotchGenerationTestScope : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
    private readonly string _dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(("signal", 0, 0, 20, 10));

    public async Task<FreeformHelperViewModel> CreateAsync()
    {
        var projectPath = Path.Combine(_directory.FullName, "legacy.json");
        await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile
        {
            Settings = new ProjectSettings
            {
                Notch = new NotchSettings
                {
                    ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                    EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                    LinkVersionThresholds = false,
                    ThresholdPercentV22 = 0,
                },
            },
        }, TestContext.Current.CancellationToken);

        return await FreeformHelperViewModelTests.CreateNotchExportCacheViewModelAsync(_dxfPath, projectPath);
    }

    public void Dispose()
    {
        File.Delete(_dxfPath);
        _directory.Delete(recursive: true);
    }
}
