namespace TabStudio.App.ViewModels;

/// <summary>What the web preview asks its embedded browser to do.</summary>
public enum WebViewAction
{
    /// <summary>Load the page again.</summary>
    Reload,

    /// <summary>Go back in the page history.</summary>
    Back,

    /// <summary>Go forward in the page history.</summary>
    Forward,
}
