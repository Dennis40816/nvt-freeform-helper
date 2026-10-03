using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Services;

internal readonly record struct SimulationDeltaViolation(
    int RegularPadId,
    int IcIndex,
    int DiffIndex,
    double ExpectedDeltaValue,
    double ActualDeltaValue);

internal static class SimulationDeltaViolationService
{
    private const double SignificantDeltaEpsilon = 1e-9;

    public static IReadOnlyList<SimulationDeltaViolation> Detect(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        IReadOnlyList<NotchApplySimulationAction> actions,
        int? selectedAreaIcIndex = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(actions);

        var expectedDeltaByIcDiff = new Dictionary<(int IcIndex, int DiffIndex), double>();
        foreach (var action in actions)
        {
            var sourceDelta = action.SourceAfterValue - action.SourceBeforeValue;
            AddExpectedDelta(action.IcIndex, action.AnchorDiffIndex, sourceDelta, expectedDeltaByIcDiff);

            foreach (var leg in action.Legs)
            {
                AddExpectedDelta(action.IcIndex, leg.TargetDiffIndex, leg.DeltaValue, expectedDeltaByIcDiff);
            }
        }

        if (expectedDeltaByIcDiff.Count == 0)
        {
            return Array.Empty<SimulationDeltaViolation>();
        }

        var violations = new List<SimulationDeltaViolation>();
        foreach (var cell in cells)
        {
            if (selectedAreaIcIndex.HasValue && cell.IcIndex != selectedAreaIcIndex.Value)
            {
                continue;
            }

            var key = (cell.IcIndex, cell.DiffIndex);
            if (!expectedDeltaByIcDiff.TryGetValue(key, out var expectedDelta))
            {
                continue;
            }

            var actualDelta = cell.DeltaValue;
            if (Math.Abs(expectedDelta - actualDelta) <= SignificantDeltaEpsilon)
            {
                continue;
            }

            violations.Add(new SimulationDeltaViolation(
                cell.RegularPadId,
                cell.IcIndex,
                cell.DiffIndex,
                expectedDelta,
                actualDelta));
        }

        return violations
            .OrderBy(static item => item.RegularPadId)
            .ToArray();
    }

    private static void AddExpectedDelta(
        int icIndex,
        int diffIndex,
        double deltaValue,
        Dictionary<(int IcIndex, int DiffIndex), double> expectedDeltaByIcDiff)
    {
        if (Math.Abs(deltaValue) <= SignificantDeltaEpsilon)
        {
            return;
        }

        var key = (icIndex, diffIndex);
        expectedDeltaByIcDiff.TryGetValue(key, out var existingDelta);
        expectedDeltaByIcDiff[key] = existingDelta + deltaValue;
    }
}
