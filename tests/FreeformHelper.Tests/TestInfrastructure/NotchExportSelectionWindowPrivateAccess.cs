using System.Runtime.CompilerServices;
using FreeformHelper.UI.Views;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private close handler of <see cref="NotchExportSelectionWindow"/>.</summary>
internal static class NotchExportSelectionWindowPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RequestClose")]
    public static extern void RequestClose(NotchExportSelectionWindow window, bool confirmed);
}
