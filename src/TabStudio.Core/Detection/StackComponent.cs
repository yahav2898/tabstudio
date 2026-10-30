namespace TabStudio.Core.Detection;

/// <summary>A technology TabStudio can recognize in a repository.</summary>
public enum StackKind
{
    /// <summary>.NET (any project, solution or <c>global.json</c>).</summary>
    DotNet,

    /// <summary>ASP.NET Core.</summary>
    AspNetCore,

    /// <summary>Blazor.</summary>
    Blazor,

    /// <summary>Windows Presentation Foundation.</summary>
    Wpf,

    /// <summary>Windows Forms.</summary>
    WinForms,

    /// <summary>WinUI 3 / Windows App SDK.</summary>
    WinUI,

    /// <summary>.NET MAUI.</summary>
    Maui,

    /// <summary>Avalonia UI.</summary>
    Avalonia,

    /// <summary>Uno Platform.</summary>
    Uno,

    /// <summary>Node.js (<c>package.json</c>).</summary>
    Node,

    /// <summary>TypeScript.</summary>
    TypeScript,

    /// <summary>React.</summary>
    React,

    /// <summary>Vue.</summary>
    Vue,

    /// <summary>Angular.</summary>
    Angular,

    /// <summary>Svelte.</summary>
    Svelte,

    /// <summary>Next.js.</summary>
    NextJs,

    /// <summary>Vite.</summary>
    Vite,

    /// <summary>Rust (<c>Cargo.toml</c>).</summary>
    Rust,

    /// <summary>Go (<c>go.mod</c>).</summary>
    Go,

    /// <summary>Python.</summary>
    Python,

    /// <summary>Java (Maven or Gradle).</summary>
    Java,

    /// <summary>Kotlin.</summary>
    Kotlin,

    /// <summary>C or C++ (CMake or Visual C++ projects).</summary>
    Cpp,

    /// <summary>Solidity smart contracts (Foundry, Hardhat or <c>.sol</c> files).</summary>
    Solidity,

    /// <summary>Move smart contracts (<c>Move.toml</c>).</summary>
    Move,

    /// <summary>A dev container definition.</summary>
    DevContainer,

    /// <summary>A Dockerfile.</summary>
    Docker,

    /// <summary>A static web site: HTML, CSS and JavaScript served as they are, from an <c>index.html</c>.</summary>
    StaticSite,

    /// <summary>PlatformIO projects and environments.</summary>
    PlatformIO,

    /// <summary>Arduino sketches and libraries.</summary>
    Arduino,

    /// <summary>STM32CubeMX or CubeIDE configuration.</summary>
    Stm32Cube,

    /// <summary>Espressif ESP-IDF.</summary>
    EspIdf,

    /// <summary>Zephyr applications built with west.</summary>
    Zephyr,

    /// <summary>Raspberry Pi Pico SDK applications.</summary>
    PicoSdk,

    /// <summary>MicroPython or CircuitPython board files.</summary>
    MicroPython,

    /// <summary>Embedded Rust with an explicit linker memory map.</summary>
    EmbeddedRust,

    /// <summary>Keil, IAR or Atmel vendor project requiring a reviewed GCC import.</summary>
    EmbeddedVendor,

    /// <summary>PHP: a <c>composer.json</c> or <c>.php</c> files.</summary>
    Php,

    /// <summary>Laravel: <c>laravel/framework</c> in <c>composer.json</c>.</summary>
    Laravel,

    /// <summary>Symfony: <c>symfony/framework-bundle</c> in <c>composer.json</c>.</summary>
    Symfony,

    /// <summary>Slim: <c>slim/slim</c> in <c>composer.json</c>.</summary>
    Slim,

    /// <summary>Swift: a SwiftPM <c>Package.swift</c> or <c>.swift</c> files.</summary>
    Swift,

    /// <summary>Vapor: a Swift package that depends on <c>vapor/vapor</c>.</summary>
    Vapor,

    /// <summary>Visual Basic: a <c>.vbproj</c> project.</summary>
    VisualBasic,
}

/// <summary>
/// One technology detected in a repository, with the file that proves it.
/// </summary>
/// <param name="Kind">The technology.</param>
/// <param name="DisplayName">A short label for the UI, for example <c>React</c>.</param>
/// <param name="Evidence">The file that revealed it, relative to the repository root, for example <c>web/package.json</c>.</param>
/// <param name="Version">The version, when the evidence states one.</param>
public sealed record StackComponent(StackKind Kind, string DisplayName, string Evidence, string? Version = null);

/// <summary>
/// Everything detected in a repository.
/// </summary>
/// <param name="Components">The detected technologies, one entry per <see cref="StackKind"/>, in a stable order.</param>
public sealed record StackProfile(IReadOnlyList<StackComponent> Components)
{
    /// <summary>An empty result.</summary>
    public static StackProfile Empty { get; } = new([]);

    /// <summary>Whether a technology was detected.</summary>
    /// <param name="kind">The technology to look for.</param>
    public bool Has(StackKind kind) => Components.Any(c => c.Kind == kind);

    /// <summary>Returns the component for a technology, or <see langword="null"/>.</summary>
    /// <param name="kind">The technology to look for.</param>
    public StackComponent? Get(StackKind kind) => Components.FirstOrDefault(c => c.Kind == kind);
}
