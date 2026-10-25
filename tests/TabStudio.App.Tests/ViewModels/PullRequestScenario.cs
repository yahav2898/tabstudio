using TabStudio.App.Editing;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.PullRequests;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// The linked sample with two pull requests on <see cref="FakeGitHub"/>: #1, a colleague's change to the greeter that
/// the signed-in tester is asked to review, with a conversation on one of its lines; and #2, the tester's own draft.
/// </summary>
internal sealed class PullRequestScenario : IDisposable
{
    /// <summary>The file the first pull request changes.</summary>
    public const string GreeterPath = "src/Greetings/Greeter.cs";

    /// <summary>The first pull request's branch.</summary>
    public const string Branch = "feature/friendly-greeting";

    /// <summary>The second pull request's branch.</summary>
    public const string DocsBranch = "docs/tour";

    /// <summary>A colleague who comments.</summary>
    public const string Colleague = "monalisa";

    /// <summary>The first pull request's title.</summary>
    public const string Title = "Greet friends by name";

    /// <summary>The line of the greeter, after the change, that the conversation is on.</summary>
    public const int ThreadLine = 13;

    /// <summary>The line of the greeter, after the change, with the new greeting.</summary>
    public const int GreetingLine = 25;

    /// <summary>The line of the greeter, after the change, with the new farewell.</summary>
    public const int FarewellLine = 28;

    /// <param name="agentHttp">Where the workspace sends requests to Claude.</param>
    public PullRequestScenario(HttpClient? agentHttp = null) => Fixture = new GitWorkspaceFixture(agentHttp);

    public GitWorkspaceFixture Fixture { get; }

    public FakeGitHub GitHub => Fixture.GitHub;

    public WorkspaceViewModel Workspace => Fixture.Workspace;

    /// <summary>The fake repository's name.</summary>
    public static string Repository => GitWorkspaceFixture.Repository;

    /// <summary>The id of the comment that starts the conversation on the greeter.</summary>
    public long ThreadId { get; private set; }

    /// <summary>Opens the linked sample, then pushes the two branches and opens their pull requests.</summary>
    public async Task OpenAsync()
    {
        await Fixture.OpenLinkedSampleAsync();
        var client = GitHub.CreateClient();
        var main = GitHub.Head(Repository, "main");

        await client.CreateBranchAsync(FakeGitHub.Login, Repository, Branch, main);
        GitHub.Push(Repository, Branch, "Greet friends and say goodbye", new Dictionary<string, string?> { [GreeterPath] = Friendly(GitHub.Files(Repository, "main")[GreeterPath]) });
        GitHub.AddPullRequest(
            Repository,
            Title,
            Branch,
            body: "Calls people **friends** instead of developers, and adds a `Farewell` for the end of the program.\n\n- Shorter greeting\n- New farewell",
            reviewers: [FakeGitHub.Login]);
        ThreadId = GitHub.AddReviewComment(Repository, 1, GreeterPath, ThreadLine, "Should `friend` come from the settings, so a class can pick its own word?", author: Colleague);
        GitHub.AddReviewComment(Repository, 1, GreeterPath, ThreadLine, "Good idea. I'd keep it simple here and do it in a follow-up.", inReplyTo: ThreadId);
        GitHub.AddIssueComment(Repository, 1, "Tried it on my tablet: the greeting fits on one line now.", author: Colleague);
        GitHub.AddReview(Repository, 1, "APPROVED", "Looks good to me.", author: "hubot");

        await client.CreateBranchAsync(FakeGitHub.Login, Repository, DocsBranch, main);
        GitHub.Push(Repository, DocsBranch, "Describe the tour", new Dictionary<string, string?> { ["README.md"] = "# HelloTablet\n\nOpen Tour.ipynb for a tour of C# on a tablet.\n" });
        GitHub.AddPullRequest(Repository, "Describe the notebook tour", DocsBranch, author: FakeGitHub.Login, draft: true);
    }

    /// <summary>Opens a pull request's document and waits until it is read from GitHub.</summary>
    public async Task<PullRequestViewModel> OpenPullRequestAsync(int number)
    {
        var pullRequest = await Workspace.ShowPullRequestAsync(number);
        Assert.NotNull(pullRequest);
        await UiTestHelpers.WaitUntilAsync(() => pullRequest.IsLoaded && !pullRequest.IsLoading, because: "the pull request to load");
        return pullRequest;
    }

    /// <summary>Opens the diff of a pull request's file and waits for its tab.</summary>
    public async Task<FileReviewViewModel> OpenFileAsync(PullRequestViewModel pullRequest, string path)
    {
        await pullRequest.Files.Single(f => f.Path == path).OpenCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => Workspace.ActiveDocument?.Review?.Path == path, because: "the file's diff");
        return Workspace.ActiveDocument!.Review!;
    }

    /// <summary>The 1-based line of a file's diff tab that shows a line of the changed file.</summary>
    public int DiffLineOf(int newLine)
    {
        var lines = Workspace.ActiveDocument!.Comparison!.Diff.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].NewNumber == newLine && lines[i].Kind != DiffLineKind.Removed)
            {
                return i + 1;
            }
        }

        throw new InvalidOperationException($"Line {newLine} is not in the diff.");
    }

    public void Dispose() => Fixture.Dispose();

    /// <summary>The greeter as the first pull request changes it: friends, a shorter greeting and a farewell.</summary>
    private static string Friendly(string greeter)
    {
        var text = greeter.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\"developer\"", "\"friend\"", StringComparison.Ordinal)
            .Replace("This program was compiled and run on your tablet.", "Built and run on your tablet.", StringComparison.Ordinal);
        var end = text.TrimEnd().LastIndexOf('}');
        return text[..end] + "\n    public string Farewell() => $\"See you soon, {_name}!\";\n}\n";
    }
}
