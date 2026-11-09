// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

internal sealed class Step4DuplicateGroupsModel
{
    private Step4DuplicateDiffGroupViewModel[] _groups = Array.Empty<Step4DuplicateDiffGroupViewModel>(); // UI thread only.

    public IReadOnlyList<Step4DuplicateDiffGroupViewModel> Groups => _groups;

    public bool HasGroups => _groups.Length > 0;

    public void Replace(Step4DuplicateDiffGroupViewModel[] groups)
    {
        _groups = groups;
    }
}
