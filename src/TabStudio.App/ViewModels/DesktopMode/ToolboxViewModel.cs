using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Toolbox;
using TabStudio.App.Xaml;

namespace TabStudio.App.ViewModels.DesktopMode;

/// <summary>
/// The Toolbox: the controls of the active document's UI framework, by category, with search. A tap puts the control
/// into the XAML at the caret on the big screen, as Visual Studio's Toolbox does in the XAML editor, and moves the
/// caret into it, so its properties show and the next control goes after it, or inside it for a panel.
/// </summary>
public sealed partial class ToolboxViewModel : ObservableObject
{
    private readonly ToolboxCatalog _catalog;
    private DocumentViewModel? _document;

    /// <summary>Creates the Toolbox.</summary>
    /// <param name="catalog">The frameworks and their controls.</param>
    public ToolboxViewModel(ToolboxCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Raised after a control was put into the XAML.</summary>
    public event EventHandler? Inserted;

    /// <summary>The active document's framework, or <see langword="null"/> when it is not XAML the Toolbox knows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable), nameof(Title), nameof(ShowsNoMatches))]
    public partial ToolboxFramework? Framework { get; private set; }

    /// <summary>Whether there are controls for the active document.</summary>
    public bool IsAvailable => Framework is not null;

    /// <summary>"Avalonia controls".</summary>
    public string Title => Framework is { } framework ? $"{framework.Name} controls" : "Toolbox";

    /// <summary>Shows only the controls whose name or description contains this.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>The categories with controls the search leaves.</summary>
    public ObservableCollection<ToolboxCategoryViewModel> Categories { get; } = [];

    /// <summary>Whether the search leaves any control.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoMatches))]
    public partial bool HasMatches { get; private set; }

    /// <summary>Whether to say that no control matches the search.</summary>
    public bool ShowsNoMatches => IsAvailable && !HasMatches;

    /// <summary>What the last tap did, such as "Added a Button at line 18 of MainView.axaml".</summary>
    [ObservableProperty]
    public partial string? Status { get; private set; }

    /// <summary>Follows the active document: the Toolbox of its framework, or none.</summary>
    /// <param name="document">The document, if any.</param>
    public void Track(DocumentViewModel? document)
    {
        _document = document;
        var framework = _catalog.ForFile(document?.FilePath);
        if (!ReferenceEquals(framework, Framework))
        {
            Framework = framework;
            Status = null;
            Categories.Clear();
            foreach (var category in framework?.Categories ?? [])
            {
                Categories.Add(new ToolboxCategoryViewModel(category, item => Insert(item)));
            }

            ApplySearch();
        }
    }

    /// <summary>Puts a control into the XAML at the caret, and moves the caret into the new element's tag.</summary>
    /// <param name="item">The control.</param>
    /// <returns>Whether it was put in.</returns>
    public bool Insert(ToolboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_document is not { } document || Framework is not { } framework)
        {
            return false;
        }

        var text = document.Document;
        var caret = Math.Clamp(document.CaretOffset, 0, text.TextLength);
        if (document.PendingNavigation is { } pending && pending.Line <= text.LineCount)
        {
            // A control tapped right after another goes after it, even before the editor has moved the caret there.
            var line = text.GetLineByNumber(pending.Line);
            caret = line.Offset + Math.Min(pending.Column - 1, line.Length);
        }

        if (XamlToolboxInsertion.Insert(text.Text, caret, item.Xaml, framework.IsContainer) is not { } insertion)
        {
            Status = $"{document.FileName} has no root element to put a {item.Name} in";
            return false;
        }

        text.Replace(insertion.Change.Offset, insertion.Change.Length, insertion.Change.Text);
        var location = text.GetLocation(insertion.ElementStart + 1);
        document.NavigateTo(location.Line, location.Column);
        Status = $"Added a {item.Name} at line {location.Line} of {document.FileName}";
        Inserted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        var search = SearchText.Trim();
        var matches = false;
        foreach (var category in Categories)
        {
            category.Filter(search);
            matches |= category.IsVisible;
        }

        HasMatches = matches;
    }
}

/// <summary>A category of the Toolbox, such as "Layout", with the controls the search leaves.</summary>
public sealed partial class ToolboxCategoryViewModel : ObservableObject
{
    private readonly IReadOnlyList<ToolboxItemViewModel> _all;

    /// <summary>Creates the category.</summary>
    /// <param name="category">The category's data.</param>
    /// <param name="insert">Puts a control into the XAML.</param>
    public ToolboxCategoryViewModel(ToolboxCategory category, Action<ToolboxItem> insert)
    {
        ArgumentNullException.ThrowIfNull(category);
        Name = category.Name;
        _all = [.. category.Items.Select(item => new ToolboxItemViewModel(item, new RelayCommand(() => insert(item))))];
        Items = [.. _all];
    }

    /// <summary>The category's name.</summary>
    public string Name { get; }

    /// <summary>The controls the search leaves.</summary>
    public ObservableCollection<ToolboxItemViewModel> Items { get; }

    /// <summary>Whether the category is open.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>Whether the search leaves any of its controls.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; } = true;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    /// <summary>Keeps the controls whose name or description contains the search.</summary>
    /// <param name="search">The search; empty keeps every control.</param>
    internal void Filter(string search)
    {
        Items.Clear();
        foreach (var item in _all)
        {
            if (search.Length == 0
                || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                Items.Add(item);
            }
        }

        IsVisible = Items.Count > 0;
        if (search.Length > 0)
        {
            IsExpanded = true;
        }
    }
}

/// <summary>A control in the Toolbox, which a tap puts into the XAML.</summary>
/// <param name="Item">The control's data.</param>
/// <param name="Insert">Puts it into the XAML at the caret.</param>
public sealed record ToolboxItemViewModel(ToolboxItem Item, ICommand Insert)
{
    /// <summary>The control's name.</summary>
    public string Name => Item.Name;

    /// <summary>What it is for.</summary>
    public string Description => Item.Description;

    /// <summary>The icon's name.</summary>
    public string Icon => Item.Icon;
}
