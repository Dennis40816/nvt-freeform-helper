// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CoordinatePlannerWorkspaceViewModel
{
    public bool HasSelectedArtifact => SelectedArtifactRow is not null;

    public string BistGenerationStatusText => HasBistRectangle ? "Generated" : "Not generated";

    public const string SyncConsequenceText = "Resets calibration and overwrites the four array corners; turns the array on.";

    public const string SafeCornersConsequenceText = "Overwrites the four corners and turns the array on.";

    public const string ClearCustomConsequenceText = "Clears every custom point and path.";

    public const string GuidePositionsHelpText = "Enter millimetres separated by commas, semicolons, spaces or new lines. Invalid entries are skipped; negative values become zero.";

    partial void OnSelectedArtifactRowChanged(CoordinateArtifactRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedArtifact));
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(BistGenerationStatusText));
    }
}
