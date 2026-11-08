// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class WorkflowPipelinePolicyBehaviorTests
{
    [Theory]
    [InlineData(WorkflowStepId.Step1Match,
        "Project load auto replay started: rebuilding Step 1 match.",
        "Project load auto replay complete: Step 1 match restored.",
        "Project load auto replay skipped: Step 1")]
    [InlineData(WorkflowStepId.Step2Freeform,
        "Project load Step 2 auto replay started.",
        "Project load Step 2 auto replay complete.",
        "Project load auto replay skipped: Step 2")]
    public void ReplayNotifications_IdentifyTheStepAndExplainWhyReplayWasSkipped(
        WorkflowStepId step, string started, string completed, string skippedPrefix)
    {
        Assert.Equal(started, WorkflowPipelineService.GetProjectLoadReplayStartedMessage(step));
        Assert.Equal(completed, WorkflowPipelineService.GetProjectLoadReplayCompletedMessage(step));
        Assert.Equal(skippedPrefix + " (missing DXF).",
            WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(step, "  missing DXF \t"));
        Assert.Equal(skippedPrefix + " (unknown reason).",
            WorkflowPipelineService.GetProjectLoadReplaySkippedMessage(step, " \t "));
    }

    [Theory]
    [InlineData(WorkflowStepId.Step1Match, "Step 1 changed: cleared Step 2-5 results.",
        new[] { WorkflowStepId.Step2Freeform, WorkflowStepId.Step3NotchPreview,
            WorkflowStepId.Step4IndexDiagnostics, WorkflowStepId.Step5Export })]
    [InlineData(WorkflowStepId.Step2Freeform, "Step 2 changed: cleared Step 3-5 results.",
        new[] { WorkflowStepId.Step3NotchPreview, WorkflowStepId.Step4IndexDiagnostics, WorkflowStepId.Step5Export })]
    [InlineData(WorkflowStepId.Step3NotchPreview, "Step 3 changed: cleared Step 4-5 results.",
        new[] { WorkflowStepId.Step4IndexDiagnostics, WorkflowStepId.Step5Export })]
    [InlineData(WorkflowStepId.Step4IndexDiagnostics, "Step 4 changed: cleared Step 5 result.",
        new[] { WorkflowStepId.Step5Export })]
    [InlineData(WorkflowStepId.Step5Export, null, new WorkflowStepId[0])]
    public void InvalidationNotification_DescribesExactlyWhichLaterResultsAreCleared(
        WorkflowStepId changedStep, string? expectedMessage, WorkflowStepId[] expectedSteps)
    {
        var steps = WorkflowPipelineService.GetDownstreamStepsToInvalidate(changedStep);
        var message = WorkflowPipelineService.GetInvalidationStatusMessage(changedStep);

        Assert.Equal(expectedSteps, steps);
        Assert.Equal(expectedMessage, message);
    }
}
