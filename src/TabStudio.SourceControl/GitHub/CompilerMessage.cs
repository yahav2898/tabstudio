using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// A compiler's message read from a CI annotation or log line: its code, such as <c>CS0103</c>, and its text,
/// with the original kept. Understands MSBuild's <c>file(line,col): error CODE: message [project]</c>,
/// <c>error CODE: message</c>, <c>CODE: message</c>, GCC's and Clang's <c>file:line:col: error: message</c>,
/// and the code in a link to a rule's documentation, as the .NET analyzers write it.
/// </summary>
/// <param name="Code">The code, such as CS0103, or <see langword="null"/> when there is none.</param>
/// <param name="Message">The message without the location, severity and code.</param>
/// <param name="Severity">The severity the text names, such as <c>error</c> or <c>warning</c>, if it names one.</param>
/// <param name="File">The file the text names, if it names one.</param>
/// <param name="Line">The 1-based line the text names, if it names one.</param>
/// <param name="Column">The 1-based column the text names, if it names one.</param>
/// <param name="Original">The text as it was.</param>
public sealed partial record CompilerMessage(string? Code, string Message, string? Severity, string? File, int? Line, int? Column, string Original)
{
    /// <summary>Reads a compiler message from one line of text. Text in no known format is kept as the message.</summary>
    /// <param name="text">The text, such as an annotation's message.</param>
    public static CompilerMessage Parse(string? text)
    {
        var original = text ?? string.Empty;
        var line = FirstLine(original);
        if (MsBuildPattern().Match(line) is { Success: true } msbuild)
        {
            return new CompilerMessage(
                msbuild.Groups["code"].Value,
                msbuild.Groups["message"].Value.Trim(),
                msbuild.Groups["severity"].Value.ToLowerInvariant(),
                msbuild.Groups["file"].Value.Trim(),
                Number(msbuild.Groups["line"]),
                Number(msbuild.Groups["column"]),
                original);
        }

        if (GccPattern().Match(line) is { Success: true } gcc)
        {
            var message = gcc.Groups["message"].Value.Trim();
            var flag = GccFlagPattern().Match(message);
            return new CompilerMessage(
                flag.Success ? flag.Groups["flag"].Value : null,
                flag.Success ? message[..flag.Index].TrimEnd() : message,
                gcc.Groups["severity"].Value.ToLowerInvariant(),
                gcc.Groups["file"].Value,
                Number(gcc.Groups["line"]),
                Number(gcc.Groups["column"]),
                original);
        }

        if (SeverityCodePattern().Match(line) is { Success: true } coded)
        {
            return new CompilerMessage(
                coded.Groups["code"].Value,
                coded.Groups["message"].Value.Trim(),
                coded.Groups["severity"].Success ? coded.Groups["severity"].Value.ToLowerInvariant() : null,
                null,
                null,
                null,
                original);
        }

        var documented = DocumentationCodePattern().Match(line);
        return new CompilerMessage(documented.Success ? documented.Groups["code"].Value.ToUpperInvariant() : null, line.Trim(), null, null, null, null, original);
    }

    /// <summary>
    /// Reads a CI annotation: the raw details when they hold a compiler line, otherwise the message, with a code
    /// from the title when the title is one.
    /// </summary>
    /// <param name="title">The annotation's title.</param>
    /// <param name="message">The annotation's message.</param>
    /// <param name="rawDetails">The annotation's raw details.</param>
    public static CompilerMessage FromAnnotation(string? title, string? message, string? rawDetails)
    {
        var fromMessage = Parse(message);
        if (fromMessage.Code is null && rawDetails is not null && Parse(rawDetails) is { Code: not null } fromDetails)
        {
            return fromDetails with { Original = message ?? rawDetails };
        }

        if (fromMessage.Code is null && title is not null && CodeOnlyPattern().IsMatch(title.Trim()))
        {
            return fromMessage with { Code = title.Trim() };
        }

        return fromMessage;
    }

    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }

    private static int? Number(Group group) =>
        group.Success && int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    // src/App/Program.cs(12,5): error CS0103: The name 'x' does not exist in the current context [/home/runner/work/app/App.csproj]
    [GeneratedRegex(@"^\s*(?<file>[^\s(][^(]*?)\((?<line>\d+)(?:,(?<column>\d+))?(?:,\d+,\d+)?\)\s*:\s*(?<severity>error|warning|info|message)\s+(?<code>[A-Za-z]{1,10}\d{1,6})\s*:\s*(?<message>.*?)(?:\s+\[[^\]]+\])?\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MsBuildPattern();

    // src/main.c:12:5: error: 'x' undeclared [-Werror=implicit]
    [GeneratedRegex(@"^\s*(?<file>[^\s:][^:]*):(?<line>\d+):(?:(?<column>\d+):)?\s*(?<severity>fatal error|error|warning|note):\s*(?<message>.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex GccPattern();

    [GeneratedRegex(@"\s*\[(?<flag>-W[^\]]+)\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex GccFlagPattern();

    // error CS0103: The name 'x' does not exist   or   CS0103: The name 'x' does not exist
    [GeneratedRegex(@"^\s*(?:(?<severity>(?i:error|warning|info))\s+)?(?<code>[A-Z]{1,10}\d{1,6})\s*:\s*(?<message>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SeverityCodePattern();

    // ... (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1826)
    [GeneratedRegex(@"learn\.microsoft\.com/[^\s)]*/(?<code>(?:ca|cs|ide|nu|msb|netsdk)\d{3,5})\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DocumentationCodePattern();

    [GeneratedRegex(@"^[A-Z]{1,10}\d{1,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeOnlyPattern();
}
