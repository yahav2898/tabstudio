using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Toolbox;
using TabStudio.App.Xaml;

namespace TabStudio.App.ViewModels.DesktopMode;

/// <summary>
/// The Properties panel of the XAML element at the caret, as Visual Studio's Properties window shows it for the XAML
/// editor: the attributes the element sets, then the other properties its type has, by category, each editable.
/// Edits are written to the XAML as one undoable change each, and the panel follows the caret and the text.
/// </summary>
public sealed partial class XamlPropertiesViewModel : ObservableObject
{
    /// <summary>The group of attributes the element sets.</summary>
    public const string SetGroupName = "Set in XAML";

    /// <summary>The group of properties in none of the framework's categories.</summary>
    public const string OtherGroupName = "Other";

    private DocumentViewModel? _document;
    private ToolboxFramework? _framework;
    private string? _elementKey;
    private List<string> _setNames = [];
    private Dictionary<string, XamlPropertyRowViewModel> _rows = new(StringComparer.Ordinal);

    /// <summary>Raised when the user finished an edit, written to the XAML or not, so the IDE gets the keyboard back.</summary>
    public event EventHandler? EditFinished;

    /// <summary>Whether there is an element at the caret.</summary>
    [ObservableProperty]
    public partial bool HasElement { get; private set; }

    /// <summary>The element's type as written, such as <c>Button</c>.</summary>
    [ObservableProperty]
    public partial string ElementName { get; private set; } = string.Empty;

    /// <summary>The element's <c>x:Name</c> or <c>Name</c>, if it has one.</summary>
    [ObservableProperty]
    public partial string? ElementId { get; private set; }

