using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchOverlayVisibilityPolicyTests
{
    private readonly NotchOverlayVisibilityPolicy _policy = new();

    [Fact]
    public void Resolve_WhenNoPreviewData_HidesAllLayers()
    {
        var state = NotchOverlayVisibilityPolicy.Resolve(new NotchOverlayVisibilityInput(
            HasPreviewData: false,
            EnableToFullComputation: true,
            ShowToFullOverlay: true,
            ShowToRegularLabels: true,
            PreviewStage: 3));

        Assert.False(state.ShowToRegularLabels);
        Assert.False(state.ShowToFullSeed);
        Assert.False(state.ShowToFullCandidate);
        Assert.False(state.ShowToFullFinal);
        Assert.False(state.ShowAnyToFull);
    }

    [Fact]
    public void Resolve_WhenToFullComputationDisabled_ShowsOnlyToRegularLabels()
    {
        var state = NotchOverlayVisibilityPolicy.Resolve(new NotchOverlayVisibilityInput(
            HasPreviewData: true,
            EnableToFullComputation: false,
            ShowToFullOverlay: true,
            ShowToRegularLabels: true,
            PreviewStage: 3));

        Assert.True(state.ShowToRegularLabels);
        Assert.False(state.ShowToFullSeed);
        Assert.False(state.ShowToFullCandidate);
        Assert.False(state.ShowToFullFinal);
    }

    [Theory]
    [InlineData(1, true, false, false)]
    [InlineData(2, false, true, false)]
    [InlineData(3, false, false, true)]
    public void Resolve_WhenToFullEnabled_FollowsPreviewStage(
        int stage,
        bool expectSeed,
        bool expectCandidate,
        bool expectFinal)
    {
        var state = NotchOverlayVisibilityPolicy.Resolve(new NotchOverlayVisibilityInput(
            HasPreviewData: true,
            EnableToFullComputation: true,
            ShowToFullOverlay: true,
            ShowToRegularLabels: true,
            PreviewStage: stage));

        Assert.True(state.ShowToRegularLabels);
        Assert.Equal(expectSeed, state.ShowToFullSeed);
        Assert.Equal(expectCandidate, state.ShowToFullCandidate);
        Assert.Equal(expectFinal, state.ShowToFullFinal);
    }

    [Fact]
    public void Resolve_WhenToFullDisplayHidden_HidesToFullLayersOnly()
    {
        var state = NotchOverlayVisibilityPolicy.Resolve(new NotchOverlayVisibilityInput(
            HasPreviewData: true,
            EnableToFullComputation: true,
            ShowToFullOverlay: false,
            ShowToRegularLabels: true,
            PreviewStage: 3));

        Assert.True(state.ShowToRegularLabels);
        Assert.False(state.ShowToFullSeed);
        Assert.False(state.ShowToFullCandidate);
        Assert.False(state.ShowToFullFinal);
        Assert.False(state.ShowAnyToFull);
    }

    [Fact]
    public void Resolve_WhenToRegularDisplayHidden_HidesOnlyToRegularLabels()
    {
        var state = NotchOverlayVisibilityPolicy.Resolve(new NotchOverlayVisibilityInput(
            HasPreviewData: true,
            EnableToFullComputation: true,
            ShowToFullOverlay: true,
            ShowToRegularLabels: false,
            PreviewStage: 3));

        Assert.False(state.ShowToRegularLabels);
        Assert.False(state.ShowToFullSeed);
        Assert.False(state.ShowToFullCandidate);
        Assert.True(state.ShowToFullFinal);
        Assert.True(state.ShowAnyToFull);
    }

    [Theory]
    [MemberData(nameof(GetVisibilityMatrixCases))]
    public void Resolve_MatrixMatchesSpec(
        bool hasPreviewData,
        bool enableToFullComputation,
        bool showToFullOverlay,
        bool showToRegularLabels,
        int previewStage,
        bool expectToRegularLabels,
        bool expectSeed,
        bool expectCandidate,
        bool expectFinal)
    {
        var state = NotchOverlayVisibilityPolicy.Resolve(new NotchOverlayVisibilityInput(
            HasPreviewData: hasPreviewData,
            EnableToFullComputation: enableToFullComputation,
            ShowToFullOverlay: showToFullOverlay,
            ShowToRegularLabels: showToRegularLabels,
            PreviewStage: previewStage));

        Assert.Equal(expectToRegularLabels, state.ShowToRegularLabels);
        Assert.Equal(expectSeed, state.ShowToFullSeed);
        Assert.Equal(expectCandidate, state.ShowToFullCandidate);
        Assert.Equal(expectFinal, state.ShowToFullFinal);
        Assert.Equal(expectSeed || expectCandidate || expectFinal, state.ShowAnyToFull);
    }

    public static IEnumerable<object[]> GetVisibilityMatrixCases()
    {
        var bools = new[] { false, true };
        var stages = new[] { 0, 1, 2, 3, 4 };
        foreach (var hasPreviewData in bools)
        {
            foreach (var enableToFullComputation in bools)
            {
                foreach (var showToFullOverlay in bools)
                {
                    foreach (var showToRegularLabels in bools)
                    {
                        foreach (var previewStage in stages)
                        {
                            var clampedStage = Math.Clamp(previewStage, 1, 3);
                            var expectToRegularLabels = hasPreviewData && showToRegularLabels;
                            var showToFull = hasPreviewData && enableToFullComputation && showToFullOverlay;
                            var expectSeed = showToFull && clampedStage == 1;
                            var expectCandidate = showToFull && clampedStage == 2;
                            var expectFinal = showToFull && clampedStage == 3;

                            yield return new object[]
                            {
                                hasPreviewData,
                                enableToFullComputation,
                                showToFullOverlay,
                                showToRegularLabels,
                                previewStage,
                                expectToRegularLabels,
                                expectSeed,
                                expectCandidate,
                                expectFinal
                            };
                        }
                    }
                }
            }
        }
    }
}
