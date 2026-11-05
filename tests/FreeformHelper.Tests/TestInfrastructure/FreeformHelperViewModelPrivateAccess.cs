using System.Runtime.CompilerServices;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Lifecycle;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to private state of <see cref="FreeformHelperViewModel"/>.</summary>
internal static class FreeformHelperViewModelPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cad")]
    public static extern ref CadPadSet? Cad(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_layerCatalogStateService")]
    public static extern ref LayerCatalogStateService LayerCatalogState(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_grid")]
    public static extern ref RegularGrid? Grid(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_projectFile")]
    public static extern ref ProjectFile ProjectFile(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cadEdit")]
    public static extern ref CadEditState CadEdit(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_undoService")]
    public static extern ref UndoService UndoService(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ClearMappingOverrideFromReport")]
    public static extern bool ClearMappingOverrideFromReport(FreeformHelperViewModel viewModel, int cadId);
}
