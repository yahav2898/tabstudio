using System.Drawing;
using System.Windows.Forms;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Forms.Design.Catalog;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Surface;

/// <summary>
/// The designer's edits: adding, moving, resizing, deleting, renaming and setting properties and event handlers. Each
/// changes the document, writes the designer file, reads it back and builds the form again, so the surface always
/// shows what the file says; the file as it was is kept for Undo.
/// </summary>
internal sealed partial class DesignSurface
{
    /// <summary>The grid controls snap to, as in Visual Studio's grid mode.</summary>
    public const int GridSize = 8;

    /// <summary>Snaps a coordinate to the grid.</summary>
    /// <param name="value">The coordinate.</param>
    public static int Snap(int value) => (int)Math.Round(value / (double)GridSize) * GridSize;

    /// <summary>Adds a new control or component of a type, as dropping it from the Toolbox does.</summary>
    /// <param name="type">The type.</param>
    /// <param name="point">Where it goes, in the root's client coordinates; it is snapped to the grid in its container.</param>
    /// <returns>The new component's name.</returns>
    public string Add(Type type, Point point)
    {
        ArgumentNullException.ThrowIfNull(type);
        var document = Document ?? throw new InvalidOperationException(LoadError);
        var (owner, collection, origin) = ContainerAt(point);
        var name = document.NewName(type);
        Edit(doc =>
        {
            var style = doc.Style;
            var component = new DesignComponent(name, TypeResolver.NameOf(type, style.FullTypeNames)) { Type = type };
            if (ComponentDefaults.JoinsContainer(type))
            {
                component.ConstructorArguments = style.Member("components");
                doc.HasContainer = true;
            }

            doc.Components.Add(component);
            if (!ComponentDefaults.IsVisual(type) || !typeof(Control).IsAssignableFrom(type))
            {
                return;
            }

            var target = doc.Find(owner.Name)!;
            var prototype = (Control)Activator.CreateInstance(type)!;
            if (ComponentDefaults.SizesItself(type))
            {
                component.SetCode("AutoSize", "true");
                prototype.AutoSize = true;
            }

            if (ComponentDefaults.ShowsNameAsText(type))
            {
                component.SetCode("Text", ValueWriter.Literal(name));
                prototype.Text = name;
            }

            var size = prototype.AutoSize ? prototype.GetPreferredSize(Size.Empty) : prototype.Size;
            var location = prototype.Dock == DockStyle.None
                ? new Point(Snap(point.X - origin.X), Snap(point.Y - origin.Y))
                : Point.Empty;
            component.SetCode("Location", ValueWriter.Write(location, style));
            component.SetCode("Name", ValueWriter.Literal(name));
            component.SetCode("Size", ValueWriter.Write(size, style));
            component.SetCode("TabIndex", target.Collection(collection).Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (ComponentDefaults.UsesVisualStyleBackColor(type))
            {
                component.SetCode("UseVisualStyleBackColor", "true");
            }

            if (prototype is GroupBox)
            {
                component.SetCode("TabStop", "false");
            }

            // A new control goes to the front, which is the start of its container's controls.
            target.Collection(collection).Items.Insert(0, name);
            if (prototype is MenuStrip && doc.Root.GetCode("MainMenuStrip") is null && doc.Root.Type is { } rootType && typeof(Form).IsAssignableFrom(rootType))
            {
                doc.Root.SetCode("MainMenuStrip", style.Member(name));
            }
        });
        return name;
    }

    /// <summary>Adds an item to a strip, such as a menu item to a menu strip or one of its menus, as typing in "Type Here" does.</summary>
    /// <param name="owner">The strip, or the menu item whose drop-down gets the item.</param>
    /// <param name="type">The item's type, such as <see cref="ToolStripMenuItem"/>.</param>
    /// <param name="text">The item's text.</param>
    /// <returns>The new item's name.</returns>
    public string AddItem(DesignComponent owner, Type type, string text)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(text);
        var document = Document ?? throw new InvalidOperationException(LoadError);
        var name = document.NewName(type);
        Edit(doc =>
        {
            var style = doc.Style;
            var component = new DesignComponent(name, TypeResolver.NameOf(type, style.FullTypeNames)) { Type = type };
            doc.Components.Add(component);
            component.SetCode("Name", ValueWriter.Literal(name));
            component.SetCode("Text", ValueWriter.Literal(text));
            var target = doc.Find(owner.Name)!;
            var items = target.Collection(target.Type is { } ownerType && typeof(ToolStripDropDownItem).IsAssignableFrom(ownerType) ? "DropDownItems" : "Items");
            items.IsRange = true;
            items.RangeType ??= TypeResolver.NameOf(typeof(ToolStripItem), style.FullTypeNames);
            items.Items.Add(name);
        });
        return name;
    }

