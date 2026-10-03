using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Interaction;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class WorkspaceInteractionStateTests
{
    [Fact]
    public void SetSelection_NormalizesAndRaisesEvent()
    {
        var state = new WorkspaceInteractionState();
        SelectionChangedEventArgs? last = null;
        var calls = 0;
        var selectedCadIds = new[] { 3, 1, 3 };
        var selectedRegularIds = new[] { 5, 2, 2 };

        state.SelectionChanged += (_, e) =>
        {
            calls++;
            last = e;
        };

        state.SetSelection(selectedCadIds, selectedRegularIds);

        Assert.Equal(1, calls);
        Assert.Equal(new List<int> { 1, 3 }, last!.CadIds);
        Assert.Equal(new List<int> { 2, 5 }, last!.RegularIndices);
    }

    [Fact]
    public void SetSelection_SameSelectionDoesNotRaiseEvent()
    {
        var state = new WorkspaceInteractionState();
        var calls = 0;
        var firstCadIds = new[] { 2, 1 };
        var secondCadIds = new[] { 1, 2 };
        var regularIds = new[] { 4 };
        state.SelectionChanged += (_, _) => calls++;

        state.SetSelection(firstCadIds, regularIds);
        state.SetSelection(secondCadIds, regularIds);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void PadInfo_OpenAndClose_RaisesEvents()
    {
        var state = new WorkspaceInteractionState();
        var events = new List<PadInfoContext?>();
        state.PadInfoChanged += (_, e) => events.Add(e.Context);

        var context = new PadInfoContext(new object(), new Rect2(0, 0, 1, 1), new List<Rect2> { new Rect2(0, 0, 1, 1) });
        state.OpenPadInfo(context);
        state.ClosePadInfo();

        Assert.Equal(2, events.Count);
        Assert.Same(context, events[0]);
        Assert.Null(events[1]);
    }
}
