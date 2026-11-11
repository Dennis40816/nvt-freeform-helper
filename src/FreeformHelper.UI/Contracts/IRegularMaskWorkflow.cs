// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Contracts;

/// <summary>Provides mask operations and accepted data without exposing a workspace view model.</summary>
internal interface IRegularMaskWorkflow
{
    RegularVisibilityMaskResult Mask { get; set; }
    string? SourcePath { get; }
    byte[]? EmbeddedMask { get; }
    string? EmbeddedName { get; }
    IReadOnlySet<int>? ActiveRegularPadIds { get; }

    Task ImportAsync(Func<Task<string?>>? pickPathAsync);
    void Refresh(RegularGrid grid);
    void ApplySnapshot(string? sourcePath, bool isEnabled, bool allowRuntimeRefresh,
        byte[]? embeddedMask = null, string? embeddedMaskName = null);
    void Clear();
    void ResetSelection(bool clearProjectEmbeddedMask);
    void ResetResult(RegularGrid? grid);
    void SetEnabled(bool value, bool refreshAssignment);
    void OnEnabledChanged(bool value);
}

/// <summary>Publishes mask presentation synchronously at the existing workflow notification phases.</summary>
internal interface IRegularMaskPresentation
{
    bool IsEnabled { get; set; }
    string Summary { get; }
    void UpdateSummary(RegularVisibilityMaskResult result, RegularGrid? grid, bool isEnabled);
}
