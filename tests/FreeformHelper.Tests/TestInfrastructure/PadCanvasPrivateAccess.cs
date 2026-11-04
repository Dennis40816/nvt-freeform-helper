using System.Runtime.CompilerServices;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to the private selection state of <see cref="PadCanvas"/>.</summary>
internal static class PadCanvasPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_selectedCadIds")]
    public static extern ref HashSet<int> SelectedCadIds(PadCanvas canvas);
}
