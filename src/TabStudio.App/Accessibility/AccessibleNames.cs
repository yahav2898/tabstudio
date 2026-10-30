using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TabStudio.App.Accessibility;

/// <summary>
/// Gives every control that takes a tap an accessible name, the words TalkBack reads (docs/design/README.md,
/// "Screen readers"), so no view has to name its controls one by one:
/// <list type="bullet">
/// <item>a button, toggle, row or tab is named by the text it shows ("Build Solution", "Program.cs");</item>
/// <item>an icon-only button by its tooltip, which every icon-only button has ("Save All (Ctrl+Shift+S)");</item>
/// <item>a text box, combo box, slider or number box by its placeholder, the label beside it, or its tooltip;</item>
/// <item>a menu item by its header, without the access key's underscore.</item>
/// </list>
/// A name a view sets itself (<see cref="AutomationProperties.NameProperty"/>) always wins. Names follow the control:
/// they are worked out again when its text, tooltip, content or data change, as when a list recycles a row.
/// </summary>
/// <remarks>
/// Without this, Avalonia names a control by its content's class ("Avalonia.Controls.StackPanel") when the content is
/// not a single text block, and leaves an icon-only button with no name at all (TR-06).
/// </remarks>
public static class AccessibleNames
{
    /// <summary>The longest name, in characters; a row's text beyond it is not read.</summary>
    private const int MaxLength = 240;

    /// <summary>How far up from a changed text block its control is looked for.</summary>
    private const int TextDepth = 8;

    /// <summary>The name this gave a control last, which tells it from a name the view set.</summary>
    private static readonly AttachedProperty<string?> NamedHereProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NamedHere", typeof(AccessibleNames));

    private static readonly HashSet<Control> Pending = [];
    private static bool _started;
    private static bool _posted;

