// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Xml.Linq;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed record BindingMarkup(string Kind, IReadOnlyDictionary<string, string> Arguments)
{
    public string Path => Get("Path") ?? string.Empty;

    public string? Get(string name) => Arguments.GetValueOrDefault(name);

    public static BindingMarkup Parse(string text)
    {
        text = text.Trim();
        if (!text.StartsWith('{') || !text.EndsWith('}'))
        {
            throw BindingWalkException.Uncovered(text, "Expected a markup extension.");
        }

        var body = text[1..^1].Trim();
        var end = body.IndexOfAny([' ', '\t', '\r', '\n', ',']);
        var kind = end < 0 ? body : body[..end];
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in Split(end < 0 ? string.Empty : body[(end + 1)..], ','))
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            var pair = Split(part, '=');
            var key = pair.Count == 1 ? PositionalKey(kind) : pair[0].Trim();
            if (pair.Count > 2 || !arguments.TryAdd(key, Unquote(pair[^1].Trim())))
            {
                throw BindingWalkException.Uncovered(part, "Duplicate or unsupported markup argument.");
            }
        }

        return new BindingMarkup(kind, arguments);
    }

    public static BindingMarkup FromElement(XElement element)
    {
        var arguments = element.Attributes().Where(static attribute => !attribute.IsNamespaceDeclaration)
            .ToDictionary(static attribute => attribute.Name.LocalName, static attribute => attribute.Value, StringComparer.Ordinal);
        foreach (var property in element.Elements())
        {
            if (element.Name.LocalName == "MultiBinding" &&
                (property.Name.LocalName.EndsWith("Binding", StringComparison.Ordinal) || property.Name.LocalName == "MultiBinding.Bindings"))
            {
                continue;
            }
            var key = property.Name.LocalName.Split('.')[^1];
            var value = property.Elements().FirstOrDefault();
            if (value is null || property.Elements().Skip(1).Any())
            {
                throw BindingWalkException.Uncovered(key, "Object property needs one explicit source.");
            }

            arguments.Add(key, "{" + value.Name.LocalName + " " + string.Join(", ",
                value.Attributes().Select(static attribute => attribute.Name.LocalName + "=" + attribute.Value)) + "}");
        }

        return new BindingMarkup(element.Name.LocalName, arguments);
    }

    public static IReadOnlyList<string> Split(string text, char separator)
    {
        var parts = new List<string>();
        var brackets = new Stack<char>();
        var quote = '\0';
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || text[index - 1] != '\\'))
                {
                    quote = '\0';
                }
                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current is '{' or '(' or '[')
            {
                brackets.Push(current);
            }
            else if (current is '}' or ')' or ']')
            {
                var expected = current switch { '}' => '{', ')' => '(', _ => '[' };
                if (!brackets.TryPop(out var opening) || opening != expected)
                {
                    throw BindingWalkException.Uncovered(text, "Unbalanced binding syntax.");
                }
            }
            else if (current == separator && brackets.Count == 0)
            {
                parts.Add(text[start..index].Trim());
                start = index + 1;
            }
        }

        if (brackets.Count != 0 || quote != '\0')
        {
            throw BindingWalkException.Uncovered(text, "Unterminated binding syntax.");
        }
        parts.Add(text[start..].Trim());
        return parts;
    }

    public void RequireSupportedArguments()
    {
        string[] supported = ["Path", "Mode", "ElementName", "RelativeSource", "Source", "Converter", "ConverterParameter",
            "StringFormat", "FallbackValue", "TargetNullValue", "UpdateSourceTrigger", "Priority", "Delay", "DataType"];
        var unknown = Arguments.Keys.FirstOrDefault(key => !supported.Contains(key, StringComparer.Ordinal));
        if (unknown is not null)
        {
            throw BindingWalkException.Uncovered(unknown, "Unknown binding option.");
        }
    }

    private static string PositionalKey(string kind) => kind switch
    {
        "RelativeSource" => "Mode",
        "StaticResource" or "DynamicResource" => "Key",
        "x:Type" => "Type",
        "x:Static" => "Member",
        _ => "Path",
    };

    private static string Unquote(string text) => text.Length >= 2 && text[0] is '\'' or '"' && text[^1] == text[0]
        ? text[1..^1] : text;
}
