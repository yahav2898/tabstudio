namespace TabStudio.App.ViewModels.Extensions;

/// <summary>The lists of the Extensions window.</summary>
public enum ExtensionsTab
{
    /// <summary>Extensions on the registry: popular ones, or those that match the search.</summary>
    Browse,

    /// <summary>The installed extensions.</summary>
    Installed,

    /// <summary>Installed extensions with a newer version on the registry.</summary>
    Updates,
}

/// <summary>The pages of an extension's details.</summary>
public enum ExtensionDetailsTab
{
    /// <summary>The README.</summary>
    Details,

    /// <summary>What the extension adds, and whether each part works in TabStudio.</summary>
    Features,

    /// <summary>The change log.</summary>
    Changelog,
}
