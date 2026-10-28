using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Toolchains.Testing;

/// <summary>
/// Reads JUnit XML test reports, the format PHPUnit (<c>--log-junit</c>), Gradle and Maven's Surefire
/// (<c>build/test-results</c>, <c>target/surefire-reports</c>) and SwiftPM (<c>--xunit-output</c>) write.
/// </summary>
public static class JUnitReport
{
    private const int MaxMessage = 8000;

    /// <summary>The results in a report.</summary>
    /// <param name="xml">The report's text.</param>
    /// <exception cref="XmlException">The report is not XML.</exception>
    public static IReadOnlyList<ToolchainTestResult> Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        var document = XDocument.Load(reader);
        var results = new List<ToolchainTestResult>();
        foreach (var testCase in document.Descendants("testcase"))
        {
            var rawName = (string?)testCase.Attribute("name") ?? string.Empty;
            var className = ClassNameOf(testCase);
            if (rawName.Length == 0 && className.Length == 0)
            {
                continue;
            }

            var name = NormalizeName(rawName);
            var failure = testCase.Element("failure");
            var error = testCase.Element("error");
            var skipped = testCase.Element("skipped");
            var outcome = failure is not null ? ToolchainTestOutcome.Failed
                : error is not null ? ToolchainTestOutcome.Errored
                : skipped is not null ? ToolchainTestOutcome.Skipped
                : ToolchainTestOutcome.Passed;
            var message = MessageOf(failure ?? error ?? skipped);
            var seconds = double.TryParse((string?)testCase.Attribute("time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var time) && time is >= 0 and < 1e7 ? time : 0;
            var line = int.TryParse((string?)testCase.Attribute("line"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : (int?)null;
            results.Add(new ToolchainTestResult(className, MethodNameOf(name), name, outcome, TimeSpan.FromSeconds(seconds), message,
                (string?)testCase.Attribute("file") is { Length: > 0 } file ? file : null, line));
        }

        return results;
    }

    /// <summary>
    /// A test's name as Test Explorer shows it: JUnit 5 and Swift Testing end a method's name with its parameter
    /// list, <c>adds()</c>, which the test's source does not.
    /// </summary>
    /// <param name="name">The name in the report.</param>
    public static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        name = name.Trim();
        return name.EndsWith("()", StringComparison.Ordinal) ? name[..^2] : name;
    }

    /// <summary>
    /// The method a test's name is of: a PHPUnit data set (<c>testAdd with data set #0</c>), a JUnit parameterized
    /// invocation (<c>adds(int)[1]</c>) or a Swift Testing argument list, without them.
    /// </summary>
    /// <param name="name">The test's name, normalized.</param>
    public static string MethodNameOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var end = name.IndexOf(" with data set ", StringComparison.Ordinal);
        if (end < 0)
        {
            end = name.IndexOfAny(['(', '[']);
        }

        return end > 0 ? name[..end].TrimEnd() : name;
    }

    private static string ClassNameOf(XElement testCase)
    {
        // PHPUnit writes the class with its namespace's backslashes in "class", and dots in "classname".
        var name = (string?)testCase.Attribute("class") ?? (string?)testCase.Attribute("classname")
            ?? (string?)testCase.Parent?.Attribute("name") ?? string.Empty;
        return name.Replace('\\', '.').Trim();
    }

    private static string? MessageOf(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var attribute = ((string?)element.Attribute("message"))?.Trim();
        var text = element.Value.Trim();
        var message = string.IsNullOrEmpty(text) ? attribute
            : string.IsNullOrEmpty(attribute) || text.Contains(attribute, StringComparison.Ordinal) ? text
            : attribute + "\n" + text;
        if (string.IsNullOrEmpty(message) || message == "failure")
        {
            return message is null ? null : "The test failed.";
        }

        return message.Length > MaxMessage ? message[..MaxMessage] + "…" : message;
    }
}
