// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.UI.CadEditing;
using NLog;

namespace FreeformHelper.UI.Services.CadEditing;

internal sealed partial class DxfEditWorkflow(
    CadDocumentSession document, CadEditWorkflow engine, CadEditPublication publication,
    CadEditLayers layers, CadEditEffects effects, DxfEditInteraction interaction) : IDxfEditActions
{
    // Preserve the logger category used by these actions before the ownership move.
    private static readonly Logger _logger = LogManager.GetLogger("FreeformHelper.UI.ViewModels.FreeformHelperViewModel");

    public void DeleteSelectedCadPads() => engine.DeleteSelectedCadPads();
    public void RestoreLastDeletedCadPads() => engine.RestoreLastDeletedCadPads();
    public void RestoreAllHiddenCadPads() => engine.RestoreAllHiddenCadPads();
    public void CombineSelectedCadPads() => engine.CombineSelectedCadPads();
    public void ClearCombinedCadPads() => engine.ClearCombinedCadPads();
    public void ToggleDuplicateEntry(int cadPadId) => engine.ToggleDuplicateEntry(cadPadId);
    public void RestoreLayer(int cadPadId) => engine.RestoreLayer(cadPadId);
    public void RestoreGeometry(int cadPadId) => engine.RestoreGeometry(cadPadId);

    public void RotateSelectedCadPads()
    {
        if (!engine.TryGetLoadedCad("DXF rotate: import DXF first.", out var cad))
        {
            _logger.Debug(CultureInfo.InvariantCulture, "DXF rotate skipped: no DXF loaded.");
            return;
        }

        var rotationDegrees = DxfEditRules.NormalizeRotationDegrees((double)interaction.ReadInput().DxfEditRotationDegrees);
        if (Math.Abs(rotationDegrees) < 1e-9)
        {
            effects.SetStatus("DXF rotate: input a non-zero angle first.");
            return;
        }

        var selectedIds = GetRotationTargetCadIds();
        if (selectedIds.Count == 0)
        {
            effects.SetStatus(BuildRotateUnavailableStatus());
            _logger.Debug(CultureInfo.InvariantCulture, "DXF rotate skipped: no CAD pad resolved for scope {0}.", interaction.ReadInput().SelectedDxfEditRotationScopeOption.Value);
            return;
        }

        engine.Rotate(cad, selectedIds, rotationDegrees);
    }

    public HashSet<int> GetRotationTargetCadIds()
    {
        var cad = document.Cad;
        if (cad is null)
        {
            return new HashSet<int>();
        }

        var baselineCadIds = document.Edit.BaselineCad?.Pads.Select(static pad => pad.Id).ToHashSet() ?? new HashSet<int>();
        if (baselineCadIds.Count == 0)
        {
            return new HashSet<int>();
        }

        return interaction.ReadInput().SelectedDxfEditRotationScopeOption.Value switch
        {
            DxfEditRotationScope.TargetLayer =>
                DxfEditRules.ResolveRotationTargetLayerCadIds(cad, baselineCadIds, interaction.ReadInput().DxfEditRotationLayerName, document.IsEffectivelyHidden),
            _ =>
                DxfEditRules.ResolveRotationSelectedCadIds(interaction.SelectedCadIds(), baselineCadIds, document.IsEffectivelyHidden),
        };
    }

    private string BuildRotateUnavailableStatus()
    {
        return interaction.ReadInput().SelectedDxfEditRotationScopeOption.Value switch
        {
            DxfEditRotationScope.TargetLayer when string.IsNullOrWhiteSpace(DxfEditRules.NormalizeLayerName(interaction.ReadInput().DxfEditRotationLayerName))
                => "DXF rotate: select rotation layer first.",
            DxfEditRotationScope.TargetLayer
                => "DXF rotate: selected layer has no visible baseline CAD pads.",
            _ => "DXF rotate: select baseline CAD pads first.",
        };
    }

    public void MoveSelectedCadPadsToLayer()
    {
        var targetLayerName = DxfEditRules.NormalizeLayerName(interaction.ReadInput().DxfEditTargetLayerName);
        if (string.IsNullOrWhiteSpace(targetLayerName))
        {
            effects.SetStatus("DXF edit: select target layer first.");
            return;
        }

        MoveSelectedCadPadsToLayerCore(targetLayerName, operationName: "move to existing layer");
    }

    public void CreateLayerAndMoveSelectedCadPads()
    {
        var newLayerName = DxfEditRules.NormalizeLayerName(interaction.ReadInput().DxfEditNewLayerName);
        if (string.IsNullOrWhiteSpace(newLayerName))
        {
            effects.SetStatus("DXF edit: input new layer name first.");
            return;
        }

        MoveSelectedCadPadsToLayerCore(newLayerName, operationName: "create layer and move");
        interaction.SetNewLayerName(string.Empty);
    }

    private void MoveSelectedCadPadsToLayerCore(string targetLayerName, string operationName)
    {
        if (!engine.TryGetLoadedCad("DXF edit: import DXF first.", out var cad))
        {
            _logger.Debug(CultureInfo.InvariantCulture, "DXF layer move skipped: no DXF loaded.");
            return;
        }

        var selected = interaction.SelectedCadIds()
            .Where(id => !document.IsEffectivelyHidden(id))
            .Distinct()
            .ToList();
        if (selected.Count == 0)
        {
            effects.SetStatus("DXF edit: select CAD pads first.");
            _logger.Debug(CultureInfo.InvariantCulture, "DXF layer move skipped: no CAD pad selected.");
            return;
        }

        engine.MoveToLayer(cad, selected.ToHashSet(), targetLayerName, operationName);
    }

    public void RestoreHiddenEntry(int cadPadId)
    {
        if (document.Combined.GroupIdBySourceCadId.ContainsKey(cadPadId))
        {
            ClearCombinedCadGroupForPad(cadPadId);
        }
        else
        {
            engine.RestoreHiddenCadPad(cadPadId);
        }
    }

    public void ClearCombinedCadGroupForPad(int cadPadId)
    {
        if (!document.TryResolveCombinedGroupId(cadPadId, out var groupId) || document.Cad is null)
        {
            return;
        }

        engine.ClearCombinedCadGroups(
            [groupId],
            undoLabel: "DXF clear combined group",
            statusPrefix: $"DXF combine: cleared group {groupId}",
            logContext: "DXF combine cleared group");
    }

}
