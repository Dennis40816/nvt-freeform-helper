// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Windows.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Live presentation inputs; getters project existing owners without retaining a snapshot.</summary>
internal interface IWorkspaceNavigationSource
{
    bool HasCad { get; }
    bool HasStep1Result { get; }
    WorkspaceWorkStep WorkStep { get; set; }
    ICommand OpenDxfCommand { get; }
    ICommand MatchCommand { get; }
    ICommand AutoDetectFreeformsCommand { get; }
    ICommand ExportNotchCommand { get; }
    int SelectedRegularCount { get; }
    bool CanShowToFullPreviewToggle { get; }
    bool IsCheckingDxfOverlap { get; }
    bool HasDxfOverlapIssues { get; }
    int DxfOverlapIssueCount { get; }
    bool IsDxfOverlapNotRun { get; }
    bool HasNotchValidationItems { get; }
    string NotchValidationSummaryText { get; }
}
