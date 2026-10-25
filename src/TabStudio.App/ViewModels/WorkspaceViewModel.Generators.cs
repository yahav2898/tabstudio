using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// .NET's own source generators, such as those behind <c>[GeneratedRegex]</c> and <c>[JsonSerializable]</c>. They
/// come with .NET's targeting pack, which is downloaded only once code that needs them shows up, and from then on
/// every C# project has them. The generators of NuGet packages come with each project's restore.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The attributes that ask .NET's generators for code.</summary>
    private static readonly string[] FrameworkGeneratorMarkers = ["GeneratedRegex", "JsonSerializable", "LibraryImport", "GeneratedComInterface"];

    /// <summary>How many files are looked through for those attributes when a workspace opens.</summary>
    private const int GeneratorScanLimit = 4000;

    private bool _frameworkGeneratorsRequested;

    /// <summary>Adds .NET's generators, in the background, when the workspace has code that uses them.</summary>
    private void StartFrameworkGenerators(Workspace workspace)
    {
        if (_frameworkGeneratorsRequested || workspace.RootDirectory is not { } root)
        {
            return;
        }

        var token = _background?.Token ?? CancellationToken.None;
        var previous = _restore;
        _restore = AddFrameworkGeneratorsAsync(previous, () => UsesFrameworkGenerators(root, token), token);
    }

    /// <summary>
    /// A saved file that starts using .NET's generators gets them, and a saved file that generators read, such as an
    /// Avalonia <c>.axaml</c> file, goes to the language service so the generated code follows it.
    /// </summary>
    private void OnDocumentSavedForGenerators(DocumentViewModel document)
    {
        if (!document.FileType.IsCSharp && document.Notebook is null)
        {
            _language?.UpdateText(document.FilePath, document.Document.Text);
        }

        if (!_frameworkGeneratorsRequested && document.FileType.IsCSharp && ContainsGeneratorMarker(document.Document.Text))
        {
            var token = _background?.Token ?? CancellationToken.None;
            _restore = AddFrameworkGeneratorsAsync(_restore, () => true, token);
        }
    }

    private async Task AddFrameworkGeneratorsAsync(Task previous, Func<bool> needed, CancellationToken cancellationToken)
    {
        await previous;
        if (_frameworkGeneratorsRequested || _language is not { } language || !await Task.Run(needed, cancellationToken))
        {
            return;
        }

        _frameworkGeneratorsRequested = true;
        try
        {
            PackagesText = "Getting .NET's source generators";
            var problems = await language.AddFrameworkGeneratorsAsync(cancellationToken);
            foreach (var problem in problems.Where(d => d.Severity >= DiagnosticSeverityLevel.Warning))
            {
                Output.WriteLine($"{problem.Id}: {problem.Message}", OutputKind.Failure);
            }

            // Ask again next time a file is saved, if the generators could not be had now.
            _frameworkGeneratorsRequested = problems.All(d => d.Severity != DiagnosticSeverityLevel.Error);
            foreach (var document in Documents)
            {
                _ = document.AnalyzeAgainAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        finally
        {
            PackagesText = null;
        }
    }

    private static bool UsesFrameworkGenerators(string root, CancellationToken cancellationToken)
    {
        try
        {
            var files = Directory.EnumerateFiles(root, "*.cs", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                .Where(path => !IsBuildOutput(root, path))
                .Take(GeneratorScanLimit);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (new FileInfo(file).Length < 512 * 1024 && ContainsGeneratorMarker(File.ReadAllText(file)))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be read has no code to generate for.
        }

        return false;
    }

    private static bool ContainsGeneratorMarker(string text) =>
        FrameworkGeneratorMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal));

    private static bool IsBuildOutput(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".git" or "node_modules");
    }
}
