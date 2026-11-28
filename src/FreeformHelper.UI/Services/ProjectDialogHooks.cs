// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace FreeformHelper.UI.Services;

/// <summary>
/// Holds the project dialog hooks that the View wires into the view model.
/// A hook that is not wired keeps the earlier behavior of its workflow.
/// </summary>
public sealed class ProjectDialogHooks
{
    /// <summary>
    /// Gets or sets the prompt shown before another project loads while the current one has unsaved changes.
    /// The argument is the path of the backup copy of the unsaved project, or null when the backup failed.
    /// It returns true to save first, false to discard the changes, and null to cancel the load.
    /// </summary>
    public Func<string?, Task<bool?>>? ConfirmSaveBeforeLoadAsync { get; set; }
}
