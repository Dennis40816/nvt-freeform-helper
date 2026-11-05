using System.Runtime.CompilerServices;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Compile-time bound access to private state of <see cref="FreeformHelperViewModel"/>.</summary>
internal static class FreeformHelperViewModelPrivateAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cad")]
    public static extern ref CadPadSet? Cad(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_grid")]
    public static extern ref RegularGrid? Grid(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_projectFile")]
    public static extern ref ProjectFile ProjectFile(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_undoSuppression")]
    public static extern ref UndoSuppression UndoSuppression(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_layerCatalogStateService")]
    public static extern ref LayerCatalogStateService LayerCatalogState(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "UpdateLayerToggles")]
    public static extern void UpdateLayerToggles(FreeformHelperViewModel viewModel, CadPadSet cad);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ResetDxfEditBaseline")]
    public static extern void ResetDxfEditBaseline(FreeformHelperViewModel viewModel, CadPadSet? cad);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ApplyCadLoadOutcomeAsync")]
    public static extern Task ApplyCadLoadOutcomeAsync(FreeformHelperViewModel viewModel, CadLoadOutcome outcome, UiViewSnapshot? snapshot);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildDxfEditChangeEntries")]
    public static extern List<DxfEditChangeListEntry> BuildDxfEditChangeEntries(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ApplyDxfEditChangeEntry")]
    public static extern void ApplyDxfEditChangeEntry(FreeformHelperViewModel viewModel, DxfEditChangeListEntry entry);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RestoreHiddenCadPad")]
    public static extern void RestoreHiddenCadPad(FreeformHelperViewModel viewModel, int cadPadId);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildActiveCadPadSet")]
    public static extern CadPadSet? BuildActiveCadPadSet(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "UpdateCadOutputFwDiffIndexing")]
    public static extern void UpdateCadOutputFwDiffIndexing(FreeformHelperViewModel viewModel, IReadOnlyList<CadPad> visiblePads);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ApplyViewSnapshot")]
    public static extern void ApplyViewSnapshot(FreeformHelperViewModel viewModel, UiViewSnapshot snapshot);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildWorkflowDataSnapshot")]
    public static extern WorkflowDataSnapshot BuildWorkflowDataSnapshot(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BumpNotchExportGridFingerprint")]
    public static extern void BumpNotchExportGridFingerprint(FreeformHelperViewModel viewModel);
}
