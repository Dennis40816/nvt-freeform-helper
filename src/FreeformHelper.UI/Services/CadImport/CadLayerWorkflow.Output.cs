using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services.CadImport;

internal sealed partial class CadLayerWorkflow
{
    public void FilterCadPadsByLayer()
    {
        if (_document.Cad is null)
        {
            _effects.ClearOutput();
            PublishFilterEffects();
            return;
        }
        try
        {
            var result = LayerFilterUseCase.Filter(_document.Cad, _state.LayerToggles);
            var visible = result.VisiblePads.Where(pad => !_document.IsEffectivelyHidden(pad.Id)).ToList();
            _effects.UpdateOutput(visible);
            PublishFilterEffects();
            // Bulk loads request their own rebuild after publication.
            if (_state.RecalcBoundsOnLayerFilter && !_guards.IsLoading()) _ = _effects.Rebuild(false);
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            _effects.SetError("Layer filter failed", ex);
            _logger.Error(ex, "Layer filter failed.");
        }
    }

    private void PublishFilterEffects()
    {
        _effects.SyncHidden();
        _effects.ClearMatches();
        if (!_guards.IsLoading()) _effects.ResetWorkflow();
    }

    public CadPadSet? BuildFilteredCadPadSet()
    {
        if (_document.Cad is null) return null;
        if (_document.FilteredCad is not null) return _document.FilteredCad;
        var result = LayerFilterUseCase.Filter(_document.Cad, _state.LayerToggles);
        var visible = result.VisiblePads.Where(pad => !_document.IsEffectivelyHidden(pad.Id)).ToList();
        _document.PublishFilteredCad(new CadPadSet(visible));
        return _document.FilteredCad;
    }

    public CadPadSet? BuildCadPadSetForBounds()
    {
        if (_document.Cad is null) return null;
        var name = _state.SelectedBoundLayerOption?.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var pads = _document.Cad.Pads.Where(pad => !_document.IsEffectivelyHidden(pad.Id))
                .Where(pad => string.Equals(pad.Layer, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (pads.Count > 0) return new CadPadSet(pads);
        }
        if (_state.RecalcBoundsOnLayerFilter) return BuildFilteredCadPadSet();
        if (_document.Edit.AutoHiddenDuplicateCadPadIds.Count == 0) return _document.Cad;
        return _document.BuildActiveCad();
    }

    public void SelectCadAreaBucketFromPad(int anchorCadPadId)
    {
        if (!_inputs.ColorByArea())
        {
            _effects.SetStatus("Area bucket select requires Color by area.");
            return;
        }
        var ids = CadAreaBucketService.ResolveBucketMembers(_inputs.VisiblePads(), (double)_inputs.AreaTolerance(), anchorCadPadId);
        if (ids.Count == 0)
        {
            _effects.SetStatus("Area bucket select: no matching visible pad.");
            return;
        }
        _effects.SelectPads(ids);
        _effects.SetStatus($"Area bucket selected: {ids.Count} pads.");
        _logger.Debug(CultureInfo.InvariantCulture, "Area bucket selected from CAD {0} (count={1}).", anchorCadPadId, ids.Count);
    }
}
