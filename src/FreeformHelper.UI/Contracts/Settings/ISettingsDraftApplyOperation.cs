// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>One resolved draft operation; all values and option adaptation remain at the presentation boundary.</summary>
internal interface ISettingsDraftApplyOperation
{
    IReadOnlyList<int> PerIcXChannels { get; }
    IReadOnlyList<int> PerIcYChannels { get; }
    HashSet<string> CollectChanges(bool hasCascadeDetailChanges);
    void ApplyValues(HashSet<string> changed);
    void ApplyNotchValues(HashSet<string> changed);
    void ApplyLogLevel();
}
