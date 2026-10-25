using CommunityToolkit.Mvvm.Input;
using TabStudio.WindowsBridge.Hosting;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.ViewModels.WindowsBridge;

/// <summary>One kind of project a PC can or cannot build, with what to install when it cannot.</summary>
/// <param name="Name">The kind, such as <c>WPF and Windows Forms</c>.</param>
/// <param name="IsAvailable">Whether the PC can build it.</param>
/// <param name="Hint">What to install on the PC for it, when it cannot.</param>
public sealed record BridgeCapabilityRow(string Name, bool IsAvailable, string Hint)
{
    /// <summary>The row's detail: ready, or what to install.</summary>
    public string Detail => IsAvailable ? "Ready" : Hint;

    /// <summary>The rows of a PC's capabilities, in the order people look for them.</summary>
    /// <param name="capabilities">What the PC can build and debug.</param>
    public static IReadOnlyList<BridgeCapabilityRow> For(BridgeCapabilities capabilities) =>
    [
        new("WPF and Windows Forms", capabilities.HasFlag(BridgeCapabilities.WindowsDesktop), "Install the .NET SDK from dot.net"),
        new("WinUI 3", capabilities.HasFlag(BridgeCapabilities.WinUI), "Install the Windows SDK"),
        new(".NET Framework", capabilities.HasFlag(BridgeCapabilities.NetFramework), "Install Visual Studio's .NET desktop build tools"),
        new("Visual C++", capabilities.HasFlag(BridgeCapabilities.Cpp), "Install Visual Studio with Desktop development with C++"),
        new("Debugging .NET apps", capabilities.HasFlag(BridgeCapabilities.DebugDotNet), "Install netcoredbg in the bridge's window on the PC"),
    ];
}

/// <summary>An SDK or tool the bridge found on its PC, as its window lists them.</summary>
/// <param name="Toolchain">The tool.</param>
public sealed record BridgeToolchainRow(BridgeToolchain Toolchain)
{
    /// <summary>Its name, such as <c>Visual Studio Community 2022</c>.</summary>
    public string Name => Toolchain.Name;

    /// <summary>What it is for, such as <c>Builds C++, .NET Framework and WinUI</c>.</summary>
    public string Detail => Toolchain.Kind switch
    {
        BridgeToolchainKind.DotNetSdk => "dotnet build: WPF, Windows Forms and every SDK-style project",
        BridgeToolchainKind.VisualStudio => $"Version {Toolchain.Version}",
        BridgeToolchainKind.MSBuild => "Builds C++, .NET Framework and WinUI projects",
        BridgeToolchainKind.Msvc => "The C++ compiler and linker",
        BridgeToolchainKind.WindowsSdk => "Windows' headers and libraries",
        BridgeToolchainKind.WindowsAppSdk => "Runs WinUI 3 apps",
        BridgeToolchainKind.NetFramework => "Builds .NET Framework apps",
        _ => "Debugs .NET apps for the tablet",
    };
}

/// <summary>A program running on the PC for a tablet, in the bridge's window.</summary>
/// <param name="Tablet">The tablet it runs for.</param>
/// <param name="App">The program.</param>
/// <param name="Stop">Stops it.</param>
public sealed record BridgeRunningAppRow(string Tablet, BridgeAppInfo App, IRelayCommand Stop)
{
    /// <summary>Its name.</summary>
    public string Name => App.Name;

    /// <summary>For whom and how it runs, such as <c>For Yahav's tablet · process 4120 · debugging</c>.</summary>
    public string Detail => $"For {Tablet} · process {App.ProcessId}{(App.IsDebugging ? " · debugging" : string.Empty)}";
}

/// <summary>A choice of how the tablet's input is played into programs on the PC.</summary>
/// <param name="Policy">The policy.</param>
/// <param name="Name">What the choice says.</param>
public sealed record BridgeInputPolicyOption(BridgeInputPolicy Policy, string Name)
{
    /// <summary>The choices, in order.</summary>
    public static IReadOnlyList<BridgeInputPolicyOption> All { get; } =
    [
        new(BridgeInputPolicy.Automatic, "Use the mouse and keyboard while nobody is at the PC"),
        new(BridgeInputPolicy.Foreground, "Always use the mouse and keyboard"),
        new(BridgeInputPolicy.Background, "Never take the mouse, keyboard or focus"),
    ];

    /// <inheritdoc />
    public override string ToString() => Name;
}
