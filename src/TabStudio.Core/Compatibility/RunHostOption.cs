using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Compatibility;

/// <summary>One way a project can run, on this device or through it, and whether it can today.</summary>
/// <param name="Host">The run host.</param>
/// <param name="Name">Its name for the Run button and the Compatibility Doctor, such as <c>Windows Compatibility</c>.</param>
/// <param name="Status">Whether it runs the project today, or what it still needs, such as a toolchain.</param>
/// <param name="Explanation">One or two sentences: how it runs the project, or why it cannot.</param>
/// <param name="Preference">
/// How good a choice it is when it can run the project, lowest first: a host that runs the code beside it in
/// TabStudio's own layers comes before Windows Compatibility, which comes before a PC.
/// </param>
public sealed record RunHostOption(RunHost Host, string Name, CompatibilityStatus Status, string Explanation, int Preference)
{
    /// <summary>The preference of TabStudio's own layers for Windows code, such as the Forms layer and OpenSilver.</summary>
    public const int OwnLayerPreference = 100;

    /// <summary>The preference of Windows Compatibility, the real app under Wine on the tablet.</summary>
    public const int WindowsCompatibilityPreference = 200;

    /// <summary>The preference of the Windows Bridge, the real app on a PC.</summary>
    public const int WindowsBridgePreference = 300;

    /// <summary>Whether it runs the project today.</summary>
    public bool IsAvailable => Status == CompatibilityStatus.Ready;

    /// <summary>Whether it could once something is installed on this device, such as its toolchain.</summary>
    public bool IsInstallable => Status == CompatibilityStatus.NeedsToolchain;
}

/// <summary>
/// A run host that says which projects it can run on this device: TabStudio's Forms layer, OpenSilver, Windows
/// Compatibility, the Windows Bridge. The IDE asks every registered provider, so a host joins the Compatibility
/// Doctor and the Run button by registering one.
/// </summary>
public interface IRunHostProvider
{
    /// <summary>The ways this host can run a project, if any.</summary>
    /// <param name="project">The project.</param>
    IEnumerable<RunHostOption> OptionsFor(ProjectInfo project);
}

/// <summary>Ranks the ways a project can run, best first.</summary>
public static class RunHostChoices
{
    /// <summary>
    /// Every provider's options for a project: the ones that run it today first, then those that need something
    /// installed, then the rest, each group by preference.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="providers">The registered providers.</param>
    public static IReadOnlyList<RunHostOption> For(ProjectInfo project, IEnumerable<IRunHostProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(providers);
        return [.. providers
            .SelectMany(provider => provider.OptionsFor(project))
            .OrderBy(option => option.IsAvailable ? 0 : option.IsInstallable ? 1 : 2)
            .ThenBy(option => option.Preference)];
    }
}
