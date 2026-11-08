// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

/// <summary>Fixed English help content; this page does not execute the documented shortcuts.</summary>
public sealed class HowToUseViewModel
{
    public IReadOnlyList<HowToUseGestureRow> CanvasGestures { get; } = Array.AsReadOnly<HowToUseGestureRow>(
    [
        new("Middle mouse drag", "Pan (recommended)."),
        new("Space + Left mouse drag", "Pan (laptop fallback)."),
        new("Mouse wheel", "Zoom around cursor. Ctrl + wheel = faster zoom."),
        new("Shift + Arrow keys", "Fine pan."),
        new("F", "Fit to content. Ctrl + A: Select all visible pads."),
        new("F / Ctrl + A", "work globally when not editing text."),
        new("Left click", "Select pad under cursor."),
        new("Ctrl + click / Shift + click", "Add/remove selection."),
        new("Alt + click", "Prefer regular pad when CAD/regular overlap."),
        new("Drag empty space", "Box select. Ctrl/Shift = add to selection."),
        new("Double click", "Open pad info for the clicked pad."),
        new("Right click", "Open pad info (keeps selection if already selected)."),
        new("Click empty space", "Clear selection."),
    ]);
}

public sealed record HowToUseGestureRow(string Gesture, string Action);
