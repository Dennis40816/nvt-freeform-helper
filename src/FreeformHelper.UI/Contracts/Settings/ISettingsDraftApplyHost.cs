// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Existing load scope, live channel inputs and commit effects used by a draft Apply.</summary>
internal interface ISettingsDraftApplyHost
{
    IEnumerable<decimal> PerIcXChannels { get; }
    IEnumerable<decimal> PerIcYChannels { get; }
    IDisposable EnterSettingsLoading();
    bool TryCommitAnchorFromCurrentValue();
    void ApplySettingsWindowOrchestration(IReadOnlyCollection<string> changedPropertyNames);
    void SetStatus(string message);
}
