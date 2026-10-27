using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>An immutable native SDK installation recipe. The installer verifies the exact source revision before these steps.</summary>
/// <param name="Id">The Toolchain Store identifier.</param>
/// <param name="Revision">The immutable Git commit, including the manifest's pinned submodule revisions.</param>
/// <param name="Repository">The authoritative upstream Git repository.</param>
/// <param name="SourceDirectory">The absolute checkout path inside the shared glibc environment.</param>
/// <param name="Packages">Prerequisites installed through signed Ubuntu indexes.</param>
/// <param name="Commands">Ordered argument-vector setup commands; no project text is evaluated as shell source.</param>
/// <param name="Files">App-owned launchers and validation firmware written before setup commands.</param>
/// <param name="Environment">SDK-specific environment inherited by setup commands and the SDK's launches.</param>
public sealed record EmbeddedSdkRecipe(string Id, string Revision, Uri Repository, string SourceDirectory,
    IReadOnlyList<string> Packages, IReadOnlyList<ProcessStartSpec> Commands, IReadOnlyList<FileDefinition> Files,
    IReadOnlyDictionary<string, string?> Environment)
{
    /// <summary>Whether source checkout must also restore upstream's exact Git submodule revisions.</summary>
    public bool Submodules { get; init; } = true;

    /// <summary>Whether this SDK comes from its Git checkout rather than verified release-component archives.</summary>
    public bool CheckoutSource { get; init; } = true;

    /// <summary>Architecture-filtered release archives and their setup commands, verified using the existing downloader.</summary>
    public IReadOnlyList<SetupStepDefinition> Setup { get; init; } = [];

    /// <summary>Existing Store packages installed in the shared guest before this SDK's commands run.</summary>
    public IReadOnlyList<string> RequiredToolchains { get; init; } = [];

    /// <summary>Additional app-owned installation directories removed when uninstalling this SDK.</summary>
    public IReadOnlyList<string> Removes { get; init; } = [];

    /// <summary>SDK validation artifacts that must exist and be nonempty before installation is considered ready.</summary>
    public IReadOnlyList<string> RequiredArtifacts { get; init; } = [];
}
