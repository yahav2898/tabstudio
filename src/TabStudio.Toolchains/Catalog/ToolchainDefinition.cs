namespace TabStudio.Toolchains.Catalog;

/// <summary>
/// One toolchain in the catalog: what it is, what to install for it, how to run its programs, and the language
/// server and debug adapter for its languages.
/// </summary>
/// <remarks>
/// Commands are argument lists, never shell lines, and may contain placeholders that the IDE fills in when it
/// runs them: <c>{file}</c> (the file to run, relative to the project folder), <c>{json}</c> (the value an entry
/// rule read from a JSON file), <c>{port}</c> (a free TCP port, for adapters that listen on one), and the names of
/// <see cref="ProjectEnvironmentDefinition.Variables"/>, such as <c>{python}</c>.
/// </remarks>
public sealed record ToolchainDefinition
{
    /// <summary>A stable identifier, such as <c>python</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The display name, such as <c>Python</c>.</summary>
    public required string Name { get; init; }

    /// <summary>One sentence about what is installed.</summary>
    public required string Description { get; init; }

    /// <summary>The languages it is for, as display names.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>
    /// Whether the Toolchain Store offers it. A toolchain is listed once everything about it works end to end; the
    /// others are described so their projects are recognized, and are finished by listing them.
    /// </summary>
    public bool Listed { get; init; }

    /// <summary>Installs supporting tools without claiming source files or standalone runnable projects.</summary>
    public bool Auxiliary { get; init; }

    /// <summary>The project's home page.</summary>
    public Uri? Homepage { get; init; }

    /// <summary>The license, as an SPDX expression.</summary>
    public string? License { get; init; }

    /// <summary>An additional license agreement shown before installation, when required by the SDK vendor.</summary>
    public string? LicenseAgreement { get; init; }

    /// <summary>The Alpine Linux packages to install. Their dependencies are installed with them.</summary>
    public IReadOnlyList<string> Packages { get; init; } = [];

    /// <summary>
    /// The toolchains this one builds on, installed first, such as Java's for Kotlin, whose programs Java's debugger
    /// debugs.
    /// </summary>
    public IReadOnlyList<string> Requires { get; init; } = [];

    /// <summary>
    /// What the toolchain installs in TabStudio's Ubuntu environment, for tools built for glibc rather than Alpine's
    /// musl, such as Swift or JetBrains' Kotlin language server; <see langword="null"/> when it needs none.
    /// </summary>
    public GlibcEnvironmentDefinition? Glibc { get; init; }

    /// <summary>The package whose version is the toolchain's version, such as <c>python3</c>.</summary>
    public string? VersionPackage { get; init; }

    /// <summary>A version pinned by a separately licensed compiler distribution rather than an Alpine package.</summary>
    public string? PinnedVersion { get; init; }

    /// <summary>The download size before the package index has been read, in bytes.</summary>
    public long EstimatedDownloadSize { get; init; }

    /// <summary>The installed size before the package index has been read, in bytes.</summary>
    public long EstimatedInstalledSize { get; init; }

    /// <summary>How to find the toolchain on a desktop computer, which uses its own installation.</summary>
    public HostDetection? Host { get; init; }

    /// <summary>The extensions of the files it runs, with the dot, such as <c>.py</c>.</summary>
    public IReadOnlyList<string> FileExtensions { get; init; } = [];

    /// <summary>The files that make a folder one of its projects, such as <c>package.json</c>.</summary>
    public IReadOnlyList<string> ProjectFiles { get; init; } = [];

    /// <summary>
    /// Whether a folder with the toolchain's source files at the top is a project even without a project file,
    /// as a folder of Python scripts is.
    /// </summary>
    public bool SourceFolderIsProject { get; init; }

    /// <summary>Whether explicit project markers inside a project define separate projects, as Go modules do.</summary>
    public bool AllowNestedProjects { get; init; }

    /// <summary>How to run a file or a project.</summary>
    public RunDefinition? Run { get; init; }

