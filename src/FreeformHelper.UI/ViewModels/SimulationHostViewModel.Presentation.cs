// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationHostViewModel
{
    public string SourceFreshnessText => IsWorkspaceStale ? "Source changed" : "Source up to date";
    public string BuildActionText => HasBuildFailure ? "Retry build" : "Build from current workspace";
    public bool HasUsableWorkspace => HasWorkspace && !HasWorkspaceFailureOverlay;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(IsWorkspaceStale):
                OnPropertyChanged(nameof(SourceFreshnessText));
                break;
            case nameof(HasBuildFailure):
                OnPropertyChanged(nameof(BuildActionText));
                break;
            case nameof(HasWorkspace):
            case nameof(HasWorkspaceFailureOverlay):
                OnPropertyChanged(nameof(HasUsableWorkspace));
                break;
        }
    }
}
