using Avalonia;
using Avalonia.Controls;

namespace TabStudio.App.Controls;

/// <summary>
/// A tool window's view, created the first time it is shown rather than with the workspace: the view of a panel that is
/// hidden, such as Locals, the Terminal or Swarm, is not measured, so it is not created until the panel is chosen, and it
/// is kept from then on, with its scroll position and state. Opening a solution then builds only the views a person
/// sees, instead of every tool window the IDE has. The view inherits this control's data context.
/// </summary>
/// <remarks>
/// Only for views that show their view model's state when they are created; a view that must hear its view model's
/// events while hidden, such as the web preview's navigation requests, is declared directly.
/// </remarks>
public sealed class DeferredView : Decorator
{
    /// <summary>The type of the view to create; it needs a public constructor without parameters.</summary>
    public static readonly StyledProperty<Type?> ViewTypeProperty =
        AvaloniaProperty.Register<DeferredView, Type?>(nameof(ViewType));

    /// <inheritdoc cref="ViewTypeProperty"/>
    public Type? ViewType
    {
        get => GetValue(ViewTypeProperty);
        set => SetValue(ViewTypeProperty, value);
    }

    /// <summary>Whether the view has been created, because it was shown or <see cref="Realize"/> was called.</summary>
    public bool IsRealized => Child is not null;

    /// <summary>Creates the view now, if it is not created yet, and returns it.</summary>
    public Control? Realize()
    {
        if (Child is null && ViewType is { } type)
        {
            Services.UiStallMonitor.Note("creating the " + type.Name);
            var created = System.Diagnostics.Stopwatch.StartNew();
            Child = (Control)Activator.CreateInstance(type)!;
            Services.UiStallMonitor.Time(type.Name, created.Elapsed);
        }

        return Child;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // Measured only while it is shown: the panel it is in is visible.
        Realize();
        return base.MeasureOverride(availableSize);
    }
}
