// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.CadEditing;

// Stable UI-thread ports. Read current selection and inputs on each action, including old dialog actions.
internal sealed record DxfEditInteraction(
    Func<IReadOnlyCollection<int>> SelectedCadIds,
    Func<DxfEditPanelInput> ReadInput,
    Action<string> SetNewLayerName,
    Action<int> FocusCadPad,
    Action<string> SetStatus,
    Action NotifyMappingEligibility,
    Action NotifyMappingSelectionSummary);
