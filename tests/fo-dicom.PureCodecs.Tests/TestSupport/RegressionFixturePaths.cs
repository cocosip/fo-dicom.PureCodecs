namespace FellowOakDicom.PureCodecs.Tests.TestSupport;

internal static class RegressionFixturePaths
{
    public static string LocalReal1 => Resolve("Regression", "LocalReal", "1.dcm");

    public static string Transcoded(string fileName)
    {
        return Resolve("Regression", "Transcoded", fileName);
    }

    public static IReadOnlyList<string> InteropFixtures()
    {
        return Enumerable.Range(1, 10)
            .Select(index => Resolve("Regression", "Interop", $"sample-{index:D2}.dcm"))
            .ToArray();
    }

    public static string Jpeg2000Baseline(string fileName)
    {
        return Resolve("Regression", "Jpeg2000Baseline", fileName);
    }

    private static string Resolve(params string[] parts)
    {
        var pathParts = new string[parts.Length + 3];
        pathParts[0] = AppContext.BaseDirectory;
        pathParts[1] = "TestSupport";
        pathParts[2] = "Fixtures";
        Array.Copy(parts, 0, pathParts, 3, parts.Length);
        var path = Path.Combine(pathParts);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Regression fixture was not copied to the test output directory.", path);
        }

        return path;
    }
}
