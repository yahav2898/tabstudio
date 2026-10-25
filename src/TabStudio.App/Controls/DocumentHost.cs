using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;

namespace TabStudio.App.Controls;

/// <summary>
/// Shows one view per open document and keeps them all alive, so switching tabs preserves each editor's
/// scroll position, selection and undo history, just like Visual Studio. Only the active document's view is
/// visible.
/// </summary>
public sealed class DocumentHost : Panel
{
    /// <summary>The open documents.</summary>
    public static readonly StyledProperty<IEnumerable?> DocumentsProperty =
        AvaloniaProperty.Register<DocumentHost, IEnumerable?>(nameof(Documents));

    /// <summary>The document whose view is visible.</summary>
    public static readonly StyledProperty<object?> ActiveDocumentProperty =
        AvaloniaProperty.Register<DocumentHost, object?>(nameof(ActiveDocument));

    /// <summary>Creates the view for a document.</summary>
    public static readonly StyledProperty<Func<object, Control>?> ViewFactoryProperty =
        AvaloniaProperty.Register<DocumentHost, Func<object, Control>?>(nameof(ViewFactory));

    private readonly Dictionary<object, Control> _views = [];
    private INotifyCollectionChanged? _subscribed;

    static DocumentHost()
    {
        DocumentsProperty.Changed.AddClassHandler<DocumentHost>((host, _) => host.OnDocumentsChanged());
        ActiveDocumentProperty.Changed.AddClassHandler<DocumentHost>((host, _) => host.UpdateVisibility());
        ViewFactoryProperty.Changed.AddClassHandler<DocumentHost>((host, _) => host.Synchronize());
    }

    /// <inheritdoc cref="DocumentsProperty"/>
    public IEnumerable? Documents
    {
        get => GetValue(DocumentsProperty);
        set => SetValue(DocumentsProperty, value);
    }

    /// <inheritdoc cref="ActiveDocumentProperty"/>
    public object? ActiveDocument
    {
        get => GetValue(ActiveDocumentProperty);
        set => SetValue(ActiveDocumentProperty, value);
    }

    /// <inheritdoc cref="ViewFactoryProperty"/>
    public Func<object, Control>? ViewFactory
    {
        get => GetValue(ViewFactoryProperty);
        set => SetValue(ViewFactoryProperty, value);
    }

    /// <summary>The view of a document, if it has been created.</summary>
    /// <param name="document">The document.</param>
    public Control? ViewFor(object document) => _views.GetValueOrDefault(document);

    /// <summary>
    /// Builds a document's view again, in the same place, such as when what its editor is built with changed. The
    /// document keeps its text, undo history and caret, which belong to it rather than to its view.
    /// </summary>
    /// <param name="document">The document.</param>
    public void Rebuild(object document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (ViewFactory is not { } factory || !_views.Remove(document, out var old))
        {
            return;
        }

        var index = Children.IndexOf(old);
        Children.Remove(old);
        (old as IDisposable)?.Dispose();
        var view = factory(document);
        view.IsVisible = false;
        _views[document] = view;
        Children.Insert(Math.Max(0, index), view);
        UpdateVisibility();
    }

    private void OnDocumentsChanged()
    {
        if (_subscribed is not null)
        {
            _subscribed.CollectionChanged -= OnCollectionChanged;
        }

        _subscribed = Documents as INotifyCollectionChanged;
        if (_subscribed is not null)
        {
            _subscribed.CollectionChanged += OnCollectionChanged;
        }

        Synchronize();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Synchronize();

    private void Synchronize()
    {
        var documents = Documents?.Cast<object>().ToList() ?? [];
        foreach (var removed in _views.Keys.Except(documents).ToList())
        {
            Children.Remove(_views[removed]);
            if (_views[removed] is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _views.Remove(removed);
        }

        if (ViewFactory is { } factory)
        {
            foreach (var document in documents.Where(d => !_views.ContainsKey(d)))
            {
                var view = factory(document);
                view.IsVisible = false;
                _views[document] = view;
                Children.Add(view);
            }
        }

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        foreach (var (document, view) in _views)
        {
            view.IsVisible = Equals(document, ActiveDocument);
        }
    }
}
