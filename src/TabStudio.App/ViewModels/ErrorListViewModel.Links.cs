using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.ViewModels;

/// <summary>An Error List row whose code has a page that explains it, as language servers give for theirs.</summary>
public sealed partial record ErrorItemViewModel
{
    /// <summary>The page that explains the diagnostic, if any.</summary>
    public Uri? HelpLink => Diagnostic.HelpLink;

    /// <summary>Whether tapping the code opens a page about it.</summary>
    public bool HasHelpLink => HelpLink is not null;

    /// <summary>The code's tooltip: where tapping it goes.</summary>
    public string? HelpLinkToolTip => HelpLink is { } link ? $"Open {link.Host}: {Code}" : null;
}

/// <summary>Opening the pages that explain diagnostics, from the Error List's Code column.</summary>
public sealed partial class ErrorListViewModel
{
    /// <summary>Raised when the user taps a code with a page; the workspace opens it in the browser.</summary>
    public event EventHandler<Uri>? HelpLinkRequested;

    /// <summary>Opens the page that explains a row's diagnostic.</summary>
    /// <param name="item">The row.</param>
    [RelayCommand]
    private void OpenHelpLink(ErrorItemViewModel? item)
    {
        if (item?.HelpLink is { } link)
        {
            HelpLinkRequested?.Invoke(this, link);
        }
    }
}
