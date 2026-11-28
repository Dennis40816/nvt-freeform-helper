// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nvt.Core.Avalonia.Panels;

namespace FreeformHelper.UI.Views;

internal sealed class LeftPanelNavigation
{
    private const double CompactWindowWidth = 1366;
    private static readonly ConditionalWeakTable<object, Session> _sessions = new();
    private readonly Control _owner;
    private readonly Grid _layout;
    private readonly Border _panel;
    private readonly LeftPanelRail _rail;
    private readonly LeftDxfPanel _sections;
    private Session? _appliedSession;

    public LeftPanelNavigation(Control owner, Grid layout, Border panel, LeftPanelRail rail, LeftDxfPanel sections)
    {
        _owner = owner;
        _layout = layout;
        _panel = panel;
        _rail = rail;
        _sections = sections;
        owner.LayoutUpdated += OnLayoutUpdated;
        rail.SectionRequested += OnSectionRequested;
    }

    public void SetVisible(bool isVisible)
    {
        var session = GetSession();
        session.IsVisible = isVisible;
        ApplyVisibility(session);
    }

    private Session GetSession() => _sessions.GetValue(_owner.DataContext ?? _owner, static _ => new Session());

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var session = GetSession();
        if (ReferenceEquals(_appliedSession, session))
        {
            return;
        }

        var window = TopLevel.GetTopLevel(_owner);
        if (window is null || window.Bounds.Width <= 0)
        {
            return;
        }

        session.IsVisible ??= window.Bounds.Width > CompactWindowWidth;
        ApplyVisibility(session);
    }

    private void ApplyVisibility(Session session)
    {
        var column = _layout.ColumnDefinitions[0];
        if (session.IsVisible == false && _panel.IsVisible)
        {
            session.ExpandedWidth = column.Width;
        }

        column.Width = session.IsVisible == true
            ? session.ExpandedWidth ?? (GridLength)_owner.FindResource("PanelLeftWidthGrid")!
            : GridLength.Auto;
        _panel.IsVisible = session.IsVisible == true;
        _rail.IsVisible = !_panel.IsVisible;
        _appliedSession = session;
    }

    private void OnSectionRequested(object? sender, LeftPanelSection section)
    {
        SetVisible(true);
        if (section == LeftPanelSection.Expand)
        {
            _panel.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("panelChromeToggle")).Focus();
            return;
        }

        var title = section switch
        {
            LeftPanelSection.Dxf => "Import & DXF check",
            LeftPanelSection.Layers => "Layers",
            _ => "DXF edits",
        };
        var target = _sections.GetVisualDescendants().OfType<CollapsiblePanel>().Single(panel => panel.Title == title);
        target.IsExpanded = true;
        _owner.Dispatcher.Post(() =>
        {
            if (!_panel.IsEffectivelyVisible) return;
            var header = target.GetVisualDescendants().OfType<ToggleButton>().Single(button =>
                button.Classes.Contains("panelBlockHeader") && button.FindAncestorOfType<CollapsiblePanel>() == target);
            header.BringIntoView();
            header.Focus();
        }, DispatcherPriority.Loaded);
    }

    private sealed class Session
    {
        public bool? IsVisible { get; set; }
        public GridLength? ExpandedWidth { get; set; }
    }
}