    /// <summary>A per-project environment, such as Python's virtual environment, or <see langword="null"/>.</summary>
    public ProjectEnvironmentDefinition? ProjectEnvironment { get; init; }

    /// <summary>How to install the packages a project's manifest lists, such as <c>npm install</c>.</summary>
    public IReadOnlyList<DependencyInstallDefinition> Dependencies { get; init; } = [];

    /// <summary>Additional build tools installed only when a project uses their manifest.</summary>
    public IReadOnlyList<ProjectToolDefinition> ProjectTools { get; init; } = [];

    /// <summary>What to install after the packages, such as tools from the language's own package registry.</summary>
    public IReadOnlyList<SetupStepDefinition> Setup { get; init; } = [];

    /// <summary>The language server, for the editor.</summary>
    public ToolCommandDefinition? LanguageServer { get; init; }

    /// <summary>Other tool ids provided by this SDK, such as a second language server.</summary>
    public IReadOnlyList<string> AdditionalToolIds { get; init; } = [];

    /// <summary>The debug adapter, for the debugger.</summary>
    public ToolCommandDefinition? DebugAdapter { get; init; }

    /// <summary>
    /// Other language servers the toolchain brings, for languages beside its own, such as Node.js's servers for
    /// HTML, CSS and JSON.
    /// </summary>
    public IReadOnlyList<ToolCommandDefinition> AdditionalLanguageServers { get; init; } = [];

    /// <summary>Patterns that find file locations in the toolchain's output, such as a stack trace's lines.</summary>
    public IReadOnlyList<ProblemPatternDefinition> ProblemPatterns { get; init; } = [];

    /// <summary>
    /// A pattern for the line that states an error that ended a program, such as Python's
    /// <c>NameError: name 'x' is not defined</c>, with a <c>message</c> group.
    /// </summary>
    public string? ErrorPattern { get; init; }

    /// <summary>Whether a file has one of <see cref="FileExtensions"/>.</summary>
    /// <param name="path">The file.</param>
    public bool RunsFile(string path) =>
        !Auxiliary && FileExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}

/// <summary>How to find a toolchain installed on a desktop computer.</summary>
public sealed record HostDetection
{
    /// <summary>The commands to look for on <c>PATH</c>, in order of preference.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>The arguments that make the command print its version.</summary>
    public IReadOnlyList<string> VersionArguments { get; init; } = [];

    /// <summary>A regular expression with a <c>version</c> group that finds the version in what it printed.</summary>
    public string? VersionPattern { get; init; }
}

/// <summary>How to run a toolchain's programs.</summary>
public sealed record RunDefinition
{
    /// <summary>The command that runs one file, with <c>{file}</c> for the file.</summary>
    public IReadOnlyList<string> File { get; init; } = [];

    /// <summary>How to find and run a project's entry point, tried in order.</summary>
    public IReadOnlyList<EntryRule> Entries { get; init; } = [];

    /// <summary>Environment variables for every run, such as <c>PYTHONUNBUFFERED</c>.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// One way to run a project: it applies when <see cref="File"/> exists in the project folder and, when
/// <see cref="Json"/> is set, that file has a value at that path; when <see cref="Contains"/> or
/// <see cref="Excludes"/> is set, the file's text matches each of the one and none of the other; and when
/// <see cref="Requires"/> is set, the project depends on those packages.
/// </summary>
public sealed record EntryRule
{
    /// <summary>
    /// The file that must exist, relative to the project folder. One folder of the path may be <c>*</c>, such as
    /// <c>src/*/__main__.py</c>: the first folder there, in name order, with the file (and, with
    /// <see cref="Contains"/>, the text it asks for), and its name is the <c>{match}</c> placeholder.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// Regular expressions that <see cref="File"/>'s text must each match, with <c>^</c> and <c>$</c> at line
    /// boundaries, such as an import of <c>fastapi</c> and a line <c>app = FastAPI(</c>; empty when the file's
    /// existence is enough. They tell a module that defines a web app apart from any other module of that name.
    /// </summary>
    public IReadOnlyList<string> Contains { get; init; } = [];

