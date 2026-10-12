// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SettingsSuppressionScopesTests
{
    public static TheoryData<string> Domains => new()
    {
        "CascadeRebuild", "LayerToggleChange", "BoundLayerSelectionChange", "RegularSourceLayerSelectionChange",
        "ScanOrderSelectionSync", "GridAlignmentSelectionSync", "RegularSourceModeSelectionSync",
        "CadOutputFwDiffAutoModeSelectionSync", "DxfIndexAnchorChange", "ManualSizingApply",
        "RegularVisibilityMaskStateChange", "ScopeSync", "NotchThresholdSync", "SettingsLoading",
    };

    [Theory]
    [MemberData(nameof(Domains))]
    public void Enter_NestedScopes_KeepsOuterActiveAndDisposesEachScopeOnce(string domain)
    {
        var owner = new SettingsSuppressionScopes();
        var (enter, active) = GetDomain(owner, domain);
        Assert.False(active());
        var outer = enter();
        var inner = enter();

        inner.Dispose();
        inner.Dispose();

        Assert.True(active());
        outer.Dispose();
        outer.Dispose();
        Assert.False(active());
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Enter_Exception_ClearsDomainAndAllowsNextScope(string domain)
    {
        var owner = new SettingsSuppressionScopes();
        var (enter, active) = GetDomain(owner, domain);

        Assert.Throws<InvalidOperationException>(() => ThrowInsideScope(enter));

        Assert.False(active());
        using (enter())
        {
            Assert.True(active());
        }
        Assert.False(active());
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Enter_ExceptionInsideNestedScope_KeepsOuterActiveUntilDisposed(string domain)
    {
        var owner = new SettingsSuppressionScopes();
        var (enter, active) = GetDomain(owner, domain);
        using (enter())
        {
            Assert.Throws<InvalidOperationException>(() => ThrowInsideScope(enter));

            Assert.True(active());
        }

        Assert.False(active());
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Enter_IndependentDomain_DoesNotSuppressOtherDomain(string domain)
    {
        var owner = new SettingsSuppressionScopes();
        var (enter, active) = GetDomain(owner, domain);
        var other = GetDomain(owner, string.Equals(domain, "SettingsLoading", StringComparison.Ordinal) ? "ScopeSync" : "SettingsLoading");

        using (enter())
        {
            Assert.True(active());
            Assert.False(other.Active());
            using (other.Enter())
            {
                Assert.True(other.Active());
            }
            Assert.True(active());
            Assert.False(other.Active());
        }

        Assert.False(active());
    }

    private static void ThrowInsideScope(Func<UndoSuppression.Scope> enter)
    {
        using var scope = enter();
        throw new InvalidOperationException("Synchronous update failed.");
    }

    private static (Func<UndoSuppression.Scope> Enter, Func<bool> Active) GetDomain(SettingsSuppressionScopes owner, string domain) => domain switch
    {
        "CascadeRebuild" => (owner.EnterCascadeRebuild, () => owner.IsCascadeRebuildActive),
        "LayerToggleChange" => (owner.EnterLayerToggleChange, () => owner.IsLayerToggleChangeActive),
        "BoundLayerSelectionChange" => (owner.EnterBoundLayerSelectionChange, () => owner.IsBoundLayerSelectionChangeActive),
        "RegularSourceLayerSelectionChange" => (owner.EnterRegularSourceLayerSelectionChange, () => owner.IsRegularSourceLayerSelectionChangeActive),
        "ScanOrderSelectionSync" => (owner.EnterScanOrderSelectionSync, () => owner.IsScanOrderSelectionSyncActive),
        "GridAlignmentSelectionSync" => (owner.EnterGridAlignmentSelectionSync, () => owner.IsGridAlignmentSelectionSyncActive),
        "RegularSourceModeSelectionSync" => (owner.EnterRegularSourceModeSelectionSync, () => owner.IsRegularSourceModeSelectionSyncActive),
        "CadOutputFwDiffAutoModeSelectionSync" => (owner.EnterCadOutputFwDiffAutoModeSelectionSync, () => owner.IsCadOutputFwDiffAutoModeSelectionSyncActive),
        "DxfIndexAnchorChange" => (owner.EnterDxfIndexAnchorChange, () => owner.IsDxfIndexAnchorChangeActive),
        "ManualSizingApply" => (owner.EnterManualSizingApply, () => owner.IsManualSizingApplyActive),
        "RegularVisibilityMaskStateChange" => (owner.EnterRegularVisibilityMaskStateChange, () => owner.IsRegularVisibilityMaskStateChangeActive),
        "ScopeSync" => (owner.EnterScopeSync, () => owner.IsScopeSyncActive),
        "SettingsLoading" => (owner.EnterSettingsLoading, () => owner.IsSettingsLoadingActive),
        "NotchThresholdSync" => (owner.EnterNotchThresholdSync, () => owner.IsNotchThresholdSyncActive),
        _ => throw new ArgumentOutOfRangeException(nameof(domain)),
    };
}
