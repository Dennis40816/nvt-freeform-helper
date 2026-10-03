namespace FreeformHelper.UI.ViewModels;

public sealed partial class CoordinatePlannerWorkspaceViewModel
{
    partial void OnSelectedLayerOptionChanged(CoordinatePlannerCadLayerOption value)
    {
        UpdateCadPadsForCanvas();
    }

    partial void OnSelectedAaBoundsOptionChanged(CoordinatePlannerBoundsOption value)
    {
        RebuildSnapshot();
        PublishWorkspacePreferencesChanged();
    }

    partial void OnSelectedGuideBasisOptionChanged(CoordinatePlannerGuideBasisOption value)
    {
        RebuildSnapshot();
    }

    partial void OnSelectedHorizontalGuideGenerationOptionChanged(CoordinatePlannerGuideGenerationOption value)
    {
        RebuildSnapshot();
        OnPropertyChanged(nameof(IsHorizontalGuideCountMode));
        OnPropertyChanged(nameof(IsHorizontalGuidePitchMode));
        OnPropertyChanged(nameof(IsHorizontalGuideExplicitMode));
    }

    partial void OnSelectedVerticalGuideGenerationOptionChanged(CoordinatePlannerGuideGenerationOption value)
    {
        RebuildSnapshot();
        OnPropertyChanged(nameof(IsVerticalGuideCountMode));
        OnPropertyChanged(nameof(IsVerticalGuidePitchMode));
        OnPropertyChanged(nameof(IsVerticalGuideExplicitMode));
    }

    partial void OnSelectedCustomArrayCornerOptionChanged(CoordinatePlannerCustomArrayCornerOption value)
    {
        NotifySelectedCustomArrayCornerChanged();
        SelectArtifactRowByKey(value.Key);
    }

    partial void OnMachineOriginXChanged(decimal value) => RebuildSnapshot();
    partial void OnMachineOriginYChanged(decimal value) => RebuildSnapshot();
    partial void OnMachineWidthChanged(decimal value) => RebuildSnapshot();
    partial void OnMachineHeightChanged(decimal value) => RebuildSnapshot();
    partial void OnPixelWidthChanged(decimal value)
    {
        RebuildSnapshot();
        PublishWorkspacePreferencesChanged();
    }

    partial void OnPixelHeightChanged(decimal value)
    {
        RebuildSnapshot();
        PublishWorkspacePreferencesChanged();
    }
    partial void OnCopperPillarDiameterChanged(decimal value) => RebuildSnapshot();
    partial void OnHorizontalGuideCountChanged(decimal value) => RebuildSnapshot();
    partial void OnVerticalGuideCountChanged(decimal value) => RebuildSnapshot();
    partial void OnHorizontalGuidePitchChanged(decimal value) => RebuildSnapshot();
    partial void OnVerticalGuidePitchChanged(decimal value) => RebuildSnapshot();
    partial void OnHorizontalGuideInsetChanged(decimal value) => RebuildSnapshot();
    partial void OnVerticalGuideInsetChanged(decimal value) => RebuildSnapshot();
    partial void OnHorizontalGuidePositionListTextChanged(string value) => RebuildSnapshot();
    partial void OnVerticalGuidePositionListTextChanged(string value) => RebuildSnapshot();
    partial void OnShowBistRectangleChanged(bool value) => RebuildSnapshot();
    partial void OnShowCustomArrayChanged(bool value) => RebuildSnapshot();
    partial void OnCustomArrayColumnCountChanged(decimal value) => RebuildSnapshot();
    partial void OnCustomArrayRowCountChanged(decimal value) => RebuildSnapshot();
    partial void OnCustomArrayTopLeftMachineXChanged(decimal value) => OnCustomArrayCornerValueChanged("array-tl");
    partial void OnCustomArrayTopLeftMachineYChanged(decimal value) => OnCustomArrayCornerValueChanged("array-tl");
    partial void OnCustomArrayTopRightMachineXChanged(decimal value) => OnCustomArrayCornerValueChanged("array-tr");
    partial void OnCustomArrayTopRightMachineYChanged(decimal value) => OnCustomArrayCornerValueChanged("array-tr");
    partial void OnCustomArrayBottomRightMachineXChanged(decimal value) => OnCustomArrayCornerValueChanged("array-br");
    partial void OnCustomArrayBottomRightMachineYChanged(decimal value) => OnCustomArrayCornerValueChanged("array-br");
    partial void OnCustomArrayBottomLeftMachineXChanged(decimal value) => OnCustomArrayCornerValueChanged("array-bl");
    partial void OnCustomArrayBottomLeftMachineYChanged(decimal value) => OnCustomArrayCornerValueChanged("array-bl");
}
