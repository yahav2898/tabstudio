using System.Globalization;
using System.Security.Cryptography;

namespace TabStudio.Extensions.Snippets;

/// <summary>
/// The variables VS Code defines for snippets: the file, the selection, the line and word at the caret, the clipboard,
/// the workspace, the date and time, random values and the language's comments.
/// </summary>
public sealed class StandardSnippetVariables : ISnippetVariables
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "TM_SELECTED_TEXT", "SELECTION", "TM_CURRENT_LINE", "TM_CURRENT_WORD", "TM_LINE_INDEX", "TM_LINE_NUMBER",
        "TM_FILENAME", "TM_FILENAME_BASE", "TM_DIRECTORY", "TM_FILEPATH", "RELATIVE_FILEPATH", "CLIPBOARD",
        "WORKSPACE_NAME", "WORKSPACE_FOLDER", "CURSOR_INDEX", "CURSOR_NUMBER",
        "CURRENT_YEAR", "CURRENT_YEAR_SHORT", "CURRENT_MONTH", "CURRENT_MONTH_NAME", "CURRENT_MONTH_NAME_SHORT",
        "CURRENT_DATE", "CURRENT_DAY_NAME", "CURRENT_DAY_NAME_SHORT", "CURRENT_HOUR", "CURRENT_MINUTE", "CURRENT_SECOND",
        "CURRENT_SECONDS_UNIX", "CURRENT_TIMEZONE_OFFSET", "RANDOM", "RANDOM_HEX", "UUID",
        "LINE_COMMENT", "BLOCK_COMMENT_START", "BLOCK_COMMENT_END",
    };

    /// <summary>The full path of the file, or <see langword="null"/>.</summary>
    public string? FilePath { get; init; }

    /// <summary>The workspace folder, or <see langword="null"/>.</summary>
    public string? WorkspaceFolder { get; init; }

    /// <summary>The selected text, or an empty string.</summary>
    public string SelectedText { get; init; } = string.Empty;

    /// <summary>The line the caret is on.</summary>
    public string CurrentLine { get; init; } = string.Empty;

    /// <summary>The word at the caret, or an empty string.</summary>
    public string CurrentWord { get; init; } = string.Empty;

    /// <summary>The caret's line, 0-based.</summary>
    public int LineIndex { get; init; }

    /// <summary>The clipboard's text, or <see langword="null"/>.</summary>
    public string? Clipboard { get; init; }

    /// <summary>The language's line comment, or <see langword="null"/>.</summary>
    public string? LineComment { get; init; }

    /// <summary>The language's block comment start, or <see langword="null"/>.</summary>
    public string? BlockCommentStart { get; init; }

    /// <summary>The language's block comment end, or <see langword="null"/>.</summary>
    public string? BlockCommentEnd { get; init; }

    /// <summary>The time the date and time variables use.</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;

    /// <inheritdoc />
    public bool TryResolve(string name, out string? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!Known.Contains(name))
        {
            value = null;
            return false;
        }

        var english = CultureInfo.InvariantCulture.DateTimeFormat;
        value = name switch
        {
            "TM_SELECTED_TEXT" or "SELECTION" => NullIfEmpty(SelectedText),
            "TM_CURRENT_LINE" => CurrentLine,
            "TM_CURRENT_WORD" => NullIfEmpty(CurrentWord),
            "TM_LINE_INDEX" => LineIndex.ToString(CultureInfo.InvariantCulture),
            "TM_LINE_NUMBER" => (LineIndex + 1).ToString(CultureInfo.InvariantCulture),
            "TM_FILENAME" => FilePath is null ? null : Path.GetFileName(FilePath),
            "TM_FILENAME_BASE" => FilePath is null ? null : Path.GetFileNameWithoutExtension(FilePath),
            "TM_DIRECTORY" => FilePath is null ? null : Path.GetDirectoryName(FilePath),
            "TM_FILEPATH" => FilePath,
            "RELATIVE_FILEPATH" => FilePath is null ? null : WorkspaceFolder is null ? FilePath : Path.GetRelativePath(WorkspaceFolder, FilePath),
            "CLIPBOARD" => NullIfEmpty(Clipboard),
            "WORKSPACE_NAME" => WorkspaceFolder is null ? null : Path.GetFileName(Path.TrimEndingDirectorySeparator(WorkspaceFolder)),
            "WORKSPACE_FOLDER" => WorkspaceFolder,
            "CURSOR_INDEX" => "0",
            "CURSOR_NUMBER" => "1",
            "CURRENT_YEAR" => Now.Year.ToString(CultureInfo.InvariantCulture),
            "CURRENT_YEAR_SHORT" => (Now.Year % 100).ToString("00", CultureInfo.InvariantCulture),
            "CURRENT_MONTH" => Now.Month.ToString("00", CultureInfo.InvariantCulture),
            "CURRENT_MONTH_NAME" => english.GetMonthName(Now.Month),
            "CURRENT_MONTH_NAME_SHORT" => english.GetAbbreviatedMonthName(Now.Month),
            "CURRENT_DATE" => Now.Day.ToString("00", CultureInfo.InvariantCulture),
            "CURRENT_DAY_NAME" => english.GetDayName(Now.DayOfWeek),
            "CURRENT_DAY_NAME_SHORT" => english.GetAbbreviatedDayName(Now.DayOfWeek),
            "CURRENT_HOUR" => Now.Hour.ToString("00", CultureInfo.InvariantCulture),
            "CURRENT_MINUTE" => Now.Minute.ToString("00", CultureInfo.InvariantCulture),
            "CURRENT_SECOND" => Now.Second.ToString("00", CultureInfo.InvariantCulture),
            "CURRENT_SECONDS_UNIX" => Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            "CURRENT_TIMEZONE_OFFSET" => TimeZoneOffset(Now.Offset),
            "RANDOM" => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("000000", CultureInfo.InvariantCulture),
            "RANDOM_HEX" => RandomNumberGenerator.GetInt32(0, 0x1000000).ToString("x6", CultureInfo.InvariantCulture),
            "UUID" => Guid.NewGuid().ToString(),
            "LINE_COMMENT" => LineComment,
            "BLOCK_COMMENT_START" => BlockCommentStart,
            "BLOCK_COMMENT_END" => BlockCommentEnd,
            _ => null,
        };
        return true;
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    private static string TimeZoneOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{absolute.Hours:00}:{absolute.Minutes:00}");
    }
}
