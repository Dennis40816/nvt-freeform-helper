// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

// TestAppSettingsIsolation initializes the isolated app-settings path before any of these owners are created.
internal sealed class SettingsDraftTestScope(FreeformHelperViewModel? owner = null) : IDisposable
{
    public FreeformHelperViewModel Owner { get; } = owner ?? new FreeformHelperViewModel();

    public void Dispose() => Owner.AppGeneralPersistenceForTests.PersistTimer?.Stop();
}

// Draft setters and their observations run synchronously on the test's calling thread.
internal sealed class SettingsDraftTrace<T>
{
    private readonly List<(string Event, T State)> _events = [];

    public SettingsDraftTrace(SettingsWindowViewModel draft, Func<T> capture)
    {
        draft.PropertyChanging += (sender, args) =>
        {
            Assert.Same(draft, sender);
            _events.Add(($"changing:{args.PropertyName}", capture()));
        };
        draft.PropertyChanged += (sender, args) =>
        {
            Assert.Same(draft, sender);
            _events.Add(($"changed:{args.PropertyName}", capture()));
        };
    }

    public void Expect(params (string Event, T State)[] expected) => Assert.Equal(expected, _events);
}

internal static class SettingsDraftSummaryExpectations
{
    public static IEnumerable<(string Event, T State)> ModelEvents<T>(T state) =>
        ModelNames.Select(name => ($"changed:{name}", state));

    private static IReadOnlyList<string> ModelNames =>
    [
        nameof(SettingsWindowViewModel.NotchEffectiveModelSummary),
        nameof(SettingsWindowViewModel.NotchAllocationModelSummary),
        nameof(SettingsWindowViewModel.NotchAllocationModelShortText),
        nameof(SettingsWindowViewModel.NotchToRegularSemanticSummary),
        nameof(SettingsWindowViewModel.NotchToRegularShortText),
        nameof(SettingsWindowViewModel.NotchToFullSemanticSummary),
        nameof(SettingsWindowViewModel.NotchToFullShortText),
        nameof(SettingsWindowViewModel.NotchBoundaryVirtualAreaCapSummary),
        nameof(SettingsWindowViewModel.NotchBoundaryVirtualAreaCapShortText),
        nameof(SettingsWindowViewModel.NotchTargetCoverageGuardSummary),
        nameof(SettingsWindowViewModel.NotchTargetCoverageGuardShortText),
        nameof(SettingsWindowViewModel.NotchTargetCoverageCapHelpText),
        nameof(SettingsWindowViewModel.NotchEmsSafetyPolicySummary),
        nameof(SettingsWindowViewModel.NotchEmsSafetyShortText),
    ];
}
