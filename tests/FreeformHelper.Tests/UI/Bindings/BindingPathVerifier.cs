// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace FreeformHelper.Tests.UI.Bindings;

internal sealed class BindingPathVerifier(BindingTypeResolver types, Func<BindingFrame, string, BindingValue> controlProperty)
{
    public BindingValue Follow(BindingValue source, string path, XElement location, bool writable = false)
    {
        var negated = path.StartsWith('!');
        path = path.TrimStart('!');
        if (path.Length == 0 || path == ".")
        {
            if (writable)
            {
                throw BindingWalkException.Failed("(source)", "The source object is not a writable property.");
            }
            return source;
        }

        var segments = BindingMarkup.Split(path, '.');
        for (var index = 0; index < segments.Count; index++)
        {
            source = Segment(source, segments[index], location, writable && index == segments.Count - 1);
        }
        if (negated && (Nullable.GetUnderlyingType(source.Type) ?? source.Type) != typeof(bool))
        {
            throw BindingWalkException.Uncovered(path, "Negation of a non-Boolean source relies on the runtime conversion of Avalonia.");
        }
        return negated ? new BindingValue(typeof(bool)) : source;
    }

    public static void RequireInitialized(object instance, string path)
    {
        object? current = instance;
        foreach (var segment in BindingMarkup.Split(path, '.'))
        {
            var property = BindingTypeResolver.Readable(current!.GetType(), segment);
            try
            {
                current = property.GetValue(current);
            }
            catch (TargetInvocationException error)
            {
                throw BindingWalkException.Failed(segment, $"Owning-child getter threw {error.InnerException?.GetType().Name}.");
            }
            if (current is null)
            {
                throw BindingWalkException.Failed(segment, "Initialized owning child is null.");
            }
        }
    }

    private BindingValue Segment(BindingValue source, string segment, XElement location, bool writable)
    {
        if (segment.StartsWith("((", StringComparison.Ordinal))
        {
            return Cast(source, segment, location, writable);
        }
        var bracket = segment.IndexOf('[', StringComparison.Ordinal);
        var member = bracket < 0 ? segment : segment[..bracket];
        if (member.Length > 0)
        {
            if (!IsIdentifier(member))
            {
                throw BindingWalkException.Uncovered(member, "Unsupported path syntax.");
            }
            if (source.Type == typeof(object))
            {
                throw BindingWalkException.Uncovered(member, "Runtime source type is erased; a declared type alone is insufficient.");
            }
            var property = BindingTypeResolver.Readable(source.Type, member);
            if (writable && bracket < 0)
            {
                BindingTypeResolver.RequireWritable(property);
            }
            source = source.Control is { } control ? controlProperty(control, member) : new BindingValue(property.PropertyType);
        }
        return bracket < 0 ? source : Index(source, segment[bracket..], writable);
    }

    private BindingValue Cast(BindingValue source, string segment, XElement location, bool writable)
    {
        var close = segment.IndexOf(')', 2);
        if (close < 0 || !segment.EndsWith(')'))
        {
            throw BindingWalkException.Uncovered(segment, "Unsupported cast syntax.");
        }
        var result = Follow(source, segment[(close + 1)..^1], location, writable);
        var castType = types.Resolve(location, segment[2..close]);
        if (result.Type == typeof(object))
        {
            throw BindingWalkException.Uncovered(segment, "A cast alone does not establish runtime context provenance.");
        }
        if (!castType.IsAssignableFrom(result.Type))
        {
            throw BindingWalkException.Failed(segment, $"Cast to {castType.Name} disagrees with source {result.Type.Name}.");
        }
        return new BindingValue(castType, result.Control);
    }

    private static BindingValue Index(BindingValue source, string syntax, bool writable)
    {
        if (!syntax.EndsWith(']') || syntax.Count(static character => character == '[') != 1)
        {
            throw BindingWalkException.Uncovered(syntax, "Unsupported indexer syntax.");
        }
        var key = syntax[1..^1].Trim();
        var numeric = int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        if (source.Type.IsArray && numeric)
        {
            return new BindingValue(source.Type.GetElementType()!);
        }
        var matches = source.Type.GetProperties().Where(property => property.GetIndexParameters() is { Length: 1 } parameters &&
            parameters[0].ParameterType == (numeric ? typeof(int) : typeof(string))).ToArray();
        if (matches.Length != 1 || matches[0].GetMethod?.IsPublic != true)
        {
            throw BindingWalkException.Uncovered(syntax, "No unique readable integer/string indexer.");
        }
        if (writable)
        {
            BindingTypeResolver.RequireWritable(matches[0]);
        }
        return new BindingValue(matches[0].PropertyType);
    }

    private static bool IsIdentifier(string member) => member.Length > 0 && (char.IsLetter(member[0]) || member[0] == '_') &&
        member.All(static character => char.IsLetterOrDigit(character) || character == '_');
}
