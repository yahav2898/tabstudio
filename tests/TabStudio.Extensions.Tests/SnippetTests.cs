using System.Text;
using TabStudio.Extensions.Snippets;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

public sealed class SnippetTests
{
    private static readonly StandardSnippetVariables NoContext = new();

    [Fact]
    public void Placeholders_get_their_default_text_and_lines_the_insertion_indentation()
    {
        var expansion = SnippetExpander.Expand("fn ${1:name}(${2:args}) {\n\t$0\n}", NoContext, new SnippetInsertion("    ", "    "));

        Assert.Equal("fn name(args) {\n        \n    }", expansion.Text);
        Assert.Equal(new[] { (1, 3, 4), (2, 8, 4), (0, 24, 0) }, expansion.Placeholders.Select(p => (p.Index, p.Start, p.Length)));
    }

    [Fact]
    public void Variables_are_resolved_and_transformed()
    {
        var variables = new StandardSnippetVariables { FilePath = "/work/hello.lum" };

        var expansion = SnippetExpander.Expand("print(\"${TM_FILENAME_BASE/(.*)/${1:/upcase}/}\")$0", variables, SnippetInsertion.Default);

        Assert.Equal("print(\"HELLO\")", expansion.Text);
        Assert.Equal((0, 14), (expansion.Placeholders[0].Index, expansion.Placeholders[0].Start));
    }

    [Fact]
    public void A_choice_starts_with_its_first_option_and_a_final_tab_stop_is_added()
    {
        var expansion = SnippetExpander.Expand("if ${1|ready,done,failed|} {\n\t$2\n}", NoContext, SnippetInsertion.Default);

        Assert.Equal("if ready {\n    \n}", expansion.Text);
        var choice = expansion.Placeholders[0];
        Assert.Equal((1, 3, 5), (choice.Index, choice.Start, choice.Length));
        Assert.Equal(new[] { "ready", "done", "failed" }, choice.Choices);
        Assert.Equal((2, 15, 0), (expansion.Placeholders[1].Index, expansion.Placeholders[1].Start, expansion.Placeholders[1].Length));
        Assert.Equal((0, 17), (expansion.Placeholders[2].Index, expansion.Placeholders[2].Start));
    }

    [Fact]
    public void Tab_stops_without_content_repeat_the_first_one_and_transforms_follow_it()
    {
        var expansion = SnippetExpander.Expand("${1:foo} and $1 and ${1/(o+)/[$1]/g}", NoContext, SnippetInsertion.Default);

        Assert.Equal("foo and foo and f[oo]", expansion.Text);
        Assert.Equal(new[] { (1, 0, 3, false), (1, 8, 3, false), (1, 16, 5, true), (0, 21, 0, false) }, expansion.Placeholders.Select(p => (p.Index, p.Start, p.Length, p.Transform is not null)));
    }

    [Fact]
    public void Unknown_variables_become_placeholders_with_their_name()
    {
        var expansion = SnippetExpander.Expand("Hello ${NAME} $UNKNOWN_VAR ${TM_FILENAME:untitled}", NoContext, SnippetInsertion.Default);

        Assert.Equal("Hello NAME UNKNOWN_VAR untitled", expansion.Text);
        Assert.Equal(new[] { (1, 6, 4), (2, 11, 11), (0, 31, 0) }, expansion.Placeholders.Select(p => (p.Index, p.Start, p.Length)));
    }

    [Fact]
    public void Escapes_and_text_that_is_not_a_construct_stay_literal()
    {
        var expansion = SnippetExpander.Expand(@"\$1 costs \${2} and \} done $$ and ${broken", NoContext, SnippetInsertion.Default);

        Assert.Equal("$1 costs ${2} and } done $$ and ${broken", expansion.Text);
        Assert.Equal(0, Assert.Single(expansion.Placeholders).Index);
    }

    [Fact]
    public void Nested_placeholders_are_found_inside_their_parent()
    {
        var expansion = SnippetExpander.Expand("${1:outer ${2:inner}}", NoContext, SnippetInsertion.Default);

        Assert.Equal("outer inner", expansion.Text);
        Assert.Equal(new[] { (1, 0, 11), (2, 6, 5), (0, 11, 0) }, expansion.Placeholders.Select(p => (p.Index, p.Start, p.Length)));
    }

