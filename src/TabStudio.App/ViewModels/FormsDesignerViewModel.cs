using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Forms.Design.Catalog;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;
using TabStudio.Forms.Design.Surface;
using TabStudio.Forms.Design.View;
using ToolboxCatalog = TabStudio.Forms.Design.Catalog.Toolbox;

namespace TabStudio.App.ViewModels;

/// <summary>Whether a form's tab shows the designer or the form's code.</summary>
public enum FormsDesignerMode
{
    /// <summary>The designer: the form, the Toolbox and the Properties.</summary>
    Design,

    /// <summary>The form's code file, as F7 shows it in Visual Studio.</summary>
    Code,
}

/// <summary>What the Forms designer needs from the workspace.</summary>
internal interface IFormsDesignerHost
{
    /// <summary>The open document of a file, if it is open.</summary>
    /// <param name="path">The file.</param>
    DocumentViewModel? FindOpenDocument(string path);

    /// <summary>Tells the language service a file's new text, so IntelliSense sees new controls at once.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text.</param>
    void UpdateLanguageText(string path, string text);

    /// <summary>Shows a message in the status bar.</summary>
    /// <param name="message">The message.</param>
    void ShowStatus(string message);
}

/// <summary>
/// The Windows Forms designer of a form's code file, such as Form1.cs: the form built from Form1.Designer.cs on a
/// touch canvas, the Toolbox, and the Properties and events of the selected control. Every change is written to
/// Form1.Designer.cs as Visual Studio writes it, and can be undone; event handlers are added to Form1.cs. A Visual
/// Basic form's Form1.vb and Form1.Designer.vb are designed the same way, its handlers wired by Handles clauses.
/// </summary>
public sealed partial class FormsDesignerViewModel : ObservableObject, IDisposable
{
    private readonly DocumentViewModel _code;
    private readonly IFormsDesignerHost _host;
    private readonly string _designerPath;
    private string _written;
    private bool _writing;
    private bool _disposed;

    /// <summary>Opens the designer of a form.</summary>
    /// <param name="code">The form's code file's document.</param>
    /// <param name="designerPath">The form's designer file.</param>
    /// <param name="simplified">Whether the project has implicit usings, which the designer writes simplified code for.</param>
    /// <param name="host">The workspace.</param>
    internal FormsDesignerViewModel(DocumentViewModel code, string designerPath, bool simplified, IFormsDesignerHost host)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(designerPath);
        ArgumentNullException.ThrowIfNull(host);
        _code = code;
        _host = host;
        _designerPath = designerPath;
        var className = Path.GetFileNameWithoutExtension(code.FilePath);
        var language = DesignerFile.LanguageOf(designerPath);
        var rootType = CodeBehind.BaseTypeOf(code.Document.Text, className, language) ?? "Form";
        _written = ReadDesignerText();
        Surface = new DesignSurface(_written, rootType, simplified, language, code.Document.Text);
        Surface.Changed += OnSurfaceChanged;
        Surface.Deleted += OnComponentsDeleted;
        Canvas = new DesignCanvas { Surface = Surface };
        Canvas.SelectionChanged += (_, _) => OnSelectionChanged();
        Canvas.HandlerRequested += (_, component) => CreateHandler(component, null);
        Canvas.ToolPlaced += (_, _) => DisarmTools();
        Toolbox = [.. CreateToolbox()];
        OnSelectionChanged();

