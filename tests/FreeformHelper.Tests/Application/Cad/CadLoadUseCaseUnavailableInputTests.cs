// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadUseCaseUnavailableInputTests
{
    [Fact]
    public void TryLoadFromPath_WithoutAnAvailableFile_ReturnsNoOutcome()
    {
        var loader = new CadLoadUseCase(new DxfImportService());
        var options = new DxfImportOptions();
        string?[] unavailablePaths = [null, string.Empty, "   ", TestFiles.NewTempFilePath("absent-cad-", ".dxf")];

        foreach (var path in unavailablePaths)
        {
            Assert.Null(loader.TryLoadFromPath(path, options));
        }
    }

    [Fact]
    public void TryLoadFromProject_CorruptEmbeddedCopyWithoutAPath_PropagatesTheImportFailure()
    {
        var loader = new CadLoadUseCase(new DxfImportService());
        var corruptDxf = Encoding.UTF8.GetBytes(
            "0\nSECTION\n2\nENTITIES\n0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\nnot-a-number\n20\n0\n0\nENDSEC\n0\nEOF\n");

        Assert.Throws<InvalidDataException>(
            () => loader.TryLoadFromProject(null, corruptDxf, new DxfImportOptions()));
    }
}
