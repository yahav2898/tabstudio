using System.Net;
using System.Text;

namespace TabStudio.SourceControl.GitHub;

/// <summary>Reading workflow files, to know whether a workflow can be run by hand and which inputs it asks for.</summary>
public sealed partial class GitHubClient
{
    /// <summary>Reads a text file of a repository through the contents API.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="path">The file, relative to the repository's root, with <c>/</c> separators.</param>
    /// <param name="reference">The branch, tag or commit; the default branch when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<string> GetFileTextAsync(string owner, string repository, string path, string? reference = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var address = $"{Repo(owner, repository)}/contents/{EscapePath(path)}";
        if (!string.IsNullOrEmpty(reference))
        {
            address += $"?ref={Uri.EscapeDataString(reference)}";
        }

        var dto = await GetCachedAsync(address, GitHubActionsJsonContext.Default.ContentDto, cancellationToken).ConfigureAwait(false);
        if (dto.Type != "file" || dto.Encoding != "base64" || dto.Content is null)
        {
            throw new GitHubApiException(HttpStatusCode.UnprocessableEntity, $"{path} is not a file GitHub can send as text.");
        }

        var bytes = Convert.FromBase64String(dto.Content.Replace("\n", string.Empty, StringComparison.Ordinal));
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes).TrimStart('﻿');
    }

    /// <summary>Reads what a workflow's file says about running it by hand: its <c>workflow_dispatch</c> trigger and inputs.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="workflow">The workflow.</param>
    /// <param name="reference">The branch or tag whose version of the file to read; the default branch when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="GitHubApiException">The file could not be read, or is not a valid workflow.</exception>
    public async Task<WorkflowDefinition> GetWorkflowDefinitionAsync(string owner, string repository, GitHubWorkflow workflow, string? reference = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        var text = await GetFileTextAsync(owner, repository, workflow.Path, reference, cancellationToken).ConfigureAwait(false);
        return WorkflowFile.TryParse(text, out var definition, out var error)
            ? definition
            : throw new GitHubApiException(HttpStatusCode.UnprocessableEntity, error);
    }
}
