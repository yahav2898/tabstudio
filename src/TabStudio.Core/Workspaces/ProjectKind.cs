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
