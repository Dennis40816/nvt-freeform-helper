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
}
