namespace TabStudio.App.ViewModels;

/// <summary>The Windows Forms designer of a form's code file.</summary>
public sealed partial class DocumentViewModel
{
    private FormsDesignerViewModel? _formsDesigner;

    /// <summary>
    /// The designer of this file's form, when it is the code file of a Windows Forms form, such as Form1.cs; its tab
    /// then opens on the designer, as in Visual Studio, and switches to the code.
    /// </summary>
    public FormsDesignerViewModel? FormsDesigner
    {
        get => _formsDesigner;
        internal set
        {
            if (_formsDesigner is not null)
            {
                _formsDesigner.LayoutChanged -= OnFormsDesignerLayoutChanged;
            }

            _formsDesigner = value;
            if (_formsDesigner is not null)
            {
                _formsDesigner.LayoutChanged += OnFormsDesignerLayoutChanged;
            }

            OnPropertyChanged();
            OnDesignerLayoutChanged();
        }
    }

    private void OnFormsDesignerLayoutChanged(object? sender, EventArgs e) => OnDesignerLayoutChanged();
}
