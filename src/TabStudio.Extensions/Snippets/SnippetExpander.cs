using System.Text;

namespace TabStudio.Extensions.Snippets;

/// <summary>Where a snippet is inserted, which decides how its lines are indented.</summary>
/// <param name="Indentation">The leading whitespace of the line the snippet is inserted into; every further line of the snippet gets it too.</param>
/// <param name="IndentUnit">One level of indentation in the editor, such as four spaces or a tab; tabs at the start of the snippet's lines become this.</param>
/// <param name="NewLine">The line break to use.</param>
public sealed record SnippetInsertion(string Indentation, string IndentUnit, string NewLine = "\n")
{
    /// <summary>Inserting at the start of an unindented line, indenting with four spaces.</summary>
    public static SnippetInsertion Default { get; } = new(string.Empty, "    ");
}

/// <summary>One occurrence of a tab stop in an expanded snippet.</summary>
/// <param name="Index">The tab stop's number; 0 is where the caret ends up.</param>
/// <param name="Start">Where the occurrence starts in <see cref="SnippetExpansion.Text"/>.</param>
/// <param name="Length">Its length.</param>
/// <param name="Choices">The options to offer, for a choice; otherwise <see langword="null"/>.</param>
/// <param name="Transform">For a transformed copy, how it follows the tab stop's text; otherwise <see langword="null"/>.</param>
public sealed record SnippetPlaceholder(int Index, int Start, int Length, IReadOnlyList<string>? Choices = null, SnippetTransform? Transform = null);

/// <summary>A snippet expanded into text, with where its tab stops are.</summary>
/// <param name="Text">The text to insert.</param>
/// <param name="Placeholders">Every tab stop occurrence, in text order. There is always one with index 0.</param>
public sealed record SnippetExpansion(string Text, IReadOnlyList<SnippetPlaceholder> Placeholders);

/// <summary>
/// Expands a snippet as VS Code does: variables are resolved (an unknown one becomes a placeholder with its name),
/// a tab stop without content repeats the content of the first one with the same number, choices start with their
/// first option, transformed copies follow their tab stop, and every line after the first gets the indentation of
/// the line the snippet is inserted into.
/// </summary>
public static class SnippetExpander
{
    /// <summary>Expands a snippet body.</summary>
    /// <param name="body">The body.</param>
    /// <param name="variables">Resolves variables.</param>
    /// <param name="insertion">Where the snippet goes.</param>
    public static SnippetExpansion Expand(string body, ISnippetVariables variables, SnippetInsertion insertion) =>
        Expand(SnippetParser.Parse(body), variables, insertion);

    /// <summary>Expands a parsed snippet.</summary>
    /// <param name="nodes">The parsed body.</param>
    /// <param name="variables">Resolves variables.</param>
    /// <param name="insertion">Where the snippet goes.</param>
    public static SnippetExpansion Expand(IReadOnlyList<SnippetNode> nodes, ISnippetVariables variables, SnippetInsertion insertion)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(insertion);

        var nextIndex = MaxIndex(nodes) + 1;
        var resolved = ResolveVariables(nodes, variables, ref nextIndex);
        var defaults = new Dictionary<int, IReadOnlyList<SnippetNode>>();
        CollectDefaults(resolved, defaults);

        var writer = new Writer(insertion, defaults);
        writer.Write(resolved, new HashSet<int>());
        if (!writer.Placeholders.Any(p => p.Index == 0))
        {
            writer.Placeholders.Add(new SnippetPlaceholder(0, writer.Text.Length, 0));
        }

