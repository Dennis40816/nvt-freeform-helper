// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>The one place where the tests read or set a process environment variable.</summary>
internal static class TestEnvironment
{
    public static string? Get(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }

    public static void Set(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
    }
}
