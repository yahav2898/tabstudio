using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using TabStudio.Forms.Design.Catalog;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Surface;

/// <summary>
/// Builds the live form of a design document: the Forms layer's own controls, created and set up as
/// InitializeComponent would, but from the document's values, so none of the app's code runs. A component of a type
/// the designer does not know, such as the app's own user control, is shown as a placeholder of its size.
/// </summary>
internal static class SurfaceBuilder
{
    /// <summary>The properties that do not change the design surface, as in Visual Studio, where a hidden control stays visible.</summary>
    private static readonly HashSet<string> Shadowed = new(StringComparer.Ordinal)
    {
        "Visible", "Location", "StartPosition", "WindowState", "TopMost", "Opacity", "ShowInTaskbar", "Modal", "AllowDrop",
    };

    /// <summary>Creates the live objects of a document's root and components, and sets them up.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The problems met, such as a value the designer could not work out, for the designer to show.</returns>
    public static IReadOnlyList<string> Build(DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var problems = new List<string>();
        var rootType = document.Root.Type ?? typeof(Form);
        document.Root.Instance = typeof(UserControl).IsAssignableFrom(rootType) && !typeof(Form).IsAssignableFrom(rootType) ? new UserControl() : new Form();
        foreach (var component in document.Components)
        {
            component.Instance = Create(component, problems);
        }

        var evaluator = DesignCode.Evaluator(document.Style, name => name == "this" ? document.Root.Instance : document.Find(name)?.Instance);
        var root = (Control)document.Root.Instance;
        root.SuspendLayout();
        foreach (var component in document.Components.Append(document.Root))
        {
            Apply(component, evaluator, problems);
        }

        root.ResumeLayout(false);
        root.PerformLayout();
        root.CreateControl();
        return problems;
    }

    /// <summary>Sets a property of a live object from a value, as the designer's property grid does.</summary>
    /// <param name="instance">The object.</param>
    /// <param name="path">The property's path.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the property was set; a shadowed one, such as Visible, is not.</returns>
    public static bool SetValue(object instance, string path, object? value)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(path);
        if (Shadowed.Contains(path) && (path != "Location" || instance is Form))
        {
            return false;
        }

        var target = instance;
        var parts = path.Split('.');
        for (var i = 0; i < parts.Length - 1; i++)
        {
            target = target.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
            if (target is null)
            {
                return false;
            }
        }

        if (target.GetType().GetProperty(parts[^1], BindingFlags.Public | BindingFlags.Instance) is not { CanWrite: true } property)
        {
            return false;
        }

        property.SetValue(target, ExpressionEvaluator.Convert(value, property.PropertyType));
        return true;
    }

    /// <summary>The value of a property of a live object, or <see langword="null"/>.</summary>
    /// <param name="instance">The object.</param>
    /// <param name="path">The property's path.</param>
    public static object? GetValue(object? instance, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var part in path.Split('.'))
        {
            instance = instance?.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        }

        return instance;
    }

    private static object? Create(DesignComponent component, List<string> problems)
    {
        if (component.Type is not { } type)
        {
            return new PlaceholderControl(component.TypeName);
        }

        try
        {
            return Activator.CreateInstance(type);
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or MemberAccessException or NotSupportedException or ArgumentException)
        {
            problems.Add($"{component.Name}: {type.Name} cannot be created on the design surface.");
            return ComponentDefaults.IsVisual(type) ? new PlaceholderControl(component.TypeName) : null;
        }
    }

    /// <summary>Sets up a component as its section in InitializeComponent does: its properties, then what joins its collections.</summary>
    private static void Apply(DesignComponent component, IValueEvaluator evaluator, List<string> problems)
    {
        if (component.Instance is not { } instance)
        {
            return;
        }

        // A timer or a binding source shows only in the component tray: its settings do nothing at design time.
        var visual = component.IsRoot || ComponentDefaults.IsVisual(instance.GetType()) || instance is PlaceholderControl;
        foreach (var property in component.Properties)
        {
            if (!visual && property.Path != "Name")
            {
                continue;
            }

            if (!evaluator.TryEvaluate(property.Code, out var value))
            {
                problems.Add($"{component.Name}.{property.Path} = {property.Code} is not shown on the design surface.");
                continue;
            }

            try
            {
                SetValue(instance, property.Path, value);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidCastException or TargetInvocationException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
            {
                problems.Add($"{component.Name}.{property.Path} could not be set: {(ex.InnerException ?? ex).Message}");
            }
        }

        foreach (var add in component.Adds)
        {
            AddItems(component, instance, add, evaluator, problems);
        }
    }

    private static void AddItems(DesignComponent component, object instance, DesignCollectionAdd add, IValueEvaluator evaluator, List<string> problems)
    {
        var collection = GetValue(instance, add.Collection);
        if (collection is null)
        {
            problems.Add($"{component.Name}.{add.Collection} is not a collection the designer knows.");
            return;
        }

        foreach (var item in add.Items)
        {
            if (!evaluator.TryEvaluateArguments(item, out var arguments) || arguments.Length == 0 || arguments[0] is null)
            {
                problems.Add($"{component.Name}.{add.Collection}: {item} is not shown on the design surface.");
                continue;
            }

            var method = collection.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Add" && m.GetParameters().Length == arguments.Length)
                .FirstOrDefault(m => m.GetParameters().Zip(arguments).All(p => p.Second is null || p.First.ParameterType.IsInstanceOfType(p.Second) || p.Second is IConvertible));
            try
            {
                method?.Invoke(collection, [.. method.GetParameters().Zip(arguments).Select(p => ExpressionEvaluator.Convert(p.Second, p.First.ParameterType))]);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidCastException or TargetInvocationException or InvalidOperationException)
            {
                problems.Add($"{component.Name}.{add.Collection}: {item} could not be added: {(ex.InnerException ?? ex).Message}");
            }
        }
    }
}

/// <summary>
/// A component of a type the designer does not know, such as the app's own user control, shown as a box of its size
/// with its type's name; it can be moved, resized and deleted like any control.
/// </summary>
/// <param name="typeName">The type's name, as the code writes it.</param>
internal sealed class PlaceholderControl(string typeName) : Control
{
    /// <summary>The type's name.</summary>
    public string TypeName { get; } = typeName;

    /// <inheritdoc />
    internal override void PaintContent(System.Drawing.Graphics g)
    {
        using (var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(246, 246, 246)))
        {
            g.FillRectangle(fill, ClientRectangle);
        }

        using (var border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
        {
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        var name = TypeName.Split('.')[^1];
        TextRenderer.DrawText(g, name, Font, ClientRectangle, System.Drawing.Color.FromArgb(96, 96, 96), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
