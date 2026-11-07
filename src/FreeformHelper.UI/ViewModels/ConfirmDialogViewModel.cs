// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

public sealed record ConfirmDialogViewModel(
    string Title, string Message, string ConfirmText, string CancelText, bool EmphasizeCancel = false,
    string? ConfirmTip = null, string? CancelTip = null, string? ConsequenceText = null,
    string? NextStartMessage = null, string? DismissText = null, string? DismissTip = null)
{
    public bool HasConsequences => !string.IsNullOrWhiteSpace(ConsequenceText);
    public bool HasNextStartMessage => !string.IsNullOrWhiteSpace(NextStartMessage);
    public bool HasDismissAction => !string.IsNullOrWhiteSpace(DismissText);
}
