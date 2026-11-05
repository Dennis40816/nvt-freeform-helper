using System.Runtime.CompilerServices;
using Avalonia.Interactivity;
using FreeformHelper.UI.Views;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private click handlers of <see cref="DxfOverlapReportWindow"/>.</summary>
internal static class DxfOverlapReportWindowPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "CopyAll_Click")]
    public static extern void CopyAll_Click(DxfOverlapReportWindow window, object? sender, RoutedEventArgs e);
}
