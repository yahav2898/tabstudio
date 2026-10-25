using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TabStudio.App.Controls;

/// <summary>
/// Shows the view of a page, such as the start screen or the IDE, built from the host's data templates, and keeps
/// that view when the host moves to another window or display.
/// </summary>
/// <remarks>
/// A <see cref="ContentControl"/> builds templated content again whenever it is attached to a new tree, which would
/// give the IDE new editors, losing their caret, scroll position, selection and undo history, each time desktop mode
/// moves it between the tablet and an external display. This host builds a page's view only when the page changes,
/// and, like a content control, reuses the view for a new page of the same template.
/// </remarks>
public sealed class PageHost : Decorator
{
    /// <summary>The page, a view model the host's data templates know.</summary>
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageHost, object?>(nameof(Page));

    private IDataTemplate? _template;

    static PageHost()
    {
        PageProperty.Changed.AddClassHandler<PageHost>((host, _) => host.ShowPage());
    }

    /// <inheritdoc cref="PageProperty"/>
    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // A page set before the data templates that show it were found; a page already shown stays as it is.
        if (Child is null && Page is not null)
        {
            ShowPage();
        }
    }

    private void ShowPage()
    {
        var page = Page;
        var template = page is null ? null : this.FindDataTemplate(page);
        if (page is null || template is null)
        {
            Release();
            return;
        }

        Control? view;
        if (ReferenceEquals(template, _template) && template is IRecyclingDataTemplate recycling && Child is { } existing)
        {
            view = recycling.Build(page, existing);
        }
        else
        {
            Release();
            view = template.Build(page);
        }

        _template = template;
        if (view is not null)
        {
            view.DataContext = page;
        }

        if (!ReferenceEquals(Child, view))
        {
            Child = view;
        }
    }

    /// <summary>Lets go of the current view, which lets go of its page, as a content control does with old content.</summary>
    private void Release()
    {
        if (Child is { } old)
        {
            Child = null;
            old.DataContext = null;
        }

        _template = null;
    }
}
