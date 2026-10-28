using Avalonia;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.App.Previews;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// An Avalonia app built for a newer Avalonia than TabStudio's says so in one line when it cannot start, rather than
/// leaving a XAML type that does not resolve as the only clue (Dock's samples, built for Avalonia 12).
/// </summary>
public sealed class NewerAvaloniaTests
{
    [Fact]
    public void An_app_built_for_a_newer_Avalonia_major_version_is_named_as_such()
    {
        using var temp = new TempDirectory();
        var host = typeof(AvaloniaObject).Assembly.GetName().Version!;
        var newer = AvaloniaBase(temp, "newer", $"{host.Major + 1}.1.4.0");

        var note = AvaloniaAppHost.NewerAvaloniaNote([temp.Resolve("newer/Other.dll"), newer]);

        Assert.NotNull(note);
        Assert.Contains($"built for Avalonia {host.Major + 1}.1", note, StringComparison.Ordinal);
        Assert.Contains($"its own Avalonia {host.Major}.{host.Minor}", note, StringComparison.Ordinal);
    }

    [Fact]
    public void An_app_built_for_TabStudio_s_Avalonia_or_an_older_one_has_nothing_to_explain()
    {
        using var temp = new TempDirectory();
        var host = typeof(AvaloniaObject).Assembly.GetName().Version!;

        Assert.Null(AvaloniaAppHost.NewerAvaloniaNote([AvaloniaBase(temp, "same", $"{host.Major}.{host.Minor}.0.0")]));
        Assert.Null(AvaloniaAppHost.NewerAvaloniaNote([AvaloniaBase(temp, "older", $"{Math.Max(host.Major - 1, 0)}.0.0.0")]));
        Assert.Null(AvaloniaAppHost.NewerAvaloniaNote([]));
    }

    /// <summary>An assembly named Avalonia.Base with a version, as an app's Avalonia package ships it.</summary>
    private static string AvaloniaBase(TempDirectory temp, string folder, string version)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "Avalonia.Base",
            [CSharpSyntaxTree.ParseText($"[assembly: System.Reflection.AssemblyVersion(\"{version}\")]")],
            platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = temp.Resolve(Path.Combine(folder, "Avalonia.Base.dll"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return path;
    }
}
