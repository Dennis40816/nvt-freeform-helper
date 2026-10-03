using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportSelectionWindowSmokeTests
{
    private static readonly int[] SmokeRowValues = [12, 100, 65535, 0, 65535, 0, 0];

    [AvaloniaFact]
    public void NotchExportSelectionWindow_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 3456,
                cadPadId: 7890,
                values: SmokeRowValues,
                comment: "smoke")
        });

        var window = new NotchExportSelectionWindow
        {
            DataContext = new NotchExportSelectionViewModel(table)
        };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);

        var size = new Size(1200, 800);
        window.Measure(size);
        window.Arrange(new Rect(size));

        Assert.NotNull(window);
    }
}
