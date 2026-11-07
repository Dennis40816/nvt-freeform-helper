// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CadPadInfoViewModel
{
    public string PanelIdentityText => IsMulti ? Title : IdentityText;
    public string BoundsText => _display.Identity.BoundsText;
    public string RotationText { get; } = "Not provided by CAD polygon";
    public string AnchorIcText => _display.Identity.AnchorIcText;
    public string AllocationHintText { get; } = "Click a card to highlight that pad";
    public string GeometrySeedToolTip => $"{MatchDetailsText}{Environment.NewLine}Seed confidence: {MatchConfidenceText}";
    public string ToFullReasonLineText => $"To Full: {ToFullReasonShortText}";
    public string ToFullReasonToolTip => $"{ToFullReasonFullText}{Environment.NewLine}Seed confidence: {MatchConfidenceText}";
    public bool HasNotchDetailAction => NotchDetailCommand is not null;
    public bool CanSelectAreaBucket => HasSelectAreaBucketCommand && HasCadOutputFwDiffOverrideEditor;
    public string LocateMatchToolTip => HasLocateMatchAction
        ? "Focus the best match when available; otherwise highlight related regular pads."
        : "No related Regular pads are available";
    public string NotchDetailToolTip => HasNotchDetailAction
        ? "Open Notch detail for the selected CAD pad"
        : "Notch detail is unavailable for this selection";
    public string SelectAreaBucketToolTip => IsMulti
        ? "Select a single CAD pad first"
        : !HasSelectAreaBucketCommand
            ? "Turn on Color CAD by area first"
            : !HasCadOutputFwDiffOverrideEditor
                ? "Output mapping is unavailable for this CAD pad"
                : "Select the current CAD area bucket";
}
