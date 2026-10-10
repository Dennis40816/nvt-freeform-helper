// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Compares text that comes from the private example data. A failure names the label, the size, the hash and the
/// position of the first difference. It never prints the text itself: the log of a public CI run is public.
/// </summary>
internal static class GoldenAssert
{
    public static void TextEqual(string expected, string actual, string label)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        var index = FirstDifference(expected, actual);
        Assert.Fail(
            $"Golden text differs: {label}. Expected {Summarize(expected)}. Actual {Summarize(actual)}. " +
            $"First difference at {Locate(index < expected.Length ? expected : actual, index)}.");
    }

    public static void TextContains(string text, string fragment, string label)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fragment);
        if (text.Contains(fragment, StringComparison.Ordinal))
        {
            return;
        }

        Assert.Fail(
            $"Golden text lacks an expected fragment: {label}. Fragment {Summarize(fragment)}. Text {Summarize(text)}.");
    }

    private static int FirstDifference(string expected, string actual)
    {
        var shared = Math.Min(expected.Length, actual.Length);
        for (var index = 0; index < shared; index++)
        {
            if (expected[index] != actual[index])
            {
                return index;
            }
        }

        return shared;
    }

    private static string Locate(string text, int index)
    {
        var line = 1;
        var lineStart = 0;
        for (var position = 0; position < index && position < text.Length; position++)
        {
            if (text[position] == '\n')
            {
                line++;
                lineStart = position + 1;
            }
        }

        return $"line {line}, column {index - lineStart + 1}";
    }

    private static string Summarize(string text)
    {
        var lines = 1;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return $"{text.Length} characters, {lines} lines, SHA-256 {hash}";
    }
}
