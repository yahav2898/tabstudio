namespace TabStudio.App.Services;

/// <summary>
/// PHP templates: a console program with a class and PHPUnit tests, and a web site served by PHP's built-in web
/// server in the web preview. Composer installs their packages when they open.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A PHP console program with PHPUnit tests.</summary>
    public const string PhpId = "php";

    /// <summary>A PHP web site served by PHP's built-in web server.</summary>
    public const string PhpSiteId = "php-site";

    private static ProjectTemplate[] PhpTemplates =>
    [
        new(PhpId, "PHP Console App", "A PHP 8 program with a class and PHPUnit tests, Phpactor IntelliSense and Xdebug debugging.", ["PHP", "Console", "PHPUnit"]),
        new(PhpSiteId, "PHP Web Site", "A PHP page with a form, served on the tablet by PHP's built-in web server and previewed beside the code.", ["PHP", "Web"]),
    ];

    private static string PhpNamespace(string name) => string.Concat(name.Split('.', '_').Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    private static string PhpPackage(string name) => "app/" + name.ToLowerInvariant().Replace('.', '-').Replace('_', '-');

    private static string CreatePhpProject(string root, string name)
    {
        var ns = PhpNamespace(name);
        File.WriteAllText(Path.Combine(root, "composer.json"), $$"""
            {
                "name": "{{PhpPackage(name)}}",
                "description": "A PHP console app made with TabStudio.",
                "type": "project",
                "require": {
                    "php": ">=8.3"
                },
                "require-dev": {
                    "phpunit/phpunit": "^12.0"
                },
                "autoload": {
                    "psr-4": {
                        "{{ns}}\\": "src/"
                    }
                },
                "autoload-dev": {
                    "psr-4": {
                        "{{ns}}\\Tests\\": "tests/"
                    }
                }
            }

            """);
        File.WriteAllText(Path.Combine(root, "main.php"), $$"""
            <?php

            // Press F5 to run this program with PHP on your tablet; set a breakpoint first to stop in the debugger.
            // Type your answer in the box under the Output panel. Test Explorer runs the PHPUnit tests in tests/.

            declare(strict_types=1);

            require __DIR__ . '/vendor/autoload.php';

            use {{ns}}\Greeter;

            echo 'What is your name? ';
            $name = trim((string) fgets(STDIN));
            $greeter = new Greeter();
            echo $greeter->greet($name === '' ? 'world' : $name), PHP_EOL;

            """);
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "Greeter.php"), $$"""
            <?php

            declare(strict_types=1);

            namespace {{ns}};

            final class Greeter
            {
                public function greet(string $name): string
                {
                    $answer = 42;
                    return sprintf('Hello, %s! This is PHP %s on TabStudio. The answer is %d.', $name, PHP_VERSION, $answer);
                }
            }

            """);
        Directory.CreateDirectory(Path.Combine(root, "tests"));
        File.WriteAllText(Path.Combine(root, "tests", "GreeterTest.php"), $$"""
            <?php

            declare(strict_types=1);

            namespace {{ns}}\Tests;

            use {{ns}}\Greeter;
            use PHPUnit\Framework\TestCase;

            final class GreeterTest extends TestCase
            {
                public function testGreetsByName(): void
                {
                    $this->assertStringStartsWith('Hello, Ada!', (new Greeter())->greet('Ada'));
                }
            }

            """);
        File.WriteAllText(Path.Combine(root, "phpunit.xml"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <phpunit xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                     xsi:noNamespaceSchemaLocation="vendor/phpunit/phpunit/phpunit.xsd"
                     bootstrap="vendor/autoload.php"
                     colors="false">
                <testsuites>
                    <testsuite name="Tests">
                        <directory>tests</directory>
                    </testsuite>
                </testsuites>
            </phpunit>

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), "/vendor/\n.phpunit.cache/\n.tabstudio/\n");
        return root;
    }

    private static string CreatePhpSite(string root, string name)
    {
        File.WriteAllText(Path.Combine(root, "index.php"), $$"""
            <?php

            // Press F5 to serve this site with PHP's built-in web server and see it in the web preview.
            // Saving a file and refreshing the preview shows your change; set a breakpoint to stop in the debugger.

            declare(strict_types=1);

            $name = trim($_GET['name'] ?? '');
            $greeting = $name === '' ? 'Hello from PHP!' : 'Hello, ' . htmlspecialchars($name) . '!';
            ?>
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{name}}</title>
                <link rel="stylesheet" href="style.css">
            </head>
            <body>
                <main>
                    <h1><?= $greeting ?></h1>
                    <p>This page is served by PHP <?= PHP_VERSION ?> on your tablet.</p>
                    <form method="get">
                        <label for="name">Your name</label>
                        <input id="name" name="name" value="<?= htmlspecialchars($name) ?>">
                        <button type="submit">Greet me</button>
                    </form>
                </main>
            </body>
            </html>

            """);
        File.WriteAllText(Path.Combine(root, "style.css"), """
            body { font-family: system-ui, sans-serif; margin: 0; background: #f5f5f7; color: #1d1d1f; }
            main { max-width: 36rem; margin: 3rem auto; padding: 0 1rem; }
            h1 { color: #5e6199; }
            input, button { font: inherit; padding: 0.4rem 0.6rem; }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), "/vendor/\n.tabstudio/\n");
        return root;
    }
}
