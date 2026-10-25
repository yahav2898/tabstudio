using Avalonia.Media;
using TabStudio.App.Editing.Extensions;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>The file icon theme in the Solution Explorer: files and folders show its icons instead of TabStudio's badges.</summary>
public sealed partial class ExplorerItemViewModel
{
    private IImage? _themeIcon;
    private bool _themeIconRead;

    /// <summary>The icon the file icon theme gives this file or folder, or <see langword="null"/> for TabStudio's own.</summary>
    public IImage? ThemeIcon
    {
        get
        {
            if (!_themeIconRead)
            {
                _themeIconRead = true;
                _themeIcon = Node.Kind switch
                {
                    WorkspaceNodeKind.File => FileIcons.ForFile(Node.Name),
                    WorkspaceNodeKind.Folder => FileIcons.ForFolder(Node.Name, IsExpanded),
                    _ => null,
                };
            }

            return _themeIcon;
        }
    }

    /// <summary>Whether the row shows the icon theme's icon.</summary>
    public bool HasThemeIcon => ThemeIcon is not null;

    /// <summary>Whether a file row shows TabStudio's badge, because no icon theme gives it an icon.</summary>
    public bool ShowsBadge => IsFile && !HasThemeIcon;

    /// <summary>Whether the row shows TabStudio's own icon, as projects and the solution always do.</summary>
    public bool ShowsSymbol => !IsFile && !HasThemeIcon;

    partial void OnIsExpandedChanged(bool value)
    {
        if (Node.Kind == WorkspaceNodeKind.Folder && _themeIconRead)
        {
            _themeIconRead = false;
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(HasThemeIcon));
            OnPropertyChanged(nameof(ShowsSymbol));
        }
    }
}
