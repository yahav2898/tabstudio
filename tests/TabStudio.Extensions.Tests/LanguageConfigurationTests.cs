using TabStudio.Extensions.Languages;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

public sealed class LanguageConfigurationTests
{
    private static readonly LanguageConfiguration Lumen = LanguageConfigurationReader.Read(LumenExtension.LanguageConfiguration);

    [Fact]
    public void The_fixture_configuration_is_read_completely()
    {
        Assert.Equal("#", Lumen.LineComment);
        Assert.Equal(new BracketPair("#[", "]#"), Lumen.BlockComment);
        Assert.Equal(3, Lumen.Brackets.Count);
        Assert.Equal(4, Lumen.AutoClosingPairs.Count);
        Assert.Equal(TokenContexts.StringLiteral | TokenContexts.Comment, Lumen.AutoClosingPairs[3].NotIn);
        Assert.Equal(4, Lumen.SurroundingPairs.Count);
        Assert.Equal(LanguageConfiguration.DefaultAutoCloseBefore, Lumen.AutoCloseBefore);
        Assert.NotNull(Lumen.Folding?.MarkerStart);
        Assert.False(Lumen.Folding!.OffSide);
        Assert.Matches(Lumen.WordPattern!, "snake_case");
        Assert.Matches(Lumen.Indentation!.DecreaseIndent!, "  }");
        Assert.Single(Lumen.OnEnterRules);
    }

    [Fact]
    public void Missing_pairs_default_to_the_brackets_as_in_vs_code()
    {
        var configuration = LanguageConfigurationReader.Read("""{ "brackets": [["{", "}"], ["(", ")"]], "comments": { "lineComment": { "comment": "//", "noIndent": true } } }""");

        Assert.Equal(new[] { "{", "(" }, configuration.AutoClosingPairs.Select(p => p.Open));
        Assert.Equal(new[] { "{", "(" }, configuration.SurroundingPairs.Select(p => p.Open));
        Assert.Equal("//", configuration.LineComment);
        Assert.Null(configuration.BlockComment);
    }

