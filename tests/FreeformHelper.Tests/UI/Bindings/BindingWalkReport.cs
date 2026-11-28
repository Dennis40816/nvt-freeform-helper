// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Xml;
using System.Xml.Linq;

namespace FreeformHelper.Tests.UI.Bindings;

internal enum BindingCoverage
{
    Checked,
    Failed,
    Uncovered,
}

internal sealed record BindingSite(string File, XElement Owner, XObject Syntax, string Target, string Text, int? SourceLine = null)
{
    public int Line => SourceLine ?? ((IXmlLineInfo)Syntax).LineNumber;
    public string Location => $"{File}:{Line} {Owner.Name.LocalName}.{Target}";
    public BindingMarkup Read() => Syntax is XElement element
        ? BindingMarkup.FromElement(element) : BindingMarkup.Parse(Text);
}

internal sealed record BindingFinding(BindingSite Site, BindingCoverage Coverage, string Source, string Path, string Segment, string Reason)
{
    public override string ToString() =>
        $"{Site.Location} | {Coverage} | source: {Source} | path: {Path} | segment: {Segment} | {Reason}";
}

internal sealed class BindingWalkException(BindingCoverage coverage, string segment, string message) : Exception(message)
{
    public BindingCoverage Coverage { get; } = coverage;
    public string Segment { get; } = segment;

    public static BindingWalkException Uncovered(string segment, string reason) => new(BindingCoverage.Uncovered, segment, reason);
    public static BindingWalkException Failed(string segment, string reason) => new(BindingCoverage.Failed, segment, reason);
}

internal sealed class BindingWalkReport
{
    // A walker and its report are confined to the calling test thread.
    private readonly Dictionary<BindingSite, List<BindingFinding>> _results = [];

    public IReadOnlyList<BindingFinding> Findings => _results.Values.SelectMany(static values => values)
        .Where(static finding => finding.Coverage != BindingCoverage.Checked).Distinct().ToArray();
    public int SiteCount => _results.Count;
    public int CheckedCount => _results.Count(static pair => pair.Value.All(static value => value.Coverage == BindingCoverage.Checked));
    public int UncoveredCount => _results.Count(static pair => pair.Value.Any(static value => value.Coverage == BindingCoverage.Uncovered));
    public int FailedCount => _results.Count(static pair => pair.Value.Any(static value => value.Coverage == BindingCoverage.Failed)
        && pair.Value.All(static value => value.Coverage != BindingCoverage.Uncovered));
    public bool IsComplete => SiteCount > 0 && Findings.Count == 0;

    public void Add(BindingFinding finding)
    {
        if (!_results.TryGetValue(finding.Site, out var values))
        {
            values = [];
            _results.Add(finding.Site, values);
        }
        values.Add(finding);
    }

    public void AccountFor(IEnumerable<BindingSite> sites)
    {
        foreach (var site in sites.Where(site => !_results.ContainsKey(site)))
        {
            Add(new BindingFinding(site, BindingCoverage.Uncovered, "unknown", site.Text, "context",
                "No instantiated context reached this site (including unused resources and lazy surfaces)."));
        }
    }

    public string Format() => $"Binding sites: {SiteCount}; checked: {CheckedCount}; failed: {FailedCount}; " +
        $"uncovered syntax/context: {UncoveredCount}." + Environment.NewLine +
        string.Join(Environment.NewLine, Findings.OrderBy(static value => value.Site.File, StringComparer.Ordinal)
            .ThenBy(static value => value.Site.Line).Select(static value => value.ToString()));
}
