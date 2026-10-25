namespace TabStudio.LanguageServers.Hosting;

/// <summary>Helpers for <see cref="IServerProcessLauncher"/>.</summary>
public static class ServerProcessLauncherExtensions
{
    /// <summary>Finds the first of several program names that is installed.</summary>
    /// <param name="launcher">The launcher.</param>
    /// <param name="commands">The names to try, in order, such as a definition's <c>Commands</c>.</param>
    /// <returns>The full path of the first one found, or <see langword="null"/> when none is installed.</returns>
    public static string? ResolveFirst(this IServerProcessLauncher launcher, IEnumerable<string> commands)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(commands);
        foreach (var command in commands)
        {
            if (launcher.Resolve(command) is { } path)
            {
                return path;
            }
        }

        return null;
    }
}
