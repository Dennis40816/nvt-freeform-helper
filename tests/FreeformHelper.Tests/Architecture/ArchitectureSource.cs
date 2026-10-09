using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeformHelper.Tests.Architecture;

// This is a lexical source guard, not a semantic C# compiler. It scans checked-in source,
// including inactive preprocessor branches, and never generated bin/obj output.
internal sealed record ArchitectureSource(string Path, string Text)
{
    public string Code { get; } = Mask(Text);

    public static Regex Pattern(string pattern) => new(pattern, RegexOptions.CultureInvariant);

    // Preserve offsets/newlines. Mask comments and literal text, but retain executable
    // interpolation expressions so $"{DateTime.Now}" cannot evade a counted API.
    // This handles ordinary/verbatim/raw strings, escaped braces and nested expressions.
    public static string Mask(string text)
    {
        char[] code = text.Select(static c => c is '\r' or '\n' ? c : ' ').ToArray();
        int ScanCode(int offset, bool interpolation = false)
        {
            int depth = 0;
            while (offset < text.Length)
            {
                char c = text[offset];
                if (interpolation && c == '}' && depth == 0)
                {
                    return offset;
                }

                if (text.AsSpan(offset).StartsWith("//", StringComparison.Ordinal))
                {
                    int end = text.IndexOf('\n', offset);
                    offset = end < 0 ? text.Length : end;
                    continue;
                }

                if (text.AsSpan(offset).StartsWith("/*", StringComparison.Ordinal))
                {
                    int end = text.IndexOf("*/", offset + 2, StringComparison.Ordinal);
                    offset = end < 0 ? text.Length : end + 2;
                    continue;
                }

                int quote = offset;
                while (quote < text.Length && text[quote] is '$' or '@')
                {
                    quote++;
                }

                if (quote < text.Length && (text[quote] == '"' || (quote == offset && c == '\'')))
                {
                    offset = ScanString(offset, quote);
                    continue;
                }

                code[offset] = c;
                depth += c == '{' ? 1 : c == '}' ? -1 : 0;
                offset++;
            }

            return offset;
        }

        int ScanString(int start, int quote)
        {
            char delimiter = text[quote];
            bool verbatim = text.AsSpan(start, quote - start).Contains('@');
            int dollars = text.AsSpan(start, quote - start).Count('$');
            int quotes = 1;
            while (delimiter == '"' && quote + quotes < text.Length && text[quote + quotes] == '"')
            {
                quotes++;
            }

            bool raw = quotes >= 3;
            int offset = quote + (raw ? quotes : 1);
            while (offset < text.Length)
            {
                if (text[offset] == delimiter)
                {
                    int run = 1;
                    while (offset + run < text.Length && text[offset + run] == delimiter)
                    {
                        run++;
                    }

                    if (raw ? run >= quotes : !verbatim || run == 1)
                    {
                        return offset + (raw ? quotes : 1);
                    }

                    offset += raw ? run : 2;
                }
                else if (!raw && !verbatim && text[offset] == '\\')
                {
                    offset += 2;
                }
                else if (dollars > 0 && text[offset] == '{')
                {
                    int run = 1;
                    while (offset + run < text.Length && text[offset + run] == '{')
                    {
                        run++;
                    }

                    if (!raw && run >= 2)
                    {
                        offset += 2;
                    }
                    else if (!raw || run >= dollars)
                    {
                        offset = ScanCode(offset + (raw ? dollars : 1), interpolation: true) + (raw ? dollars : 1);
                    }
                    else
                    {
                        offset += run;
                    }
                }
                else
                {
                    offset++;
                }
            }

            return offset;
        }

        ScanCode(0);
        return new string(code);
    }

    public string Location(int offset) => $"{Path}:{Text.AsSpan(0, offset).Count('\n') + 1}";

    public string[] Find(string pattern, string? owner = null) => Pattern(pattern).Matches(Code)
        .Where(match => owner is null || IsDirectMember(match.Index, owner))
        .Select(match => Location(match.Index)).ToArray();

