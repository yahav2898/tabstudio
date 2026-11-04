using System.Text.Json;

namespace TabStudio.App.Tests.Upgrades;

/// <summary>
/// An app data folder exactly as an earlier TabStudio left it on a tablet: the files under <c>State</c> and the Toolchain
/// Store's Linux environment under <c>Toolchains/linux</c>, written in that version's own format, with a C# solution under
/// <c>Projects</c> that the saved session has open.
/// </summary>
/// <remarks>
/// <para>
/// Each file's shape was read from the code of the tags v0.19.0, v0.19.1, v0.19.2, v0.20.0 to v0.20.4, v0.20.6, v0.21.0
/// and v0.21.1 (there is no v0.20.5). Between them only one format changed: <c>appearance.json</c> gained
/// <c>textSize</c> in 0.20.4 (<c>AppearanceSettings(IdeTheme Theme, TextSize TextSize = System)</c>; before it was
/// <c>AppearanceSettings(IdeTheme Theme)</c>). <c>session.json</c> has the same shape from v0.19.0 to v0.21.1
/// (<c>SessionStore.cs</c>: the store's logic changed in 0.20.1 and 0.20.6, the record did not); 0.21.2 added
/// <c>isStartupProjectChosen</c>, which no earlier version wrote. Every other file is written by code that is the same
/// from v0.19.0 to v0.21.1: <c>RecentWorkspaces.cs</c>, <c>DesktopModeSettings.cs</c>,
/// <c>Toolchains/ProjectSetupStore.cs</c>, <c>Agent/AgentSettings.cs</c> (0.20.1 only made the file private to its
/// owner), <c>GitHubAccount.cs</c>, <c>LiveTestingSettingsStore.cs</c>, <c>PreviewZoomStore.cs</c>, and the Toolchain
/// Store's <c>InstalledToolchains.cs</c> and <c>AlpineEnvironment.cs</c> (ready marker); <c>lib/apk/db/installed</c> is
/// apk's own database. The C++, Go and Python definitions in <c>Catalog/toolchains.json</c>, with Python's setup steps,
/// are the same from v0.19.0 to now, and every one of those versions used Alpine 3.24.3.
/// </para>
/// </remarks>
internal sealed class EarlierVersionData
{
    /// <summary>Lays out the folder <paramref name="root"/> as <paramref name="version"/> left it.</summary>
    /// <param name="root">The app's data root, as <c>AppPaths.Root</c>.</param>
    /// <param name="version">The earlier version, such as <c>0.20.4</c>.</param>
    public EarlierVersionData(string root, string version)
    {
        Root = root;
        EarlierVersion = version;
        State = Path.Combine(root, "State");
        Linux = Path.Combine(root, "Toolchains", "linux");

        WriteSolution();
        WriteSettings();
        WriteSession();
        WriteToolchains();
    }

    public string Root { get; }

    public string EarlierVersion { get; }

    public string State { get; }

    /// <summary>The Toolchain Store's Alpine environment, <c>Toolchains/linux</c>.</summary>
    public string Linux { get; }

    /// <summary>Whether this version saved the text size with the theme: 0.20.4 and later.</summary>
    public bool SavesTextSize => System.Version.Parse(EarlierVersion) >= new System.Version(0, 20, 4);

    public string Solution => Resolve("Projects/Hello/Hello.slnx");

    public string AppProject => Resolve("Projects/Hello/Hello/Hello.csproj");

    public string LibraryProject => Resolve("Projects/Hello/Greetings/Greetings.csproj");

    public string Program => Resolve("Projects/Hello/Hello/Program.cs");

    public string Greeter => Resolve("Projects/Hello/Greetings/Greeter.cs");

    /// <summary>Another workspace in the recent list, a folder.</summary>
    public string OlderFolder => Resolve("Projects/Older");

    /// <summary>Program.cs as saved on disk.</summary>
    public const string ProgramOnDisk = """
        using Greetings;

        Console.WriteLine(Greeter.Greet("tablet"));
        Console.WriteLine("done");

        """;

    /// <summary>Program.cs as it was in the editor, not saved yet, when the earlier version was closed for the update.</summary>
    public const string ProgramUnsaved = """
        using Greetings;

        var name = args.Length > 0 ? args[0] : "tablet";
        Console.WriteLine(Greeter.Greet(name));
        Console.WriteLine("done");

        """;

    public const string GreeterOnDisk = """
        namespace Greetings;

        public static class Greeter
        {
            public static string Greet(string name) =>
                $"Hello, {name}!";
        }

        """;

    /// <summary>Where the caret was in Program.cs, as session.json says.</summary>
    public const int ProgramCaret = 40;

    /// <summary>Where the caret was in Greeter.cs, as session.json says.</summary>
    public const int GreeterCaret = 75;

    /// <summary>The text of each settings file as written, by its path under the root.</summary>
    public Dictionary<string, string> SettingsFiles { get; } = new(StringComparer.Ordinal);