    [Fact]
    public void Variable_values_keep_their_own_lines()
    {
        var variables = new StandardSnippetVariables { SelectedText = "a\nb" };

        var expansion = SnippetExpander.Expand("{\n\t$TM_SELECTED_TEXT\n}", variables, new SnippetInsertion("  ", "  "));

        Assert.Equal("{\n    a\nb\n  }", expansion.Text);
    }

    [Fact]
    public void Date_line_and_comment_variables_follow_vs_code()
    {
        var variables = new StandardSnippetVariables
        {
            Now = new DateTimeOffset(2026, 3, 5, 9, 7, 2, TimeSpan.FromHours(5.5)),
            LineIndex = 41,
            CurrentLine = "  let x = 1",
            LineComment = "#",
            FilePath = "/work/src/app.lum",
            WorkspaceFolder = "/work",
        };

        var expansion = SnippetExpander.Expand(
            "$CURRENT_YEAR-$CURRENT_MONTH-$CURRENT_DATE $CURRENT_DAY_NAME_SHORT $CURRENT_MONTH_NAME $CURRENT_HOUR:$CURRENT_MINUTE:$CURRENT_SECOND $CURRENT_TIMEZONE_OFFSET|$TM_LINE_NUMBER|$LINE_COMMENT|$RELATIVE_FILEPATH|$WORKSPACE_NAME|$TM_SELECTED_TEXT|${CLIPBOARD:none}",
            variables,
            SnippetInsertion.Default);

        Assert.Equal("2026-03-05 Thu March 09:07:02 +05:30|42|#|" + Path.Combine("src", "app.lum") + "|work||none", expansion.Text);
        Assert.Matches("^[0-9]{6}$", SnippetExpander.Expand("$RANDOM", NoContext, SnippetInsertion.Default).Text);
        Assert.Matches("^[0-9a-f]{6}$", SnippetExpander.Expand("$RANDOM_HEX", NoContext, SnippetInsertion.Default).Text);
        Assert.True(Guid.TryParse(SnippetExpander.Expand("$UUID", NoContext, SnippetInsertion.Default).Text, out _));
    }

    [Theory]
    [InlineData("${1/(.*)/${1:/downcase}/}", "HeLLo", "hello")]
    [InlineData("${1/(.*)/${1:/capitalize}/}", "hello", "Hello")]
    [InlineData("${1/(.*)/${1:/pascalcase}/}", "my-file_name", "MyFileName")]
    [InlineData("${1/(.*)/${1:/camelcase}/}", "my-file_name", "myFileName")]
    [InlineData("${1/^(x)?.*$/${1:?has x:no x}/}", "xyz", "has x")]
    [InlineData("${1/^(x)?.*$/${1:?has x:no x}/}", "abc", "no x")]
    [InlineData("${1/(z)/${1:+found}/}", "abc", "abc")]
    [InlineData("${1/(z)/${1:-missing}/}", "abc", "missing")]
    [InlineData("${1/a/A/g}", "banana", "bAnAnA")]
    [InlineData("${1/a/A/}", "banana", "bAnana")]
    [InlineData("${1/\\//-/g}", "a/b/c", "a-b-c")]
    public void Transforms_work_as_in_javascript(string mirror, string value, string expected)
    {
        var transform = Assert.IsType<SnippetTabStop>(Assert.Single(SnippetParser.Parse(mirror))).Transform!;

        Assert.Equal(expected, transform.Apply(value));
    }

    [Fact]
    public void Tab_and_shift_tab_move_between_tab_stops_and_tab_stop_0_ends_the_session()
    {
        var expansion = SnippetExpander.Expand("fn ${1:name}(${2:args}) {\n\t$0\n}", NoContext, new SnippetInsertion("    ", "    "));
        var session = new SnippetSession(expansion, offset: 100);

        Assert.Equal((103, 4), session.MoveNext());
        Assert.Equal((108, 4), session.MoveNext());
        Assert.True(session.IsAtLastTabStop);
        Assert.Equal((103, 4), session.MovePrevious());
        Assert.Null(session.MovePrevious());
        Assert.Equal((108, 4), session.MoveNext());
        Assert.Equal((124, 0), session.MoveNext());
        Assert.False(session.IsActive);
    }

