using System.Runtime.CompilerServices;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to private state of <see cref="FreeformHelperViewModel"/>.</summary>
internal static class FreeformHelperViewModelPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cad")]
    public static extern ref CadPadSet? Cad(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_layerCatalogStateService")]
    public static extern ref LayerCatalogStateService LayerCatalogState(FreeformHelperViewModel viewModel);
}
