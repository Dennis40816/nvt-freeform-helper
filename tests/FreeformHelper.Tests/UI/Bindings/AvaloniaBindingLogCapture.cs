// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed class AvaloniaBindingLogCapture : ILogSink, IDisposable
{
    // Logger.Sink is scoped by HeadlessUiSerial; callbacks and phase labels are protected by _gate.
    private readonly Lock _gate = new();
    private readonly ILogSink? _previous = Logger.Sink;
    private readonly List<BindingLogEvent> _events = [];
    private string _phase = "construction";

    public AvaloniaBindingLogCapture()
    {
        Logger.Sink = this;
    }

    public string Phase
    {
        set
        {
            lock (_gate)
            {
                _phase = value;
            }
        }
    }

    public IReadOnlyList<BindingLogEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    public IReadOnlyList<BindingLogEvent> Errors => Events.Where(static entry => entry.Level >= LogEventLevel.Warning).ToArray();

    public bool IsEnabled(LogEventLevel level, string area) => area is "Binding" or "Property" || _previous?.IsEnabled(level, area) == true;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Capture(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        Capture(level, area, source, messageTemplate, propertyValues);

    public string Format(BindingDocuments documents) => string.Join(Environment.NewLine, Errors.Select(entry =>
    {
        var candidates = documents.Sites.Where(site =>
            (site.Owner.Name.LocalName == entry.ControlType || site.Owner.Name.LocalName == "Setter") &&
            (entry.Target == "unknown" || site.Target == entry.Target)).Select(static site => site.Location).Distinct().ToArray();
        var mapping = candidates.Length == 1 ? "candidate: " : "unmapped; candidates: ";
        return $"{entry.Phase}: {entry.Level} {entry.Area} {entry.ControlType}#{entry.ControlName}.{entry.Target}; " +
            $"template: {entry.Template}; {mapping}{string.Join(", ", candidates)}";
    }));

    public void Dispose() => Logger.Sink = _previous;

    private void Capture(LogEventLevel level, string area, object? source, string template, object?[] values)
    {
        if (area == "Binding" || (area == "Property" && template.Contains("binding", StringComparison.OrdinalIgnoreCase)))
        {
            var property = values.OfType<AvaloniaProperty>().FirstOrDefault();
            lock (_gate)
            {
                _events.Add(new BindingLogEvent(_phase, level, area, source?.GetType().Name ?? "unknown",
                    (source as Control)?.Name ?? "unnamed", property?.Name ?? PropertyName(template, values), template));
            }
            // Do not interpolate arguments: they can contain private project values or source-object dumps.
            return;
        }
        if (_previous?.IsEnabled(level, area) == true)
        {
            _previous.Log(level, area, source, template, values);
        }
    }

    private static string PropertyName(string template, object?[] values)
    {
        var placeholders = Regex.Matches(template, @"\{(?<name>\w+)\}", RegexOptions.CultureInvariant);
        for (var index = 0; index < placeholders.Count && index < values.Length; index++)
        {
            if (placeholders[index].Groups["name"].Value is "Property" or "TargetProperty" && values[index] is string property &&
                property.All(static character => char.IsLetterOrDigit(character) || character is '_' or '.'))
            {
                return property;
            }
        }
        return "unknown";
    }
}

internal sealed record BindingLogEvent(string Phase, LogEventLevel Level, string Area, string ControlType,
    string ControlName, string Target, string Template);
