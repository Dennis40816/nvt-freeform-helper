using System.Collections.Immutable;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Provides data for the <see cref="PadCanvas.SelectionChanged"/> event.
/// This class encapsulates the current selection state of both CAD and regular pads
/// on the <see cref="PadCanvas"/>.
/// </summary>
public sealed class PadCanvasSelectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets an immutable array of unique IDs of the currently selected CAD pads, sorted in ascending order.
    /// </summary>
    public ImmutableArray<int> SelectedCadPadIds { get; }
    /// <summary>
    /// Gets an immutable array of unique indices of the currently selected regular pads, sorted in ascending order.
    /// </summary>
    public ImmutableArray<int> SelectedRegularPadIndices { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PadCanvasSelectionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="cadPadIds">A collection of CAD pad IDs that are selected.</param>
    /// <param name="regularPadIndices">A collection of regular pad indices that are selected.</param>
    public PadCanvasSelectionChangedEventArgs(IEnumerable<int> cadPadIds, IEnumerable<int> regularPadIndices)
    {
        SelectedCadPadIds = NormalizeSelection(cadPadIds);
        SelectedRegularPadIndices = NormalizeSelection(regularPadIndices);
    }

    private static ImmutableArray<int> NormalizeSelection(IEnumerable<int> values)
    {
        if (values is IReadOnlyList<int> sortedList && IsStrictlyIncreasing(sortedList))
        {
            if (sortedList.Count == 0)
            {
                return ImmutableArray<int>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<int>(sortedList.Count);
            for (var i = 0; i < sortedList.Count; i++)
            {
                builder.Add(sortedList[i]);
            }

            return builder.MoveToImmutable();
        }

        if (values is ISet<int> set)
        {
            if (set.Count == 0)
            {
                return ImmutableArray<int>.Empty;
            }

            var buffer = new int[set.Count];
            var index = 0;
            foreach (var value in set)
            {
                buffer[index++] = value;
            }

            Array.Sort(buffer);
            return ImmutableArray.Create(buffer);
        }

        return values.Distinct().OrderBy(static value => value).ToImmutableArray();
    }

    private static bool IsStrictlyIncreasing(IReadOnlyList<int> values)
    {
        if (values.Count <= 1)
        {
            return true;
        }

        var previous = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            var current = values[i];
            if (current <= previous)
            {
                return false;
            }

            previous = current;
        }

        return true;
    }
}
