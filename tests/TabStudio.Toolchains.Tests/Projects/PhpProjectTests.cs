using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>PHP projects: Laravel, Symfony, Slim and plain sites and scripts, and how F5 serves or runs each.</summary>
public sealed class PhpProjectTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    private static ToolchainDefinition Php => ToolchainCatalog.Default.Find("php")!;

    private const string Laravel = """{ "require": { "php": "^8.2", "laravel/framework": "^13.0" }, "require-dev": { "phpunit/phpunit": "^12.0" } }""";

    [Fact]
    public void A_Laravel_app_is_served_by_artisan_on_a_free_local_port()
    {
        _folder.Write("composer.json", Laravel);
        _folder.Write("artisan", "<?php");
        _folder.Write("public/index.php", "<?php");
        Assert.Equal(PhpProjectKind.Laravel, PhpProject.Detect(_folder.Path));

        var plan = RunPlanner.PlanProject(new(Php, _folder.Path, "composer.json"))!;

        Assert.Null(plan.Build);
        Assert.Equal("php", plan.Run.Program);
        Assert.Equal(["artisan", "serve", "--host=127.0.0.1"], plan.Run.Arguments.Take(3));
        Assert.StartsWith("--port=", plan.Run.Arguments[3], StringComparison.Ordinal);
        Assert.Equal("1", plan.Run.Environment["COMPOSER_ALLOW_SUPERUSER"]);
    }

    [Fact]
    public void A_Laravel_app_with_Vite_builds_its_assets_until_they_are_built()
    {
        _folder.Write("composer.json", Laravel);
        _folder.Write("artisan", "<?php");
        _folder.Write("package.json", """{ "scripts": { "build": "vite build", "dev": "vite" }, "devDependencies": { "vite": "^7.0", "laravel-vite-plugin": "^2.0" } }""");
        _folder.Write("vite.config.js", "export default {}");
        _folder.Write("node_modules/.package-lock.json", "{}");

        Assert.True(PhpProject.UsesVite(_folder.Path));
        var plan = RunPlanner.PlanProject(new(Php, _folder.Path, "composer.json"))!;
        Assert.Equal(["npm", "run", "build"], [plan.Build!.Program, .. plan.Build.Arguments]);

        var manifest = _folder.Write("public/build/manifest.json", "{}");
        File.SetLastWriteTimeUtc(manifest, DateTime.UtcNow.AddMinutes(1));
        Assert.Null(RunPlanner.PlanProject(new(Php, _folder.Path, "composer.json"))!.Build);
    }

    [Theory]
    [InlineData("symfony/framework-bundle", PhpProjectKind.Symfony)]
    [InlineData("slim/slim", PhpProjectKind.Slim)]
    public void Symfony_and_Slim_apps_are_served_from_public_by_PHPs_web_server(string package, PhpProjectKind kind)
    {
        _folder.Write("composer.json", $$"""{ "require": { "{{package}}": "*" } }""");
        _folder.Write("public/index.php", "<?php");
        Assert.Equal(kind, PhpProject.Detect(_folder.Path));

        var plan = RunPlanner.PlanProject(new(Php, _folder.Path, "composer.json"))!;

        Assert.Equal("php", plan.Run.Program);
        Assert.Equal("-S", plan.Run.Arguments[0]);
        Assert.StartsWith("127.0.0.1:", plan.Run.Arguments[1], StringComparison.Ordinal);
        Assert.Equal(["-t", "public"], plan.Run.Arguments.Skip(2));
    }

    [Fact]
    public void A_plain_site_is_served_from_its_folder_and_a_script_project_runs_main_php()
    {
        _folder.Write("index.php", "<?php echo 'hi';");
        Assert.Equal(PhpProjectKind.Site, PhpProject.Detect(_folder.Path));
        Assert.Equal(["-t", "."], RunPlanner.PlanProject(new(Php, _folder.Path, null))!.Run.Arguments.Skip(2));

        using var scripts = new TempDirectory();
        scripts.Write("main.php", "<?php echo 'hi';");
        scripts.Write("src/Greeter.php", "<?php");
        var plan = RunPlanner.PlanProject(new(Php, scripts.Path, null))!;
        Assert.Equal(PhpProjectKind.Scripts, PhpProject.Detect(scripts.Path));
        Assert.Equal(["php", "main.php"], [plan.Run.Program, .. plan.Run.Arguments]);
    }

    [Fact]
    public void Reads_values_from_a_dotenv_file_as_Laravel_does()
    {
        var env = _folder.Write(".env", "APP_KEY=\nDB_CONNECTION=mysql\nDB_DATABASE=\"shop\" # the database\nDB_PASSWORD='secret value'\n");

        Assert.Equal(string.Empty, PhpProject.EnvValue(env, "APP_KEY"));
        Assert.Equal("mysql", PhpProject.EnvValue(env, "DB_CONNECTION"));
        Assert.Equal("shop", PhpProject.EnvValue(env, "DB_DATABASE"));
        Assert.Equal("secret value", PhpProject.EnvValue(env, "DB_PASSWORD"));
        Assert.Null(PhpProject.EnvValue(env, "DB_HOST"));
    }

    [Fact]
    public void A_PHP_app_comes_before_the_Node_js_package_of_its_assets_in_the_same_folder()
    {
        _folder.Write("composer.json", Laravel);
        _folder.Write("artisan", "<?php");
        _folder.Write("package.json", """{ "scripts": { "build": "vite build" } }""");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.Equal("php", projects[0].Toolchain.Id);
    }

    [Fact]
    public void PHP_errors_reach_the_error_list()
    {
        var matcher = new ProblemMatcher(Php);
        var errors = matcher.FindErrors(
            ["PHP Parse error:  syntax error, unexpected token \"}\" in /work/app/src/Greeter.php on line 12"], _ => true);

        var error = Assert.Single(errors);
        Assert.Equal(12, error.Location!.Line);
        Assert.Equal("/work/app/src/Greeter.php", error.Location.File);
        Assert.StartsWith("syntax error", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_syntax_error_is_listed_once_also_in_a_folder_with_spaces()
    {
        // php.ini's log_errors printed each error twice ("PHP Parse error:" and "Parse error:"), and a path with a
        // space, as "My App", was not matched at all.
        var errors = new ProblemMatcher(Php).FindErrors(
            [
                "PHP Parse error:  Unclosed '(' on line 1 in /work/My App/sub dir/Bad.php on line 2",
                "Parse error: Unclosed '(' on line 1 in /work/My App/sub dir/Bad.php on line 2",
            ],
            _ => true);

        var error = Assert.Single(errors);
        Assert.Equal(("/work/My App/sub dir/Bad.php", 2), (error.Location!.File, error.Location.Line));
        Assert.Equal("Unclosed '(' on line 1", error.Message);
    }

    [Fact]
    public void The_build_checks_every_file_even_after_a_syntax_error()
    {
        // php -l exits with 255 on an error, which made xargs stop before the next 64 files were checked.
        var build = RunPlanner.PlanBuild(new ToolchainProject(Php, _folder.Path, null))!;
        var script = build.Arguments[^1];

        Assert.Contains("|| exit 1", script, StringComparison.Ordinal);
        Assert.Contains("-d log_errors=0", script, StringComparison.Ordinal);
    }

    public void Dispose() => _folder.Dispose();
}
