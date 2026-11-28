// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.CadEditing;

internal interface IDxfEditActions
{
    void DeleteSelectedCadPads();
    void RestoreLastDeletedCadPads();
    void RestoreAllHiddenCadPads();
    void CombineSelectedCadPads();
    void ClearCombinedCadPads();
    void ResetAllDxfEdits();
    void MoveSelectedCadPadsToLayer();
    void CreateLayerAndMoveSelectedCadPads();
    void RotateSelectedCadPads();
    void RestoreHiddenEntry(int cadPadId);
    void ClearCombinedCadGroupForPad(int cadPadId);
    void ToggleDuplicateEntry(int cadPadId);
    void RestoreLayer(int cadPadId);
    void RestoreGeometry(int cadPadId);
    HashSet<int> GetRotationTargetCadIds();
}
