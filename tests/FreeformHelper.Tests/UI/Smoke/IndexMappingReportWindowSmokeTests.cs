using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class IndexMappingReportWindowSmokeTests
{
    [AvaloniaFact]
    public void IndexMappingReportWindow_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var issue = new DxfRegularMappingIssue(
            DxfRegularMappingIssueKind.LowConfidence,
            "Low confidence",
            DxfIndex: 42,
            CadPadId: 142,
            RegularPadIndex: 77,
            DiffIndex: 77);
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 1,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 1,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[] { issue },
            SampleIssues: new[] { issue });

        var window = new IndexMappingReportWindow
        {
            DataContext = new IndexMappingReportViewModel("summary", report)
        };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);

        var size = new Size(1320, 860);
        window.Measure(size);
        window.Arrange(new Rect(size));

        Assert.NotNull(window);
    }
}