    /// <summary>
    /// Regular expressions none of which <see cref="File"/>'s text may match, as <see cref="Contains"/> reads them,
    /// such as an <c>if __name__ == "__main__":</c> block, which makes a module a script that runs itself.
    /// </summary>
    public IReadOnlyList<string> Excludes { get; init; } = [];

    /// <summary>
    /// Packages the project must depend on for the command to work, such as <c>uvicorn</c>, as the project's Python
    /// manifests and lock files list them (<c>requirements.txt</c>, <c>pyproject.toml</c>, <c>Pipfile</c>,
    /// <c>poetry.lock</c>, <c>uv.lock</c>); empty when the command needs none. A rule whose file matches but whose
    /// packages are missing does not apply, and the run says which are missing.
    /// </summary>
    public IReadOnlyList<string> Requires { get; init; } = [];

    /// <summary>
    /// A dotted path into <see cref="File"/>, which must then be JSON, such as <c>scripts.start</c>; the value is the
    /// <c>{json}</c> placeholder.
    /// </summary>
    public string? Json { get; init; }

    /// <summary>
    /// A regular expression: the rule does not apply when the <see cref="Json"/> value matches it, such as a
    /// <c>start</c> script that serves a production build (<c>next start</c>), which fails until that build exists.
    /// </summary>
    public string? NotMatching { get; init; }

    /// <summary>A command to run first, such as a build, or empty.</summary>
    public IReadOnlyList<string> Build { get; init; } = [];

    /// <summary>The command that runs the project.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>
    /// What the program is called in the Output panel, such as <c>flask run</c>, when <see cref="File"/> is a
    /// settings file rather than the program; <see langword="null"/> names it after the file. Placeholders such as
    /// <c>{match}</c> are filled in, as in <c>uvicorn {match}.main:app</c>.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Environment variables for this entry's commands, on top of the toolchain's, such as <c>PYTHONPATH=src</c> for
    /// a package in a <c>src</c> folder, so that it runs before it is installed.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}

/// <summary>A per-project environment, such as Python's <c>.venv</c>.</summary>
public sealed record ProjectEnvironmentDefinition
{
    /// <summary>The folder it lives in, relative to the project folder.</summary>
    public required string Directory { get; init; }

    /// <summary>The command that creates it.</summary>
    public IReadOnlyList<string> Create { get; init; } = [];

    /// <summary>Placeholders whose values come from the environment while it exists, such as <c>python</c>.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    /// <summary>The values of <see cref="Variables"/> while it does not exist.</summary>
    public IReadOnlyDictionary<string, string> Defaults { get; init; } = new Dictionary<string, string>();

    /// <summary>A folder to put first on <c>PATH</c> while it exists, relative to the project folder.</summary>
    public string? Path { get; init; }
}

/// <summary>How to install what a project's manifest lists.</summary>
public sealed record DependencyInstallDefinition
{
    /// <summary>The manifest, relative to the project folder, such as <c>requirements.txt</c>.</summary>
    public required string Manifest { get; init; }

    /// <summary>A short name for the action, such as <c>npm install</c>.</summary>
    public required string Name { get; init; }

    /// <summary>One sentence about what it does.</summary>
    public string? Description { get; init; }

    /// <summary>The command, run in the project folder.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>A file or folder that exists once the packages are installed, such as <c>node_modules</c>.</summary>
    public string? Installed { get; init; }

    /// <summary>
    /// For a JSON manifest, the properties that list packages, such as <c>dependencies</c>: the install is offered
    /// only when one of them lists any. Empty for a text manifest, which lists packages one per line, and is offered
    /// when it has a line that is not blank or a comment.
    /// </summary>
    public IReadOnlyList<string> Declares { get; init; } = [];

