using System.Globalization;
using System.Text;
using TabStudio.Agent;
using TabStudio.App.Services.Link;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.SourceControl;
using TabStudio.TestRunner;

namespace TabStudio.App.Services.Agent;

/// <summary>What the agent needs from the open workspace; the workspace view model implements it.</summary>
internal interface IAgentWorkspaceHost
{
    /// <summary>The open workspace.</summary>
    Workspace? Workspace { get; }

    /// <summary>The language service, once the workspace has loaded.</summary>
    ILanguageService? Language { get; }

    /// <summary>The text of an open document, including unsaved edits, or null when it is not open. UI thread only.</summary>
    string? GetOpenDocumentText(string fullPath);

    /// <summary>Writes an approved change into the open editor or to disk. UI thread only.</summary>
    Task ApplyAgentChangeAsync(string fullPath, string? text);
}

/// <summary>
/// The agent's view of the IDE: files as the editors show them, the Roslyn workspace for compiling its
/// proposals, the test runner, and the editors for applying approved changes.
/// </summary>
internal sealed class IdeAgentWorkspace(IAgentWorkspaceHost host, IUiDispatcher ui) : IAgentWorkspace
{
    private const long MaxFileLength = 2 * 1024 * 1024;

    public string RootDirectory => host.Workspace?.RootDirectory ?? throw new InvalidOperationException("No workspace is open.");

    public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken) =>
        Task.Run(() => WorkingTree.EnumerateFiles(RootDirectory), cancellationToken);

    public async Task<string?> ReadFileAsync(string fullPath, CancellationToken cancellationToken)
    {
        string? open = null;
        await ui.InvokeAsync(() => open = host.GetOpenDocumentText(fullPath));
        if (open is not null)
        {
            return open;
        }

        return await Task.Run(() =>
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length > MaxFileLength)
            {
                return null;
            }

            var bytes = File.ReadAllBytes(fullPath);
            return bytes.AsSpan(0, Math.Min(bytes.Length, 8000)).Contains((byte)0) ? null : Encoding.UTF8.GetString(bytes);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<BuildDiagnostic>> GetDiagnosticsAsync(IReadOnlyDictionary<string, string?> pendingFiles, CancellationToken cancellationToken) =>
        host.Language switch
        {
            // With TabStudio Link, wherever the workspace is: the linked host, or this device.
            LinkedLanguageService linked => linked.GetSnapshotDiagnosticsAsync(pendingFiles, cancellationToken),
            LocalLanguageService local => local.Host.CreateSnapshot(pendingFiles).GetDiagnosticsAsync(cancellationToken),
            _ => Task.FromResult<IReadOnlyList<BuildDiagnostic>>([]),
        };

    public async Task<string> RunTestsAsync(string? filter, IReadOnlyDictionary<string, string?> pendingFiles, CancellationToken cancellationToken)
    {
        var builds = host.Language switch
        {
            LinkedLanguageService linked => await linked.BuildSnapshotTestProjectsAsync(pendingFiles, cancellationToken),
            LocalLanguageService local => await SnapshotBuilds.BuildTestProjectsAsync(local.Host, pendingFiles, cancellationToken),
            _ => null,
        };
        if (builds is null || host.Language is not { } language)
        {
            return "The workspace is still loading.";
        }

        if (builds.Count == 0)
        {
            return "The solution has no test projects.";
        }

        var report = new StringBuilder();
        int passed = 0, failed = 0, skipped = 0;
        foreach (var (projectFilePath, build) in builds)
        {
            var project = host.Workspace?.Projects.FirstOrDefault(p => string.Equals(p.FilePath, projectFilePath, StringComparison.Ordinal));
            if (project is null)
            {
                continue;
            }

            if (!build.Success)
            {
                report.Append(CultureInfo.InvariantCulture, $"{project.Name} does not build:\n");
                foreach (var error in build.Diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error).Take(20))
                {
                    report.Append(CultureInfo.InvariantCulture, $"  {error}\n");
                }

                continue;
            }

            using var session = await TestSession.LoadAsync([build.EntryAssembly!, .. build.Dependencies], language.GetPackageRuntimeAssemblies(project.FilePath), cancellationToken: cancellationToken);
            var summary = filter is null
                ? await session.RunAllAsync(cancellationToken)
                : await session.RunSelectedAsync([filter], cancellationToken);
            passed += summary.Passed;
            failed += summary.Failed + summary.Errored;
            skipped += summary.Skipped;

            foreach (var result in summary.Results.Where(r => r.Outcome is TestOutcome.Failed or TestOutcome.Errored).Take(20))
            {
                var location = result.FailureLocation is { } at ? $" at {Path.GetRelativePath(RootDirectory, at.FilePath)}:{at.Line.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
                report.Append(CultureInfo.InvariantCulture, $"FAILED {result.Test.Id}{location}\n  {result.Message?.ReplaceLineEndings("\n  ")}\n");
            }
        }

        return $"{passed} passed, {failed} failed, {skipped} skipped\n{report}".TrimEnd();
    }

    public Task ApplyChangeAsync(string fullPath, string? text, CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ui.Post(async () =>
        {
            try
            {
                await host.ApplyAgentChangeAsync(fullPath, text);
                done.SetResult();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                done.SetException(ex);
            }
        });
        return done.Task;
    }
}
