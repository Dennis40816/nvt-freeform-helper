using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Typed access to the process-wide UI-thread slot of Avalonia's <see cref="Dispatcher"/>.</summary>
internal static class DispatcherPrivateAccess
{
    // Static kind: the first parameter only names the owner type; its value is ignored, so callers pass null.
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_uiThread")]
    public static extern ref Dispatcher? UiThreadSlot(Dispatcher? owner);
}
