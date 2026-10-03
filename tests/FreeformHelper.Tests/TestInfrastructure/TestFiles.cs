namespace FreeformHelper.Tests.TestInfrastructure;

internal static class TestFiles
{
    public static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        return path;
    }
}
