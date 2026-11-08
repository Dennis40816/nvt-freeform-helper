// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class WorkflowStepGateBehaviorTests
{
    [Theory]
    [InlineData(WorkflowStepId.Step1Match, true, false, false, "Step 1: import DXF and build grid first.")]
    [InlineData(WorkflowStepId.Step1Match, false, true, false, "Step 1: import DXF and build grid first.")]
    [InlineData(WorkflowStepId.Step1Match, true, true, false, null)]
    [InlineData(WorkflowStepId.Step2Freeform, false, false, false, "Step 2: import DXF and run Step 1 first.")]
    [InlineData(WorkflowStepId.Step2Freeform, true, true, false, "Step 2: run Step 1 (Match) first.")]
    [InlineData(WorkflowStepId.Step2Freeform, true, true, true, null)]
    [InlineData(WorkflowStepId.Step3NotchPreview, false, false, false, "Step 3: import DXF and build grid first.")]
    [InlineData(WorkflowStepId.Step3NotchPreview, true, true, false, "Step 3: run Step 1 (Match) first.")]
    [InlineData(WorkflowStepId.Step3NotchPreview, true, true, true, null)]
    [InlineData(WorkflowStepId.Step4IndexDiagnostics, false, false, false, "Step 4: import DXF and build grid first.")]
    [InlineData(WorkflowStepId.Step4IndexDiagnostics, true, true, false, "Step 4: run Step 1 (Match) first.")]
    [InlineData(WorkflowStepId.Step4IndexDiagnostics, true, true, true, null)]
    [InlineData(WorkflowStepId.Step5Export, false, false, false,
        "Step 5: import DXF and run Step 1 first. Step 4 is optional diagnostics.")]
    [InlineData(WorkflowStepId.Step5Export, true, true, false,
        "Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional.")]
    [InlineData(WorkflowStepId.Step5Export, true, true, true, null)]
    public void Validate_ReportsTheMissingPrerequisiteAndAllowsStepsWithoutOptionalResults(
        WorkflowStepId step, bool hasCad, bool hasGrid, bool hasMatch, string? expectedMessage)
    {
        var snapshot = new WorkflowStateSnapshot(
            HasCad: hasCad,
            HasGrid: hasGrid,
            HasStep1Result: hasMatch,
            HasStep2Result: false,
            HasStep3Result: false,
            HasStep4Result: false);

        var result = WorkflowStepGateService.Validate(step, snapshot);

        Assert.Equal(expectedMessage is null, result.IsAllowed);
        Assert.Equal(expectedMessage, result.Message);
    }
}
