namespace TabStudio.App.Services;

/// <summary>
/// The folders TabStudio keeps its own data in. Everything lives under one root, which on Android is the
/// app's private storage.
/// </summary>
public sealed class AppPaths
{
    /// <summary>Creates the paths under a root folder and makes sure the folders exist.</summary>
    /// <param name="root">The root folder. Tests pass a temporary folder.</param>
    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        Root = root;
        Projects = Path.Combine(root, "Projects");
        Cache = Path.Combine(root, "Cache");
        State = Path.Combine(root, "State");
        Trash = Path.Combine(root, "Trash");

        Directory.CreateDirectory(Projects);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(State);
    }

    /// <summary>The root of everything TabStudio stores.</summary>
    public string Root { get; }

    /// <summary>Where new, imported and downloaded projects are created.</summary>
    public string Projects { get; }

    /// <summary>Data that can be rebuilt at any time, such as extracted reference assemblies.</summary>
    public string Cache { get; }

    /// <summary>Small files that remember the user's state: recent workspaces, the last session.</summary>
    public string State { get; }

    /// <summary>
    /// Where deleted files and folders go. A tablet has no recycle bin, so TabStudio keeps its own: deleting from
    /// the Solution Explorer can always be undone by hand.
    /// </summary>
    public string Trash { get; }

    /// <summary>The list of recently opened workspaces.</summary>
    public string RecentWorkspacesFile => Path.Combine(State, "recent.json");

    /// <summary>The open documents and unsaved edits of the last session.</summary>
    public string SessionFile => Path.Combine(State, "session.json");

    /// <summary>The default root: the platform's local application data folder.</summary>
    public static AppPaths CreateDefault() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "TabStudio"));

    /// <summary>
    /// Returns a folder under <see cref="Projects"/> for a new project, adding a number when the name is taken.
    /// </summary>
    /// <param name="name">The preferred folder name.</param>
    public string GetAvailableProjectDirectory(string name)
    {
        var safe = MakeSafeFolderName(name);
        var candidate = Path.Combine(Projects, safe);
        for (var i = 2; Directory.Exists(candidate) || File.Exists(candidate); i++)
        {
            candidate = Path.Combine(Projects, $"{safe}-{i}");
        }

        return candidate;
    }

    /// <summary>Turns any text into a name for a new folder that is valid and visible on every platform.</summary>
    /// <param name="name">The text, such as a repository name.</param>
    public static string MakeSafeFolderName(string name)
    {
        var cleaned = ReplaceInvalidCharacters(name.Trim()).Trim('.', ' ', '-');
        return cleaned.Length == 0 ? "Project" : cleaned;
    }

    /// <summary>
    /// Makes an existing file or folder name valid on every platform, keeping it as close to the original as
    /// possible: <c>.gitignore</c> stays <c>.gitignore</c>.
    /// </summary>
    /// <param name="name">The original name.</param>
    public static string MakeSafeFileName(string name)
    {
        var cleaned = ReplaceInvalidCharacters(name);
        return cleaned is "" or "." or ".." ? "_" : cleaned;
    }

    private static readonly HashSet<char> InvalidNameCharacters =
        [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    private static string ReplaceInvalidCharacters(string name) =>
        new(name.Select(c => InvalidNameCharacters.Contains(c) || char.IsControl(c) ? '-' : c).ToArray());
}
