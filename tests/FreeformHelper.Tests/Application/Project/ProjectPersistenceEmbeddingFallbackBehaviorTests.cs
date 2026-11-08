// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ProjectPersistenceEmbeddingFallbackBehaviorTests
{
    [Fact]
    public async Task SaveAsync_UnavailableSources_PreservesEmbeddedBytesAndAssignsDefaultNamesAsync()
    {
        var path = TestFiles.NewTempFilePath("cov77-embedded-fallback-", ".json");
        var dxfBytes = Encoding.UTF8.GetBytes(
            "0\r\nSECTION\r\n2\r\nENTITIES\r\n0\r\nLWPOLYLINE\r\n8\r\nSIGNAL\r\n90\r\n4\r\n70\r\n1\r\n" +
            "10\r\n0\r\n20\r\n0\r\n10\r\n1\r\n20\r\n0\r\n10\r\n1\r\n20\r\n1\r\n10\r\n0\r\n20\r\n1\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n");
        var maskBytes = Encoding.UTF8.GetBytes("Xch:\"2\",Ych:\"1\"\r\ntimestamp(0:0:0),DiffData,10,0\r\n");
        var project = new ProjectFile
        {
            LastDxfPath = TestFiles.NewTempFilePath("cov77-missing-", ".dxf"),
            EmbeddedDxf = dxfBytes,
            EmbeddedRegularVisibilityMask = maskBytes,
        };
        var snapshot = CreateMaskSnapshot(TestFiles.NewTempFilePath("cov77-missing-", ".csv"));
        var useCase = new ProjectPersistenceUseCase(new JsonProjectStore());

        var result = await useCase.SaveAsync(CreateSaveRequest(project, path, snapshot));

        Assert.True(result.IsSaved);
        Assert.False(result.IsCancelled);
        Assert.Equal(path, result.Path);
        Assert.Null(result.EmbedWarning);
        var saved = JsonProjectStore.Load(path);
        Assert.Equal(dxfBytes, saved.EmbeddedDxf);
        Assert.Equal("embedded.dxf", saved.EmbeddedDxfName);
        Assert.Equal(maskBytes, saved.EmbeddedRegularVisibilityMask);
        Assert.Equal("SeeRegular.csv", saved.EmbeddedRegularVisibilityMaskName);
    }

    [Fact]
    public async Task SaveAsync_UnavailableSourcesWithoutEmbeddedData_SavesWithBothWarningsAsync()
    {
        var path = TestFiles.NewTempFilePath("cov77-missing-sources-", ".json");
        var project = new ProjectFile
        {
            LastDxfPath = TestFiles.NewTempFilePath("cov77-missing-", ".dxf"),
        };
        var maskPath = TestFiles.NewTempFilePath("cov77-missing-", ".csv");
        var snapshot = CreateMaskSnapshot(maskPath);
        var useCase = new ProjectPersistenceUseCase(new JsonProjectStore());

        var result = await useCase.SaveAsync(CreateSaveRequest(project, path, snapshot));

        Assert.True(result.IsSaved);
        Assert.False(result.IsCancelled);
        Assert.Equal(path, result.Path);
        Assert.Equal(
            "Embed DXF skipped: source file not available. Embed SeeRegular mask skipped: source file not available.",
            result.EmbedWarning);
        var saved = JsonProjectStore.Load(path);
        Assert.Null(saved.EmbeddedDxf);
        Assert.Null(saved.EmbeddedRegularVisibilityMask);
        Assert.Equal(project.LastDxfPath, saved.LastDxfPath);
        Assert.Equal(maskPath, saved.UiSnapshot.Import.RegularVisibilityMaskSourcePath);
        Assert.True(saved.UiSnapshot.Import.UseRegularVisibilityMask);
    }

    [Fact]
    public async Task SaveAsync_NoDxfSourceAndNoMaskRequested_ReportsOnlyTheDxfWarningAsync()
    {
        var path = TestFiles.NewTempFilePath("cov77-no-mask-", ".json");
        var useCase = new ProjectPersistenceUseCase(new JsonProjectStore());

        var result = await useCase.SaveAsync(CreateSaveRequest(new ProjectFile(), path, new ProjectUiSnapshot()));

        Assert.True(result.IsSaved);
        Assert.False(result.IsCancelled);
        Assert.Equal("Embed DXF skipped: source file not available.", result.EmbedWarning);
        var saved = JsonProjectStore.Load(path);
        Assert.Null(saved.EmbeddedDxf);
        Assert.Null(saved.EmbeddedRegularVisibilityMask);
        Assert.False(saved.UiSnapshot.Import.UseRegularVisibilityMask);
    }

    private static ProjectUiSnapshot CreateMaskSnapshot(string path) => new()
    {
        Import = new UiImportSnapshot
        {
            RegularVisibilityMaskSourcePath = path,
            UseRegularVisibilityMask = true,
        },
    };

    private static ProjectSaveRequest CreateSaveRequest(ProjectFile project, string path, ProjectUiSnapshot snapshot) => new(
        Project: project,
        Grid: null,
        CadPadCustomValues: new Dictionary<int, double>(),
        PickSaveProjectPathAsync: () => Task.FromResult<string?>(path),
        ConfirmEmbedDxfAsync: () => Task.FromResult(true),
        BuildUiSnapshot: () => snapshot,
        ApplyUiToSettings: _ => { },
        HasCadLoaded: true);
}
