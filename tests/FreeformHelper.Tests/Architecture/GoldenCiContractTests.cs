// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.Json;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Architecture;

public sealed class GoldenCiContractTests
{
    [Fact]
    public void StructureLaneDoesNotRunShardPolicyHarness()
    {
        using var source = File.OpenText(TestPaths.FromRepo("scripts", "verify.ps1"));

        Assert.DoesNotContain("check-test-shard-policy.ps1", source.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void BaselineUsesSchemaVersionTwo()
    {
        using var baseline = ReadBaseline();

        Assert.Equal(2, baseline.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void BaselineOmitsGlobalExampleData()
    {
        using var baseline = ReadBaseline();

        Assert.False(baseline.RootElement.TryGetProperty("exampleData", out _));
    }

    [Fact]
    public void BaselineContainsEveryCiShard()
    {
        using var baseline = ReadBaseline();

        Assert.Equal(["core", "snapshots", "ui", "viewmodel"],
            baseline.RootElement.GetProperty("shards").EnumerateObject().Select(static shard => shard.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("core")]
    [InlineData("ui")]
    [InlineData("viewmodel")]
    [InlineData("snapshots")]
    public void BaselineShardDisallowsSkips(string shard)
    {
        using var baseline = ReadBaseline();

        Assert.Equal(0, baseline.RootElement.GetProperty("shards").GetProperty(shard).GetProperty("maxSkipped").GetInt32());
    }

    [Theory]
    [InlineData("core", 11)]
    [InlineData("ui", 0)]
    [InlineData("viewmodel", 15)]
    [InlineData("snapshots", 0)]
    public void BaselineShardContainsMeasuredExampleFloor(string shard, int expected)
    {
        using var baseline = ReadBaseline();

        Assert.Equal(expected, baseline.RootElement.GetProperty("shards").GetProperty(shard).GetProperty("exampleMinTests").GetInt32());
    }

    [Fact]
    public void CoreShardRequiresGoldenClasses()
    {
        using var baseline = ReadBaseline();

        Assert.Equal(
            ["FreeformHelper.Tests.NotchExampleCExportDriftTests", "FreeformHelper.Tests.NotchGoldenBaselineTests", "FreeformHelper.Tests.Tm81NotchAcceptanceMatrixTests"],
            baseline.RootElement.GetProperty("shards").GetProperty("core").GetProperty("requiredClasses")
                .EnumerateArray().Select(static name => name.GetString()));
    }

    [Theory]
    [InlineData("ui")]
    [InlineData("viewmodel")]
    [InlineData("snapshots")]
    public void OtherShardsHaveNoRequiredGoldenClasses(string shard)
    {
        using var baseline = ReadBaseline();

        Assert.Empty(baseline.RootElement.GetProperty("shards").GetProperty(shard).GetProperty("requiredClasses").EnumerateArray());
    }

    [Fact]
    public void BaselineFloorsArePositive()
    {
        using var baseline = ReadBaseline();

        Assert.All(baseline.RootElement.GetProperty("shards").EnumerateObject(),
            static shard => AssertPositiveFloor(shard.Value.GetProperty("minTotal").GetInt32()));
    }

    private static JsonDocument ReadBaseline()
    {
        using var stream = File.OpenRead(TestPaths.FromRepo("scripts", "ci", "test-baseline.json"));
        return JsonDocument.Parse(stream);
    }

    private static void AssertPositiveFloor(int floor) => Assert.True(floor >= 1);
}
