using System.Text;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.Compilation.Features;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>
/// The engine of Find in Files and Replace in Files: Match case, Match whole word and regular expressions, as Visual
/// Studio has them; an invalid regular expression said in words rather than thrown; replacements with <c>$1</c>; and
/// files rewritten in their own encoding, byte order mark and line endings.
/// </summary>
public sealed class FileTextMatcherTests
{
    private const string Animals = "cat concat Cat cat_ cats (cat) CAT";

    [Fact]
    public void Letter_case_is_ignored_unless_Match_case_is_on()
    {
        Assert.Equal(new[] { 0, 7, 11, 15, 20, 26, 31 }, Starts(new FindInFilesQuery("cat")));
        Assert.Equal(new[] { 0, 7, 15, 20, 26 }, Starts(new FindInFilesQuery("cat", MatchCase: true)));
        Assert.Equal(new[] { 31 }, Starts(new FindInFilesQuery("CAT", MatchCase: true)));
    }

    [Fact]
    public void Match_whole_word_leaves_out_matches_inside_words()
    {
        // "concat", "cat_" and "cats" run on into a word; "(cat)" is a whole word between parentheses.
        Assert.Equal(new[] { 0, 11, 26, 31 }, Starts(new FindInFilesQuery("cat", MatchWholeWord: true)));
        Assert.Equal(new[] { 0, 26 }, Starts(new FindInFilesQuery("cat", MatchCase: true, MatchWholeWord: true)));

        // After a match that is part of a longer word, the search goes on to the whole word after it.
        Assert.Equal(new[] { 4 }, Starts(new FindInFilesQuery("aa", MatchWholeWord: true), "aaa aa"));
    }

    [Fact]
    public void Regular_expressions_are_used_only_when_asked_for()
    {
        Assert.Equal(new[] { 0, 11, 31 }, Starts(new FindInFilesQuery(@"\bcat\b(?!\))", UseRegularExpressions: true)));
        Assert.Empty(Starts(new FindInFilesQuery(@"\bcat\b")));
        Assert.Equal(new[] { 25 }, Starts(new FindInFilesQuery("(cat)")));

        // ^ and $ match at every line, as in Visual Studio.
        Assert.Equal(new[] { 0, 6 }, Starts(new FindInFilesQuery("^line", UseRegularExpressions: true), "line1\nline2"));
        Assert.Equal(new[] { 3 }, Starts(new FindInFilesQuery(@"\d$", UseRegularExpressions: true, MatchWholeWord: true), "ab 1\nab2"));
    }

    [Fact]
    public void An_invalid_regular_expression_is_explained_rather_than_thrown()
    {
        Assert.False(FileTextMatcher.TryCreate(new FindInFilesQuery("(unclosed", UseRegularExpressions: true), out var matcher, out var error));
        Assert.Null(matcher);
        Assert.StartsWith("Not a valid regular expression: ", error, StringComparison.Ordinal);
        Assert.Contains("at character", error, StringComparison.Ordinal);
        Assert.DoesNotContain("RegexParseException", error, StringComparison.Ordinal);

        // The same text is plain text without regular expressions.
        Assert.Single(Create(new FindInFilesQuery("(unclosed")).Find("call(unclosed"));
    }

    [Fact]
    public void Replacements_fill_in_the_groups_of_a_regular_expression_and_take_plain_text_as_it_is()
    {
        Assert.Equal("var sum = a + b;", ReplaceAll(new FindInFilesQuery(@"(\w+)\.Value", UseRegularExpressions: true), "var sum = a.Value + b.Value;", "$1"));
        Assert.Equal("b = a; d = c;", ReplaceAll(new FindInFilesQuery(@"(?<left>\w) = (?<right>\w)", UseRegularExpressions: true), "a = b; c = d;", "${right} = ${left}"));
        Assert.Equal("[x] costs $5", ReplaceAll(new FindInFilesQuery(@"x", UseRegularExpressions: true), "x costs $5", "[$0]"));
        Assert.Equal("$ $ $", ReplaceAll(new FindInFilesQuery(@"\w+", UseRegularExpressions: true), "dollar and euro", "$$"));

        // Without regular expressions, "$1" is what is written.
        Assert.Equal("x = $1;", ReplaceAll(new FindInFilesQuery("a.Value"), "x = a.Value;", "$1"));

        // A whole-word, case-sensitive replace leaves the other spellings alone.
        Assert.Equal("dog concat Cat cat_ cats (dog) CAT", ReplaceAll(new FindInFilesQuery("cat", MatchCase: true, MatchWholeWord: true), Animals, "dog"));

        // A pattern that matches nothing in particular replaces nothing.
        Assert.Equal("abc", ReplaceAll(new FindInFilesQuery("x*", UseRegularExpressions: true), "abc", "y"));
    }

    [Fact]
    public void Edits_apply_in_one_pass_as_the_editor_applies_them()
    {
        var text = string.Concat(Enumerable.Repeat("a.Value b\r\n", 2000));
        var matcher = Create(new FindInFilesQuery(@"(\w)\.Value", UseRegularExpressions: true));
        var edits = matcher.Replacements(text, "$1");

        Assert.Equal(2000, edits.Count);
        Assert.Equal(TextEdits.Apply(text, edits), FileTextMatcher.Apply(text, edits));
        Assert.Equal(string.Concat(Enumerable.Repeat("a b\r\n", 2000)), FileTextMatcher.Apply(text, edits));
    }