        return new SnippetExpansion(writer.Text.ToString(), [.. writer.Placeholders.OrderBy(p => p.Start).ThenBy(p => p.Index == 0)]);
    }

    /// <summary>The text of nodes with tab stops reduced to their content, as a transform reads a tab stop's value.</summary>
    /// <param name="nodes">The nodes.</param>
    internal static string PlainText(IReadOnlyList<SnippetNode> nodes)
    {
        var text = new StringBuilder();
        foreach (var node in nodes)
        {
            switch (node)
            {
                case SnippetText literal:
                    text.Append(literal.Value);
                    break;
                case ResolvedText value:
                    text.Append(value.Value);
                    break;
                case SnippetTabStop { Choices: { Count: > 0 } choices }:
                    text.Append(choices[0]);
                    break;
                case SnippetTabStop tabStop:
                    text.Append(PlainText(tabStop.Placeholder));
                    break;
                case SnippetVariable variable:
                    text.Append(PlainText(variable.Default));
                    break;
            }
        }

        return text.ToString();
    }

    private static int MaxIndex(IReadOnlyList<SnippetNode> nodes)
    {
        var max = 0;
        foreach (var node in nodes)
        {
            max = node switch
            {
                SnippetTabStop tabStop => Math.Max(max, Math.Max(tabStop.Index, MaxIndex(tabStop.Placeholder))),
                SnippetVariable variable => Math.Max(max, MaxIndex(variable.Default)),
                _ => max,
            };
        }

        return max;
    }

    /// <summary>
    /// Replaces variables by their values, or by their default when they have none; an unknown variable without a
    /// default becomes a new placeholder showing its name.
    /// </summary>
    private static List<SnippetNode> ResolveVariables(IReadOnlyList<SnippetNode> nodes, ISnippetVariables variables, ref int nextIndex)
    {
        var result = new List<SnippetNode>(nodes.Count);
        foreach (var node in nodes)
        {
            switch (node)
            {
                case SnippetVariable variable:
                    var known = variables.TryResolve(variable.Name, out var value);
                    if (known && variable.Transform is { } transform)
                    {
                        result.Add(new ResolvedText(transform.Apply(value ?? string.Empty)));
                    }
                    else if (known && value is not null)
                    {
                        result.Add(new ResolvedText(value));
                    }
                    else if (variable.Default.Count > 0)
                    {
                        result.AddRange(ResolveVariables(variable.Default, variables, ref nextIndex));
                    }
                    else if (!known)
                    {
                        result.Add(new SnippetTabStop(nextIndex++, [new SnippetText(variable.Name)]));
                    }

                    break;
                case SnippetTabStop tabStop when tabStop.Placeholder.Count > 0:
                    result.Add(tabStop with { Placeholder = ResolveVariables(tabStop.Placeholder, variables, ref nextIndex) });
                    break;
                default:
                    result.Add(node);
                    break;
            }
        }

        return result;
    }

    /// <summary>The content of the first occurrence of each tab stop that has content, which the others repeat.</summary>
    private static void CollectDefaults(IReadOnlyList<SnippetNode> nodes, Dictionary<int, IReadOnlyList<SnippetNode>> defaults)
    {
        foreach (var node in nodes)
        {
            if (node is SnippetTabStop { Transform: null } tabStop)
            {
                if (tabStop.Choices is { Count: > 0 } choices)
                {
                    defaults.TryAdd(tabStop.Index, [new ResolvedText(choices[0])]);
                }
                else if (tabStop.Placeholder.Count > 0)
                {
                    defaults.TryAdd(tabStop.Index, tabStop.Placeholder);
                    CollectDefaults(tabStop.Placeholder, defaults);
                }
            }
        }
    }

    /// <summary>A variable's value: inserted as it is, without adjusting its indentation.</summary>
    private sealed record ResolvedText(string Value) : SnippetNode;

    /// <summary>Writes the expanded text and records where each tab stop occurrence lands.</summary>
    private sealed class Writer(SnippetInsertion insertion, Dictionary<int, IReadOnlyList<SnippetNode>> defaults)
    {
        public StringBuilder Text { get; } = new();

        public List<SnippetPlaceholder> Placeholders { get; } = [];

        public void Write(IReadOnlyList<SnippetNode> nodes, HashSet<int> filling)
        {
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case SnippetText literal:
                        WriteTemplateText(literal.Value);
                        break;
                    case ResolvedText value:
                        Text.Append(value.Value);
                        break;
                    case SnippetTabStop tabStop:
                        WriteTabStop(tabStop, filling);
                        break;
                }
            }
        }

        private void WriteTabStop(SnippetTabStop tabStop, HashSet<int> filling)
        {
            var start = Text.Length;
            if (tabStop.Transform is { } transform)
            {
                var source = defaults.TryGetValue(tabStop.Index, out var content) ? PlainText(content) : string.Empty;
                Text.Append(transform.Apply(source));
                Placeholders.Add(new SnippetPlaceholder(tabStop.Index, start, Text.Length - start, Transform: transform));
                return;
            }

            if (tabStop.Choices is { Count: > 0 } choices)
            {
                Text.Append(choices[0]);
                Placeholders.Add(new SnippetPlaceholder(tabStop.Index, start, Text.Length - start, choices));
                return;
            }

            if (tabStop.Placeholder.Count > 0)
            {
                Write(tabStop.Placeholder, filling);
            }
            else if (defaults.TryGetValue(tabStop.Index, out var content) && filling.Add(tabStop.Index))
            {
                // A tab stop without content repeats the first one's; the guard stops ${1:${2}} ${2:${1}} from looping.
                Write(content, filling);
                filling.Remove(tabStop.Index);
            }

            Placeholders.Add(new SnippetPlaceholder(tabStop.Index, start, Text.Length - start));
        }

        /// <summary>
        /// Writes text from the snippet itself: each line after a line break gets the insertion line's indentation,
        /// and tabs in the snippet's own indentation become the editor's indentation unit.
        /// </summary>
        private void WriteTemplateText(string value)
        {
            var lines = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (i > 0)
                {
                    Text.Append(insertion.NewLine).Append(insertion.Indentation);
                }

                if (i > 0 || IsAtLineStart())
                {
                    var whitespace = line.Length - line.TrimStart(' ', '\t').Length;
                    Text.Append(line[..whitespace].Replace("\t", insertion.IndentUnit, StringComparison.Ordinal)).Append(line, whitespace, line.Length - whitespace);
                }
                else
                {
                    Text.Append(line);
                }
            }
        }

        /// <summary>Whether the text written so far ends at the start of a snippet line, where tabs are indentation.</summary>
        private bool IsAtLineStart()
        {
            for (var i = Text.Length - 1; i >= 0; i--)
            {
                var c = Text[i];
                if (c == '\n')
                {
                    return true;
                }

                if (c is not ' ' and not '\t')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
