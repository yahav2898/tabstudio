namespace TabStudio.Web.Tests;

/// <summary>Files copied next to the tests from <c>TestData</c>: what the .NET SDK produced for the templates.</summary>
internal static class TestData
{
    public static string PathOf(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", relativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string Read(string relativePath) => File.ReadAllText(PathOf(relativePath));
}
