# PHP and Laravel

TabStudio runs PHP projects on the tablet with PHP 8.5, Composer and Xdebug from the Toolchain Store. Laravel apps
set themselves up the way their README asks, F5 serves them in the web preview, and the debugger stops in your
controllers. Symfony, Slim and plain PHP sites are served by PHP's built-in web server.

## Install

Install **PHP** from the Toolchain Store. It brings:

| Part | What it is | Where it comes from |
| --- | --- | --- |
| PHP 8.5 | The CLI with the extensions Laravel, Symfony and Slim need (`mbstring`, `intl`, `pdo_sqlite`, `pdo_mysql`, `pdo_pgsql`, `gd`, `zip`, `sodium` and others) | Alpine packages, verified against Alpine's signatures |
| Composer | `composer install` for the project's packages | Alpine package |
| Xdebug | The debugger extension, off until a debug run loads it | Alpine package |
| Phpactor | Completion, Quick Info, Go to Definition, Find All References and rename | `phpactor.phar` 2026.10.07.0 from its GitHub release, SHA-256 pinned (MIT) |
| PHP Debug | The debug adapter that talks to Xdebug | `xdebug.php-debug` 1.40.2 from Open VSX, SHA-256 pinned (MIT); it runs on Node.js |

Phpactor and PHP Debug are open source. Phpactor's PHPStan and Psalm extensions are off, so diagnostics are its own.

## Opening a Laravel app

A folder with `artisan` and `laravel/framework` in `composer.json` is a Laravel app. When it opens, the project
setup does what Laravel's installation guide asks, once, with progress in the long-operations bar:

1. `composer install`, from `composer.lock` when there is one.
2. `.env` from `.env.example`, when the app has none.
3. `php artisan key:generate`, when `APP_KEY` is empty.
4. The SQLite database file `.env` names (by default `database/database.sqlite`), when it uses SQLite.
5. `npm install`, when `package.json` is there: Laravel's front end builds with Vite.

Turn this off with **Tools > Set projects up when they open**; F5 then offers each step.

## Running it

**F5** and **Ctrl+F5**:

1. Apply the app's migrations: `php artisan migrate --force`.
2. Build its Vite assets (`npm run build`) when `public/build/manifest.json` is missing or older than a file under
   `resources`, `vite.config.*` or `package.json`.
3. Start `php artisan serve` on a free local port, and open the app in the web preview beside the code.

**Ctrl+Shift+B** checks the syntax of the project's own PHP files with `php -l` and lists errors in the Error List.

## Databases

When `.env` says `DB_CONNECTION=mysql`, `mariadb` or `pgsql` on this device, **View > Services** offers that
database: MariaDB 11 or PostgreSQL 17, run natively, with the database, user and password `.env` names. F5 starts
it before the migrations, and the app gets `DB_HOST` and `DB_PORT` for it. Laravel reads the process's environment
before `.env`, so `.env` itself is never changed.

A Laravel Sail app's `docker-compose.yml` works the same way: its database service (`pgsql`, `mysql`, `mariadb`)
runs natively, and `laravel.test`, Sail's PHP container, is TabStudio's own run. SQLite, and a database on another
machine, are the app's own.

If the database cannot be reached, F5 says which one, and how to start it or switch to SQLite.

## Debugging

Set a breakpoint and press **F5**. TabStudio writes an ini file of its own under `.tabstudio/php-debug` that loads
Xdebug for this run only (`xdebug.mode=debug`, `start_with_request=yes`, a free local port), and adds it to
`PHP_INI_SCAN_DIR`. Every PHP process the run starts loads it, so breakpoints hit in requests that
`php artisan serve` handles, in Artisan commands, and in scripts. Locals, Watch, the Call Stack, stepping and
DataTips work as in C#. Runs without the debugger never load Xdebug, so they are not slowed down.

A script's input (`fgets(STDIN)`, `readline()`) is answered in the Output panel.

## Tests

Test Explorer lists PHPUnit and Pest tests found in `tests/`: `test*` methods and methods marked `#[Test]` or
`@test`. Running them starts `vendor/bin/phpunit` (or `vendor/bin/pest`) with `--log-junit`, filtered to the tests
you chose, and shows each result, its duration and its failure message, with links to the test.

## Other PHP projects

| Project | How F5 runs it |
| --- | --- |
| Symfony (`symfony/framework-bundle`, `public/index.php`) | `php -S 127.0.0.1:<port> -t public`, in the web preview |
| Slim (`slim/slim`, `public/index.php`) | `php -S 127.0.0.1:<port> -t public`, in the web preview |
| A plain site (`index.php`) | `php -S 127.0.0.1:<port> -t .`, in the web preview |
| A `composer.json` with a `start` script | `composer start` |
| Scripts (`main.php`, `app.php`) | `php main.php`, with input in the Output panel; Run Current File runs any `.php` file |

The **PHP Console App** template has a class, a PHPUnit test and a Composer autoloader; **PHP Web Site** is a page with
a form for the built-in server.

## Verified in CI

- The IDE job installs PHP 8.5, Composer, Xdebug, Phpactor and PHP Debug, and opens the PHP Console App template:
  Composer sets it up, Phpactor completes a method, `php -l` puts a syntax error in the Error List, the program runs
  with its input answered, PHPUnit passes in Test Explorer, and Xdebug stops at a breakpoint with its locals.
  Screenshots: [completion](../images/toolchains/php-completion.png), [breakpoint](../images/toolchains/php-breakpoint.png).
- The real-repositories job opens `laravel/laravel` (v13.11.0) and BookStack (a Laravel 12 app, on the job's MySQL),
  sets them up, applies their migrations, and checks their first page through the web preview; and the Symfony
  skeleton (v8.1.99) through PHP's built-in server.

## Limits

- `php artisan serve` is single-threaded, as on a computer: an app that calls itself over HTTP during a request waits
  for itself. Laravel Octane, Horizon and queues with Redis need their own processes; start them in the Terminal.
- Laravel Sail's PHP container does not run; TabStudio's PHP does. Extensions Alpine does not package for PHP 8.5
  (such as `swoole`) are not available.
- Phpactor's diagnostics are lighter than PHPStan's or Psalm's. Run those from the Terminal with Composer.
