using System.Drawing;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Surface;

/// <summary>Copied components: their types, properties and the controls and items inside them; not their event handlers.</summary>
/// <param name="Components">The components, the copied ones and those inside them.</param>
/// <param name="TopLevel">The names of the copied components, which a paste puts in its container.</param>
/// <param name="Container">The name of the container they were copied from.</param>
internal sealed record DesignClip(IReadOnlyList<DesignClip.Item> Components, IReadOnlyList<string> TopLevel, string Container)
{
    /// <summary>One copied component.</summary>
    /// <param name="Name">Its name.</param>
    /// <param name="TypeName">Its type as the code names it.</param>
    /// <param name="Type">Its type, if known.</param>
    /// <param name="ConstructorArguments">Its creation's arguments.</param>
    /// <param name="Properties">Its properties.</param>
    /// <param name="Adds">What joins its collections: the collection, whether in one AddRange call, the array's type, and the items.</param>
    internal sealed record Item(
        string Name,
        string TypeName,
        Type? Type,
        string ConstructorArguments,
        IReadOnlyList<DesignAssignment> Properties,
        IReadOnlyList<(string Collection, bool IsRange, string? RangeType, IReadOnlyList<string> Items)> Adds);
}

/// <summary>Copy and paste on the design surface.</summary>
internal sealed partial class DesignSurface
{
    /// <summary>How far a pasted copy is moved from the original when it goes in the same container.</summary>
    private const int PasteOffset = 16;

    /// <summary>Copies components, with the controls and items inside them.</summary>
    /// <param name="components">The components; the root is left out.</param>
    /// <returns>The copy, or <see langword="null"/> when nothing can be copied.</returns>
    public DesignClip? Copy(IReadOnlyCollection<DesignComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        if (Document is not { } document)
        {
            return null;
        }

        var selected = components.Where(c => !c.IsRoot).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var all = selected.SelectMany(name => WithDescendants(document, name)).Distinct().ToList();

        // A copied control inside another copied one goes with its container, not on its own.
        var topLevel = selected.Where(name => !selected.Any(other => other != name && WithDescendants(document, other).Skip(1).Contains(name))).ToList();
        if (topLevel.Count == 0)
        {
            return null;
        }

        var items = all.Select(document.Find).OfType<DesignComponent>().Select(c => new DesignClip.Item(
            c.Name,
            c.TypeName,
            c.Type,
            c.ConstructorArguments,
            [.. c.Properties],
            [.. c.Adds.Select(a => (a.Collection, a.IsRange, a.RangeType, (IReadOnlyList<string>)[.. a.Items]))])).ToList();
        var container = ParentOf(document.Find(topLevel[0])!)?.Owner.Name ?? document.Root.Name;
        return new DesignClip(items, topLevel, container);
    }

    /// <summary>Pastes copied components into a container, with new names, in front of its other controls.</summary>
    /// <param name="clip">The copy.</param>
    /// <param name="point">A point in the container, in the root's client coordinates, or <see langword="null"/> for the form.</param>
    /// <returns>The pasted components' new names.</returns>
    public IReadOnlyList<string> Paste(DesignClip clip, Point? point = null)
    {
        ArgumentNullException.ThrowIfNull(clip);
        var document = Document ?? throw new InvalidOperationException(LoadError);
        var (owner, collection, _) = point is { } at ? ContainerAt(at) : (document.Root, "Controls", Point.Empty);
        var pasted = new List<string>();
        Edit(doc =>
        {
            // New names first, so copies that name each other, as a group box names its controls, are renamed together.
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in clip.Components)
            {
                var name = item.Type is { } type ? doc.NewName(type) : doc.NewName(typeof(System.Windows.Forms.Control));
                names[item.Name] = name;
                doc.Components.Add(new DesignComponent(name, item.TypeName)
                {
                    Type = item.Type,
                    ConstructorArguments = item.ConstructorArguments,
                    FieldModifiers = DesignCode.FieldModifiers(doc.Style),
                });
            }

            var offset = owner.Name == clip.Container ? PasteOffset : 0;
            foreach (var item in clip.Components)
            {
                var copy = doc.Find(names[item.Name])!;
                foreach (var property in item.Properties)
                {
                    var code = property.Path == "Name" ? DesignCode.Literal(doc.Style, copy.Name) : DesignCode.Rename(doc.Style, property.Code, names);
                    copy.SetCode(property.Path, code);
                }

                if (clip.TopLevel.Contains(item.Name) && offset > 0 && item.Properties.FirstOrDefault(p => p.Path == "Location") is { } location
                    && TryReadPoint(doc.Style, location.Code, out var original))
                {
                    copy.SetCode("Location", DesignCode.Write(doc.Style, new Point(original.X + offset, original.Y + offset)));
                }

                foreach (var (name, isRange, rangeType, adds) in item.Adds)
                {
                    var target = copy.Collection(name);
                    target.IsRange = isRange;
                    target.RangeType = rangeType;
                    target.Items.AddRange(adds.Select(a => DesignCode.Rename(doc.Style, a, names)));
                }
            }

            var container = doc.Find(owner.Name)!.Collection(collection);
            foreach (var top in clip.TopLevel)
            {
                container.Items.Insert(0, names[top]);
                pasted.Add(names[top]);
            }
        });
        return pasted;
    }

    private static bool TryReadPoint(CodeStyle style, string code, out Point point)
    {
        if (DesignCode.Evaluator(style, _ => null).TryEvaluate(code, out var value) && value is Point read)
        {
            point = read;
            return true;
        }

        point = Point.Empty;
        return false;
    }
}
