using System.Text.Json;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Projects;

/// <summary>What kind of PHP project a folder is, which decides how F5 serves it.</summary>
public enum PhpProjectKind
{
    /// <summary>PHP scripts run from the command line.</summary>
    Scripts,

    /// <summary>A plain PHP web site: an <c>index.php</c> served by PHP's built-in web server.</summary>
    Site,

    /// <summary>A Laravel app: <c>artisan</c> and <c>laravel/framework</c>.</summary>
    Laravel,

    /// <summary>A Symfony app: <c>symfony/framework-bundle</c> and <c>public/index.php</c>.</summary>
    Symfony,

    /// <summary>A Slim app: <c>slim/slim</c> and <c>public/index.php</c>.</summary>
    Slim,
}

/// <summary>
/// Reads a PHP project: its <c>composer.json</c>, its framework, its front controller and the Vite build of a
/// Laravel app's assets.
/// </summary>
public static partial class PhpProject
{
    private static readonly string[] PackageSections = ["dependencies", "devDependencies"];

    /// <summary>The kind of a PHP project folder.</summary>
    /// <param name="directory">The project folder.</param>
    public static PhpProjectKind Detect(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var packages = RequiredPackages(directory);
        if (File.Exists(Path.Combine(directory, "artisan")) && packages.Contains("laravel/framework"))
        {
            return PhpProjectKind.Laravel;
        }

        var frontController = DocumentRoot(directory) != ".";
        if (frontController && packages.Contains("symfony/framework-bundle"))
        {
            return PhpProjectKind.Symfony;
        }

        if (frontController && packages.Contains("slim/slim"))
        {
            return PhpProjectKind.Slim;
        }

        return frontController || File.Exists(Path.Combine(directory, "index.php")) ? PhpProjectKind.Site : PhpProjectKind.Scripts;
    }

