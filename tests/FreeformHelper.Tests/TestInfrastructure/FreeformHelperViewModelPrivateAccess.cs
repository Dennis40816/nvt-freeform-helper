using System.Runtime.CompilerServices;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Lifecycle;
using Nvt.Core.Progress;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Typed access to private state of <see cref="FreeformHelperViewModel"/>.</summary>
internal static class FreeformHelperViewModelPrivateAccess
{
    private const string NotchTableGenerationTaskType = "System.Threading.Tasks.Task`1[[FreeformHelper.UI.ViewModels.FreeformHelperViewModel+NotchTableGenerationResult, FreeformHelper.UI]]";

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cad")]
    public static extern ref CadPadSet? Cad(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_undoSuppression")]
    public static extern ref UndoSuppression UndoSuppression(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_layerCatalogStateService")]
    public static extern ref LayerCatalogStateService LayerCatalogState(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_grid")]
    public static extern ref RegularGrid? Grid(FreeformHelperViewModel viewModel);

    public static ProjectFile ProjectFile(FreeformHelperViewModel viewModel) => viewModel.ProjectSession.Document;

    public static void ReplaceProjectFile(FreeformHelperViewModel viewModel, ProjectFile document)
        => viewModel.ProjectSession.Document = document;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cadEdit")]
    public static extern ref CadEditState CadEdit(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dxfImportService")]
    public static extern ref DxfImportService DxfImportService(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_undoService")]
    public static extern ref UndoService UndoService(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cadLoadOverlayScope")]
    public static extern ref LoadingScopeCoordinator CadLoadOverlayScope(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ClearMappingOverrideFromReport")]
    public static extern bool ClearMappingOverrideFromReport(FreeformHelperViewModel viewModel, int cadId);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildDxfOptions")]
    public static extern DxfImportOptions BuildDxfOptions(FreeformHelperViewModel viewModel);

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildFilteredCadPadSet")]
    public static extern CadPadSet? BuildFilteredCadPadSet(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "UpdateCadOutputFwDiffIndexing")]
    public static extern void UpdateCadOutputFwDiffIndexing(FreeformHelperViewModel viewModel, IReadOnlyList<CadPad> visiblePads);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ApplyViewSnapshot")]
    public static extern void ApplyViewSnapshot(FreeformHelperViewModel viewModel, UiViewSnapshot snapshot);

    // The real return type is Task<NotchTableGenerationResult?>, a private nested record, so it is named by string and returned as object.
    // The object is always a Task; callers read its result by public property name.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GenerateCurrentNotchTableAsync")]
    [return: UnsafeAccessorType(NotchTableGenerationTaskType)]
    public static extern object GenerateCurrentNotchTable(
        FreeformHelperViewModel viewModel,
        CadPadSet cad,
        RegularGrid grid,
        string operationLabel,
        IProgress<NotchGenerationProgress>? progress,
        WorkflowDataSnapshot? workflowSnapshot,
        bool reportStep5Progress,
        bool projectToCadOutputFwDiff,
        Action<string>? progressTextReporter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildWorkflowDataSnapshot")]
    public static extern WorkflowDataSnapshot BuildWorkflowDataSnapshot(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BumpNotchExportGridFingerprint")]
    public static extern void BumpNotchExportGridFingerprint(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "CreateExportSettingsSnapshot")]
    public static extern ProjectSettings CreateExportSettingsSnapshot(FreeformHelperViewModel? owner, ProjectSettings source);
}
