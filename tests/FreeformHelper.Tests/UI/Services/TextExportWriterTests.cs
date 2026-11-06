// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class TextExportWriterTests
{
    private const string ExportText = "Export 測試 € 🚀\r\n";

    [Fact]
    public async Task WriteAsyncWithoutBomWritesOnlyUtf8Text()
    {
        string path = TestFiles.NewTempFilePath("text-export-no-bom-", ".txt");

        await TextExportWriter.WriteAsync(path, ExportText, includeBom: false, TestContext.Current.CancellationToken);

        byte[] bytes = TestFiles.ReadBytes(path);
        Assert.False(bytes.AsSpan().StartsWith("\uFEFF"u8));
        Assert.Equal(Encoding.UTF8.GetBytes(ExportText), bytes);
    }

    [Fact]
    public async Task WriteAsyncWithBomPrependsUtf8Preamble()
    {
        string path = TestFiles.NewTempFilePath("text-export-bom-", ".txt");

        await TextExportWriter.WriteAsync(path, ExportText, includeBom: true, TestContext.Current.CancellationToken);

        byte[] bytes = TestFiles.ReadBytes(path);
        Assert.True(bytes.AsSpan().StartsWith("\uFEFF"u8));
        Assert.Equal(Encoding.UTF8.GetBytes(ExportText), bytes["\uFEFF"u8.Length..]);
    }

    [Fact]
    public async Task WriteAsyncReplacesExistingTarget()
    {
        string path = TestFiles.NewTempFilePath("text-export-replace-", ".txt");
        TestFiles.WriteText(path, "Existing content that is longer than the replacement text.");

        await TextExportWriter.WriteAsync(path, ExportText, includeBom: false, TestContext.Current.CancellationToken);

        Assert.Equal(Encoding.UTF8.GetBytes(ExportText), TestFiles.ReadBytes(path));
    }

    [Fact]
    public async Task WriteAsyncWithMissingFolderFailsAndCreatesNothing()
    {
        string folder = TestFiles.NewTempFilePath("text-export-missing-", string.Empty);
        string path = Path.Combine(folder, "out.txt");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            TextExportWriter.WriteAsync(path, ExportText, includeBom: false, TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task WriteAsyncWithDirectoryTargetFailsNamingTheTargetAndKeepsTheDirectory()
    {
        string path = TestFiles.NewTempFilePath("text-export-dir-", ".txt");
        Directory.CreateDirectory(path);

        Exception error = await Assert.ThrowsAnyAsync<Exception>(() =>
            TextExportWriter.WriteAsync(path, ExportText, includeBom: false, TestContext.Current.CancellationToken));

        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Contains(Path.GetFileName(path), error.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(path));
    }
}