    /// <summary>Starts naming controls. Call once, on the UI thread, as the app starts.</summary>
    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Control.LoadedEvent.AddClassHandler<Control>((control, _) => Schedule(control));
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((control, _) => Schedule(control));
        ContentControl.ContentProperty.Changed.AddClassHandler<ContentControl>((control, _) => Schedule(control));
        HeaderedItemsControl.HeaderProperty.Changed.AddClassHandler<HeaderedItemsControl>((control, _) => Schedule(control));
        HeaderedSelectingItemsControl.HeaderProperty.Changed.AddClassHandler<HeaderedSelectingItemsControl>((control, _) => Schedule(control));
        StyledElement.DataContextProperty.Changed.AddClassHandler<Control>((control, _) => Schedule(control));
        TextBox.WatermarkProperty.Changed.AddClassHandler<TextBox>((control, _) => Schedule(control));
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<TemplatedControl>((control, _) => Schedule(control));
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((text, _) => ScheduleOwner(text));
        Visual.IsVisibleProperty.Changed.AddClassHandler<TextBlock>((text, _) => ScheduleOwner(text));
    }

    /// <summary>Whether a control is one TalkBack should name: one that takes a tap or text.</summary>
    /// <param name="control">The control.</param>
    public static bool IsTarget(Control control) => control switch
    {
        ComboBox or MenuItem or TreeViewItem or TabItem => true,
        ItemsControl => false,
        Button or TextBox or Slider or ListBoxItem or AutoCompleteBox or NumericUpDown => true,
        TemplatedControl { Focusable: true } and not UserControl and not ScrollViewer => true,
        _ => false,
    };

    /// <summary>The name this would give a control, from what it shows; <see langword="null"/> when it shows nothing to read.</summary>
    /// <param name="control">The control.</param>
    public static string? NameOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        var tip = TipOf(control);
        var name = control switch
        {
            TextBox box => First(box.Watermark, LabelOf(box), tip),
            AutoCompleteBox box => First(box.Watermark, LabelOf(box), tip),
            ComboBox box => First(LabelOf(box), tip, box.PlaceholderText),
            NumericUpDown or Slider => First(LabelOf(control), tip),
            MenuItem item => First(HeaderOf(item.Header), TextIn(item), tip),
            TreeViewItem item => First(HeaderOf(item.Header), TextIn(item), tip),
            TabItem tab => First(HeaderOf(tab.Header), tip),
            AvaloniaEdit.Editing.TextArea area => First(EditorNameOf(area), tip, "Text editor"),
            ToggleSwitch toggle => First(HeaderOf(toggle.Content), TextIn(toggle), LabelOf(toggle), tip),
            ContentControl content => First(HeaderOf(content.Content), TextIn(content), tip),
            _ => First(TextIn(control), tip),
        };
        return name is null ? null : Clean(name);
    }

    /// <summary>Names a control now, unless a view named it.</summary>
    /// <param name="control">The control.</param>
    public static void Update(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!IsTarget(control) || A11y.GetIsContent(control) || IsInContent(control))
        {
            return;
        }

        var current = AutomationProperties.GetName(control);
        if (!string.IsNullOrEmpty(current) && current != control.GetValue(NamedHereProperty))
        {
            // The view's own name.
            return;
        }

        var name = NameOf(control);
        if (name == current)
        {
            return;
        }

        control.SetValue(NamedHereProperty, name);
        control.SetCurrentValue(AutomationProperties.NameProperty, name);
    }

    private static void Schedule(Control control)
    {
        if (!_started || !IsTarget(control))
        {
            return;
        }

        Pending.Add(control);
        if (!_posted)
        {
            _posted = true;
            Dispatcher.UIThread.Post(Flush, DispatcherPriority.Background);
        }
    }

    /// <summary>A text block changed: the control around it that it names is named again.</summary>
    private static void ScheduleOwner(TextBlock text)
    {
        if (!_started || text.Parent is null)
        {
            return;
        }

        var depth = 0;
        foreach (var ancestor in text.GetVisualAncestors())
        {
            if (++depth > TextDepth)
            {
                return;
            }

            if (ancestor is Control control && IsTarget(control))
            {
                Schedule(control);
                return;
            }
        }
    }

    private static void Flush()
    {
        _posted = false;
        var controls = Pending.ToArray();
        Pending.Clear();
        foreach (var control in controls)
        {
            if (control.IsAttachedToVisualTree())
            {
                Update(control);
            }
        }
    }

    private static bool IsInContent(Control control) =>
        control.GetVisualAncestors().Any(a => a is Control c && A11y.GetIsContent(c));

    private static string? TipOf(Control control) => ToolTip.GetTip(control) switch
    {
        string text => text,
        TextBlock block => block.Text,
        Control tip => TextIn(tip),
        _ => null,
    };

    /// <summary>The text a header or content shows when it is a string, without an access key's underscore.</summary>
    private static string? HeaderOf(object? header) => header is string text ? text.Replace("__", "\u0001", StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).Replace('\u0001', '_') : null;

    /// <summary>
    /// The text a control shows, its text blocks in order, joined by commas; not the text of controls inside it that
    /// take taps of their own (a row's buttons), nor of what is hidden.
    /// </summary>
    /// <summary>An editor's text is named as its editor is: "Output", "SQL query", or the label beside it.</summary>
    private static string? EditorNameOf(AvaloniaEdit.Editing.TextArea area) =>
        area.GetVisualAncestors().OfType<AvaloniaEdit.TextEditor>().FirstOrDefault() is { } editor
            ? First(AutomationProperties.GetName(editor), TipOf(editor), LabelOf(editor))
            : null;

    private static string? TextIn(Control control)
    {
        var text = new List<string>();
        var states = new List<string>();

        // A control's content is read before its template shows it, as in a panel that is not open yet.
        if (control is ContentControl { Content: Control content } && control.GetVisualChildren().FirstOrDefault() is null)
        {
            if (content is TextBlock block && TextOf(block) is { } shown)
            {
                return Clean(shown);
            }

            Collect(content, control, text, states);
        }
        else
        {
            Collect(control, control, text, states);
        }

        var all = text.Concat(states).ToList();
        return all.Count == 0 ? null : string.Join(", ", all);
    }

    /// <summary>
    /// Collects the text a control shows, in order, and the names of the icons in it that mean something (an error's
    /// glyph, a folder's chevron), which are read after the text: "Program.cs, C#", then "Error" or "expanded".
    /// </summary>
    private static void Collect(Visual visual, Control owner, List<string> text, List<string> states)
    {
        foreach (var child in visual.GetVisualChildren())
        {
            if (!child.IsVisible || text.Sum(t => t.Length + 2) > MaxLength)
            {
                continue;
            }

            if (child is Control nested && !ReferenceEquals(nested, owner) && IsTarget(nested) && nested.TemplatedParent is null)
            {
                continue;
            }

            if (child is TextBlock block)
            {
                if (TextOf(block) is { } shown && !string.IsNullOrWhiteSpace(shown))
                {
                    text.Add(shown.Trim());
                }

                continue;
            }

            if (child is Control part && AutomationProperties.GetName(part) is { } named && !string.IsNullOrWhiteSpace(named))
            {
                states.Add(named.Trim());
                continue;
            }

            Collect(child, owner, text, states);
        }
    }

    private static string? TextOf(TextBlock block) =>
        block.Text ?? (block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : null);

    /// <summary>
    /// The label of a field: a text block just before it in its panel ("Name" above a text box), or in the same row of
    /// its grid, or the header of the control around it.
    /// </summary>
    private static string? LabelOf(Control field)
    {
        if (AutomationProperties.GetLabeledBy(field) is not null)
        {
            return null;
        }

        var child = field;
        for (var level = 0; level < 3; level++)
        {
            if (child.Parent is not Control parent)
            {
                return null;
            }

            if (parent is Panel panel)
            {
                var index = panel.Children.IndexOf(child);
                if (panel is Grid)
                {
                    var row = Grid.GetRow(child);
                    var column = Grid.GetColumn(child);
                    var label = panel.Children
                        .Where(c => c.IsVisible && !ReferenceEquals(c, child) && Grid.GetRow(c) == row && Grid.GetColumn(c) < column && LabelText(c) is not null)
                        .MaxBy(Grid.GetColumn);
                    if (label is not null && LabelText(label) is { } text)
                    {
                        return text;
                    }
                }

                for (var i = index - 1; i >= 0 && i >= index - 2; i--)
                {
                    if (panel.Children[i] is TextBlock { IsVisible: true } block && TextOf(block) is { Length: > 0 } text)
                    {
                        return text;
                    }
                }
            }
            else if (parent is HeaderedContentControl { Header: string header })
            {
                return header;
            }

            child = parent;
        }

        return null;
    }

    /// <summary>The text of a label: a text block's, or the first text block's in a panel (a title above its detail).</summary>
    private static string? LabelText(Control label) => label switch
    {
        TextBlock block => TextOf(block) is { Length: > 0 } text ? text : null,
        Panel panel => panel.Children.OfType<TextBlock>().Select(TextOf).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
        _ => null,
    };

    private static string? First(params string?[] candidates) => candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    private static string Clean(string name)
    {
        var text = name.ReplaceLineEndings(" ").Trim().TrimEnd(':').Trim();
        return text.Length <= MaxLength ? text : text[..MaxLength] + "…";
    }
}
