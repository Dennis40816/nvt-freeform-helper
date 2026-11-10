using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationWorkspaceViewModel
{
    public Task ImportCsvAsync() => _source.ImportCsvAsync();

    private void RefreshSnapshot() => _source.RefreshSnapshot();

    private void AcceptSnapshot(SimulationScenarioSnapshot snapshot)
    {
        _snapshotCellsByRegularPadId = snapshot.SimulationResult.Cells.ToDictionary(
            static cell => cell.RegularPadId,
            static cell => cell);
        RebuildImpactItemIndex(snapshot.SimulationResult.Actions);

        RebuildOverlayItems(snapshot.SimulationResult.Cells);
        RefreshDiffViolationState();
        RefreshSimulationSafetyState();

        UpdateStatus(snapshot);
        RefreshSelectionPresentation();
        RefreshCommandState();
    }

    internal SimulationScenarioSnapshot? GetSimulationScenarioSnapshot()
    {
        return _snapshot;
    }

    private void UpdateStatus(SimulationScenarioSnapshot snapshot)
    {
        if (!snapshot.SimulationResult.IsSupported)
        {
            StatusText = snapshot.SimulationResult.PrimaryDiagnostic ?? "Simulation contract not supported under current version.";
            return;
        }

        var delta = snapshot.SimulationResult.DeltaTotal.ToString("+0.###;-0.###;0", System.Globalization.CultureInfo.InvariantCulture);
        StatusText = IsCopperSource
            ? $"{CopperProjectionModelText} · total Δ {delta}"
            : $"{snapshot.SimulationResult.ContractText} · frames {snapshot.SimulationResult.ConsumedFrameCount} · total Δ {delta}";
    }

    private static IReadOnlyList<NotchApplySimulationVersionOption> BuildVersionOptions(NotchTable table)
    {
        var versions = table.Rows
            .Select(static row => row.Version)
            .Distinct()
            .OrderBy(static version => (int)version)
            .Select(static version => new NotchApplySimulationVersionOption(version, version.ToDisplayLabel()))
            .ToList();

        return versions.Count > 0
            ? versions
            : new[] { new NotchApplySimulationVersionOption(NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V22.ToDisplayLabel()) };
    }

    private static List<NotchApplySimulationAreaOption> BuildAreaOptions(IReadOnlyList<RegularPad> pads)
    {
        var options = new List<NotchApplySimulationAreaOption>
        {
            new(null, "All regular pads"),
        };

        options.AddRange(pads
            .Select(static pad => pad.IcIndex)
            .Distinct()
            .OrderBy(static icIndex => icIndex)
            .Select(static icIndex => new NotchApplySimulationAreaOption(icIndex, $"IC {icIndex + 1}")));

        return options;
    }

}