    /// <summary>"MainView.axaml · line 17".</summary>
    [ObservableProperty]
    public partial string Location { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the element's type is known, so all its properties are listed. Otherwise, such as for the app's own
    /// controls, only the attributes it sets are, and others are added by name.
    /// </summary>
    [ObservableProperty]
    public partial bool IsKnownType { get; private set; }

    /// <summary>Shows only the properties whose name contains this.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>The groups of rows: the attributes set, then the categories of the type's other properties.</summary>
    public ObservableCollection<XamlPropertyGroupViewModel> Groups { get; } = [];

    /// <summary>The name of a property to add, for types whose properties are not known.</summary>
    [ObservableProperty]
    public partial string NewName { get; set; } = string.Empty;

    /// <summary>The value of the property to add.</summary>
    [ObservableProperty]
    public partial string NewValue { get; set; } = string.Empty;

    /// <summary>Why the last edit could not be made, if it could not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Whether there is an error to show.</summary>
    public bool HasError => Error is not null;

    /// <summary>Follows a document, showing the element at its caret when the framework's Properties apply to it.</summary>
    /// <param name="document">The active document, if any.</param>
    /// <param name="framework">The UI framework of its XAML, or <see langword="null"/> when it is not XAML of a known framework.</param>
    public void Track(DocumentViewModel? document, ToolboxFramework? framework)
    {
        _document = document;
        _framework = framework;
        _elementKey = null;
        Error = null;
        Refresh();
    }

    /// <summary>Reads the element at the caret again, after the caret moved or the text changed.</summary>
    public void Refresh()
    {
        if (_document is not { } document || _framework is not { } framework)
        {
            Clear();
            return;
        }

        var markup = XamlMarkup.Parse(document.Document.Text);
        if (markup.ObjectAt(CaretOffset(document)) is not { } element)
        {
            Clear();
            return;
        }

        var key = KeyOf(element);
        var setNames = element.Attributes.Where(a => !a.IsNamespaceDeclaration).Select(a => a.Name).ToList();
        if (key != _elementKey || !setNames.SequenceEqual(_setNames, StringComparer.Ordinal))
        {
            Rebuild(element, key, setNames, framework);
        }

        Show(document, element);
    }

    /// <summary>Writes a row's value to the XAML: sets the attribute, or removes it for an empty value.</summary>
    /// <param name="row">The row.</param>
    /// <param name="value">The value.</param>
    /// <param name="finished">Whether the user is done editing, so the IDE gets the keyboard back.</param>
    internal void Write(XamlPropertyRowViewModel row, string value, bool finished) => Write(row.Name, value, finished);

    /// <summary>Ends an edit that changed nothing, which still gives the IDE the keyboard back.</summary>
    internal void FinishEdit() => EditFinished?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void AddProperty()
    {
        var name = NewName.Trim();
        if (!XamlAttributeEdits.IsValidName(name))
        {
            Error = name.Length == 0 ? "Type the name of the property to add" : $"'{name}' is not a property name XAML accepts";
            return;
        }

        if (_rows.TryGetValue(name, out var row) && row.IsSet)
        {
            Error = $"{ElementName} already sets {name}: change it above";
            return;
        }

        if (NewValue.Length == 0)
        {
            Error = $"Type a value for {name}";
            return;
        }

        if (Write(name, NewValue, finished: true))
        {
            NewName = string.Empty;
            NewValue = string.Empty;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private bool Write(string name, string value, bool finished)
    {
        if (_document is not { } document)
        {
            return false;
        }

        var text = document.Document.Text;
        var markup = XamlMarkup.Parse(text);
        var element = Find(markup, _elementKey);
        if (element is null)
        {
            Error = "The element changed on the big screen; tap it again";
            return false;
        }

        var change = value.Length == 0
            ? XamlAttributeEdits.Remove(text, element, name)
            : XamlAttributeEdits.Set(text, element, name, value);
        Error = null;
        if (change is not null)
        {
            document.Document.Replace(change.Offset, change.Length, change.Text);
        }

        Refresh();
        if (finished)
        {
            EditFinished?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    /// <summary>Builds the groups for an element, or for a change in the attributes it sets.</summary>
    private void Rebuild(XamlElement element, string key, List<string> setNames, ToolboxFramework framework)
    {
        // For the same element, a row the user is editing survives the rebuild that writing another row causes.
        var previousRows = key == _elementKey ? _rows : new Dictionary<string, XamlPropertyRowViewModel>(StringComparer.Ordinal);
        _elementKey = key;
        _setNames = setNames;
        var known = KnownProperties(element, framework);
        IsKnownType = known.Count > 0;

        var rows = new Dictionary<string, XamlPropertyRowViewModel>(StringComparer.Ordinal);
        XamlPropertyRowViewModel RowFor(string name, XamlPropertyInfo? info)
        {
            if (!rows.TryGetValue(name, out var row))
            {
                row = previousRows.TryGetValue(name, out var previous) && ReferenceEquals(previous.Choices, info?.Choices)
                    ? previous
                    : new XamlPropertyRowViewModel(this, name, info?.TypeName ?? "string", info?.Choices);
                rows[name] = row;
            }

            return row;
        }

        var groups = new List<XamlPropertyGroupViewModel>
        {
            new(SetGroupName, [.. setNames.Select(name => RowFor(name, known.GetValueOrDefault(name)))], isExpanded: true),
        };

        var unset = known.Values.Where(p => !setNames.Contains(p.Name, StringComparer.Ordinal)).ToList();
        foreach (var category in framework.PropertyCategories)
        {
            var members = category.Properties
                .Select(name => unset.Find(p => p.Name == name))
                .OfType<XamlPropertyInfo>()
                .Select(p => RowFor(p.Name, p))
                .ToList();
            if (members.Count > 0)
            {
                groups.Add(new XamlPropertyGroupViewModel(category.Name, members, isExpanded: true));
            }
        }

        var other = unset.Where(p => framework.CategoryOf(p.Name) is null).OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => RowFor(p.Name, p)).ToList();
        if (other.Count > 0)
        {
            groups.Add(new XamlPropertyGroupViewModel(OtherGroupName, other, isExpanded: false));
        }

        _rows = rows;
        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(group);
        }

        ApplySearch();
    }

    /// <summary>Shows the element's header and the values of its attributes.</summary>
    private void Show(DocumentViewModel document, XamlElement element)
    {
        HasElement = true;
        ElementName = element.Name;
        ElementId = (element.FindAttribute("x:Name") ?? element.FindAttribute("Name"))?.Value;
        var line = document.Document.GetLineByOffset(Math.Min(element.Start, document.Document.TextLength)).LineNumber;
        Location = string.Create(CultureInfo.CurrentCulture, $"{document.FileName} · line {line}");
        foreach (var (name, row) in _rows)
        {
            var attribute = element.FindAttribute(name);
            row.Show(attribute?.Value ?? string.Empty, attribute is not null);
        }
    }

    /// <summary>The properties the element's type has: Avalonia's, those its parent attaches, and the framework's common ones.</summary>
    private static Dictionary<string, XamlPropertyInfo> KnownProperties(XamlElement element, ToolboxFramework framework)
    {
        var known = new Dictionary<string, XamlPropertyInfo>(StringComparer.Ordinal);
        if (XamlMarkup.NamespaceOf(element) != framework.XmlNamespace
            || framework.XmlNamespace != AvaloniaXamlTypes.Namespace
            || AvaloniaXamlTypes.Find(element.LocalName) is not { } type)
        {
            return known;
        }

        foreach (var property in AvaloniaXamlTypes.PropertiesOf(type))
        {
            known[property.Name] = property;
        }

        foreach (var name in framework.AttachedPropertiesFor(element.Parent?.LocalName))
        {
            if (AvaloniaXamlTypes.Attached(name) is { } attached)
            {
                known[name] = attached;
            }
        }

        foreach (var name in framework.CommonAttributes)
        {
            known.TryAdd(name, AvaloniaXamlTypes.Describe(name, typeof(string)));
        }

        return known;
    }

    private void ApplySearch()
    {
        var search = SearchText.Trim();
        foreach (var group in Groups)
        {
            group.Filter(search, group.Name != OtherGroupName);
        }
    }

    private void Clear()
    {
        _elementKey = null;
        _setNames = [];
        _rows = new Dictionary<string, XamlPropertyRowViewModel>(StringComparer.Ordinal);
        HasElement = false;
        ElementName = string.Empty;
        ElementId = null;
        Location = string.Empty;
        IsKnownType = false;
        Groups.Clear();
    }

    /// <summary>
    /// Where the caret is: where the editor last put it, or where a Toolbox insertion is about to put it.
    /// </summary>
    private static int CaretOffset(DocumentViewModel document)
    {
        var text = document.Document;
        if (document.PendingNavigation is { } pending && pending.Line <= text.LineCount)
        {
            var line = text.GetLineByNumber(pending.Line);
            return line.Offset + Math.Min(pending.Column - 1, line.Length);
        }

        return Math.Clamp(document.CaretOffset, 0, text.TextLength);
    }

    /// <summary>
    /// Identifies an element by where it is in the tree, such as <c>UserControl:0/StackPanel:1/Button:2</c>, so it is
    /// found again after edits move it in the text.
    /// </summary>
    private static string KeyOf(XamlElement element)
    {
        var parts = new List<string>();
        for (var current = element; current is not null; current = current.Parent)
        {
            var index = current.Parent is { } parent ? IndexOf(parent.Children, current) : 0;
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{current.Name}:{index}"));
        }

        parts.Reverse();
        return string.Join('/', parts);
    }

    private static int IndexOf(IReadOnlyList<XamlElement> elements, XamlElement element)
    {
        for (var i = 0; i < elements.Count; i++)
        {
            if (ReferenceEquals(elements[i], element))
            {
                return i;
            }
        }

        return -1;
    }

    private static XamlElement? Find(XamlMarkup markup, string? key)
    {
        if (key is null)
        {
            return null;
        }

        foreach (var element in markup.Elements)
        {
            if (KeyOf(element) == key)
            {
                return element;
            }
        }

        return null;
    }
}
