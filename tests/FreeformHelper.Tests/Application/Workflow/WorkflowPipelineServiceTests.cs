using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class WorkflowPipelineServiceTests
{
    [Fact]
    public void BuildProjectLoadReplayPlan_WhenStep2Disabled_ReturnsStep1Only()
    {
        _ = new WorkflowPipelineService();

        var plan = WorkflowPipelineService.BuildProjectLoadReplayPlan(autoReplayStep2AfterLoad: false);

        Assert.Equal(new[] { WorkflowStepId.Step1Match }, plan);
    }

    [Fact]
    public void BuildProjectLoadReplayPlan_WhenStep2Enabled_ReturnsStep1ThenStep2()
    {
        _ = new WorkflowPipelineService();

        var plan = WorkflowPipelineService.BuildProjectLoadReplayPlan(autoReplayStep2AfterLoad: true);

        Assert.Equal(new[] { WorkflowStepId.Step1Match, WorkflowStepId.Step2Freeform }, plan);
    }

    [Fact]
    public void GetDownstreamStepsToInvalidate_ForStep2_ReturnsStep3ToStep5()
    {
        _ = new WorkflowPipelineService();

        var downstream = WorkflowPipelineService.GetDownstreamStepsToInvalidate(WorkflowStepId.Step2Freeform);

        Assert.Equal(
            new[]
            {
                WorkflowStepId.Step3NotchPreview,
                WorkflowStepId.Step4IndexDiagnostics,
                WorkflowStepId.Step5Export
            },
            downstream);
    }

    [Fact]
    public void BuildExpansionState_ForStep4_OnlyStep4Expanded()
    {
        _ = new WorkflowPipelineService();

        var state = WorkflowPipelineService.BuildExpansionState(WorkflowStepId.Step4IndexDiagnostics);

        Assert.False(state.IsStep1Expanded);
        Assert.False(state.IsStep2Expanded);
        Assert.False(state.IsStep3Expanded);
        Assert.True(state.IsStep4Expanded);
        Assert.False(state.IsStep5Expanded);
    }

    [Theory]
    [InlineData(1, WorkflowStepId.Step1Match)]
    [InlineData(2, WorkflowStepId.Step2Freeform)]
    [InlineData(3, WorkflowStepId.Step3NotchPreview)]
    [InlineData(4, WorkflowStepId.Step4IndexDiagnostics)]
    [InlineData(5, WorkflowStepId.Step5Export)]
    [InlineData(999, WorkflowStepId.Step1Match)]
    public void NormalizeStepOrDefault_ReturnsExpectedStep(int input, WorkflowStepId expected)
    {
        _ = new WorkflowPipelineService();

        var normalized = WorkflowPipelineService.NormalizeStepOrDefault(input);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(WorkflowStepId.Step1Match, WorkflowStepId.Step2Freeform)]
    [InlineData(WorkflowStepId.Step2Freeform, WorkflowStepId.Step3NotchPreview)]
    [InlineData(WorkflowStepId.Step3NotchPreview, WorkflowStepId.Step4IndexDiagnostics)]
    [InlineData(WorkflowStepId.Step4IndexDiagnostics, WorkflowStepId.Step5Export)]
    public void TryGetNextStepOnSuccess_ForSupportedSteps_ReturnsExpectedNext(
        WorkflowStepId step,
        WorkflowStepId expectedNext)
    {
        _ = new WorkflowPipelineService();

        var ok = WorkflowPipelineService.TryGetNextStepOnSuccess(step, out var next);

        Assert.True(ok);
        Assert.Equal(expectedNext, next);
    }

    [Fact]
    public void TryGetNextStepOnSuccess_ForStep5_ReturnsFalse()
    {
        _ = new WorkflowPipelineService();

        var ok = WorkflowPipelineService.TryGetNextStepOnSuccess(WorkflowStepId.Step5Export, out var next);

        Assert.False(ok);
        Assert.Equal(default, next);
    }
}