        // Going to a line of the code, from the Error List, a search or the debugger, shows the code.
        _code.NavigationRequested += OnCodeNavigationRequested;
        if (_code.PendingNavigation is not null)
        {
            Mode = FormsDesignerMode.Code;
        }
    }

    /// <summary>The design surface.</summary>
    internal DesignSurface Surface { get; }

    /// <summary>The touch canvas the form is designed on.</summary>
    internal DesignCanvas Canvas { get; }

    /// <summary>The Toolbox's tabs.</summary>
    public IReadOnlyList<FormsToolboxCategoryViewModel> Toolbox { get; }

    /// <summary>The selected component's properties.</summary>
    public ObservableCollection<FormsPropertyViewModel> Properties { get; } = [];

    /// <summary>The selected component's events.</summary>
    public ObservableCollection<FormsEventViewModel> Events { get; } = [];

    /// <summary>The items of the selected strip or menu item, such as a menu strip's menus, listed to select them.</summary>
    public ObservableCollection<StripItemViewModel> StripItems { get; } = [];

    /// <summary>Whether the selection is a strip or a menu item, which items are added to.</summary>
    [ObservableProperty]
    public partial bool HasStripItems { get; private set; }

    /// <summary>What adding an item to the selection adds, such as <c>Add menu item</c>.</summary>
    [ObservableProperty]
    public partial string AddItemLabel { get; private set; } = string.Empty;

    /// <summary>The text of the next item, typed where Visual Studio's strips say "Type Here"; <c>-</c> adds a separator.</summary>
    [ObservableProperty]
    public partial string NewItemText { get; set; } = string.Empty;

    /// <summary>Whether the tab shows the designer or the code.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDesign), nameof(IsCode), nameof(ShowsCode))]
    public partial FormsDesignerMode Mode { get; set; }

    /// <summary>Whether the Properties panel shows events rather than properties.</summary>
    [ObservableProperty]
    public partial bool ShowsEvents { get; set; }

    /// <summary>Whether the Toolbox is shown.</summary>
    [ObservableProperty]
    public partial bool IsToolboxOpen { get; set; } = true;

    /// <summary>Whether the Properties panel is shown.</summary>
    [ObservableProperty]
    public partial bool IsPropertiesOpen { get; set; } = true;

    /// <summary>The selected component's name and type, as the Properties panel's header shows them.</summary>
    [ObservableProperty]
    public partial string SelectionTitle { get; private set; } = string.Empty;

    /// <summary>The selected component's type.</summary>
    [ObservableProperty]
    public partial string SelectionType { get; private set; } = string.Empty;

    /// <summary>Whether a control other than the form is selected, which Delete, Copy and the order commands act on.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(CopyCommand), nameof(CutCommand), nameof(BringToFrontCommand), nameof(SendToBackCommand))]
    public partial bool HasSelection { get; private set; }

    /// <summary>Why the form cannot be designed, if it cannot.</summary>
    public string? LoadError => Surface.LoadError;

    /// <summary>Whether the form cannot be designed.</summary>
    public bool HasLoadError => Surface.LoadError is not null;

    /// <summary>What the designer cannot show of the file, in one line, if anything.</summary>
    public string? Problems => Surface.Problems.Count == 0 ? null
        : Surface.Problems.Count == 1 ? Surface.Problems[0]
        : $"{Surface.Problems[0]} (and {Surface.Problems.Count - 1} more)";

    /// <summary>Whether there are problems to show.</summary>
    public bool HasProblems => Surface.Problems.Count > 0;

    /// <summary>Whether the designer is shown.</summary>
    public bool IsDesign => Mode == FormsDesignerMode.Design;

    /// <summary>Whether the code is shown.</summary>
    public bool IsCode => Mode == FormsDesignerMode.Code;

    /// <summary>Whether the code editor is shown, which shows the editing actions.</summary>
    public bool ShowsCode => Mode == FormsDesignerMode.Code;

    /// <summary>The form's designer file.</summary>
    public string DesignerPath => _designerPath;

    /// <summary>Raised when the mode changes, for the document to show or hide the editing actions.</summary>
    internal event EventHandler? LayoutChanged;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _code.NavigationRequested -= OnCodeNavigationRequested;
        Surface.Changed -= OnSurfaceChanged;
        Surface.Deleted -= OnComponentsDeleted;
        Canvas.Surface = null;
    }

    /// <summary>Sets a property of the selected component, as its row in the Properties panel does.</summary>
    /// <param name="path">The property.</param>
    /// <param name="value">The value.</param>
    internal void SetProperty(string path, object? value)
    {
        if (Canvas.PrimarySelection is not { } component)
        {
            return;
        }

        try
        {
            if (path == "Name" && value is string name && !component.IsRoot)
            {
                var old = component.Name;
                Surface.Rename(component, name);
                RenameInCode(old, name);
                Canvas.Select([name]);
                return;
            }

            Surface.SetProperty(component, path, value);
        }
        catch (ArgumentException ex)
        {
            _host.ShowStatus(ex.Message);
            LoadProperties();
        }
    }

    /// <summary>
    /// Creates the handler of an event, or goes to the one there is: the default event of a component on a double-tap,
    /// or an event from the Properties panel's events. The code is shown with the caret in the handler.
    /// </summary>
    /// <param name="component">The component.</param>
    /// <param name="eventName">The event, or <see langword="null"/> for its default event.</param>
    /// <param name="handlerName">The handler's name, as typed in the events; by default, the one there is or Visual Studio's.</param>
    internal void CreateHandler(DesignComponent component, string? eventName, string? handlerName = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        var type = component.Type ?? component.Instance?.GetType() ?? typeof(System.Windows.Forms.Form);
        eventName ??= ComponentDefaults.DefaultEvent(type);
        if (CodeBehind.DelegateTypeOf(type, eventName) is not { } delegateType)
        {
            return;
        }

        var existing = component.GetHandler(eventName);
        var handler = handlerName ?? existing ?? CodeBehind.HandlerName(component.IsRoot ? Surface.ClassName : component.Name, eventName);
        try
        {
            // In C#, the designer file wires it; in Visual Basic, the handler's Handles clause.
            var (text, caret) = Surface.AttachHandler(_code.Document.Text, component, eventName, handler, delegateType);
            ApplyCode(text);
            Mode = FormsDesignerMode.Code;
            _code.NavigateToOffset(caret);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // A name that is not a method's, or a code file without the form's class.
            _host.ShowStatus(ex.Message);
            LoadProperties();
        }
    }

    /// <summary>Arms a tool: the next tap on the form adds it there.</summary>
    /// <param name="tool">The tool, or <see langword="null"/> to put it away.</param>
    internal void Arm(FormsToolboxItemViewModel? tool)
    {
        foreach (var item in Toolbox.SelectMany(c => c.Items))
        {
            item.IsArmed = item == tool;
        }

        Canvas.ArmedTool = tool?.Type;
    }

    /// <summary>Reads the designer file again if it changed in the code editor or on disk since the designer wrote it.</summary>
    internal void Refresh()
    {
        var text = ReadDesignerText();
        if (!string.Equals(text, Surface.Text, StringComparison.Ordinal))
        {
            _written = text;
            Surface.Reload(text);
        }

        // A Visual Basic form's handlers are wired by Handles clauses in its code, which may have changed there.
        Surface.CodeText = _code.Document.Text;
    }

    partial void OnModeChanged(FormsDesignerMode value)
    {
        if (value == FormsDesignerMode.Design)
        {
            Refresh();
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ShowDesign() => Mode = FormsDesignerMode.Design;

    [RelayCommand]
    private void ShowCode() => Mode = FormsDesignerMode.Code;

    [RelayCommand]
    private void ToggleToolbox() => IsToolboxOpen = !IsToolboxOpen;

    [RelayCommand]
    private void ToggleProperties() => IsPropertiesOpen = !IsPropertiesOpen;

    [RelayCommand]
    private void ShowProperties() => ShowsEvents = false;

    [RelayCommand]
    private void ShowEvents() => ShowsEvents = true;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Surface.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Surface.Redo();

    private bool CanUndo() => Surface.CanUndo;

    private bool CanRedo() => Surface.CanRedo;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete() => Surface.Delete(Canvas.Selection);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Copy() => DesignCanvas.Clipboard = Surface.Copy(Canvas.Selection);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut()
    {
        DesignCanvas.Clipboard = Surface.Copy(Canvas.Selection);
        Surface.Delete(Canvas.Selection);
    }

    [RelayCommand]
    private void Paste()
    {
        if (DesignCanvas.Clipboard is { } clip)
        {
            Canvas.Select(Surface.Paste(clip, Canvas.PasteTarget()));
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringToFront() => Surface.ChangeZOrder(Canvas.Selection, front: true);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendToBack() => Surface.ChangeZOrder(Canvas.Selection, front: false);

    [RelayCommand]
    private void SelectForm() => Canvas.Select([Surface.ClassName]);

    /// <summary>
    /// Adds an item to the selected strip or menu item: a menu item to a menu, a label to a status strip, a button to a
    /// tool strip, or a separator for <c>-</c>. The strip stays selected, for the next item.
    /// </summary>
    [RelayCommand]
    private void AddStripItem()
    {
        if (Canvas.PrimarySelection is not { Instance: System.Windows.Forms.ToolStrip or System.Windows.Forms.ToolStripDropDownItem } owner)
        {
            return;
        }

        var text = NewItemText.Trim();
        var type = text == "-" ? typeof(System.Windows.Forms.ToolStripSeparator) : ItemTypeFor(owner.Instance);
        if (text.Length == 0 && type == typeof(System.Windows.Forms.ToolStripMenuItem))
        {
            _host.ShowStatus("Type the menu item's text, then add it.");
            return;
        }

        Surface.AddItem(owner, type, type == typeof(System.Windows.Forms.ToolStripSeparator) ? string.Empty : text);
        NewItemText = string.Empty;
    }

    /// <summary>The item a strip gets from "Type Here": a status label, a tool strip button, or a menu item.</summary>
    private static Type ItemTypeFor(object? owner) => owner switch
    {
        System.Windows.Forms.StatusStrip => typeof(System.Windows.Forms.ToolStripStatusLabel),
        System.Windows.Forms.MenuStrip or System.Windows.Forms.ToolStripDropDown or System.Windows.Forms.ToolStripDropDownItem => typeof(System.Windows.Forms.ToolStripMenuItem),
        _ => typeof(System.Windows.Forms.ToolStripButton),
    };

    private void DisarmTools() => Arm(null);

    private IEnumerable<FormsToolboxCategoryViewModel> CreateToolbox() =>
        ToolboxCatalog.Categories.Select(category => new FormsToolboxCategoryViewModel(
            category,
            [.. ToolboxCatalog.Items.Where(i => i.Category == category)
                .Select(i => new FormsToolboxItemViewModel(i.Type, i.Name, i.Icon, tool => Arm(tool.IsArmed ? null : tool)))]));

    private void OnCodeNavigationRequested(object? sender, EventArgs e) => Mode = FormsDesignerMode.Code;

    /// <summary>Deleted controls leave a Visual Basic form's Handles clauses, as in Visual Studio; their handlers stay.</summary>
    private void OnComponentsDeleted(object? sender, IReadOnlyList<string> names) => ApplyCode(Surface.ForgetHandlers(_code.Document.Text, names));

    private void OnSurfaceChanged(object? sender, EventArgs e)
    {
        WriteDesignerText(Surface.Text);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(LoadError));
        OnPropertyChanged(nameof(HasLoadError));
        OnPropertyChanged(nameof(Problems));
        OnPropertyChanged(nameof(HasProblems));
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        var primary = Canvas.PrimarySelection;
        HasSelection = Canvas.Selection.Any(c => !c.IsRoot);
        SelectionTitle = primary?.Name ?? string.Empty;
        SelectionType = primary is null ? string.Empty : (primary.Type ?? primary.Instance?.GetType())?.FullName ?? primary.TypeName;
        LoadProperties();
        LoadStripItems(primary);
    }

    /// <summary>Lists the items of a strip or a menu item, the ones its drop-down shows when it opens.</summary>
    private void LoadStripItems(DesignComponent? component)
    {
        StripItems.Clear();
        IEnumerable<System.Windows.Forms.ToolStripItem>? items = component?.Instance switch
        {
            System.Windows.Forms.ToolStrip strip => strip.Items.Cast<System.Windows.Forms.ToolStripItem>(),
            System.Windows.Forms.ToolStripDropDownItem menu => menu.DropDownItems.Cast<System.Windows.Forms.ToolStripItem>(),
            _ => null,
        };
        HasStripItems = items is not null;
        if (items is null || Surface.Document is not { } document)
        {
            return;
        }

        AddItemLabel = ItemTypeFor(component!.Instance) == typeof(System.Windows.Forms.ToolStripMenuItem) ? "Add menu item"
            : component.Instance is System.Windows.Forms.StatusStrip ? "Add label"
            : "Add button";
        foreach (var item in items)
        {
            if (document.Of(item) is { } itemComponent)
            {
                StripItems.Add(new StripItemViewModel(Canvas, itemComponent.Name, item is System.Windows.Forms.ToolStripSeparator ? "—" : item.Text ?? string.Empty));
            }
        }
    }

    /// <summary>
    /// Shows the selected component's properties and events: the same rows keep their places and take the new values,
    /// so editing one after another is not interrupted; another component gets its own rows.
    /// </summary>
    private void LoadProperties()
    {
        if (Canvas.PrimarySelection is not { } component || (component.Type ?? component.Instance?.GetType()) is not { } type)
        {
            Properties.Clear();
            Events.Clear();
            return;
        }

        var descriptors = PropertyCatalog.PropertiesOf(type, component.IsRoot);
        var sameRows = Properties.Count == descriptors.Count && Properties.Select(p => p.Path).SequenceEqual(descriptors.Select(d => d.Path))
            && Properties.FirstOrDefault()?.ComponentName == component.Name;
        if (sameRows)
        {
            foreach (var row in Properties)
            {
                row.Load(ValueOf(component, row.Path));
            }
        }
        else
        {
            Properties.Clear();
            string? category = null;
            foreach (var descriptor in descriptors)
            {
                var row = FormsPropertyViewModel.Create(this, component.Name, descriptor, ValueOf(component, descriptor.Path));
                row.StartsCategory = descriptor.Category != category;
                category = descriptor.Category;
                Properties.Add(row);
            }
        }

        Events.Clear();
        foreach (var eventName in PropertyCatalog.EventsOf(type))
        {
            Events.Add(new FormsEventViewModel(this, component.Name, eventName, component.GetHandler(eventName)));
        }
    }

    /// <summary>A property's value: the live control's, or the file's for a property the surface does not show, such as Visible.</summary>
    private static object? ValueOf(DesignComponent component, string path)
    {
        if (path == "Name")
        {
            return component.Name;
        }

        if (path == "Visible")
        {
            return !string.Equals(component.GetCode("Visible"), "false", StringComparison.OrdinalIgnoreCase);
        }

        if (path == "Font.Size")
        {
            return (SurfaceBuilder.GetValue(component.Instance, "Font") as System.Drawing.Font)?.SizeInPoints;
        }

        return SurfaceBuilder.GetValue(component.Instance, path);
    }

    /// <summary>The component a row belongs to, by name, as the document is rebuilt after each change.</summary>
    internal DesignComponent? Find(string name) => Surface.Document?.Find(name);

    /// <summary>Sets a property of a component by name.</summary>
    internal void SetProperty(string componentName, string path, object? value)
    {
        if (Find(componentName) is not { } component)
        {
            return;
        }

        if (Canvas.PrimarySelection?.Name != componentName)
        {
            Canvas.Select([componentName]);
        }

        SetProperty(path, value);
    }

    /// <summary>
    /// Sets an event's handler from the Properties panel's events: a name typed there gets its method in the code, which
    /// is shown, as in Visual Studio; an empty name takes the handler off the event and leaves its method.
    /// </summary>
    internal void SetHandler(string componentName, string eventName, string? handler)
    {
        if (Find(componentName) is not { } component)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(handler))
        {
            ApplyCode(Surface.DetachHandler(_code.Document.Text, component, eventName));
            return;
        }

        CreateHandler(component, eventName, handler.Trim());
    }

    private string ReadDesignerText()
    {
        if (_host.FindOpenDocument(_designerPath) is { } open)
        {
            return open.Document.Text;
        }

        return File.Exists(_designerPath) ? File.ReadAllText(_designerPath) : string.Empty;
    }

    /// <summary>
    /// Writes the designer file: into its tab when it is open, where saving writes it, or straight to the file, as it
    /// is generated code nobody edits there.
    /// </summary>
    private void WriteDesignerText(string text)
    {
        // A reload reads what the file says already, which is not written again.
        if (_writing || string.Equals(text, _written, StringComparison.Ordinal))
        {
            return;
        }

        _writing = true;
        try
        {
            if (_host.FindOpenDocument(_designerPath) is { } open)
            {
                if (!string.Equals(open.Document.Text, text, StringComparison.Ordinal))
                {
                    open.Document.Replace(0, open.Document.TextLength, text);
                }
            }
            else
            {
                File.WriteAllText(_designerPath, text);
                _host.UpdateLanguageText(_designerPath, text);
            }

            _written = text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _host.ShowStatus($"Could not write {Path.GetFileName(_designerPath)}: {ex.Message}");
        }
        finally
        {
            _writing = false;
        }
    }

    /// <summary>Puts new text in the code file's editor as one edit, which its Undo takes back.</summary>
    private void ApplyCode(string text)
    {
        var document = _code.Document;
        var old = document.Text;
        if (string.Equals(old, text, StringComparison.Ordinal))
        {
            return;
        }

        var start = 0;
        while (start < old.Length && start < text.Length && old[start] == text[start])
        {
            start++;
        }

        var end = 0;
        while (end < old.Length - start && end < text.Length - start && old[^(end + 1)] == text[^(end + 1)])
        {
            end++;
        }

        document.Replace(start, old.Length - start - end, text.Substring(start, text.Length - start - end));
    }

    private void RenameInCode(string oldName, string newName) => ApplyCode(Surface.RenameInCode(_code.Document.Text, oldName, newName));
}
