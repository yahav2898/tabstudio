namespace System.Windows.Forms;

/// <summary>Data binding: the bindings of the control's properties, and the binding context they find their data sources' managers in.</summary>
public partial class Control : IBindableComponent
{
    private static readonly object EventBindingContextChanged = new();
    private ControlBindingsCollection? _dataBindings;
    private BindingContext? _bindingContext;

    /// <summary>Occurs when the binding context changes, as when the control is put on a form.</summary>
    public event EventHandler? BindingContextChanged
    {
        add => Events.AddHandler(EventBindingContextChanged, value);
        remove => Events.RemoveHandler(EventBindingContextChanged, value);
    }

    /// <summary>The bindings of the control's properties to data sources.</summary>
    public ControlBindingsCollection DataBindings => _dataBindings ??= new ControlBindingsCollection(this);

    /// <summary>The managers of the data sources: the parent's unless set; a form and a user control have their own.</summary>
    public virtual BindingContext? BindingContext
    {
        get
        {
            if (_bindingContext is not null)
            {
                return _bindingContext;
            }

            if (_parent?.BindingContext is { } inherited)
            {
                return inherited;
            }

            if (this is ContainerControl)
            {
                // A form or user control gets a context of its own when it has none, as in Windows Forms.
                _bindingContext = new BindingContext();
            }

            return _bindingContext;
        }

        set
        {
            if (ReferenceEquals(_bindingContext, value))
            {
                return;
            }

            _bindingContext = value;
            OnBindingContextChanged(EventArgs.Empty);
        }
    }

    /// <summary>Raises BindingContextChanged, and connects the bindings of the control and its children again.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnBindingContextChanged(EventArgs e)
    {
        _dataBindings?.Reattach();
        ((EventHandler?)Events[EventBindingContextChanged])?.Invoke(this, e);
        foreach (var child in _controls.ToArray())
        {
            child.OnParentBindingContextChanged(e);
        }
    }

    /// <summary>Called when the parent's binding context changes.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnParentBindingContextChanged(EventArgs e)
    {
        if (_bindingContext is null || this is ContainerControl)
        {
            OnBindingContextChanged(e);
        }
    }

    /// <summary>Connects the bindings again when the control moves to another parent.</summary>
    private void OnParentChangedForBinding()
    {
        if (_bindingContext is null || this is not ContainerControl)
        {
            OnBindingContextChanged(EventArgs.Empty);
        }
    }
}
