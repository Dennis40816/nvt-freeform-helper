// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Security.Cryptography;
using System.Text.Json;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using Xunit.Sdk;

namespace FreeformHelper.Tests.Architecture;

public sealed class CoreHealthBundleTests
{
    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData(".editorconfig")]
    [InlineData("BannedSymbols.Common.txt")]
    [InlineData("BannedSymbols.Files.txt")]
    [InlineData("BannedSymbols.Tests.txt")]
    [InlineData("schema.json")]
    public void BundleFile_MatchesLock(string name)
    {
        using var manifestStream = File.OpenRead(TestPaths.FromRepo("eng", "core-health.lock.json"));
        using var manifest = JsonDocument.Parse(manifestStream);

        AssertHash(ReadBundleFile(name), manifest.RootElement.GetProperty("files").GetProperty(name).GetString()!);
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData(".editorconfig")]
    [InlineData("BannedSymbols.Common.txt")]
    [InlineData("BannedSymbols.Files.txt")]
    [InlineData("BannedSymbols.Tests.txt")]
    [InlineData("schema.json")]
    public void BundleFile_UsesLf(string name)
    {
        Assert.DoesNotContain((byte)13, ReadBundleFile(name));
    }

    [Fact]
    public void BundleFile_WrongHash_FailsIntegrityGuard()
    {
        Assert.Throws<EqualException>(() => AssertHash("fixture\n"u8.ToArray(), new string('0', 64)));
    }

    private static byte[] ReadBundleFile(string name)
    {
        using var source = File.OpenRead(TestPaths.FromRepo("eng", "core-health", name));
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void AssertHash(byte[] bytes, string expectedHash) =>
        Assert.Equal(expectedHash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
}
