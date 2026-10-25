using TabStudio.Extensions.Contributions;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>Whether one thing an extension adds works on this tablet.</summary>
public enum ExtensionFeatureState
{
    /// <summary>It works: TabStudio uses it.</summary>
    Active,

    /// <summary>It would work, but the extension is not installed or is disabled.</summary>
    Inactive,

    /// <summary>It needs the extension's JavaScript, which TabStudio does not run.</summary>
    NeedsCode,

    /// <summary>TabStudio has nothing that uses it.</summary>
    Unused,
}

/// <summary>One thing an extension adds, as the Features page of its details lists it.</summary>
/// <param name="Title">What it is, such as "Language: Perl".</param>
/// <param name="Detail">More about it, such as the language's file extensions.</param>
/// <param name="State">Whether it works on this tablet.</param>
public sealed record ExtensionFeatureViewModel(string Title, string Detail, ExtensionFeatureState State)
{
    /// <summary>The state in words.</summary>
    public string StateText => State switch
    {
        ExtensionFeatureState.Active => "Active",
        ExtensionFeatureState.Inactive => "Active once installed and enabled",
        ExtensionFeatureState.NeedsCode => "Not available: needs the extension's code to run",
        _ => "Not used by TabStudio",
    };

    /// <summary>Whether it works now.</summary>
    public bool IsActive => State == ExtensionFeatureState.Active;

    /// <summary>Whether it works once the extension is installed and enabled.</summary>
    public bool IsInactive => State == ExtensionFeatureState.Inactive;

    /// <summary>Whether it cannot work on this tablet.</summary>
    public bool IsUnavailable => State is ExtensionFeatureState.NeedsCode or ExtensionFeatureState.Unused;

    /// <summary>Describes what an extension adds.</summary>
    /// <param name="statuses">Its contributions, from <see cref="ContributionSupport.Describe"/>.</param>
    /// <param name="isActive">Whether it is installed and enabled.</param>
    public static IReadOnlyList<ExtensionFeatureViewModel> From(IReadOnlyList<ContributionStatus> statuses, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        return [.. statuses.Select(s => new ExtensionFeatureViewModel(s.Title, s.Detail, s.Support switch
        {
            ContributionSupportLevel.Supported => isActive ? ExtensionFeatureState.Active : ExtensionFeatureState.Inactive,
            ContributionSupportLevel.NeedsCodeHost => ExtensionFeatureState.NeedsCode,
            _ => ExtensionFeatureState.Unused,
        }))];
    }
}

/// <summary>A row of the "More information" table of an extension's details.</summary>
/// <param name="Label">What the row is, such as "Version".</param>
/// <param name="Value">Its value.</param>
public sealed record ExtensionInfoRow(string Label, string Value);