    /// <summary>
    /// How to install the packages when a lock file records their exact versions, such as <c>npm ci</c> for
    /// <c>package-lock.json</c>; <see langword="null"/> when the manifest is the only record.
    /// </summary>
    public LockedInstallDefinition? Locked { get; init; }
}

/// <summary>How to install the exact packages a lock file records, in place of a manifest's install.</summary>
public sealed record LockedInstallDefinition
{
    /// <summary>The lock files, relative to the project folder; the first one there applies, such as <c>package-lock.json</c>.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>A short name for the action, such as <c>npm ci</c>.</summary>
    public required string Name { get; init; }

    /// <summary>One sentence about what it does.</summary>
    public string? Description { get; init; }

    /// <summary>The command, run in the project folder.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];
}

/// <summary>A step that runs after a toolchain's packages are installed: a command, or an archive to unpack.</summary>
public sealed record SetupStepDefinition
{
    /// <summary>Alpine architectures for this step, or empty for every device.</summary>
    public IReadOnlyList<string> Architectures { get; init; } = [];

    /// <summary>Reruns this configuration command when another setup step changes during an update.</summary>
    public bool RunAfterSetupChanges { get; init; }

    /// <summary>What the step installs, such as <c>debugpy</c>.</summary>
    public required string Name { get; init; }

    /// <summary>One sentence about it.</summary>
    public string? Description { get; init; }

    /// <summary>A command to run in the toolchain's environment, or empty.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>An archive to download, verify and unpack, or <see langword="null"/>.</summary>
    public ArchiveDefinition? Archive { get; init; }

    /// <summary>A file to write, such as a small script that starts a tool by the command name others use, or <see langword="null"/>.</summary>
    public FileDefinition? File { get; init; }

    /// <summary>
    /// What removing the toolchain deletes for this step, as paths in the environment, such as the folders a
    /// command installed. An archive's destination and a written file are always deleted.
    /// </summary>
    public IReadOnlyList<string> Removes { get; init; } = [];

    /// <summary>
    /// What identifies this version of the step: the archive's hash, the file's content, or the command. A step
    /// whose fingerprint changed runs again on update.
    /// </summary>
    public string Fingerprint => (Archive, File) switch
    {
        ({ } archive, _) => $"sha256:{archive.Sha256}",
        (_, { } file) => $"file:{file.Path}\u001f{file.Content}",
        _ => $"command:{string.Join('\u001f', Command)}",
    };
}

/// <summary>A file a setup step writes into the environment.</summary>
public sealed record FileDefinition
{
    /// <summary>Its path, as programs in the environment see it, such as <c>/usr/local/bin/js-debug-adapter</c>.</summary>
    public required string Path { get; init; }

    /// <summary>Its text.</summary>
    public required string Content { get; init; }

    /// <summary>Whether it is a program: a script that can be run by name.</summary>
    public bool Executable { get; init; }
}

/// <summary>A downloadable archive with a pinned hash.</summary>
public sealed record ArchiveDefinition
{
    /// <summary>Where to download it.</summary>
    public required Uri Url { get; init; }

    /// <summary>Its SHA-256, in hexadecimal.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Its size in bytes, for progress before the server says.</summary>
    public long Size { get; init; }

    /// <summary>The folder to unpack it into, as programs in the environment see it.</summary>
    public required string Destination { get; init; }

    /// <summary>
    /// How many leading folders of each entry to drop when unpacking, as <c>tar --strip-components</c> does, for an
    /// archive whose files are all inside one top folder named after its version.
    /// </summary>
    public int StripComponents { get; init; }
}

/// <summary>
/// The part of a toolchain that lives in TabStudio's Ubuntu (glibc) environment, beside Alpine: Ubuntu packages,
/// pinned downloads, and the commands and programs that run there.
/// </summary>
public sealed record GlibcEnvironmentDefinition
{
    /// <summary>The Ubuntu packages to install with <c>apt-get</c>, from Ubuntu's signed archive.</summary>
    public IReadOnlyList<string> Packages { get; init; } = [];

    /// <summary>What to install after the packages: archives pinned by their hash, and files.</summary>
    public IReadOnlyList<SetupStepDefinition> Setup { get; init; } = [];

