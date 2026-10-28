using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Design.Surface;

/// <summary>
/// The design surface of one form: its designer file read into a document, the live form built from it, and the
/// edits the designer makes, each of which writes the designer file again and can be undone. Nothing of the app's own
/// code runs: the form is built from the Forms layer's controls and the values in the designer file.
/// </summary>
/// <remarks>Used on the UI thread only: the live form has no window and no thread of its own.</remarks>
internal sealed partial class DesignSurface
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private readonly string _rootTypeName;
    private readonly bool _simplified;
    private readonly DesignerLanguage _language;
    private DesignerFile? _file;

    /// <summary>Opens a designer file.</summary>
    /// <param name="designerText">The designer file's text, such as Form1.Designer.cs's or Form1.Designer.vb's.</param>
    /// <param name="rootTypeName">The designed class's base type from its code file, such as <c>Form</c>.</param>
    /// <param name="simplified">
    /// Whether the project imports System.Drawing and System.Windows.Forms, as one with implicit usings does, and every
    /// Visual Basic Windows Forms project does: the designer then writes <c>button1.Location = new Point(12, 12);</c>
    /// (<c>Button1.Location = New Point(12, 12)</c>), as Visual Studio does for such projects, even in a file written
    /// with <c>this.</c> or <c>Me.</c> and namespaces, such as the Windows Forms App templates'.
    /// </param>
    /// <param name="language">The designer file's language.</param>
    /// <param name="codeText">
    /// The form's code file's text, such as Form1.vb's, whose handlers' Handles clauses wire a Visual Basic form's
    /// events; see <see cref="CodeText"/>.
    /// </param>
    public DesignSurface(string designerText, string rootTypeName = "Form", bool simplified = false, DesignerLanguage language = DesignerLanguage.CSharp, string? codeText = null)
    {
        ArgumentNullException.ThrowIfNull(designerText);
        _rootTypeName = rootTypeName;
        _simplified = simplified;
        _language = language;
        _codeText = codeText;
        Text = designerText;
        Load(designerText);
    }

    /// <summary>Raised after an edit, an undo or a redo changed the designer file.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Raised after components were deleted, with their names and those of the controls and items inside them, for a
    /// Visual Basic form's handlers to let go of them (see <see cref="ForgetHandlers"/>).
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? Deleted;

    /// <summary>The designer file's text as the designer last wrote it.</summary>
    public string Text { get; private set; }

    /// <summary>Why the file could not be designed, or <see langword="null"/>.</summary>
    public string? LoadError { get; private set; }

    /// <summary>What the designer could not show of the file, such as a value only the app's code can work out.</summary>
    public IReadOnlyList<string> Problems { get; private set; } = [];

    /// <summary>The designed form.</summary>
    public DesignDocument? Document => _file?.Document;

    /// <summary>The designed class's name, such as <c>Form1</c>.</summary>
    public string ClassName => _file?.ClassName ?? string.Empty;

    /// <summary>The designed class's namespace.</summary>
    public string Namespace => _file?.Namespace ?? string.Empty;

    /// <summary>The designer file's language.</summary>
    public DesignerLanguage Language => _language;

    /// <summary>The live form or user control.</summary>
    public Control? Root => Document?.Root.Instance as Control;

    /// <summary>Whether an edit can be undone.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether an undone edit can be made again.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Reads the designer file again, as when it changed in the code editor. Its edits can no longer be undone.</summary>
    /// <param name="designerText">The text.</param>
    public void Reload(string designerText)
    {
        ArgumentNullException.ThrowIfNull(designerText);
        if (string.Equals(designerText, Text, StringComparison.Ordinal) && _file is not null)
        {
            return;
        }

        _undo.Clear();
        _redo.Clear();
        Text = designerText;
        Load(designerText);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the last edit.</summary>
    public void Undo()
    {
        if (_undo.TryPop(out var previous))
        {
            _redo.Push(Text);
            Text = previous;
            Load(previous);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Makes the last undone edit again.</summary>
    public void Redo()
    {
        if (_redo.TryPop(out var next))
        {
            _undo.Push(Text);
            Text = next;
            Load(next);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The live form drawn as it looks now, its client area at the origin.</summary>
    public DisplayList Paint()
    {
        var builder = new DisplayListBuilder();
        if (Root is { } root)
        {
            root.PaintClient(builder, new Rectangle(Point.Empty, root.ClientSize), Point.Empty);
        }

        return builder.Build();
    }

    /// <summary>
    /// The form's window frame as the app preview draws it, around the client area at the origin; a user control has
    /// no frame. The title bar shows the form's text and its buttons as the form's properties say.
    /// </summary>
    public WindowFrame? Frame(DisplayList content)
    {
        if (Root is not Form form)
        {
            return null;
        }

        var client = new Rectangle(form.ClientOriginOnScreen, form.ClientSize);
        var frame = form.VisibleFrameOnScreen;
        frame.Offset(-client.X, -client.Y);
        return new WindowFrame(0, form.Text, frame, new Rectangle(Point.Empty, client.Size), form.Chrome, false, false, false, true, false, content, [], null, null);
    }

    /// <summary>A component's bounds in the root's client coordinates; empty for a component that is not drawn, such as a timer.</summary>
    /// <param name="component">The component.</param>
    public static Rectangle BoundsOf(DesignComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return component.Instance switch
        {
            Control control when component.IsRoot => new Rectangle(Point.Empty, control.ClientSize),
            Control control => control.BoundsInWindow(),
            ToolStripItem { Owner: { } owner } item when owner.Visible || owner.Parent is not null =>
                new Rectangle(owner.BoundsInWindow().Location + (Size)item.Bounds.Location, item.Bounds.Size),
            _ => Rectangle.Empty,
        };
    }

    /// <summary>The bounds of a component in its parent's client coordinates, as its Location and Size properties say.</summary>
    /// <param name="component">The component.</param>
    public static Rectangle LocalBoundsOf(DesignComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        return component.Instance switch
        {
            Control control when component.IsRoot => new Rectangle(Point.Empty, control.ClientSize),
            Control control => control.Bounds,
            _ => BoundsOf(component),
        };
    }

    /// <summary>The innermost component at a point of the root's client area: a control, a strip's item, or the root.</summary>
    /// <param name="point">The point.</param>
    public DesignComponent? HitTest(Point point)
    {
        if (Document is not { } document || Root is not { } root)
        {
            return null;
        }

        var best = document.Root;
        Control current = root;
        while (true)
        {
            Control? hit = null;
            foreach (var child in current.Controls.Cast<Control>())
            {
                if (child.VisibleSelf && child.BoundsInWindow().Contains(point))
                {
                    hit = child;
                    break;
                }
            }

            if (hit is null)
            {
                return best;
            }

            if (document.Of(hit) is { } component)
            {
                best = component;
            }

            if (hit is ToolStrip strip)
            {
                var origin = strip.BoundsInWindow().Location;
                foreach (ToolStripItem item in strip.Items)
                {
                    if (item.Available && document.Of(item) is { } itemComponent && new Rectangle(origin + (Size)item.Bounds.Location, item.Bounds.Size).Contains(point))
                    {
                        return itemComponent;
                    }
                }

                return best;
            }

            if (!new Rectangle(hit.ClientOriginInWindow(), hit.ClientSize).Contains(point))
            {
                return best;
            }

            current = hit;
        }
    }

    /// <summary>The component a component belongs to, and the collection it is in: its container, or the strip of a strip's item.</summary>
    /// <param name="component">The component.</param>
    public (DesignComponent Owner, string Collection)? ParentOf(DesignComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (Document is not { } document)
        {
            return null;
        }

        foreach (var owner in document.All)
        {
            foreach (var add in owner.Adds)
            {
                if (add.Items.Any(item => FirstName(item) == component.Name))
                {
                    return (owner, add.Collection);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The innermost container at a point, where a control dropped there goes: a panel, a group box, a tab page, a
    /// split container's panel, or the root; never a control being moved or one inside it.
    /// </summary>
    /// <param name="point">The point, in the root's client coordinates.</param>
    /// <param name="moving">The controls being moved, if any.</param>
    /// <returns>The container's component, the collection its children go in, and its client area's origin in the root.</returns>
    public (DesignComponent Owner, string Collection, Point Origin) ContainerAt(Point point, IReadOnlyCollection<DesignComponent>? moving = null)
    {
        var document = Document ?? throw new InvalidOperationException("The file is not loaded.");
        var root = Root!;
        var result = (document.Root, "Controls", Point.Empty);
        Control current = root;
        while (true)
        {
            var hit = current.Controls.Cast<Control>().FirstOrDefault(c => c.VisibleSelf && c.BoundsInWindow().Contains(point));
            if (hit is null || (moving is not null && moving.Any(m => ReferenceEquals(m.Instance, hit))))
            {
                return result;
            }

            if (hit is SplitterPanel panel && panel.Parent is SplitContainer split && document.Of(split) is { } splitComponent)
            {
                result = (splitComponent, ReferenceEquals(split.Panel1, panel) ? "Panel1.Controls" : "Panel2.Controls", panel.ClientOriginInWindow());
            }
            else if (IsContainer(hit) && document.Of(hit) is { } container)
            {
                result = (container, "Controls", hit.ClientOriginInWindow());
            }
            else if (document.Of(hit) is not null && hit is not SplitContainer)
            {
                return result;
            }

            current = hit;
        }
    }

    /// <summary>Moves or resizes controls on the surface while a finger drags them, without writing the file yet.</summary>
    /// <param name="bounds">Each control's new bounds in its parent's client coordinates.</param>
    public void Preview(IEnumerable<(DesignComponent Component, Rectangle Bounds)> bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        foreach (var (component, rectangle) in bounds)
        {
            switch (component.Instance)
            {
                case Form form when component.IsRoot:
                    form.ClientSize = rectangle.Size;
                    break;
                case Control control when component.IsRoot:
                    control.Size = rectangle.Size;
                    break;
                case Control control:
                    control.Bounds = rectangle;
                    break;
            }
        }

        Root?.PerformLayout();
    }

    /// <summary>Whether controls can be put in a control in the designer.</summary>
    /// <param name="control">The control.</param>
    public static bool IsContainer(Control control) =>
        control is Panel or GroupBox or TabPage or UserControl or Form || (control is ContainerControl and not SplitContainer and not UpDownBase);

    /// <summary>The component a collection item names: the first argument, as in <c>button1</c> or <c>button1, 0, 0</c>.</summary>
    internal static string FirstName(string item) => item.Split(',', 2)[0].Trim();

    /// <summary>
    /// Turns a document written with <c>this.</c> or <c>Me.</c> and namespaces into the simplified style; the file
    /// changes on the first edit.
    /// </summary>
    private static void Simplify(DesignDocument document)
    {
        var style = document.Style;
        string Simplified(string code) => DesignCode.Simplify(style, code);
        document.Style = style with { QualifyWithThis = false, FullTypeNames = false };
        document.ContainerStatement = document.ContainerStatement is null ? null : Simplified(document.ContainerStatement);
        for (var i = 0; i < document.LeadingStatements.Count; i++)
        {
            document.LeadingStatements[i] = Simplified(document.LeadingStatements[i]);
        }

        foreach (var component in document.All)
        {
            if (!component.IsRoot)
            {
                component.TypeName = Simplified(component.TypeName);
                component.ConstructorArguments = Simplified(component.ConstructorArguments);
            }

            foreach (var property in component.Properties.ToList())
            {
                component.SetCode(property.Path, Simplified(property.Code));
            }

            foreach (var add in component.Adds)
            {
                add.RangeType = add.RangeType is null ? null : Simplified(add.RangeType);
                for (var i = 0; i < add.Items.Count; i++)
                {
                    add.Items[i] = Simplified(add.Items[i]);
                }
            }

            for (var i = 0; i < component.OtherStatements.Count; i++)
            {
                component.OtherStatements[i] = Simplified(component.OtherStatements[i]);
            }
        }
    }

    private void Load(string text)
    {
        try
        {
            _file = DesignerFile.Parse(text, _rootTypeName, _language);
            if (_file is null)
            {
                LoadError = "The file has no InitializeComponent method to design.";
                Problems = [];
                return;
            }

            LoadError = null;
            if (_simplified && (_file.Document.Style.QualifyWithThis || _file.Document.Style.FullTypeNames))
            {
                Simplify(_file.Document);
            }

            ApplyHandles(_file.Document);
            Problems = SurfaceBuilder.Build(_file.Document);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A form the designer cannot build is reported, never thrown at the IDE, as Visual Studio shows its error page.
            LoadError = $"The form could not be built: {ex.Message}";
            _file = null;
        }
    }
}
