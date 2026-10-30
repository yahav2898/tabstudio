using System.Globalization;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

/// <summary>
/// PHP projects: a Laravel app is set up as its README asks once its packages are installed (<c>.env</c> from
/// <c>.env.example</c>, <c>php artisan key:generate</c>, its SQLite database file), F5 applies its migrations before
/// <c>php artisan serve</c>, and the debugger is Xdebug, loaded only for debug runs.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// After a Laravel app's packages are installed: its <c>.env</c>, from <c>.env.example</c> when it has none, an
    /// application key when it is empty, and the SQLite database file its <c>.env</c> names, when it uses SQLite.
    /// </summary>
    /// <returns>Whether anything was done.</returns>
    private async Task<bool> SetUpLaravelAsync(ToolchainProject project, CancellationToken cancellationToken)
    {
        if (project.Toolchain.Id != "php" || PhpProject.Detect(project.Directory) != PhpProjectKind.Laravel
            || !File.Exists(Path.Combine(project.Directory, "vendor", "autoload.php")))
        {
            return false;
        }

        var changed = false;
        var settings = Path.Combine(project.Directory, ".env");
        var example = Path.Combine(project.Directory, ".env.example");
        if (!File.Exists(settings) && File.Exists(example))
        {
            File.Copy(example, settings);
            Output.WriteLine($"{project.Name}: created .env from .env.example.", OutputKind.Info);
            changed = true;
        }

        if (File.Exists(settings) && string.IsNullOrEmpty(PhpProject.EnvValue(settings, "APP_KEY")))
        {
            Output.WriteLine($"------ Setting up {project.Name}: php artisan key:generate ------", OutputKind.Info);
            ProjectSetupText = $"Setting up {project.Name}: application key";
            var key = await OutputProcess.RunAsync(_toolchains.Runtime, Artisan(project, "key:generate", "--force", "--no-interaction"), Output, null, cancellationToken, toolchain: "php");
            Output.WriteLine($"========== php artisan key:generate: {(key.Succeeded ? "succeeded" : "failed")} ==========", key.Succeeded ? OutputKind.Success : OutputKind.Failure);
            if (!key.Succeeded)
            {
                ShowSetupOutput();
            }

            changed = true;
        }

        if (SqliteDatabaseOf(project) is { } database && !File.Exists(database))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(database)!);
            await File.WriteAllBytesAsync(database, Array.Empty<byte>(), cancellationToken);
            Output.WriteLine($"{project.Name}: created its SQLite database, {Path.GetRelativePath(project.Directory, database)}.", OutputKind.Info);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// The SQLite database file a Laravel app uses, from <c>DB_CONNECTION=sqlite</c> and <c>DB_DATABASE</c> (by
    /// default <c>database/database.sqlite</c>), or <see langword="null"/> when it uses another database.
    /// </summary>
    private static string? SqliteDatabaseOf(ToolchainProject project)
    {
        var settings = Path.Combine(project.Directory, ".env");
        if (!string.Equals(PhpProject.EnvValue(settings, "DB_CONNECTION"), "sqlite", StringComparison.Ordinal))
        {
            return null;
        }

        var configured = PhpProject.EnvValue(settings, "DB_DATABASE");
        return configured is { Length: > 0 } path && path != ":memory:"
            ? Path.GetFullPath(path, project.Directory)
            : configured is ":memory:" ? null : Path.Combine(project.Directory, "database", "database.sqlite");
    }

    /// <summary>
    /// Before a Laravel app runs, as F5 and Ctrl+F5 start it: <c>php artisan migrate --force</c>, against the database
    /// Services runs for it or the one its <c>.env</c> names.
    /// </summary>
    /// <returns>The failed migration, or <see langword="null"/> when there was nothing to do or it succeeded.</returns>
    private async Task<OutputProcessResult?> PrepareLaravelDatabaseAsync(ToolchainProject project, CancellationToken cancellationToken)
    {
        if (project.Toolchain.Id != "php" || PhpProject.Detect(project.Directory) != PhpProjectKind.Laravel
            || !Directory.Exists(Path.Combine(project.Directory, "database", "migrations"))
            || !File.Exists(Path.Combine(project.Directory, "vendor", "autoload.php")))
        {
            return null;
        }

        await SetUpLaravelAsync(project, cancellationToken);
        StatusText = $"Applying {project.Name}'s database migrations";
        var migrate = WithServiceEnvironment(Artisan(project, "migrate", "--force", "--no-interaction"));
        Output.WriteLine("> " + migrate.CommandLine, OutputKind.Info);
        var result = await OutputProcess.RunAsync(_toolchains.Runtime, migrate, Output, null, cancellationToken, toolchain: "php");
        if (!result.Succeeded)
        {
            Output.WriteLine($"{project.Name}'s migrations could not be applied. Start the database its .env names in View > Services, or set DB_CONNECTION=sqlite in .env.", OutputKind.Failure);
        }

        return result.Succeeded ? null : result;
    }

    private static ProcessStartSpec Artisan(ToolchainProject project, params string[] arguments) => new()
    {
        Program = "php",
        Arguments = ["artisan", .. arguments],
        WorkingDirectory = project.Directory,
        Environment = (project.Toolchain.Run?.Environment ?? new Dictionary<string, string>()).ToDictionary(e => e.Key, e => (string?)e.Value, StringComparer.Ordinal),
    };

    /// <summary>
    /// A PHP debug launch: Xdebug is loaded from an ini file of the project's own (<c>.tabstudio/php-debug</c>), which
    /// <c>PHP_INI_SCAN_DIR</c> adds to PHP's, for the program and every PHP process it starts, as <c>php artisan
    /// serve</c> does, and it connects to the adapter on a free port of this device.
    /// </summary>
    private static LaunchConfiguration PreparePhpDebug(ToolchainProject project, LaunchConfiguration configuration)
    {
        var port = RunPlanner.FreeLoopbackPort();
        var folder = Path.Combine(project.Directory, ".tabstudio", "php-debug");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "99-tabstudio-xdebug.ini"), string.Create(CultureInfo.InvariantCulture, $"""
            ; Written by TabStudio for debugging: loads Xdebug and connects it to the debugger.
            zend_extension=xdebug.so
            xdebug.mode=debug
            xdebug.start_with_request=yes
            xdebug.client_host=127.0.0.1
            xdebug.client_port={port}

            """));
        var properties = (JsonObject)configuration.Properties.DeepClone();
        properties["port"] = port;
        properties["hostname"] = "127.0.0.1";
        var environment = properties["env"] as JsonObject ?? new JsonObject();
        // An empty entry stands for PHP's own scan directory, so its extensions still load.
        environment["PHP_INI_SCAN_DIR"] = Path.PathSeparator + folder;
        environment["XDEBUG_MODE"] = "debug";
        properties["env"] = environment.DeepClone();
        return configuration with { Properties = properties };
    }
}
