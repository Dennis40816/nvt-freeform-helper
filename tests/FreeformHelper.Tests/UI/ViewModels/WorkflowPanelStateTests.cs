// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class WorkflowPanelStateTests
{
    public static TheoryData<string> PropertyNames => new()
    {
        nameof(WorkflowPanelState.IsConsoleExpanded),
        nameof(WorkflowPanelState.IsStep1Expanded),
        nameof(WorkflowPanelState.IsStep2Expanded),
        nameof(WorkflowPanelState.IsStep3Expanded),
        nameof(WorkflowPanelState.IsStep4Expanded),
        nameof(WorkflowPanelState.IsStep5Expanded),
        nameof(WorkflowPanelState.IsStep6Expanded),
    };

    [Fact]
    public void InitialStateDoesNotCallDelegates()
    {
        var context = new PanelRecordingContext();

        Assert.Equal("1100000", context.Flags);
        Assert.Empty(context.Entries);
    }

    [Theory]
    [InlineData(nameof(WorkflowPanelState.IsConsoleExpanded), "0100000", "1100000", null)]
    [InlineData(nameof(WorkflowPanelState.IsStep1Expanded), "1000000", "1100000", WorkspaceWorkStep.Step1Match)]
    [InlineData(nameof(WorkflowPanelState.IsStep2Expanded), "1100000", "1110000", WorkspaceWorkStep.Step2Freeform)]
    [InlineData(nameof(WorkflowPanelState.IsStep3Expanded), "1100000", "1101000", WorkspaceWorkStep.Step3NotchPreview)]
    [InlineData(nameof(WorkflowPanelState.IsStep4Expanded), "1100000", "1100100", null)]
    [InlineData(nameof(WorkflowPanelState.IsStep5Expanded), "1100000", "1100010", WorkspaceWorkStep.Step5Export)]
    [InlineData(nameof(WorkflowPanelState.IsStep6Expanded), "1100000", "1100001", null)]
    public void ExpansionStoresValueBeforeSelectingWorkAndPublishingChanged(
        string name, string before, string after, WorkspaceWorkStep? selectedStep)
    {
        var context = new PanelRecordingContext();
        context.Set(name, false);
        context.Entries.Clear();

        context.Set(name, true);

        var expected = new List<Notification>();
        AppendAssignment(expected, name, before, after, selectedStep);
        Assert.Equal(expected, context.Entries);
        Assert.Equal(after, context.Flags);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void CollapsePublishesOldThenNewValueWithoutSelectingWork(string name)
    {
        var context = new PanelRecordingContext();
        context.Set(name, true);
        var before = context.Flags;
        context.Entries.Clear();

        context.Set(name, false);

        Assert.Equal(new Notification[]
        {
            new("changing", name, before),
            new("changed", name, context.Flags),
        }, context.Entries);
        Assert.False(context.Get(name));
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void EqualValueDoesNotCallAnyDelegateBeforeOrAfterAChange(string name)
    {
        var context = new PanelRecordingContext();

        context.Set(name, context.Get(name));

        Assert.Empty(context.Entries);
        context.Set(name, !context.Get(name));
        context.Entries.Clear();

        context.Set(name, context.Get(name));

        Assert.Empty(context.Entries);
    }

    [Fact]
    public void ApplyExpansionPublishesAllSixWritesInOrderWithoutNormalizingTheSuppliedFlags()
    {
        var context = new PanelRecordingContext();
        context.State.IsStep1Expanded = false;
        context.Entries.Clear();

        context.State.ApplyExpansion(true, true, true, true, true, true);

        var expected = new List<Notification>();
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep1Expanded), "1000000", "1100000", WorkspaceWorkStep.Step1Match);
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep2Expanded), "1100000", "1110000", WorkspaceWorkStep.Step2Freeform);
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep3Expanded), "1110000", "1111000", WorkspaceWorkStep.Step3NotchPreview);
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep4Expanded), "1111000", "1111100");
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep5Expanded), "1111100", "1111110", WorkspaceWorkStep.Step5Export);
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep6Expanded), "1111110", "1111111");
        Assert.Equal(expected, context.Entries);
        Assert.Equal("1111111", context.Flags);
    }

    [Fact]
    public void ApplyExpansionCollapsesInOrderKeepsConsoleClosedAndSkipsEqualValues()
    {
        var context = new PanelRecordingContext();
        context.State.IsConsoleExpanded = false;
        context.State.ApplyExpansion(true, true, true, true, true, true);
        context.Entries.Clear();

        context.State.ApplyExpansion(false, false, false, false, false, false);
        context.State.ApplyExpansion(false, false, false, false, false, false);

        var expected = new List<Notification>();
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep1Expanded), "0111111", "0011111");
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep2Expanded), "0011111", "0001111");
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep3Expanded), "0001111", "0000111");
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep4Expanded), "0000111", "0000011");
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep5Expanded), "0000011", "0000001");
        AppendAssignment(expected, nameof(WorkflowPanelState.IsStep6Expanded), "0000001", "0000000");
        Assert.Equal(expected, context.Entries);
        Assert.Equal("0000000", context.Flags);
    }

    [Fact]
    public void WorkSelectionCanReenterExpansionBeforeTheOriginalChangedNotification()
    {
        var context = new PanelRecordingContext(reapplyExpansion: true);

        context.State.IsStep2Expanded = true;

        Assert.Equal(ReentrantTrace(), context.Entries);
        Assert.Equal("1010000", context.Flags);
    }

    [Theory]
    [InlineData("changing", nameof(WorkflowPanelState.IsStep2Expanded), 1, "1100000")]
    [InlineData("select", nameof(WorkspaceWorkStep.Step2Freeform), 2, "1110000")]
    [InlineData("changing", nameof(WorkflowPanelState.IsStep1Expanded), 3, "1110000")]
    [InlineData("changed", nameof(WorkflowPanelState.IsStep1Expanded), 4, "1010000")]
    [InlineData("changed", nameof(WorkflowPanelState.IsStep2Expanded), 5, "1010000")]
    public void DelegateFailurePropagatesWithoutRollbackOrContinuation(string phase, string name, int eventCount, string flags)
    {
        var context = new PanelRecordingContext(reapplyExpansion: true) { ThrowPhase = phase, ThrowName = name };

        var error = Assert.Throws<InvalidOperationException>(() => context.State.IsStep2Expanded = true);

        Assert.Same(context.Failure, error);
        Assert.Equal(ReentrantTrace().Take(eventCount), context.Entries);
        Assert.Equal(flags, context.Flags);
    }

    private static Notification[] ReentrantTrace() =>
    [
        new("changing", nameof(WorkflowPanelState.IsStep2Expanded), "1100000"),
        new("select", nameof(WorkspaceWorkStep.Step2Freeform), "1110000"),
        new("changing", nameof(WorkflowPanelState.IsStep1Expanded), "1110000"),
        new("changed", nameof(WorkflowPanelState.IsStep1Expanded), "1010000"),
        new("changed", nameof(WorkflowPanelState.IsStep2Expanded), "1010000"),
    ];

    private static void AppendAssignment(
        List<Notification> entries, string name, string before, string after, WorkspaceWorkStep? selectedStep = null)
    {
        entries.Add(new("changing", name, before));
        if (selectedStep is { } step) entries.Add(new("select", step.ToString(), after));
        entries.Add(new("changed", name, after));
    }

    private sealed record Notification(string Phase, string Name, string Flags);

    private sealed class PanelRecordingContext
    {
        private readonly bool _reapplyExpansion;
        public WorkflowPanelState State { get; }
        public List<Notification> Entries { get; } = [];
        public string? ThrowPhase { get; init; }
        public string? ThrowName { get; init; }
        public InvalidOperationException Failure { get; } = new("workflow panel delegate failed");

        // All delegate observations and mutations run synchronously on the test's calling thread.
        public string Flags
            => $"{Bit(State.IsConsoleExpanded)}{Bit(State.IsStep1Expanded)}{Bit(State.IsStep2Expanded)}{Bit(State.IsStep3Expanded)}" +
                $"{Bit(State.IsStep4Expanded)}{Bit(State.IsStep5Expanded)}{Bit(State.IsStep6Expanded)}";

        public PanelRecordingContext(bool reapplyExpansion = false)
        {
            _reapplyExpansion = reapplyExpansion;
            State = new WorkflowPanelState(SelectWorkStep,
                name => Record("changing", name), name => Record("changed", name));
        }

        private static char Bit(bool value) => value ? '1' : '0';

        public bool Get(string name) => name switch
        {
            nameof(State.IsConsoleExpanded) => State.IsConsoleExpanded,
            nameof(State.IsStep1Expanded) => State.IsStep1Expanded,
            nameof(State.IsStep2Expanded) => State.IsStep2Expanded,
            nameof(State.IsStep3Expanded) => State.IsStep3Expanded,
            nameof(State.IsStep4Expanded) => State.IsStep4Expanded,
            nameof(State.IsStep5Expanded) => State.IsStep5Expanded,
            nameof(State.IsStep6Expanded) => State.IsStep6Expanded,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        public void Set(string name, bool value)
        {
            switch (name)
            {
                case nameof(State.IsConsoleExpanded): State.IsConsoleExpanded = value; break;
                case nameof(State.IsStep1Expanded): State.IsStep1Expanded = value; break;
                case nameof(State.IsStep2Expanded): State.IsStep2Expanded = value; break;
                case nameof(State.IsStep3Expanded): State.IsStep3Expanded = value; break;
                case nameof(State.IsStep4Expanded): State.IsStep4Expanded = value; break;
                case nameof(State.IsStep5Expanded): State.IsStep5Expanded = value; break;
                case nameof(State.IsStep6Expanded): State.IsStep6Expanded = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
        }

        private void SelectWorkStep(WorkspaceWorkStep step)
        {
            Record("select", step.ToString());
            if (_reapplyExpansion)
            {
                State.ApplyExpansion(
                    step == WorkspaceWorkStep.Step1Match,
                    step == WorkspaceWorkStep.Step2Freeform,
                    step == WorkspaceWorkStep.Step3NotchPreview,
                    false,
                    step == WorkspaceWorkStep.Step5Export,
                    false);
            }
        }

        private void Record(string phase, string name)
        {
            Entries.Add(new(phase, name, Flags));
            if (phase == ThrowPhase && name == ThrowName) throw Failure;
        }
    }
}
