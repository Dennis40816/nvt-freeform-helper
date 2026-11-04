using System.Runtime.CompilerServices;
using Avalonia;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private view-refresh seams of <see cref="PadCanvas"/>.</summary>
internal static class PadCanvasPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_isViewRefreshQueued")]
    public static extern ref bool IsViewRefreshQueued(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_zoom")]
    public static extern ref double Zoom(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pan")]
    public static extern ref Vector Pan(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RequestViewRefresh")]
    public static extern void RequestViewRefresh(PadCanvas canvas, bool raiseViewChanged);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RequestVisualRefresh")]
    public static extern void RequestVisualRefresh(PadCanvas canvas, bool raiseViewChanged);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InvalidateViewFrameSnapshot")]
    public static extern void InvalidateViewFrameSnapshot(PadCanvas canvas);
}
