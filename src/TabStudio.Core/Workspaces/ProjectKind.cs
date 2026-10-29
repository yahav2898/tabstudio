namespace TabStudio.Core.Workspaces;

/// <summary>
/// What a project produces, as far as TabStudio is concerned. Drives the Solution Explorer icon,
/// the Compatibility Doctor and the run host that F5 picks.
/// </summary>
public enum ProjectKind
{
    /// <summary>A project TabStudio does not recognize.</summary>
    Unknown,

    /// <summary>A console application (<c>OutputType</c> of <c>Exe</c> with no UI framework).</summary>
    ConsoleApp,

    /// <summary>A class library.</summary>
    ClassLibrary,

    /// <summary>A test project (xUnit, NUnit, MSTest).</summary>
    Test,

    /// <summary>An ASP.NET Core web application (<c>Microsoft.NET.Sdk.Web</c>).</summary>
    AspNetCore,

    /// <summary>A Blazor WebAssembly application.</summary>
    BlazorWebAssembly,

    /// <summary>A background worker service (<c>Microsoft.NET.Sdk.Worker</c>).</summary>
    Worker,

    /// <summary>A Windows Forms desktop application.</summary>
    WinForms,

    /// <summary>A WPF desktop application.</summary>
    Wpf,

    /// <summary>A WinUI 3 / Windows App SDK application.</summary>
    WinUI,

    /// <summary>A .NET MAUI application.</summary>
    Maui,

    /// <summary>An Avalonia UI application.</summary>
    Avalonia,

    /// <summary>An Uno Platform application.</summary>
    Uno,

    /// <summary>A native C or C++ project (<c>.vcxproj</c>).</summary>
    NativeCpp,

    /// <summary>
    /// An ASP.NET Core app with Razor components, such as one from the Blazor Web App template: Interactive Server,
    /// WebAssembly or Auto components served by an ASP.NET Core host.
    /// </summary>
    BlazorWebApp,

    /// <summary>A Python program: a folder with <c>pyproject.toml</c>, <c>requirements.txt</c> or <c>.py</c> files.</summary>
    Python,

    /// <summary>A Node.js program: a folder with <c>package.json</c>.</summary>
    Node,

    /// <summary>A Go module.</summary>
    Go,

    /// <summary>A Rust Cargo package or workspace.</summary>
    Rust,

    /// <summary>A portable C or C++ project built with CMake, make or a compiler from the Toolchain Store.</summary>
    Cpp,

    /// <summary>A Java Maven, Gradle or source project.</summary>
    Java,
    /// <summary>
    /// A static web site: a folder of HTML, CSS and JavaScript with an <c>index.html</c>, served as it is, without
    /// a .NET or Node.js project to build it.
    /// </summary>
    StaticWebSite,
    /// <summary>An Android application built with Gradle on this device.</summary>
    Android,

    /// <summary>
    /// A .NET Aspire AppHost (<c>Aspire.AppHost.Sdk</c>): it starts an app's projects and containers through Aspire's
    /// developer control plane on a computer, and builds with code its SDK generates for the projects it references.
    /// </summary>
    AspireAppHost,

    /// <summary>Firmware built by its native project system and uploaded to an explicitly selected external device.</summary>
    EmbeddedFirmware,

    /// <summary>
    /// A repository's build script written as a .NET program, such as one on Bullseye, SimpleExec, NUKE or
    /// Cake Frosting: it builds, tests and publishes the other projects by starting tools like <c>dotnet</c> as
    /// processes, so it is never an app to run.
    /// </summary>
    BuildScript,

    /// <summary>A PHP project: a <c>composer.json</c>, a Laravel, Symfony or Slim app, a PHP site or PHP scripts.</summary>
    Php,

    /// <summary>A Kotlin program on the JVM: a Gradle or Maven build that compiles Kotlin, or <c>.kt</c> and <c>.kts</c> files.</summary>
    Kotlin,

    /// <summary>A Swift package (<c>Package.swift</c>) or Swift files.</summary>
    Swift,

    /// <summary>
    /// Benchmarks written with BenchmarkDotNet: a program that builds each benchmark into a separate program with the
    /// .NET SDK and starts it as a process, so it is never an app to run on a tablet.
    /// </summary>
    Benchmark,
}

/// <summary>
/// The language a project is written in, inferred from its file extension.
/// </summary>
public enum ProjectLanguage
{
    /// <summary>The language could not be determined.</summary>
    Unknown,

    /// <summary>C# (<c>.csproj</c>).</summary>
    CSharp,

    /// <summary>Visual Basic (<c>.vbproj</c>).</summary>
    VisualBasic,

    /// <summary>F# (<c>.fsproj</c>).</summary>
    FSharp,

    /// <summary>C or C++ (<c>.vcxproj</c>).</summary>
    Cpp,
}
