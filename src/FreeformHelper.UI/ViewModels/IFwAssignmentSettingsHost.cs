// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Connects FW assignment inputs to the existing synchronous shell transitions.</summary>
internal interface IFwAssignmentSettingsHost
{
    bool IsLoadingSettings { get; }

    SettingsSuppressionScopes Suppression { get; }

    UndoSuppression UndoSuppression { get; }

    void NotifyPropertyChanging(string propertyName);

    void NotifyPropertyChanged(string propertyName);

    void OnShowDiffIndexOverlayChanging(bool value);

    void OnCadOutputFwDiffIndexStartChanging(decimal value);

    void OnCadOutputFwDiffAutoModeChanging(CadOutputFwDiffAutoMode value);

    void OnCadOutputFwDiffIndexAnchorCadIdChanging(decimal value);

    void NotifySelectedAutoModeChanged();

    bool TryCommitAnchorFromCurrentValue();

    void ApplySettingsOrchestration(string changedPropertyName);
}