    public static ArchitectureSource[] Read(string repoRoot, string extension, string folder = "src") =>
        Directory.EnumerateFiles(System.IO.Path.Combine(repoRoot, folder), "*" + extension, SearchOption.AllDirectories)
            .Select(path => System.IO.Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Where(static path => !path.Split('/').Any(static part => part is "bin" or "obj" or "build"))
            .Order(StringComparer.Ordinal)
            .Select(path => new ArchitectureSource(path, File.ReadAllText(System.IO.Path.Combine(repoRoot, path))))
            .ToArray();

    // Each variable declarator is one storage slot. Readonly dependencies/collections count;
    // instanceOnly excludes constants/static fields; events, properties and generated members never count.
    // An ObservableProperty field counts once, regardless of the generated public property.
    public IEnumerable<(string Name, string Type, string Modifiers, int Offset)> Fields(string? owner = null, bool instanceOnly = true)
    {
        var declarations = Pattern(
            @"(?m)^[ \t]*(?:\[[^\]]*\]\s*)*(?<mods>(?:(?:private|public|internal|protected|static|readonly|const|volatile|new|unsafe)\s+)+)" +
            @"(?<type>(?:[\w:.?\[\]\s]|[<(](?<depth>)|[>)](?<-depth>)|,(?(depth)|(?!)))+?)" +
            @"(?(depth)(?!))\s+(?<name>@?\w+)\s*(?=[;,]|=(?!>))");
        foreach (Match match in declarations.Matches(Code))
        {
            if (owner is not null && !IsDirectMember(match.Index, owner))
            {
                continue;
            }

            string modifiers = match.Groups["mods"].Value;
            if ((instanceOnly && Pattern(@"\b(?:static|const)\b").IsMatch(modifiers)) ||
                match.Groups["type"].Value.TrimStart().StartsWith("event ", StringComparison.Ordinal))
            {
                continue;
            }

            var name = match.Groups["name"];
            yield return (name.Value, match.Groups["type"].Value.Trim(), modifiers, name.Index);
            // Additional declarators after top-level commas; ignore commas in type arguments,
            // calls, collection/object initializers and lambdas in the field initializer.
            // A "<" opens a generic argument list only when it touches the preceding identifier;
            // a spaced "<" or ">" is a comparison and must not hide the next declarator.
            var open = new Stack<char>();
            for (int i = name.Index + name.Length; i < Code.Length; i++)
            {
                char c = Code[i];
                if (c == ';' && open.Count == 0)
                {
                    break;
                }

                if (c is '(' or '[' or '{')
                {
                    open.Push(c);
                }
                else if (c == '<' && i > 0 && (char.IsLetterOrDigit(Code[i - 1]) || Code[i - 1] == '_'))
                {
                    open.Push('<');
                }
                else if (c is ')' or ']' or '}')
                {
                    if (open.Count > 0)
                    {
                        open.Pop();
                    }
                }
                else if (c == '>' && open.Count > 0 && open.Peek() == '<' && Code[i - 1] != '=')
                {
                    open.Pop();
                }
                else if (c == ',' && open.Count == 0)
                {
                    var next = Pattern(@"\G\s*(?<name>@?\w+)\s*(?=[;,]|=(?!>))").Match(Code, i + 1);
                    if (next.Success)
                    {
                        var variable = next.Groups["name"];
                        yield return (variable.Value, match.Groups["type"].Value.Trim(), modifiers, variable.Index);
                    }
                }
            }
        }
    }

    private bool IsDirectMember(int offset, string owner)
    {
        foreach (Match type in Pattern(@"\bclass\s+" + Regex.Escape(owner) + @"\b[^{}]*\{").Matches(Code))
        {
            int depth = 1;
            int start = type.Index + type.Length;
            for (int i = start; i < offset && depth > 0; i++)
            {
                depth += Code[i] == '{' ? 1 : Code[i] == '}' ? -1 : 0;
            }

            if (start <= offset && depth == 1)
            {
                return true;
            }
        }

        return false;
    }

    // Both using directives (including aliases/static imports) and qualified names count.
    // The boundary excludes Nvt.Core.Avalonia from the Avalonia framework rule.
    // Inside the FreeformHelper.UI assembly a namespace can also be written relative to the enclosing
    // namespace, for example "ViewModels.ShellViewModel" in FreeformHelper.UI.Controls; that counts too.
    public bool References(string ns)
    {
        if (Pattern(@"(?<![\w.])(?:global::)?" + Regex.Escape(ns) + @"\b").IsMatch(Code))
        {
            return true;
        }

        const string uiPrefix = "FreeformHelper.UI.";
        return ns.StartsWith(uiPrefix, StringComparison.Ordinal) &&
               Pattern(@"(?<![\w.])" + Regex.Escape(ns[uiPrefix.Length..]) + @"\s*\.\s*[A-Z]").IsMatch(Code);
    }

    public bool CallsPlatformIo()
    {
        string code = Code;
        foreach (Match alias in Pattern(@"\busing\s+(\w+)\s*=\s*(?:global::)?([\w.]+)\s*;").Matches(Code))
        {
            code = Pattern(@"\b" + Regex.Escape(alias.Groups[1].Value) + @"\b(?!\s*=)")
                .Replace(code, alias.Groups[2].Value);
        }

        // File/Directory/Process are also available via ordinary/implicit namespace imports.
        // Static imports of these types are conservatively treated as an IO dependency.
        return Pattern(@"(?<![\w.])(?:(?:global::)?System\.(?:IO|Diagnostics)\.)?" +
                       @"(?:File|Directory|Process)\s*\.\s*\w+\s*\(|" +
                       @"\bnew\s+(?:(?:global::)?System\.Diagnostics\.)?Process\s*[({]|" +
                       @"\busing\s+static\s+(?:global::)?System\.(?:IO\.(?:File|Directory)|Diagnostics\.Process)\s*;")
            .IsMatch(code);
    }
}

internal sealed class ArchitectureBaseline
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Dictionary<string, int> Metrics { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string[]> MetricOffenders { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string[]> Layering { get; set; } = new(StringComparer.Ordinal);
    public string[] CompiledBindingExceptions { get; set; } = [];

    public static ArchitectureBaseline Load(string repoRoot) => JsonSerializer.Deserialize<ArchitectureBaseline>(
        File.ReadAllText(System.IO.Path.Combine(repoRoot, "tests/FreeformHelper.Tests/Architecture/debt-baseline.json")),
        JsonOptions)
        ?? throw new InvalidOperationException("Invalid debt-baseline.json.");

    public static string[] CompareFiles(string rule, string[] baseline, string[] actual)
    {
        var added = actual.Except(baseline, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var removed = baseline.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var errors = new List<string>();
        if (added.Length > 0)
        {
            errors.Add($"{rule}: new offenders: {string.Join(", ", added)}");
        }

        if (removed.Length > 0)
        {
            errors.Add($"{rule}: lower the baseline in debt-baseline.json to {actual.Length}; remove: {string.Join(", ", removed)}");
        }

        return errors.ToArray();
    }

    public static XElement XamlRoot(ArchitectureSource source) => XDocument.Parse(source.Text).Root
        ?? throw new InvalidOperationException($"Missing XAML root: {source.Path}");

    public static XNamespace XamlNamespace => "http://schemas.microsoft.com/winfx/2006/xaml";
}
