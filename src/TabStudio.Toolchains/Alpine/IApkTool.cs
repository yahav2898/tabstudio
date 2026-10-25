using System.Text;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Alpine;

/// <summary>What a run of <c>apk</c> printed, and how it ended.</summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="Output">Its standard output and error, as they came.</param>
public sealed record ApkResult(int ExitCode, string Output)
{
    /// <summary>Whether it succeeded.</summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// Whether it failed because a package it needed was not in the local repositories, which installing from
    /// the network fixes.
    /// </summary>
    public bool NeedsNetwork => !Succeeded
        && (Output.Contains("not found", StringComparison.OrdinalIgnoreCase) || Output.Contains("unable to select packages", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Runs Alpine's package manager on the Linux environment's root file system.</summary>
public interface IApkTool
{
    /// <summary>Installs packages from local repositories, without the network.</summary>
    /// <param name="packages">The packages to install, or upgrade.</param>
    /// <param name="repositories">The local repositories, as paths inside the environment.</param>
    /// <param name="upgrade">Whether to upgrade the packages and what they need to their newest versions.</param>
    /// <param name="line">Receives each line apk prints, such as <c>(3/12) Installing libffi (3.5.2-r1)</c>.</param>
    /// <param name="cancellationToken">Stops apk.</param>
    Task<ApkResult> AddAsync(IReadOnlyList<string> packages, IReadOnlyList<string> repositories, bool upgrade, Action<string> line, CancellationToken cancellationToken);

    /// <summary>Installs packages from the distribution's repositories over the network.</summary>
    /// <param name="packages">The packages to install, or upgrade.</param>
    /// <param name="upgrade">Whether to upgrade them.</param>
    /// <param name="line">Receives each line apk prints.</param>
    /// <param name="cancellationToken">Stops apk.</param>
    Task<ApkResult> AddOnlineAsync(IReadOnlyList<string> packages, bool upgrade, Action<string> line, CancellationToken cancellationToken);

    /// <summary>Removes packages, and the dependencies nothing else needs.</summary>
    /// <param name="packages">The packages to remove.</param>
    /// <param name="line">Receives each line apk prints.</param>
    /// <param name="cancellationToken">Stops apk.</param>
    Task<ApkResult> DeleteAsync(IReadOnlyList<string> packages, Action<string> line, CancellationToken cancellationToken);
}

/// <summary>Runs <c>/sbin/apk</c> inside the Linux environment.</summary>
/// <param name="runtime">The environment's runtime.</param>
public sealed class ApkTool(IToolchainRuntime runtime) : IApkTool
{
    /// <inheritdoc />
    public Task<ApkResult> AddAsync(IReadOnlyList<string> packages, IReadOnlyList<string> repositories, bool upgrade, Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(repositories);
        var arguments = new List<string> { "add", "--no-network", "--no-progress", "--repositories-file", "/dev/null" };
        foreach (var repository in repositories)
        {
            arguments.Add("--repository");
            arguments.Add(repository);
        }

        if (upgrade)
        {
            arguments.Add("--upgrade");
        }

        arguments.AddRange(packages);
        return RunAsync(arguments, line, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApkResult> AddOnlineAsync(IReadOnlyList<string> packages, bool upgrade, Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        return RunAsync(["add", "--no-progress", .. upgrade ? new[] { "--upgrade" } : [], .. packages], line, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApkResult> DeleteAsync(IReadOnlyList<string> packages, Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        return RunAsync(["del", "--no-progress", .. packages], line, cancellationToken);
    }

    private async Task<ApkResult> RunAsync(IReadOnlyList<string> arguments, Action<string> line, CancellationToken cancellationToken)
    {
        var result = await CommandRunner.RunAsync(runtime, new ProcessStartSpec { Program = "/sbin/apk", Arguments = arguments }, line, cancellationToken).ConfigureAwait(false);
        return new ApkResult(result.Exit.ExitCode, result.Output);
    }
}
