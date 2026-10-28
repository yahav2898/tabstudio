using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Surface;

/// <summary>
/// Event handlers, which live in the form's code file: a C# form wires them in its designer file
/// (<c>button1.Click += button1_Click;</c>), a Visual Basic form with the handlers' Handles clauses
/// (<c>Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click</c>), as Visual Studio does.
/// </summary>
internal sealed partial class DesignSurface
{
    private string? _codeText;

    /// <summary>
    /// The form's code file's text, as the designer last saw it: a Visual Basic form's events are those its handlers'
    /// Handles clauses handle, which the Properties panel's events show. Setting it shows the events it handles.
    /// </summary>
    public string? CodeText
    {
        get => _codeText;
        set
        {
            if (string.Equals(value, _codeText, StringComparison.Ordinal))
            {
                return;
            }

            _codeText = value;
            if (_language == DesignerLanguage.VisualBasic && _file is { } file)
            {
                ApplyHandles(file.Document);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Creates the handler of an event in the form's code file, or goes to the one there is, and wires it to the event:
    /// in C#, in the designer file; in Visual Basic, with a Handles clause on the handler, which a handler the event had
    /// before gives up. As a double-tap does in Visual Studio.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="component">The component.</param>
    /// <param name="eventName">The event.</param>
    /// <param name="handler">The handler's name, such as <c>button1_Click</c>.</param>
    /// <param name="delegateType">The event's delegate type.</param>
    /// <returns>The code file's new text, and the offset in it of the line inside the handler's body.</returns>
    /// <exception cref="ArgumentException">The name is not a method's.</exception>
    /// <exception cref="InvalidOperationException">The code file has no class of the form's name.</exception>
    public (string Text, int Caret) AttachHandler(string codeText, DesignComponent component, string eventName, string handler, Type delegateType)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(delegateType);
        var style = Document?.Style ?? CodeStyle.Simplified;
        if (_language != DesignerLanguage.VisualBasic)
        {
            var added = CodeBehind.AddHandler(codeText, ClassName, handler, delegateType, style.FullTypeNames);
            if (component.GetHandler(eventName) != handler)
            {
                SetHandler(component, eventName, handler);
            }

            return added;
        }

        ThrowIfNotMethodName(handler);
        var target = Document?.Find(component.Name) ?? component;
        if (target.Events.Find(e => e.Event == eventName && !e.IsHandlesClause) is { } wired)
        {
            // An event the designer file wires with AddHandler stays wired there.
            var result = VisualBasicCodeBehind.AddHandler(codeText, ClassName, handler, delegateType, null, eventName, style.FullTypeNames);
            if (!string.Equals(wired.Handler, handler, StringComparison.OrdinalIgnoreCase))
            {
                SetHandler(component, eventName, handler);
            }

            CodeText = result.Text;
            return result;
        }

        var text = codeText;
        if (target.GetHandler(eventName) is { } current && !string.Equals(current, handler, StringComparison.OrdinalIgnoreCase))
        {
            text = VisualBasicCodeBehind.RemoveHandler(text, ClassName, target.IsRoot ? null : target.Name, eventName);
        }

        var source = target.IsRoot ? VisualBasicCodeBehind.FormEventSource : target.Name;
        var attached = VisualBasicCodeBehind.AddHandler(text, ClassName, handler, delegateType, source, eventName, style.FullTypeNames);
        CodeText = attached.Text;
        return attached;
    }

    /// <summary>
    /// Takes the handler off an event, leaving its method in the code file, as clearing it in the Properties panel does:
    /// in C#, in the designer file; in Visual Basic, from the handler's Handles clause.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="component">The component.</param>
    /// <param name="eventName">The event.</param>
    /// <returns>The code file's new text.</returns>
    public string DetachHandler(string codeText, DesignComponent component, string eventName)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(eventName);
        if (_language != DesignerLanguage.VisualBasic)
        {
            SetHandler(component, eventName, null);
            return codeText;
        }

        var target = Document?.Find(component.Name) ?? component;
        if (target.Events.Any(e => e.Event == eventName && !e.IsHandlesClause))
        {
            SetHandler(component, eventName, null);
        }

        var text = VisualBasicCodeBehind.RemoveHandler(codeText, ClassName, target.IsRoot ? null : target.Name, eventName);
        CodeText = text;
        return text;
    }

    /// <summary>
    /// Renames a renamed component in the form's code file: wherever it names the field, and in Visual Basic its
    /// Handles clauses and the handlers named after it, such as <c>Button1_Click</c>.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="oldName">The old name.</param>
    /// <param name="newName">The new name.</param>
    /// <returns>The code file's new text.</returns>
    public string RenameInCode(string codeText, string oldName, string newName)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        if (_language != DesignerLanguage.VisualBasic)
        {
            return CodeBehind.RenameField(codeText, oldName, newName);
        }

        var text = VisualBasicCodeBehind.RenameField(codeText, ClassName, oldName, newName);
        CodeText = text;
        return text;
    }

    /// <summary>
    /// Takes deleted components off the Handles clauses of a Visual Basic form's code file, as Visual Studio does when
    /// it deletes them, since a Handles clause cannot name a field that is gone; their handlers stay. A C# form's
    /// designer file let go of their handlers with them.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="names">The deleted components' names, as <see cref="Deleted"/> gives them.</param>
    /// <returns>The code file's new text.</returns>
    public string ForgetHandlers(string codeText, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(names);
        if (_language != DesignerLanguage.VisualBasic)
        {
            return codeText;
        }

        var text = codeText;
        foreach (var name in names)
        {
            text = VisualBasicCodeBehind.RemoveHandlesOf(text, ClassName, name);
        }

        CodeText = text;
        return text;
    }

    private void ThrowIfNotMethodName(string handler)
    {
        if (!DesignCode.IsValidName(_language, handler))
        {
            throw new ArgumentException($"'{handler}' is not a valid method name: use letters, digits and underscores, starting with a letter.", nameof(handler));
        }
    }

    /// <summary>
    /// Gives a Visual Basic document's components the events the code file's Handles clauses handle, in place of
    /// those it gave them before.
    /// </summary>
    private void ApplyHandles(DesignDocument document)
    {
        if (_language != DesignerLanguage.VisualBasic)
        {
            return;
        }

        foreach (var component in document.All)
        {
            component.Events.RemoveAll(e => e.IsHandlesClause);
        }

        if (_codeText is null)
        {
            return;
        }

        foreach (var (source, eventName, handler) in VisualBasicCodeBehind.HandlersOf(_codeText, document.Root.Name))
        {
            var component = source.Length == 0 ? document.Root : document.Find(source);
            if (component is null)
            {
                continue;
            }

            // The event as its type names it, whatever the case the clause writes it in.
            var type = component.Type ?? component.Instance?.GetType();
            var name = type?.GetEvents().FirstOrDefault(e => string.Equals(e.Name, eventName, StringComparison.OrdinalIgnoreCase))?.Name ?? eventName;
            if (component.GetHandler(name) is null)
            {
                component.Events.Add(new DesignEvent(name, handler) { IsHandlesClause = true });
            }
        }
    }
}
