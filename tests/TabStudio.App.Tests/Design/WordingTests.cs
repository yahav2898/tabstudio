using System.Text.RegularExpressions;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// User-facing sentences read from the source: an exception's message already ends with a period, so a message that
/// puts one after it reads "The connection closed.." (Java debugging, project setup, backing services and analyzers
/// did); and "cancelled" is spelled one way.
/// </summary>
public sealed partial class WordingTests
{
    [Fact]
    public void No_message_puts_a_period_after_an_exceptions_own()
    {
        var offenders = SourceFiles()
            .SelectMany(file => File.ReadAllLines(file).Select((line, index) => (file, line, index)))
            .Where(entry => PeriodAfterMessage().IsMatch(entry.line))
            .Select(entry => $"{Path.GetRelativePath(Root(), entry.file)}:{entry.index + 1}: {entry.line.Trim()}")
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Cancelled_is_spelled_one_way()
    {
        var offenders = SourceFiles().Where(file => File.ReadAllText(file).Contains("\"Canceled.\"", StringComparison.Ordinal)).ToList();

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}Templates{Path.DirectorySeparatorChar}Content{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TabStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root, with TabStudio.slnx, was not found.");
    }

    [GeneratedRegex(@"\{(ex|e|exception|error|failure)\.Message\}\.", RegexOptions.CultureInvariant)]
    private static partial Regex PeriodAfterMessage();
}
