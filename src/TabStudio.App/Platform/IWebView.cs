using Avalonia.Controls;

namespace TabStudio.App.Platform;

/// <summary>
/// An embedded browser, which previews web apps beside the editor. Android provides one built on its system
/// WebView; where the platform has none, the preview offers the system browser instead.
/// </summary>
public interface IWebView
{
    /// <summary>The control to put in the layout.</summary>
    Control Control { get; }

    /// <summary>Raised when the page changes, with its address, so the address bar can follow links.</summary>
    event EventHandler<Uri>? Navigated;

    /// <summary>Opens a page.</summary>
    /// <param name="uri">The page.</param>
    void Navigate(Uri uri);

    /// <summary>Loads the page again.</summary>
    void Reload();

    /// <summary>Goes back in the page history, if there is a page to go back to.</summary>
    void GoBack();

    /// <summary>Goes forward in the page history, if there is a page to go forward to.</summary>
    void GoForward();
}

/// <summary>Creates the platform's embedded browser.</summary>
public interface IWebViewFactory
{
    /// <summary>Creates a web view, or returns <see langword="null"/> when the platform has none.</summary>
    IWebView? Create();
}

/// <summary>The <see cref="IWebViewFactory"/> of platforms without an embedded browser.</summary>
public sealed class NoWebViewFactory : IWebViewFactory
{
    /// <inheritdoc />
    public IWebView? Create() => null;
}
