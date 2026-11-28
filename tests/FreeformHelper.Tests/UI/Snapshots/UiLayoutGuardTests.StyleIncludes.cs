// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.RegularExpressions;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class UiLayoutGuardTests
{
    private static string ReadStylesWithIncludes(string path)
    {
        return Regex.Replace(
            TestFiles.ReadContent(path),
            @"<StyleInclude\s+Source=""(?<source>[^""]+)""\s*/>",
            match =>
            {
                const string prefix = "avares://FreeformHelper.UI/Styles/";
                var source = match.Groups["source"].Value;
                Assert.StartsWith(prefix, source, StringComparison.Ordinal);
                var includePath = Path.Combine(Path.GetDirectoryName(path)!, source[prefix.Length..]);
                return ReadStylesWithIncludes(includePath);
            });
    }
}