    /// <summary>The text of each Toolchain Store file as written, by its path under the root.</summary>
    public Dictionary<string, string> ToolchainFiles { get; } = new(StringComparer.Ordinal);

    public string Resolve(string relativePath) =>
        Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>A JSON string, escaped as System.Text.Json writes it.</summary>
    private static string Json(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";

    private string Write(string relativePath, string content)
    {
        var path = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private void WriteSolution()
    {
        Write("Projects/Hello/Hello.slnx", """
            <Solution>
              <Project Path="Hello/Hello.csproj" />
              <Project Path="Greetings/Greetings.csproj" />
            </Solution>
            """);
        Write("Projects/Hello/Hello/Hello.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Greetings\Greetings.csproj" />
              </ItemGroup>
            </Project>
            """);
        Write("Projects/Hello/Hello/Program.cs", ProgramOnDisk);
        Write("Projects/Hello/Greetings/Greetings.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        Write("Projects/Hello/Greetings/Greeter.cs", GreeterOnDisk);
        Write("Projects/Older/notes.txt", "older workspace\n");
    }

    private void WriteSetting(string relativePath, string content)
    {
        Write(relativePath, content);
        SettingsFiles[relativePath] = content;
    }

    private void WriteSettings()
    {
        // Services/Appearance/AppearanceSettings.cs: camelCase, string enums. TextSize exists from v0.20.4.
        WriteSetting("State/appearance.json", SavesTextSize
            ? """
              {
                "theme": "Light",
                "textSize": "Larger"
              }
              """
            : """
              {
                "theme": "Light"
              }
              """);

        // Services/DesktopModeSettings.cs: camelCase, string enums.
        WriteSetting("State/desktop-mode.json", """
            {
              "useDisplayAutomatically": true,
              "scale": "Percent150"
            }
            """);

        // Services/Toolchains/ProjectSetupStore.cs: camelCase. Off, so opening the workspace installs nothing.
        WriteSetting("State/project-setup.json", $$"""
            {
              "setUpProjectsWhenTheyOpen": false,
              "projects": {
                {{Json(AppProject)}}: "csproj:4f1c2e"
              }
            }
            """);

        // Services/Agent/AgentSettings.cs: camelCase.
        WriteSetting("State/agent.json", """
            {
              "apiKey": "sk-ant-api03-earlier-version",
              "model": "claude-sonnet-5-5",
              "instructions": "Prefer file-scoped namespaces.",
              "mcpServers": [
                {
                  "name": "docs",
                  "url": "https://mcp.example.org/docs",
                  "token": "mcp-token"
                }
              ],
              "localMode": false
            }
            """);

        // Services/GitHubAccount.cs: camelCase.
        WriteSetting("State/github-account.json", """
            {
              "token": "ghp_earlierVersionToken",
              "login": "tablet-dev",
              "name": "Tablet Dev"
            }
            """);

        // Services/LiveTestingSettingsStore.cs: each solution's settings by its path; camelCase, string enums.
        WriteSetting("State/live-unit-testing.json", $$"""
            {
              {{Json(Solution)}}: {
                "trigger": "OnSave",
                "rules": [
                  {
                    "projectFilePath": {{Json(LibraryProject)}},
                    "name": "",
                    "include": false
                  }
                ]
              }
            }
            """);

        // Services/PreviewZoomStore.cs: each project's mode by its path; string enums.
        WriteSetting("State/preview-zoom.json", $$"""
            {
              {{Json(AppProject)}}: "Fill"
            }
            """);

        // Services/RecentWorkspaces.cs: camelCase, newest first.
        WriteSetting("State/recent.json", $$"""
            [
              {
                "path": {{Json(Solution)}},
                "name": "Hello",
                "description": "2 projects",
                "lastOpened": "2026-10-01T09:30:00+03:00"
              },
              {
                "path": {{Json(OlderFolder)}},
                "name": "Older",
                "description": "Folder",
                "lastOpened": "2026-09-20T18:00:00+03:00"
              }
            ]
            """);
    }

    private void WriteSession()
    {
        // Services/SessionStore.cs (StateJsonContext: camelCase, indented), the same record from v0.19.0 to v0.21.1:
        // WorkspaceViewModel.Documents.cs saved the startup project whether the person chose it or TabStudio did, and
        // had no isStartupProjectChosen. The breakpoint in Program.cs is on a line of the saved file too.
        Write("State/session.json", $$"""
            {
              "workspacePath": {{Json(Solution)}},
              "documents": [
                {
                  "path": {{Json(Program)}},
                  "caretOffset": 40,
                  "unsavedText": {{Json(ProgramUnsaved)}}
                },
                {
                  "path": {{Json(Greeter)}},
                  "caretOffset": 75,
                  "unsavedText": null
                }
              ],
              "activeDocument": {{Json(Greeter)}},
              "startupProject": {{Json(AppProject)}},
              "breakpoints": [
                {
                  "path": {{Json(Program)}},
                  "line": 3,
                  "condition": "name == \"tablet\"",
                  "hitCount": null,
                  "logMessage": null
                },
                {
                  "path": {{Json(Greeter)}},
                  "line": 6,
                  "condition": null,
                  "hitCount": "2",
                  "logMessage": "Greeting {name}"
                }
              ],
              "watchExpressions": [
                "name",
                "args.Length"
              ],
              "recordingBufferMegabytes": 128,
              "isPropertiesExpanded": true
            }
            """);
    }

    private void WriteToolchainFile(string relativePath, string content)
    {
        Write(relativePath, content);
        ToolchainFiles[relativePath] = content;
    }

    private void WriteToolchains()
    {
        // Linux/AlpineEnvironment.cs: the environment is set up once rootfs/.tabstudio/ready exists.
        WriteToolchainFile("Toolchains/linux/rootfs/.tabstudio/ready", "Alpine Linux 3.24.3 x86_64\n");

        // apk's database of installed packages, which says a toolchain's packages are there; and apk's world.
        WriteToolchainFile("Toolchains/linux/rootfs/lib/apk/db/installed", string.Concat(
            Package("alpine-baselayout", "3.7.0-r0", 6_000),
            Package("busybox", "1.37.0-r20", 900_000),
            Package("musl", "1.2.5-r10", 600_000),
            Package("apk-tools", "2.14.9-r2", 300_000),
            Package("build-base", "0.5-r3", 0),
            Package("clang22", "22.1.4-r0", 120_000_000),
            Package("clang22-extra-tools", "22.1.4-r0", 40_000_000),
            Package("cmake", "4.1.2-r0", 30_000_000),
            Package("samurai", "1.2-r7", 100_000),
            Package("gdb", "16.3-r0", 12_000_000),
            Package("lldb", "22.1.4-r0", 20_000_000),
            Package("go", "1.25.4-r0", 200_000_000),
            Package("gopls", "0.20.0-r1", 30_000_000),
            Package("delve", "1.25.2-r1", 15_000_000),
            Package("python3", "3.14.0-r1", 52_000_000),
            Package("py3-pip", "25.3-r0", 14_000_000),
            Package("nodejs", "24.11.1-r0", 68_000_000),
            Package("npm", "11.6.2-r0", 11_000_000)));
        WriteToolchainFile("Toolchains/linux/rootfs/etc/apk/world",
            "alpine-baselayout\napk-tools\nbusybox\nmusl\nbuild-base\nclang22\nclang22-extra-tools\ncmake\nsamurai\ngdb\nlldb\ngo\ngopls\ndelve\npython3\npy3-pip\nnodejs\nnpm\n");

        // What Python's setup steps unpacked: debugpy's archive.
        WriteToolchainFile("Toolchains/linux/rootfs/opt/tabstudio/python/debugpy/debugpy/__init__.py", "__version__ = \"1.8.16\"\n");

        // Installation/InstalledToolchains.cs: System.Text.Json's defaults (PascalCase, indented, its default escaping).
        // Setup holds each step's fingerprint (ToolchainDefinition.cs: "sha256:" + the archive's hash, or "command:" and
        // the command's words joined by U+001F), as Python's steps are in Catalog/toolchains.json from v0.19.0 to now.
        WriteToolchainFile("Toolchains/linux/toolchains.json", """
            {
              "Toolchains": {
                "cpp": {
                  "Version": "22.1.4-r0",
                  "Setup": {},
                  "UpdatedAt": "2026-09-14T08:12:44.1234567+00:00"
                },
                "go": {
                  "Version": "1.25.4-r0",
                  "Setup": {},
                  "UpdatedAt": "2026-09-14T08:20:03.7654321+00:00"
                },
                "python": {
                  "Version": "3.14.0-r1",
                  "Setup": {
                    "debugpy": "sha256:a9e9d3550e15ca479c59333e90845029190531f0cacfedab3b815a57bd913947",
                    "debugpy on Python's path": "command:python3\u001F-c\u001Fimport site; open(site.getsitepackages()[0] + '/tabstudio-debugpy.pth', 'w').write('/opt/tabstudio/python/debugpy\\n')",
                    "Pyright": "command:npm\u001Finstall\u001F--global\u001F--no-fund\u001F--no-audit\u001Fpyright@1.1.403"
                  },
                  "UpdatedAt": "2026-09-15T17:45:10.5000000+00:00"
                }
              },
              "BasePackages": [
                "alpine-baselayout",
                "busybox",
                "musl",
                "apk-tools"
              ]
            }
            """);
    }

    /// <summary>One package in apk's installed database: its name, version, architecture and installed size.</summary>
    private static string Package(string name, string version, long installedSize) =>
        "P:" + name + "\nV:" + version + "\nA:x86_64\nI:" + installedSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n\n";
}