    /// <summary>The packages <c>composer.json</c> requires, development ones included, by name.</summary>
    /// <param name="directory">The project folder.</param>
    public static IReadOnlySet<string> RequiredPackages(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifest = Path.Combine(directory, "composer.json");
        try
        {
            if (!File.Exists(manifest))
            {
                return result;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            foreach (var section in new[] { "require", "require-dev" })
            {
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(section, out var packages) && packages.ValueKind == JsonValueKind.Object)
                {
                    foreach (var package in packages.EnumerateObject())
                    {
                        result.Add(package.Name);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable manifest names no packages; composer install says what is wrong with it.
        }

        return result;
    }

    /// <summary>
    /// The folders a PHP framework serves its front controller from, in the order looked at: <c>public</c> (Laravel,
    /// Symfony, Slim, CodeIgniter), <c>web</c> (Yii 2) and <c>webroot</c> (CakePHP).
    /// </summary>
    public static IReadOnlyList<string> WebRoots { get; } = ["public", "web", "webroot"];

    /// <summary>
    /// The folder PHP's web server serves the project from: the first of <see cref="WebRoots"/> with an
    /// <c>index.php</c>, else the project folder itself (<c>.</c>).
    /// </summary>
    /// <param name="directory">The project folder.</param>
    public static string DocumentRoot(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return WebRoots.FirstOrDefault(root => File.Exists(Path.Combine(directory, root, "index.php"))) ?? ".";
    }

    /// <summary>
    /// The version Composer should give the project itself when Git cannot say: on a detached checkout (a commit or a
    /// tag, as a pinned clone is), Composer calls the root package <c>dev-&lt;commit&gt;</c>, which no dependency of its
    /// own development requirements accepts (guzzle's test server requires guzzle 8, rollbar requires monolog 2 or 3).
    /// The branch alias in <c>composer.json</c> (<c>extra.branch-alias</c>, such as <c>"dev-main": "3.x-dev"</c>) says
    /// what the default branch is; <see langword="null"/> on a branch, which Composer reads itself, or without an alias.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    public static string? ComposerRootVersion(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var manifest = Path.Combine(directory, "composer.json");
        if (!File.Exists(manifest) || !IsDetachedCheckout(directory))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("extra", out var extra) || extra.ValueKind != JsonValueKind.Object
                || !extra.TryGetProperty("branch-alias", out var aliases) || aliases.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var all = aliases.EnumerateObject().Where(a => a.Value.ValueKind == JsonValueKind.String).ToList();
            var alias = all.FirstOrDefault(a => a.Name is "dev-main" or "dev-master" or "dev-trunk" or "dev-develop");
            return (alias.Value.ValueKind == JsonValueKind.String ? alias : all.FirstOrDefault()).Value is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether the Git repository a folder is in has a commit checked out rather than a branch.</summary>
    private static bool IsDetachedCheckout(string directory)
    {
        for (var folder = Path.GetFullPath(directory); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            var head = Path.Combine(folder, ".git", "HEAD");
            if (File.Exists(head))
            {
                try
                {
                    return !File.ReadAllText(head).TrimStart().StartsWith("ref:", StringComparison.Ordinal);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            }

            if (File.Exists(Path.Combine(folder, ".git")))
            {
                // A worktree or submodule: its HEAD is elsewhere, and Composer reads it there.
                return false;
            }
        }

        return false;
    }

    /// <summary>The <c>composer.json</c> script that starts the app, such as Slim's <c>start</c>, or <see langword="null"/>.</summary>
    /// <param name="directory">The project folder.</param>
    public static string? StartScript(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var manifest = Path.Combine(directory, "composer.json");
        return File.Exists(manifest) && RunPlanner.ReadJson(manifest, "scripts.start") is not null ? "start" : null;
    }

    /// <summary>
    /// Whether the project builds its assets with Vite, as Laravel's starter does: a <c>package.json</c> with a
    /// <c>build</c> script and <c>vite</c> among its packages.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    public static bool UsesVite(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var manifest = Path.Combine(directory, "package.json");
        if (!File.Exists(manifest) || RunPlanner.ReadJson(manifest, "scripts.build") is null)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return PackageSections.Any(section =>
                document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(section, out var packages)
                && packages.ValueKind == JsonValueKind.Object
                && packages.TryGetProperty("vite", out _));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether a Laravel app's Vite assets need building: it uses Vite, its <c>node_modules</c> are installed, and its
    /// <c>public/build/manifest.json</c> is missing or older than a file under <c>resources</c>, <c>vite.config.*</c>
    /// or <c>package.json</c>.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    public static bool NeedsAssetBuild(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!UsesVite(directory) || !Directory.Exists(Path.Combine(directory, "node_modules")))
        {
            return false;
        }

        var manifest = new FileInfo(Path.Combine(directory, "public", "build", "manifest.json"));
        if (!manifest.Exists)
        {
            return true;
        }

        try
        {
            var inputs = Directory.EnumerateFiles(directory, "vite.config.*")
                .Append(Path.Combine(directory, "package.json"))
                .Concat(Directory.Exists(Path.Combine(directory, "resources"))
                    ? Directory.EnumerateFiles(Path.Combine(directory, "resources"), "*", SearchOption.AllDirectories)
                    : []);
            return inputs.Any(file => File.GetLastWriteTimeUtc(file) > manifest.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// The value of a key in a <c>.env</c> file, as Laravel reads it (quotes removed), or <see langword="null"/> when
    /// it has none.
    /// </summary>
    /// <param name="file">The <c>.env</c> file.</param>
    /// <param name="key">The key, such as <c>DB_CONNECTION</c>.</param>
    public static string? EnvValue(string file, string key)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(key);
        if (!File.Exists(file))
        {
            return null;
        }

        foreach (var line in File.ReadLines(file))
        {
            var match = EnvLine().Match(line);
            if (match.Success && string.Equals(match.Groups["key"].Value, key, StringComparison.Ordinal))
            {
                var value = match.Groups["value"].Value.Trim();
                return value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0] ? value[1..^1] : value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^\s*(?:export\s+)?(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[^#\r\n]*?)\s*(?:#.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvLine();
}