    [Fact]
    public void Typing_in_a_tab_stop_changes_its_copies()
    {
        var text = new StringBuilder();
        var session = Insert(text, "${1:a} $1 ${1/(.*)/<${1:/upcase}>/}");
        Assert.Equal("a a <A>", text.ToString());
        Assert.Equal((0, 1), session.MoveNext());

        Type(text, session, 0, 1, "xy");
        Assert.Equal("xy xy <XY>", text.ToString());

        Type(text, session, 2, 0, "z");
        Assert.Equal("xyz xyz <XYZ>", text.ToString());

        Type(text, session, 2, 1, string.Empty);
        Assert.Equal("xy xy <XY>", text.ToString());
        Assert.Equal(new[] { (0, 2, true), (3, 2, false), (6, 4, false) }, session.Ranges.Select(r => (r.Start, r.Length, r.IsPrimary)));
    }

    [Fact]
    public void Text_typed_before_the_snippet_moves_its_tab_stops()
    {
        var text = new StringBuilder();
        var session = Insert(text, "${1:one} ${2:two}");
        session.MoveNext();

        Type(text, session, 0, 0, ">> ");

        Assert.Equal(">> one two", text.ToString());
        Assert.Equal((7, 3), session.MoveNext());
    }

    [Fact]
    public void Replacing_a_placeholder_removes_the_ones_nested_in_it()
    {
        var text = new StringBuilder();
        var session = Insert(text, "${1:outer ${2:inner}}");
        Assert.Equal((0, 11), session.MoveNext());

        Type(text, session, 0, 11, "x");

        Assert.Equal("x", text.ToString());
        Assert.Equal((1, 0), session.MoveNext());
        Assert.False(session.IsActive);
    }

    [Fact]
    public void An_empty_tab_stop_takes_what_is_typed_into_it()
    {
        var text = new StringBuilder();
        var session = Insert(text, "($1, $2)");
        Assert.Equal((1, 0), session.MoveNext());

        Type(text, session, 1, 0, "a");
        Assert.Equal((4, 0), session.MoveNext());
        Type(text, session, 4, 0, "b");

        Assert.Equal("(a, b)", text.ToString());
        Assert.Equal((6, 0), session.MoveNext());
    }

    [Fact]
    public void Snippet_files_are_read_with_string_and_array_bodies_and_prefixes()
    {
        var snippets = SnippetDefinition.ReadFile(LumenExtension.Snippets);

        Assert.Equal(new[] { "Function", "Print the file's name", "If" }, snippets.Select(s => s.Name));
        Assert.Equal("fn ${1:name}(${2:args}) {\n\t$0\n}", snippets[0].Body);
        Assert.Equal(new[] { "pfile", "printfile" }, snippets[1].Prefixes);
        Assert.Equal("A function", snippets[0].Description);

        var global = SnippetDefinition.ReadFile("""{ "x": { "prefix": "x", "body": "x", "scope": "lumen, python" }, "no body": { "prefix": "y" } }""");
        Assert.Equal(new[] { "lumen", "python" }, Assert.Single(global).Languages);
    }

    private static SnippetSession Insert(StringBuilder text, string body)
    {
        var expansion = SnippetExpander.Expand(body, NoContext, SnippetInsertion.Default);
        text.Insert(0, expansion.Text);
        return new SnippetSession(expansion, 0);
    }

    /// <summary>Changes the text as an editor would, and applies the replacements the session asks for.</summary>
    private static void Type(StringBuilder text, SnippetSession session, int offset, int removed, string inserted)
    {
        string Read(int start, int length) => text.ToString(start, length);

        text.Remove(offset, removed).Insert(offset, inserted);
        foreach (var replacement in session.OnTextChanged(offset, removed, inserted.Length, Read))
        {
            text.Remove(replacement.Start, replacement.Length).Insert(replacement.Start, replacement.Text);
            session.OnTextChanged(replacement.Start, replacement.Length, replacement.Text.Length, Read, isSessionEdit: true);
        }
    }
}