    /// <summary>Moves and resizes components, as dragging them or their handles does.</summary>
    /// <param name="bounds">Each component's new bounds in its parent's client coordinates; for the root, its client size.</param>
    public void SetBounds(IReadOnlyCollection<(DesignComponent Component, Rectangle Bounds)> bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        Edit(doc =>
        {
            foreach (var (component, rectangle) in bounds)
            {
                var target = doc.Find(component.Name)!;
                if (target.IsRoot)
                {
                    target.SetCode(target.Instance is Form || target.Type is { } t && typeof(Form).IsAssignableFrom(t) ? "ClientSize" : "Size", ValueWriter.Write(rectangle.Size, doc.Style));
                    continue;
                }

                target.SetCode("Location", ValueWriter.Write(rectangle.Location, doc.Style));
                target.SetCode("Size", ValueWriter.Write(rectangle.Size, doc.Style));
            }
        });
    }

    /// <summary>Moves a control into another container, as dropping it on a panel or a group box does.</summary>
    /// <param name="component">The control.</param>
    /// <param name="owner">The new container.</param>
    /// <param name="collection">The container's collection, such as <c>Controls</c> or <c>Panel1.Controls</c>.</param>
    /// <param name="location">Its location in the new container's client coordinates.</param>
    public void Reparent(DesignComponent component, DesignComponent owner, string collection, Point location)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(owner);
        Edit(doc =>
        {
            RemoveReferences(doc, component.Name, keepAssignments: true);
            var target = doc.Find(owner.Name)!;
            target.Collection(collection).Items.Insert(0, component.Name);
            doc.Find(component.Name)!.SetCode("Location", ValueWriter.Write(location, doc.Style));
        });
    }

    /// <summary>Brings controls to the front of their containers, or sends them to the back.</summary>
    /// <param name="components">The controls.</param>
    /// <param name="front">Whether they go to the front.</param>
    public void ChangeZOrder(IReadOnlyCollection<DesignComponent> components, bool front)
    {
        ArgumentNullException.ThrowIfNull(components);
        var names = components.Where(c => !c.IsRoot).Select(c => c.Name).ToList();
        Edit(doc =>
        {
            foreach (var name in names)
            {
                foreach (var add in doc.All.SelectMany(c => c.Adds).Where(a => !a.IsRange && a.Collection.EndsWith("Controls", StringComparison.Ordinal)))
                {
                    var index = add.Items.FindIndex(item => FirstName(item) == name);
                    if (index >= 0)
                    {
                        var item = add.Items[index];
                        add.Items.RemoveAt(index);
                        add.Items.Insert(front ? 0 : add.Items.Count, item);
                    }
                }
            }
        });
    }

    /// <summary>Sets a property, as the Properties panel does; a value equal to a new control's is not written.</summary>
    /// <param name="component">The component.</param>
    /// <param name="path">The property's path, such as <c>Text</c> or <c>Font</c>.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">The value cannot be written as C#.</exception>
    public void SetProperty(DesignComponent component, string path, object? value)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(path);
        if (path == "Name")
        {
            Rename(component, value as string ?? string.Empty);
            return;
        }

        var style = Document?.Style ?? CodeStyle.Simplified;
        var code = ValueWriter.Write(value, style) ?? throw new ArgumentException($"{value} cannot be written in the designer file.", nameof(value));
        Edit(doc =>
        {
            var target = doc.Find(component.Name)!;
            var type = target.Type ?? target.Instance?.GetType();
            target.SetCode(path, type is null || ComponentDefaults.IsWritten(type, path, value) ? code : null);
        });
    }

    /// <summary>Sets or removes the handler of an event.</summary>
    /// <param name="component">The component.</param>
    /// <param name="eventName">The event.</param>
    /// <param name="handler">The handler method's name, or <see langword="null"/> to remove it.</param>
    /// <exception cref="ArgumentException">The name is not a C# identifier.</exception>
    public void SetHandler(DesignComponent component, string eventName, string? handler)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(eventName);
        if (handler is not null && (!SyntaxFacts.IsValidIdentifier(handler) || SyntaxFacts.GetKeywordKind(handler) != SyntaxKind.None))
        {
            throw new ArgumentException($"'{handler}' is not a valid method name: use letters, digits and underscores, starting with a letter.", nameof(handler));
        }

        Edit(doc => doc.Find(component.Name)!.SetHandler(eventName, handler));
    }

    /// <summary>Renames a component: its field, its Name, and the designer code that names it.</summary>
    /// <param name="component">The component.</param>
    /// <param name="name">The new name.</param>
    /// <exception cref="ArgumentException">The name is not a C# identifier, or another component has it.</exception>
    public void Rename(DesignComponent component, string name)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(name);
        if (component.IsRoot)
        {
            throw new ArgumentException("The form's name is its class's name: rename the class in its code.", nameof(name));
        }

        if (!SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
        {
            throw new ArgumentException($"'{name}' is not a valid name: use letters, digits and underscores, starting with a letter.", nameof(name));
        }

        if (name == component.Name)
        {
            return;
        }

        if (Document?.Find(name) is not null)
        {
            throw new ArgumentException($"There is already a component named '{name}'.", nameof(name));
        }

        var old = component.Name;
        Edit(doc =>
        {
            var target = doc.Find(old)!;
            target.Name = name;
            target.SetCode("Name", ValueWriter.Literal(name));
            foreach (var each in doc.All)
            {
                foreach (var add in each.Adds)
                {
                    for (var i = 0; i < add.Items.Count; i++)
                    {
                        add.Items[i] = CodeTokens.Rename(add.Items[i], old, name);
                    }
                }

                foreach (var property in each.Properties.ToList())
                {
                    each.SetCode(property.Path, CodeTokens.Rename(property.Code, old, name));
                }

                for (var i = 0; i < each.OtherStatements.Count; i++)
                {
                    each.OtherStatements[i] = CodeTokens.Rename(each.OtherStatements[i], old, name);
                }
            }
        });
    }

    /// <summary>Deletes components, with the controls and items inside them, and the designer code that names them.</summary>
    /// <param name="components">The components; the root is never deleted.</param>
    public void Delete(IReadOnlyCollection<DesignComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        var names = components.Where(c => !c.IsRoot).Select(c => c.Name).ToList();
        if (names.Count == 0)
        {
            return;
        }

        Edit(doc =>
        {
            foreach (var name in names.SelectMany(n => WithDescendants(doc, n)).Distinct().ToList())
            {
                if (doc.Find(name) is { } removed)
                {
                    doc.Components.Remove(removed);
                }

                RemoveReferences(doc, name, keepAssignments: false);
            }
        });
    }

    /// <summary>
    /// Changes the document and writes the file, keeping the file as it was for Undo; the surface is then built again
    /// from the file it wrote.
    /// </summary>
    private void Edit(Action<DesignDocument> change)
    {
        if (_file is not { } file)
        {
            throw new InvalidOperationException(LoadError ?? "The file is not loaded.");
        }

        var before = Text;
        string after;
        try
        {
            change(file.Document);

            // As Visual Studio, the designer names the form and records the font it was designed with.
            var root = file.Document.Root;
            if (root.GetCode("Name") is null)
            {
                root.SetCode("Name", ValueWriter.Literal(file.ClassName));
            }

            if (root.GetCode("AutoScaleMode")?.EndsWith("AutoScaleMode.Font", StringComparison.Ordinal) == true && root.GetCode("AutoScaleDimensions") is null)
            {
                root.SetCode("AutoScaleDimensions", ValueWriter.Write(new SizeF(7F, 15F), file.Document.Style));
            }

            after = DesignerWriter.Write(file, file.Document);
        }
        catch
        {
            // The document is half changed: it is read again from the file as it was.
            Load(before);
            throw;
        }

        if (string.Equals(after, before, StringComparison.Ordinal))
        {
            Load(before);
            return;
        }

        _undo.Push(before);
        _redo.Clear();
        Text = after;
        Load(after);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A component's name with the names of the controls and items inside it, innermost last.</summary>
    private static IEnumerable<string> WithDescendants(DesignDocument document, string name)
    {
        yield return name;
        if (document.Find(name) is not { } component)
        {
            yield break;
        }

        foreach (var add in component.Adds.Where(a => a.Collection.EndsWith("Controls", StringComparison.Ordinal) || a.Collection is "Items" or "DropDownItems" or "TabPages" or "Columns"))
        {
            foreach (var item in add.Items.Select(FirstName).Where(i => document.Find(i) is not null).ToList())
            {
                foreach (var descendant in WithDescendants(document, item))
                {
                    yield return descendant;
                }
            }
        }
    }

    /// <summary>
    /// Takes a component out of every collection; unless the component stays, also out of the properties and
    /// statements that name it, such as a form's AcceptButton.
    /// </summary>
    private static void RemoveReferences(DesignDocument document, string name, bool keepAssignments)
    {
        foreach (var component in document.All)
        {
            foreach (var add in component.Adds)
            {
                add.Items.RemoveAll(item => FirstName(item) == name);
            }

            component.Adds.RemoveAll(a => a.Items.Count == 0);
            if (keepAssignments)
            {
                continue;
            }

            foreach (var property in component.Properties.Where(p => CodeTokens.Mentions(p.Code, name)).ToList())
            {
                component.SetCode(property.Path, null);
            }

            component.OtherStatements.RemoveAll(statement => CodeTokens.Mentions(statement, name));
        }
    }
}
