// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.UI.Bindings;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowBindingWalkerTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void MainWindowCompositionHasReadableAndWritableBindingPaths()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var documents = BindingDocuments.ReadRepository();

        var report = new AxamlBindingWalker(documents).WalkMainWindow();

        output.WriteLine(report.Format());
        Assert.NotEmpty(documents.Sites);
        Assert.True(report.IsComplete, report.Format());
    }
}
