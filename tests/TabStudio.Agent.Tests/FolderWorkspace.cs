using TabStudio.Core.Diagnostics;
using TabStudio.Testing;

namespace TabStudio.Agent.Tests;

/// <summary>A workspace over a temporary folder, recording what the agent asks of it.</summary>
internal sealed class FolderWorkspace(TempDirectory folder) : IAgentWorkspace
{
    public string RootDirectory => folder.Path;

    /// <summary>What get_diagnostics returns.</summary>
    public List<BuildDiagnostic> Diagnostics { get; } = [];

    /// <summary>The pending files the last diagnostics or test request saw.</summary>
    public IReadOnlyDictionary<string, string?>? LastOverlay { get; private set; }

    /// <summary>Changes written after approval.</summary>
    public List<(string Path, string? Text)> Applied { get; } = [];

    public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(folder.Path, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)]);

    public Task<string?> ReadFileAsync(string fullPath, CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(fullPath) ? File.ReadAllText(fullPath) : null);

    public Task<IReadOnlyList<BuildDiagnostic>> GetDiagnosticsAsync(IReadOnlyDictionary<string, string?> pendingFiles, CancellationToken cancellationToken)
    {
        LastOverlay = pendingFiles;
        return Task.FromResult<IReadOnlyList<BuildDiagnostic>>(Diagnostics);
    }

    public Task<string> RunTestsAsync(string? filter, IReadOnlyDictionary<string, string?> pendingFiles, CancellationToken cancellationToken)
    {
        LastOverlay = pendingFiles;
        return Task.FromResult($"3 passed, 0 failed{(filter is null ? string.Empty : $" ({filter})")}");
    }

    public Task ApplyChangeAsync(string fullPath, string? text, CancellationToken cancellationToken)
    {
        Applied.Add((fullPath, text));
        if (text is null)
        {
            File.Delete(fullPath);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, text);
        }

        return Task.CompletedTask;
    }
}
