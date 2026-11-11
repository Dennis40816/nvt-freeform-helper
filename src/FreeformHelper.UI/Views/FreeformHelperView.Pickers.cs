using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>
/// This partial class of <see cref="FreeformHelperView"/> handles the integration
/// of file picker dialogs and associated interactions for the ViewModel.
/// It bridges between the platform-specific file dialogs and the ViewModel's requests.
/// </summary>
public sealed partial class FreeformHelperView
{
    /// <summary>
    /// Attaches the <see cref="FreeformHelperViewModel"/> to this view and
    /// configures necessary UI-related delegates for file pickers and initial setup.
    /// </summary>
    /// <param name="viewModel">The <see cref="FreeformHelperViewModel"/> instance.</param>
    private void AttachViewModel(FreeformHelperViewModel viewModel)
    {
        var canvas = _canvas ?? FindCanvas(); // Ensure canvas is found.
        viewModel.CanvasHost = new CanvasHost(this, canvas); // Provide canvas host implementation to ViewModel.
        viewModel.InteractionState.SelectionChanged += OnInteractionSelectionChanged; // Subscribe to selection changes.
        viewModel.InteractionState.PadInfoChanged += OnPadInfoChanged; // Subscribe to pad info changes.

        var storage = GetStorageProvider(); // Get platform-specific storage provider.
        StartupPerfTracker.Mark("workspace.view-attach");
        ScheduleInitialGridBuild(viewModel); // Defer initial grid request to keep first paint responsive.
        ScheduleInitialFit(viewModel); // Ensure initial canvas fit after window is ready.

        ConfigureFilePickers(viewModel, storage); // Configure file picker delegates.
        viewModel.EnsureNoPendingEdits = EnsureNoPendingEdits;
        viewModel.OpenNotchDetailAsync = ShowNotchDetailWindowAsync;
        viewModel.OpenNotchExportSelectionAsync = ShowNotchExportSelectionWindowAsync;
        viewModel.GetCurrentSimulationSafetyAudit = ResolveCurrentSimulationSafetyAudit;
        viewModel.OpenDxfEditChangeListAsync = ShowDxfEditChangeListWindowAsync;
        viewModel.OpenIndexMappingReportAsync = ShowIndexMappingReportWindowAsync;
        viewModel.OpenDxfOverlapReportAsync = ShowDxfOverlapReportWindowAsync;
        viewModel.CadExport.Dialogs.OpenDxfLayerImageExportAsync = ShowDxfLayerImageExportWindowAsync;
        viewModel.CadExport.Dialogs.ShowWarningAsync = ShowWarningDialogAsync;
        AttachCadLoadSpinnerWindow(viewModel);
    }

    private static void ScheduleInitialGridBuild(FreeformHelperViewModel viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        StartupPerfTracker.Mark("workspace.initial-grid-queued", "priority=Immediate");
        StartupPerfTracker.Mark("workspace.initial-grid-dispatch");
        viewModel.EnsureInitialGrid();
        StartupPerfTracker.Mark("workspace.initial-grid-requested");
    }

    /// <summary>
    /// Detaches the <see cref="FreeformHelperViewModel"/> from this view,
    /// unsubscribing from events and clearing delegates to prevent memory leaks.
    /// </summary>
    /// <param name="viewModel">The <see cref="FreeformHelperViewModel"/> instance.</param>
    private void DetachViewModel(FreeformHelperViewModel viewModel)
    {
        viewModel.CanvasHost = null;
        viewModel.InteractionState.SelectionChanged -= OnInteractionSelectionChanged;
        viewModel.InteractionState.PadInfoChanged -= OnPadInfoChanged;
        // Clear all delegate references to prevent memory leaks.
        viewModel.PickOpenDxfPathAsync = null;
        viewModel.RegularMask.PickOpenRegularVisibilityMaskPathAsync = null;
        viewModel.PickLoadProjectPathAsync = null;
        viewModel.PickSaveProjectPathAsync = null;
        viewModel.PickSaveNotchPathAsync = null;
        viewModel.CadExport.Dialogs.PickSaveExportPathAsync = null;
        viewModel.ConfirmEmbedDxfAsync = null;
        viewModel.ProjectDialogs.ConfirmSaveBeforeLoadAsync = null;
        viewModel.EnsureNoPendingEdits = null;
        viewModel.OpenNotchDetailAsync = null;
        viewModel.OpenNotchExportSelectionAsync = null;
        viewModel.GetCurrentSimulationSafetyAudit = null;
        viewModel.OpenDxfEditChangeListAsync = null;
        viewModel.OpenIndexMappingReportAsync = null;
        viewModel.OpenDxfOverlapReportAsync = null;
        viewModel.CadExport.Dialogs.OpenDxfLayerImageExportAsync = null;
        viewModel.CadExport.Dialogs.ShowWarningAsync = null;
        DetachCadLoadSpinnerWindow(viewModel);
    }

    /// <summary>
    /// Retrieves the platform-specific <see cref="IStorageProvider"/>.
    /// </summary>
    /// <returns>The <see cref="IStorageProvider"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the storage provider is not available.</exception>
    private IStorageProvider GetStorageProvider()
        => TopLevel.GetTopLevel(this)?.StorageProvider // Get storage provider from the top-level window.
            ?? throw new InvalidOperationException("StorageProvider is not available.");

    /// <summary>
    /// Configures the ViewModel's file picker delegates using the provided <see cref="IStorageProvider"/>.
    /// </summary>
    /// <param name="viewModel">The <see cref="FreeformHelperViewModel"/> instance.</param>
    /// <param name="storage">The <see cref="IStorageProvider"/> for file operations.</param>
    private void ConfigureFilePickers(FreeformHelperViewModel viewModel, IStorageProvider storage)
    {
        var pickers = new MainWindowFilePickerService(storage);

        viewModel.PickOpenDxfPathAsync = pickers.OpenDxfAsync;
        viewModel.RegularMask.PickOpenRegularVisibilityMaskPathAsync = pickers.OpenRegularVisibilityMaskAsync;
        viewModel.PickLoadProjectPathAsync = pickers.OpenProjectAsync;
        viewModel.PickSaveProjectPathAsync = pickers.SaveProjectAsync;
        viewModel.PickSaveNotchPathAsync = pickers.SaveNotchAsync;
        viewModel.CadExport.Dialogs.PickSaveExportPathAsync = pickers.SaveExportAsync;

        // Delegates for the project save/load confirmation dialogs.
        viewModel.ConfirmEmbedDxfAsync = () => ProjectDialogPrompts.ConfirmEmbedDxfAsync(TopLevel.GetTopLevel(this) as Window);
        viewModel.ProjectDialogs.ConfirmSaveBeforeLoadAsync = backupPath => ProjectDialogPrompts.ConfirmSaveBeforeLoadAsync(TopLevel.GetTopLevel(this) as Window, backupPath);
    }

    private async Task ShowWarningDialogAsync(string title, string message)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var dialog = new WarningDialog(title, message);
        if (owner is null)
        {
            dialog.Show();
            return;
        }

        await dialog.ShowDialog(owner);
    }
}
