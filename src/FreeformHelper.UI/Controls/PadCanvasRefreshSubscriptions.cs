using System.Reactive.Linq;
using Avalonia;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Subscribes a <see cref="PadCanvas"/> to the styled properties that only need a new visual refresh when they change.
/// </summary>
internal static class PadCanvasRefreshSubscriptions
{
    /// <summary>
    /// Calls <paramref name="refresh"/> when any of the properties changes, and once for each property when it subscribes.
    /// </summary>
    public static void SubscribeVisualRefresh(AvaloniaObject owner, Action refresh)
    {
        owner.GetObservable(PadCanvas.FitBoundsOverrideProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ColorCadByAreaProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowCadProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowRegularProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowAxisLabelsProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.HighlightUnmatchedProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.MatchThresholdProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.HighlightFreeformProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowSimulationRegularOverlayProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.SimulationRegularOverlayColorModeProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.SimulationRegularOverlayViewModeProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.SimulationRegularOverlayThresholdValueProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowSimulationCopperPillarProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.SimulationCopperCenterXProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.SimulationCopperCenterYProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.SimulationCopperDiameterProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.CadLineWidthProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.CadLineColorProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.CadLineOpacityProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.CadFillOpacityProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.RegularLineWidthProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.HighlightStrokeWidthAdjustProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.RegularLineColorProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.RegularLineOpacityProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.RegularFillOpacityProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.RegularSelectedColorProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.RegularSelectedFillOpacityProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowDiffIndexOverlayProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowNotchCanvasPreviewProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowNotchToRegularLabelsProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowNotchToFullSeedOverlayProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowNotchToFullCandidateOverlayProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.ShowNotchToFullFinalOverlayProperty).Subscribe(_ => refresh());
        owner.GetObservable(PadCanvas.NotchPreviewStageProperty).Subscribe(_ => refresh());
    }
}
