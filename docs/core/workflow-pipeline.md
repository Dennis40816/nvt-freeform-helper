# Workflow Pipeline (Non-Notch Architecture)
Last updated: 2026-08-08

## Purpose
- Centralize Step dependencies, downstream invalidation cleanup, and automatic replay rules after Load Project.
- Reduce scattered conditional branches in the ViewModel so that future AI agents/people can trace the flow through a single rule table.

## Current Single Source
- `src/FreeformHelper.UI/Services/WorkflowPipelineService.cs`

## Rule One: Downstream Invalidation
- Step1 changes: clear Step2~Step5
- Step2 changes: clear Step3~Step5
- Step3 changes: clear Step4~Step5
- Step4 changes: clear Step5

## Rule Two: Step Expansion Focus on the Right
- `BuildExpansionState(activeStep)` determines that only the current step is expanded and the others are collapsed.
- The VM no longer manually defines multiple sets of `IsStepXExpanded = ...`.

## Rule Two Supplement: Main Flow After Success
- Expand Step2 after Step1 succeeds.
- Expand Step3 after Step2 succeeds, because Step3 currently includes compensation guards that affect Simulation / Step5 C export.
- Expand Step4 after Step3 succeeds.
- Expand Step5 after Step4 succeeds.
- Step4 remains optional diagnostics; Step3 is not merely a debug step and cannot simply be skipped in the main flow.

This describes current 1.3.0 production behavior, not the final operator-flow contract. `WorkflowStepGateService` already allows Step5 to be independent of Step4, so the R13.305 target is to expand Step5 directly after Step3 succeeds; Step4 mapping diagnostics and Step6 validation move to an unnumbered Diagnostics/Inspector entry. That UI move must wait until the R13.301/R13.302 draft/typed invalidation owner is stable; this documentation calibration must not pretend it has already been implemented.

## Rule Three: Single Entry for Step Execution/Clearing
- The VM provides unified entries:
  - `RunWorkflowStepAsync(WorkflowStepId)` (currently supports Step1~Step4)
  - `ClearWorkflowStep(WorkflowStepId)` (supports Step1~Step5)
- Runtime CLI delegates through `RunWorkflowStepAsync`; the right-side UI commands still call each Step handler directly. Both share the same underlying handlers and pipeline policy, but do not yet share a single execution entry, and `RunWorkflowStepAsync` is still a switch. Clear actions already share `ClearWorkflowStep`.
- Benefits:
  - Consistent behavior (consistent state updates, step expansion, and clearing side effects)
  - Lower debugging costs caused by CLI/UI behavior differences

Readiness is still inferred from row/preview count, freeform value, or summary text; a valid zero result may be mistaken for a step that has not run. R13.302a will replace this content-derived gate with revisioned `Completed/Stale` state; this document must not describe it as resolved before completion.

## Rule Four: Load Project Auto Replay
- Entry: `BuildProjectLoadReplayPlan(autoReplayStep2AfterLoad)`
- Current plan:
  - Always attempt Step1 replay
  - Also attempt Step2 replay when `AutoReplayStep2AfterProjectLoad=true`
- Each replay step reports:
  - Whether replay actually succeeded (`replayed`)
  - Elapsed milliseconds (`elapsed ms`)
  - Reason for skipping (such as the setting being disabled or prerequisites not being met)

## CLI Observability (load-project)
- `query load-project --path ...` returns `timings.stepReplay`:
  - `step1ReplayElapsedMs`
  - `step2ReplayElapsedMs`
  - `step1Replayed`
  - `step2Replayed`

## Recommendations for Future Extensions
- When adding Step6/Step7, do not add if/else directly in the VM; extend `WorkflowPipelineService` first.
- New replay conditions (such as Step3 preview replay) should also extend the replay plan instead of being hardcoded in the `LoadProject` flow.