    [Fact]
    public async Task Replace_all_rewrites_closed_files_in_their_encoding_and_leaves_open_ones_to_their_editors()
    {
        using var temp = new TempDirectory();
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var utf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        var latin1 = Encoding.Latin1;
        var bom = Write(temp, "bom-crlf.cs", utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes("int old = 1;\r\nold++;\r\n")));
        var plain = Write(temp, "plain-lf.txt", Encoding.UTF8.GetBytes("old\nnew\nold"));
        var wide = Write(temp, "wide.txt", utf16.GetPreamble().Concat(utf16.GetBytes("old שלום\r\n")));
        var legacy = Write(temp, "legacy.txt", latin1.GetBytes("café old\n"));
        var binary = Write(temp, "image.bin", [0x6f, 0x6c, 0x64, 0x00, 0x01]);
        var untouched = Write(temp, "none.txt", Encoding.UTF8.GetBytes("nothing here\n"));
        var open = Write(temp, "open.cs", Encoding.UTF8.GetBytes("old on disk"));
        var notebook = Write(temp, "notes.ipynb", Encoding.UTF8.GetBytes("{\"old\": 1}"));
        var before = Snapshot(binary, untouched, open, notebook);

        var result = await FileTextSearch.ReplaceAsync(
            temp.Path, Create(new FindInFilesQuery("old", MatchWholeWord: true)), "new",
            new Dictionary<string, string>(StringComparer.Ordinal) { [open] = "old in the editor, old" },
            new HashSet<string>(StringComparer.Ordinal) { notebook },
            CancellationToken.None);

        Assert.Equal(utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes("int new = 1;\r\nnew++;\r\n")), await File.ReadAllBytesAsync(bom));
        Assert.Equal(Encoding.UTF8.GetBytes("new\nnew\nnew"), await File.ReadAllBytesAsync(plain));
        Assert.Equal(utf16.GetPreamble().Concat(utf16.GetBytes("new שלום\r\n")), await File.ReadAllBytesAsync(wide));
        Assert.Equal(latin1.GetBytes("café new\n"), await File.ReadAllBytesAsync(legacy));

        // Binary files, files without a match, open documents and files open in another editor stay as they were.
        Assert.Equal(before, Snapshot(binary, untouched, open, notebook));
        var edit = Assert.Single(result.OpenDocumentEdits);
        Assert.Equal((open, DocumentEditKind.Modified), (edit.FilePath, edit.Kind));
        Assert.Equal("new in the editor, new", FileTextMatcher.Apply("old in the editor, old", edit.Edits));

        Assert.Equal(8, result.Replacements);
        Assert.Equal(5, result.Files);
        Assert.Equal(8, result.Matches.Count);
        Assert.Empty(result.Failures);
        Assert.Equal(new[] { bom, legacy, plain, wide }, result.WrittenFiles.Select(f => f.Path).Order(StringComparer.Ordinal));
        Assert.Contains(result.Matches, m => m.FilePath == bom && m.Line == 2 && m.Column == 1 && m.LineText == "old++;");
    }

    [Fact]
    public async Task Replace_all_stops_when_cancelled()
    {
        using var temp = new TempDirectory();
        var file = Write(temp, "a.txt", Encoding.UTF8.GetBytes("old"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FileTextSearch.ReplaceAsync(temp.Path, Create(new FindInFilesQuery("old")), "new", null, null, cancelled.Token));
        Assert.Equal("old", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public void Search_finds_regular_expressions_across_files_with_their_lines()
    {
        using var temp = new TempDirectory();
        temp.Write("a.cs", "var x = items.Count;\nvar y = list.Count();\n");
        temp.Write("b.cs", "Count = 3;\n");

        var matches = FileTextSearch.Search(temp.Path, Create(new FindInFilesQuery(@"\.Count\(\)", MatchCase: true, UseRegularExpressions: true)), null, CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal(("a.cs", 2, 13, 8, "var y = list.Count();"), (Path.GetFileName(match.FilePath), match.Line, match.Column, match.Length, match.LineText));
    }

    private static FileTextMatcher Create(FindInFilesQuery query) =>
        FileTextMatcher.TryCreate(query, out var matcher, out var error) ? matcher : throw new InvalidOperationException(error);

    private static int[] Starts(FindInFilesQuery query, string text = Animals) =>
        [.. Create(query).Find(text).Select(m => m.Start)];

    private static string ReplaceAll(FindInFilesQuery query, string text, string replacement) =>
        FileTextMatcher.Apply(text, Create(query).Replacements(text, replacement));

    private static string Write(TempDirectory temp, string name, IEnumerable<byte> bytes)
    {
        var path = Path.Combine(temp.Path, name);
        File.WriteAllBytes(path, bytes.ToArray());
        return path;
    }

    private static string Snapshot(params string[] files) =>
        string.Join("|", files.Select(f => Convert.ToHexString(File.ReadAllBytes(f))));
}
