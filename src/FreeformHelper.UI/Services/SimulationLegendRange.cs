namespace FreeformHelper.UI.Services;

internal readonly record struct SimulationLegendRange(
    double Minimum,
    double Maximum,
    double MaximumAbsDelta,
    double AutoNegativeClampAbs,
    double AutoPositiveClamp);
