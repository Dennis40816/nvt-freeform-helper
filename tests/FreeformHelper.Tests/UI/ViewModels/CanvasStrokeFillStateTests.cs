// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CanvasStrokeFillStateTests
{
    public static TheoryData<string> PropertyNames => CanvasStrokeFillStateCharacterizationTests.PropertyNames;

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void AssignmentRegistersUndoBeforePublishingOldAndNewValues(string propertyName)
    {
        var context = new RecordingContext(propertyName);
        Assert.Equal(context.Preference.Default, context.Preference.Get());

        context.Preference.Set(0.6m);

        Assert.Equal(ExpectedAssignment(context, 0.6m), context.Entries);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void EqualValueDoesNotCallAnyDelegate(string propertyName)
    {
        var context = new RecordingContext(propertyName);

        context.Preference.Set(context.Preference.Get());

        Assert.Empty(context.Entries);
        Assert.Empty(context.UndoEntries);
    }

    [Theory]
    [InlineData(nameof(CanvasStrokeFillState.CadLineWidth), -3)]
    [InlineData(nameof(CanvasStrokeFillState.RegularLineWidth), -3)]
    [InlineData(nameof(CanvasStrokeFillState.CadLineOpacity), 2.5)]
    [InlineData(nameof(CanvasStrokeFillState.CadFillOpacity), -0.5)]
    [InlineData(nameof(CanvasStrokeFillState.RegularLineOpacity), -0.5)]
    [InlineData(nameof(CanvasStrokeFillState.RegularFillOpacity), 2.5)]
    public void OrdinarySetterDoesNotNormalize(string propertyName, double requested)
    {
        var context = new RecordingContext(propertyName);

        context.Preference.Set((decimal)requested);

        Assert.Equal((decimal)requested, context.Preference.Get());
        Assert.Equal(ExpectedAssignment(context, (decimal)requested), context.Entries);
    }

    [Theory]
    [InlineData(nameof(CanvasStrokeFillState.HighlightStrokeWidthAdjust), -3, -2)]
    [InlineData(nameof(CanvasStrokeFillState.HighlightStrokeWidthAdjust), 5, 4)]
    [InlineData(nameof(CanvasStrokeFillState.RegularSelectedFillOpacity), -0.5, 0)]
    [InlineData(nameof(CanvasStrokeFillState.RegularSelectedFillOpacity), 1.5, 1)]
    public void ClampPublishesNestedCorrectionUnderUndoSuppression(string propertyName, double requested, double corrected)
    {
        var context = new RecordingContext(propertyName);

        context.Preference.Set((decimal)requested);

        Assert.Equal(new Notification[]
        {
            new("undo", context.Preference.UndoDescription!, context.Preference.Default, false),
            new("changing", propertyName, context.Preference.Default, false),
            new("changing", propertyName, (decimal)requested, true),
            new("changed", propertyName, (decimal)corrected, true),
            new("changed", propertyName, (decimal)corrected, false),
        }, context.Entries);
        Assert.Single(context.UndoEntries);
        Assert.Equal((decimal)corrected, context.Preference.Get());
        Assert.False(context.Suppression.IsActive);
    }

    [Theory]
    [InlineData(nameof(CanvasStrokeFillState.CadLineWidth))]
    [InlineData(nameof(CanvasStrokeFillState.RegularLineWidth))]
    [InlineData(nameof(CanvasStrokeFillState.HighlightStrokeWidthAdjust))]
    [InlineData(nameof(CanvasStrokeFillState.RegularSelectedFillOpacity))]
    public void OneUndoRestoresValueBeforeRawOrClampedRequest(string propertyName)
    {
        var context = new RecordingContext(propertyName);
        context.Preference.Set(9m);
        var beforeUndo = context.Preference.Get();
        context.Entries.Clear();

        context.Undo();

        Assert.Equal(context.Preference.Default, context.Preference.Get());
        Assert.Empty(context.UndoEntries);
        Assert.Equal(new Notification[]
        {
            new("changing", propertyName, beforeUndo, true),
            new("changed", propertyName, context.Preference.Default, true),
        }, context.Entries);
        Assert.False(context.Suppression.IsActive);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void LoadingSuppressesUndoInvalidationAndClampingOnly(string propertyName)
    {
        var context = new RecordingContext(propertyName) { IsLoading = true };

        context.Preference.Set(-3m);

        Assert.Equal(-3m, context.Preference.Get());
        Assert.Empty(context.UndoEntries);
        Assert.Equal(new Notification[]
        {
            new("changing", propertyName, context.Preference.Default, false),
            new("changed", propertyName, -3m, false),
        }, context.Entries);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void UndoSuppressionKeepsNotificationsAndInvalidation(string propertyName)
    {
        var context = new RecordingContext(propertyName);

        using (context.Suppression.Enter())
        {
            context.Preference.Set(0.6m);
        }

        Assert.Empty(context.UndoEntries);
        Assert.Equal(ExpectedSuppressedAssignment(context), context.Entries);
        Assert.False(context.Suppression.IsActive);
    }

    private static List<Notification> ExpectedAssignment(RecordingContext context, decimal value)
    {
        var expected = new List<Notification>();
        if (context.Preference.UndoDescription is { } description)
        {
            expected.Add(new("undo", description, context.Preference.Default, false));
        }
        expected.Add(new("changing", context.Name, context.Preference.Default, false));
        if (context.Preference.Invalidates)
        {
            expected.Add(new("invalidate", context.Name, value, false));
        }
        expected.Add(new("changed", context.Name, value, false));
        return expected;
    }

    private static List<Notification> ExpectedSuppressedAssignment(RecordingContext context)
    {
        var expected = new List<Notification> { new("changing", context.Name, context.Preference.Default, true) };
        if (context.Preference.Invalidates)
        {
            expected.Add(new("invalidate", context.Name, 0.6m, true));
        }
        expected.Add(new("changed", context.Name, 0.6m, true));
        return expected;
    }

    private static Preference Access(CanvasStrokeFillState state, string name) => name switch
    {
        nameof(state.CadLineWidth) => new(() => state.CadLineWidth, v => state.CadLineWidth = v, 0.2m, "CAD line width", false),
        nameof(state.CadLineOpacity) => new(() => state.CadLineOpacity, v => state.CadLineOpacity = v, 1m, null, true),
        nameof(state.CadFillOpacity) => new(() => state.CadFillOpacity, v => state.CadFillOpacity = v, 1m, null, true),
        nameof(state.RegularLineWidth) => new(
            () => state.RegularLineWidth, v => state.RegularLineWidth = v, 0.2m, "Regular line width", false),
        nameof(state.HighlightStrokeWidthAdjust) => new(
            () => state.HighlightStrokeWidthAdjust, v => state.HighlightStrokeWidthAdjust = v, -1m, "Highlight line width", false),
        nameof(state.RegularLineOpacity) => new(() => state.RegularLineOpacity, v => state.RegularLineOpacity = v, 1m, null, true),
        nameof(state.RegularFillOpacity) => new(() => state.RegularFillOpacity, v => state.RegularFillOpacity = v, 1m, null, true),
        nameof(state.RegularSelectedFillOpacity) => new(
            () => state.RegularSelectedFillOpacity, v => state.RegularSelectedFillOpacity = v, 0.24m,
            "Regular selection fill opacity", false),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private sealed record Preference(
        Func<decimal> Get, Action<decimal> Set, decimal Default, string? UndoDescription, bool Invalidates);

    private sealed record Notification(string Phase, string Name, decimal Value, bool UndoSuppressed);

    private sealed class RecordingContext
    {
        public string Name { get; }
        public Preference Preference { get; }
        public bool IsLoading { get; init; }
        public UndoSuppression Suppression { get; } = new();
        public List<Notification> Entries { get; } = [];
        public Stack<Action> UndoEntries { get; } = new();

        public RecordingContext(string name)
        {
            Name = name;
            var state = new CanvasStrokeFillState(
                () => IsLoading,
                () => Suppression.IsActive,
                () => Suppression.Enter(),
                RegisterUndo,
                () => Record("invalidate", Name),
                property => Record("changing", property),
                property => Record("changed", property));
            Preference = Access(state, name);
        }

        private void RegisterUndo(Action undo, string description)
        {
            Record("undo", description);
            UndoEntries.Push(undo);
        }

        private void Record(string phase, string name)
            => Entries.Add(new(phase, name, Preference.Get(), Suppression.IsActive));

        public void Undo()
        {
            var undo = UndoEntries.Pop();
            using (Suppression.Enter())
            {
                undo();
            }
        }
    }
}
