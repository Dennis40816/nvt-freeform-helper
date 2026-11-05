using System.Runtime.CompilerServices;
using Avalonia.Interactivity;
using FreeformHelper.UI.Views.SettingsSections;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private click handlers of <see cref="SettingsGeneralSectionView"/>.</summary>
internal static class SettingsGeneralSectionViewPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EditCascadeDetailsButton_Click")]
    public static extern void EditCascadeDetailsButton_Click(SettingsGeneralSectionView view, object? sender, RoutedEventArgs e);
}
