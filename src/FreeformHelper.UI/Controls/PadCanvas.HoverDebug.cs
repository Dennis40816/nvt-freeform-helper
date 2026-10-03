using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private readonly record struct HoverDebugSnapshot(Point2 Anchor, string Text, bool IsRegularMode);

    private void UpdateHoverDebugHit(Point screenPoint, KeyModifiers modifiers)
    {
        if (_isMiddlePanning || _isSpacePanning || _isBoxSelecting || _isLeftPointerDown)
        {
            ClearHoverDebugHit();
            return;
        }

        var regularOnly = modifiers.HasFlag(KeyModifiers.Alt);
        ScheduleHoverDebugHit(screenPoint, regularOnly);
    }

    private void RefreshHoverDebugHitFromCurrentPointer(KeyModifiers modifiers)
    {
        if (!IsPointerOver)
        {
            ClearHoverDebugHit();
            return;
        }

        UpdateHoverDebugHit(_lastPointer, modifiers);
    }

    private void ClearHoverDebugHit(bool invalidateVisual = true)
    {
        if (_hoverDebugHit.kind == HitKind.None && !_hoverDebugRegularOnlyMode && !_hoverDebugVisible)
        {
            return;
        }

        _hoverDebugRevealTimer?.Stop();
        _hoverDebugVisible = false;
        _hoverDebugHit = new HitResult(HitKind.None, 0);
        _hoverDebugRegularOnlyMode = false;
        if (invalidateVisual)
        {
            RequestVisualRefresh();
        }
    }

    private void ScheduleHoverDebugHit(Point screenPoint, bool regularOnly)
    {
        _hoverDebugPendingPointer = screenPoint;
        _hoverDebugPendingRegularOnlyMode = regularOnly;

        if (_hoverDebugVisible || _hoverDebugHit.kind != HitKind.None || _hoverDebugRegularOnlyMode != regularOnly)
        {
            _hoverDebugVisible = false;
            _hoverDebugHit = new HitResult(HitKind.None, 0);
            _hoverDebugRegularOnlyMode = regularOnly;
            RequestVisualRefresh();
        }

        EnsureHoverDebugRevealTimer();
        _hoverDebugRevealTimer!.Stop();
        _hoverDebugRevealTimer.Start();
    }

    private void EnsureHoverDebugRevealTimer()
    {
        if (_hoverDebugRevealTimer is not null)
        {
            return;
        }

        _hoverDebugRevealTimer = new DispatcherTimer
        {
            Interval = HoverDebugRevealDelay
        };
        _hoverDebugRevealTimer.Tick += (_, _) => ApplyPendingHoverDebugHit();
    }

    private void ApplyPendingHoverDebugHit()
    {
        _hoverDebugRevealTimer?.Stop();
        if (!IsPointerOver || _isMiddlePanning || _isSpacePanning || _isBoxSelecting || _isLeftPointerDown)
        {
            return;
        }

        var hit = HitTestForHoverDebug(_hoverDebugPendingPointer, _hoverDebugPendingRegularOnlyMode);
        var changed = hit.kind != _hoverDebugHit.kind ||
                      hit.idOrIndex != _hoverDebugHit.idOrIndex ||
                      _hoverDebugPendingRegularOnlyMode != _hoverDebugRegularOnlyMode ||
                      !_hoverDebugVisible;

        _hoverDebugHit = hit;
        _hoverDebugRegularOnlyMode = _hoverDebugPendingRegularOnlyMode;
        _hoverDebugVisible = hit.kind != HitKind.None;
        if (changed)
        {
            RequestVisualRefresh();
        }
    }

    private HoverDebugSnapshot? BuildHoverDebugSnapshot()
    {
        if (!_hoverDebugVisible || _hoverDebugHit.kind == HitKind.None)
        {
            return null;
        }

        if (_hoverDebugHit.kind == HitKind.Regular)
        {
            var regular = TryGetRegularPadById(_hoverDebugHit.idOrIndex);
            if (regular is null)
            {
                return null;
            }

            var cadId = regular.MatchedCadPadId ?? ResolveBestCadIdByRegular(regular.Index);
            var text = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"REG {regular.Index} | CAD {(cadId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-")} | Diff {FormatHoverDiff(regular.DiffIndex)}");
            return new HoverDebugSnapshot(regular.Centroid, text, IsRegularMode: true);
        }

        var cad = TryGetCadPadById(_hoverDebugHit.idOrIndex);
        if (cad is null)
        {
            return null;
        }

        var bestRegular = ResolveBestRegularByCad(cad.Id);
        var regularId = bestRegular?.Index;
        var diffText = bestRegular is null ? "-" : FormatHoverDiff(bestRegular.DiffIndex);
        var cadText = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"CAD {cad.Id} | REG {(regularId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-")} | Diff {diffText}");
        return new HoverDebugSnapshot(cad.Centroid, cadText, IsRegularMode: false);
    }

    private CadPad? TryGetCadPadById(int cadPadId)
    {
        if (CadPads is not { Count: > 0 })
        {
            return null;
        }

        _cadPadById ??= CadPads.ToDictionary(static pad => pad.Id);
        return _cadPadById.TryGetValue(cadPadId, out var pad)
            ? pad
            : null;
    }

    private RegularPad? TryGetRegularPadById(int regularPadId)
    {
        if (RegularPads is not { Count: > 0 })
        {
            return null;
        }

        _regularPadById ??= RegularPads.ToDictionary(static pad => pad.Index);
        return _regularPadById.TryGetValue(regularPadId, out var pad)
            ? pad
            : null;
    }

    private RegularPad? ResolveBestRegularByCad(int cadPadId)
    {
        if (!_matchLinksByCadId.TryGetValue(cadPadId, out var links) || links.Count == 0)
        {
            return null;
        }

        PadMatchLink bestLink = links[0];
        for (var i = 1; i < links.Count; i++)
        {
            var candidate = links[i];
            if (candidate.CadCoverage > bestLink.CadCoverage ||
                (Math.Abs(candidate.CadCoverage - bestLink.CadCoverage) < 1e-12 &&
                 candidate.OverlapArea > bestLink.OverlapArea))
            {
                bestLink = candidate;
            }
        }

        return TryGetRegularPadById(bestLink.RegularPadId);
    }

    private int? ResolveBestCadIdByRegular(int regularPadId)
    {
        if (!_matchLinksByRegularId.TryGetValue(regularPadId, out var links) || links.Count == 0)
        {
            return null;
        }

        PadMatchLink bestLink = links[0];
        for (var i = 1; i < links.Count; i++)
        {
            var candidate = links[i];
            if (candidate.RegularCoverage > bestLink.RegularCoverage ||
                (Math.Abs(candidate.RegularCoverage - bestLink.RegularCoverage) < 1e-12 &&
                 candidate.OverlapArea > bestLink.OverlapArea))
            {
                bestLink = candidate;
            }
        }

        return bestLink.CadPadId;
    }

    private static string FormatHoverDiff(int diffIndex)
    {
        return diffIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
