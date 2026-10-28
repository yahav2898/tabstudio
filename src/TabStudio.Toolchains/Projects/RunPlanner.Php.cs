using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// PHP run plans: a Laravel app with <c>php artisan serve</c> (its Vite assets built first), a Slim, Symfony or plain
/// PHP site with PHP's built-in web server, an app's own <c>composer start</c>, or a script.
/// </summary>
public static partial class RunPlanner
{
    private static readonly string[] PhpScripts = ["main.php", "app.php"];

    private static RunPlan? PlanPhpProject(ToolchainProject project)
    {
        var directory = project.Directory;
        var variables = Variables(project, null, null);
        var kind = PhpProject.Detect(directory);
        if (kind == PhpProjectKind.Laravel)
        {
            var port = FreeLoopbackPort().ToString(CultureInfo.InvariantCulture);
            var build = PhpProject.NeedsAssetBuild(directory) ? Spec(project, ["npm", "run", "build"], variables) : null;
            return new RunPlan(build, Spec(project, ["php", "artisan", "serve", "--host=127.0.0.1", "--port=" + port], variables), "artisan serve")
            {
                ProgramName = "artisan serve",
            };
        }

        if (kind == PhpProjectKind.Scripts && PhpScripts.FirstOrDefault(f => File.Exists(Path.Combine(directory, f))) is { } main)
        {
            return new RunPlan(null, Spec(project, ["php", main], Variables(project, main, null)), main);
        }

        if (kind != PhpProjectKind.Scripts && !File.Exists(Path.Combine(directory, "public", "index.php"))
            && !File.Exists(Path.Combine(directory, "index.php")) && PhpProject.StartScript(directory) is { } script)
        {
            return new RunPlan(null, Spec(project, ["composer", script], variables), "composer " + script) { ProgramName = "composer " + script };
        }

        if (kind is PhpProjectKind.Site or PhpProjectKind.Slim or PhpProjectKind.Symfony)
        {
            var port = FreeLoopbackPort().ToString(CultureInfo.InvariantCulture);
            var documentRoot = File.Exists(Path.Combine(directory, "public", "index.php")) ? "public" : ".";
            return new RunPlan(null, Spec(project, ["php", "-S", "127.0.0.1:" + port, "-t", documentRoot], variables), "php -S")
            {
                ProgramName = "php -S",
            };
        }

        if (PhpProject.StartScript(directory) is { } start)
        {
            return new RunPlan(null, Spec(project, ["composer", start], variables), "composer " + start) { ProgramName = "composer " + start };
        }

        return null;
    }

    /// <summary>A TCP port on the loopback address that nothing listens on now, for a web server to serve on.</summary>
    public static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