    [Fact]
    public void Patterns_that_are_not_valid_are_left_out_rather_than_failing_the_file()
    {
        var configuration = LanguageConfigurationReader.Read("""
            {
              "wordPattern": "(unclosed",
              "indentationRules": { "increaseIndentPattern": "\\{$", "decreaseIndentPattern": { "pattern": "^\\s*\\}", "flags": "i" } },
              "onEnterRules": [{ "beforeText": "[", "action": { "indent": "indent" } }, { "beforeText": "^x", "action": { "indent": "outdent", "removeText": 2 } }]
            }
            """);

        Assert.Null(configuration.WordPattern);
        Assert.NotNull(configuration.Indentation!.IncreaseIndent);
        Assert.True(configuration.Indentation!.DecreaseIndent!.Options.HasFlag(System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        var rule = Assert.Single(configuration.OnEnterRules);
        Assert.Equal(new EnterAction(IndentAction.Outdent, null, 2), rule.Action);
    }

    [Theory]
    [InlineData(@"\a\e\q", false, "aeq")]
    [InlineData("[^]x", false, @"[\s\S]x")]
    [InlineData("a[]b", false, "a(?!)b")]
    [InlineData("[a-z-[]", false, @"[a-z-\[]")]
    [InlineData(@"\u{1F600}", true, @"\uD83D\uDE00")]
    [InlineData(@"^\s*\/\/\s*\d+\w", false, @"^\s*\/\/\s*[0-9]+[a-zA-Z0-9_]")]
    [InlineData(@"[\w.]\D\W", false, @"[a-zA-Z0-9_.][^0-9][^a-zA-Z0-9_]")]
    public void JavaScript_patterns_are_translated_to_dotnet(string pattern, bool unicode, string expected)
    {
        Assert.Equal(expected, JsRegex.Translate(pattern, unicode));
    }

    [Fact]
    public void Word_and_digit_classes_match_only_ASCII_as_in_JavaScript()
    {
        var word = JsRegex.TryCreate(@"^\w+:$")!;
        Assert.True(word.IsMatch("case_1:"));
        Assert.False(word.IsMatch("é:"));
        Assert.False(JsRegex.TryCreate(@"^\d$")!.IsMatch("\u0663"));
    }

    [Fact]
    public void JavaScript_flags_become_options()
    {
        var regex = JsRegex.TryCreate("^a.b$", "ims")!;

        Assert.Matches(regex, "x\nA\nB");
        Assert.Null(JsRegex.TryCreate("(", null));
        Assert.Null(JsRegex.TryCreate(null));
    }

    [Theory]
    [InlineData("fn greet(name) {", "", IndentAction.Indent, null)]
    [InlineData("fn greet(name) {", "}", IndentAction.IndentOutdent, null)]
    [InlineData("    # keeps going", "here", IndentAction.None, "# ")]
    public void Enter_follows_the_rules_then_the_brackets(string before, string after, IndentAction indent, string? append)
    {
        var action = EnterRules.Decide(Lumen, before, after, string.Empty);

        Assert.Equal(new EnterAction(indent, append), action);
    }

    [Theory]
    [InlineData("# a comment at the end", "")]
    [InlineData("let x = 1", "")]
    public void Enter_without_a_matching_rule_keeps_the_indentation(string before, string after)
    {
        Assert.Null(EnterRules.Decide(Lumen, before, after, string.Empty));
    }

    [Fact]
    public void Indentation_rules_indent_after_a_line_and_outdent_after_a_single_indented_line()
    {
        var configuration = LanguageConfigurationReader.Read("""
            {
              "indentationRules": {
                "increaseIndentPattern": "^.*:\\s*$",
                "decreaseIndentPattern": "^\\s*(else|elif)\\b",
                "indentNextLinePattern": "^\\s*(if|while)\\s*\\(.*\\)\\s*$"
              },
              "onEnterRules": [{ "beforeText": "^\\s*(?:def|class|for|if|elif|else|while).*?:\\s*$", "action": { "indent": "indent" } }]
            }
            """);

        Assert.Equal(IndentAction.Indent, EnterRules.Decide(configuration, "def run():", string.Empty, string.Empty)!.Indent);
        Assert.Equal(IndentAction.Indent, EnterRules.Decide(configuration, "while (x)", string.Empty, string.Empty)!.Indent);
        Assert.Equal(IndentAction.Outdent, EnterRules.Decide(configuration, "    step();", string.Empty, "while (x)")!.Indent);
        Assert.Equal(IndentAction.IndentOutdent, EnterRules.Decide(configuration, "options:", "else", string.Empty)!.Indent);
        Assert.True(EnterRules.ShouldOutdent(configuration, "    else"));
        Assert.True(EnterRules.IncreasesIndent(configuration, "if ready:"));
    }

    [Fact]
    public void Closing_brackets_outdent_and_opening_ones_indent()
    {
        Assert.True(EnterRules.ShouldOutdent(Lumen, "        }"));
        Assert.False(EnterRules.ShouldOutdent(Lumen, "        x"));
        Assert.True(EnterRules.IncreasesIndent(Lumen, "fn a() {"));
        Assert.False(EnterRules.IncreasesIndent(Lumen, "fn a() { }"));

        var bracketsOnly = LanguageConfigurationReader.Read("""{ "brackets": [["{", "}"], ["begin", "end"]] }""");
        Assert.True(EnterRules.ShouldOutdent(bracketsOnly, "  }"));
        Assert.True(EnterRules.IncreasesIndent(bracketsOnly, "  begin"));
        Assert.False(EnterRules.IncreasesIndent(bracketsOnly, "  xbegin"));
    }

    [Theory]
    [InlineData("let a = ", '(', "", "(")]
    [InlineData("let a = ", '(', ")", "(")]
    [InlineData("let a = ", '(', " more", "(")]
    [InlineData("let a = ", '(', "abc", null)]
    [InlineData("say ", '"', "", "\"")]
    [InlineData("say", '"', "", null)]
    [InlineData("say ", 'x', "", null)]
    public void Pairs_close_only_before_whitespace_or_closing_characters(string before, char typed, string after, string? expected)
    {
        Assert.Equal(expected, AutoClosingRules.FindPairToClose(Lumen, before, typed, after, TokenContexts.None)?.Open);
    }

    [Fact]
    public void Quotes_do_not_close_inside_strings_or_comments()
    {
        Assert.Null(AutoClosingRules.FindPairToClose(Lumen, "print(\"a ", '"', ")", TokenContexts.StringLiteral));
        Assert.Null(AutoClosingRules.FindPairToClose(Lumen, "# it ", '"', "", TokenContexts.Comment));
        Assert.Equal("(", AutoClosingRules.FindPairToClose(Lumen, "# it ", '(', "", TokenContexts.Comment)?.Open);
    }

    [Fact]
    public void The_longest_opening_text_wins()
    {
        var configuration = LanguageConfigurationReader.Read("""
            { "autoClosingPairs": [{ "open": "\"", "close": "\"" }, { "open": "r\"", "close": "\"" }, { "open": "/**", "close": " */" }] }
            """);

        Assert.Equal("r\"", AutoClosingRules.FindPairToClose(configuration, "x = r", '"', "", TokenContexts.None)?.Open);
        Assert.Equal(" */", AutoClosingRules.FindPairToClose(configuration, "/*", '*', "", TokenContexts.None)?.Close);
    }

    [Fact]
    public void Closing_characters_are_typed_over_surround_and_delete_in_pairs()
    {
        Assert.True(AutoClosingRules.ShouldTypeOver(Lumen, ')', ")", closedAutomatically: true));
        Assert.False(AutoClosingRules.ShouldTypeOver(Lumen, ')', ")", closedAutomatically: false));
        Assert.False(AutoClosingRules.ShouldTypeOver(Lumen, 'x', "x", closedAutomatically: true));
        Assert.Equal(new BracketPair("\"", "\""), AutoClosingRules.FindSurroundingPair(Lumen, '"'));
        Assert.Null(AutoClosingRules.FindSurroundingPair(Lumen, 'x'));
        Assert.True(AutoClosingRules.ShouldDeletePair(Lumen, '(', ')'));
        Assert.False(AutoClosingRules.ShouldDeletePair(Lumen, '(', ']'));
    }

    [Fact]
    public void Folding_follows_indentation_and_region_markers()
    {
        string[] lines =
        [
            "fn a() {",
            "    let x = 1",
            "    if x {",
            "        print(x)",
            "    }",
            "}",
            "# region Helpers",
            "fn b() {",
            "}",
            "# endregion",
        ];

        var ranges = FoldingRanges.Compute(lines, Lumen.Folding);

        Assert.Equal(new[] { (0, 4, false), (2, 3, false), (6, 9, true) }, ranges.Select(r => (r.StartLine, r.EndLine, r.IsRegion)));
    }

    [Fact]
    public void Off_side_languages_leave_trailing_blank_lines_out_of_a_fold()
    {
        string[] lines = ["def f():", "    x = 1", "", "def g():", "    pass"];

        var offSide = FoldingRanges.Compute(lines, new FoldingRules(OffSide: true, null, null));
        var braces = FoldingRanges.Compute(lines, null);

        Assert.Equal(new[] { (0, 1), (3, 4) }, offSide.Select(r => (r.StartLine, r.EndLine)));
        Assert.Equal(new[] { (0, 2), (3, 4) }, braces.Select(r => (r.StartLine, r.EndLine)));
    }

    [Fact]
    public void Tabs_count_to_the_next_tab_stop()
    {
        Assert.Equal(8, FoldingRanges.IndentLevel("\t\tx", 4));
        Assert.Equal(4, FoldingRanges.IndentLevel("  \tx", 4));
        Assert.Equal(-1, FoldingRanges.IndentLevel(" \t ", 4));
    }

    [Theory]
    [InlineData(5, 4, 6)]
    [InlineData(7, 4, 6)]
    [InlineData(8, 8, 14)]
    [InlineData(15, 8, 14)]
    [InlineData(11, 10, 12)]
    public void Brackets_next_to_the_caret_are_matched(int caret, int open, int close)
    {
        var match = BracketMatcher.Find("fn a(b) { [c] }", caret, Lumen.Brackets);

        Assert.Equal(new BracketMatch(open, 1, close, 1), match);
    }

    [Fact]
    public void Brackets_in_strings_do_not_count_and_unbalanced_ones_have_no_match()
    {
        Assert.Equal(new BracketMatch(1, 1, 5, 1), BracketMatcher.Find("f(\"(\")", 2, Lumen.Brackets, i => i is < 2 or > 4));
        Assert.Null(BracketMatcher.Find("(a", 1, Lumen.Brackets));
        Assert.Null(BracketMatcher.Find("fn", 1, Lumen.Brackets));
        Assert.Equal(new BracketMatch(0, 5, 8, 3), BracketMatcher.Find("begin x end", 5, [new BracketPair("begin", "end")]));
    }
}
