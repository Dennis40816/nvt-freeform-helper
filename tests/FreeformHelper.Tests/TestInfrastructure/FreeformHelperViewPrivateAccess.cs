using System.Runtime.CompilerServices;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private window slots and handlers of <see cref="FreeformHelperView"/>.</summary>
internal static class FreeformHelperViewPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_notchExportSelectionWindow")]
    public static extern ref NotchExportSelectionWindow? NotchExportSelectionWindow(FreeformHelperView view);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dxfEditChangeListWindow")]
    public static extern ref DxfEditChangeListWindow? DxfEditChangeListWindow(FreeformHelperView view);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_indexMappingReportWindow")]
    public static extern ref IndexMappingReportWindow? IndexMappingReportWindow(FreeformHelperView view);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SaveProjectFromShortcutAsync")]
    public static extern Task SaveProjectFromShortcutAsync(FreeformHelperView view, FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ShowIndexMappingReportWindowAsync")]
    public static extern Task ShowIndexMappingReportWindowAsync(FreeformHelperView view, IndexMappingReportViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ShowNotchExportSelectionWindowAsync")]
    public static extern Task<NotchTable?> ShowNotchExportSelectionWindowAsync(FreeformHelperView view, NotchExportSelectionViewModel viewModel);
}