    /// <summary>The commands that run in the Ubuntu environment rather than in Alpine, such as <c>swift</c>.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>
    /// Programs that run in the Ubuntu environment by their path: a path that starts with one of these, or, for one
    /// that starts and ends with <c>/</c>, such as <c>/.build/</c>, contains it, as the programs SwiftPM builds do.
    /// </summary>
    public IReadOnlyList<string> ProgramPaths { get; init; } = [];

    /// <summary>Whether a program, by name or path, runs in the Ubuntu environment.</summary>
    /// <param name="program">The program, such as <c>swift</c> or <c>/work/app/.build/debug/app</c>.</param>
    public bool Runs(string program)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (Commands.Contains(program, StringComparer.Ordinal))
        {
            return true;
        }

        return ProgramPaths.Any(path => program.StartsWith(path, StringComparison.Ordinal)
            || (path.Length > 2 && path[0] == '/' && path[^1] == '/' && !path.StartsWith("/opt/", StringComparison.Ordinal)
                && program.Contains(path, StringComparison.Ordinal)));
    }
}

/// <summary>A program the IDE starts to serve an editor or a debugger, such as a language server.</summary>
public sealed record ToolCommandDefinition
{
    /// <summary>
    /// The tool's id, the one the language server and debug adapter clients know it by, such as <c>pyright</c>,
    /// <c>typescript-language-server</c>, <c>debugpy</c> or <c>js-debug</c>.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>The command.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>Environment variables it needs.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// How to talk to it: <c>stdio</c> (the default) over its standard streams, or <c>tcp</c> on the
    /// <c>{port}</c> in its command.
    /// </summary>
    public string Transport { get; init; } = "stdio";

    /// <summary>For a debug adapter, the type its launch configurations name, such as <c>debugpy</c>.</summary>
    public string? Type { get; init; }

    /// <summary>The Language Server Protocol language ids it serves, such as <c>python</c>.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>Whether it talks over TCP on a port in its command, rather than over its standard streams.</summary>
    public bool UsesTcp => string.Equals(Transport, "tcp", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The command as a process to start with <see cref="Runtimes.IToolchainRuntime.Start"/>, with <c>{port}</c>
    /// filled in. The toolchain's runtime sees workspace folders at their own paths, so file paths and URIs mean
    /// the same inside and outside it.
    /// </summary>
    /// <param name="workingDirectory">The folder to start it in, such as the workspace's.</param>
    /// <param name="port">The port for a TCP adapter, which the caller picked free.</param>
    /// <exception cref="InvalidOperationException">The command is empty, or needs a port and none was given.</exception>
    public Processes.ProcessStartSpec ToStartSpec(string? workingDirectory = null, int? port = null)
    {
        if (Command.Count == 0)
        {
            throw new InvalidOperationException("The catalog gives no command.");
        }

        var arguments = Command.Select(a => a.Contains("{port}", StringComparison.Ordinal)
            ? a.Replace("{port}", (port ?? throw new InvalidOperationException("The command needs a port.")).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            : a).ToList();
        return new Processes.ProcessStartSpec
        {
            Program = arguments[0],
            Arguments = arguments[1..],
            WorkingDirectory = workingDirectory,
            Environment = Environment.ToDictionary(e => e.Key, e => (string?)e.Value, StringComparer.Ordinal),
        };
    }
}

/// <summary>A regular expression that finds a file location in output.</summary>
public sealed record ProblemPatternDefinition
{
    /// <summary>
    /// The pattern, with the groups <c>file</c> and <c>line</c>, and optionally <c>column</c>, <c>severity</c>
    /// and <c>message</c>.
    /// </summary>
    public required string Pattern { get; init; }
}

/// <summary>An optional tool needed by a particular project layout.</summary>
public sealed record ProjectToolDefinition
{
    /// <summary>The manifest that requires the tool.</summary>
    public required string Manifest { get; init; }

    /// <summary>The command to find on PATH.</summary>
    public required string Command { get; init; }

    /// <summary>The Alpine packages that provide it.</summary>
    public IReadOnlyList<string> Packages { get; init; } = [];
}
