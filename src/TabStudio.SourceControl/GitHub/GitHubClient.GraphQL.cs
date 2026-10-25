using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// The few review operations that GitHub offers only through GraphQL: whether threads are resolved and resolving
/// them, adding to a pending review, and marking a draft ready for review. They need a signed-in account.
/// </summary>
public sealed partial class GitHubClient
{
    private const string ThreadStatesQuery = """
        query($owner: String!, $name: String!, $number: Int!, $after: String) {
          repository(owner: $owner, name: $name) {
            pullRequest(number: $number) {
              reviewThreads(first: 100, after: $after) {
                pageInfo { hasNextPage endCursor }
                nodes { id isResolved viewerCanResolve viewerCanUnresolve comments(first: 1) { nodes { databaseId } } }
              }
            }
          }
        }
        """;

    private const string ResolveThreadMutation = "mutation($id: ID!) { resolveReviewThread(input: {threadId: $id}) { thread { id isResolved } } }";

    private const string UnresolveThreadMutation = "mutation($id: ID!) { unresolveReviewThread(input: {threadId: $id}) { thread { id isResolved } } }";

    private const string ReadyForReviewMutation = "mutation($id: ID!) { markPullRequestReadyForReview(input: {pullRequestId: $id}) { pullRequest { isDraft } } }";

    private const string AddThreadMutation = """
        mutation($review: ID!, $path: String!, $body: String!, $line: Int!, $side: DiffSide!, $startLine: Int, $startSide: DiffSide) {
          addPullRequestReviewThread(input: {pullRequestReviewId: $review, path: $path, body: $body, line: $line, side: $side, startLine: $startLine, startSide: $startSide}) {
            thread { id }
          }
        }
        """;

    private const string AddReplyMutation = """
        mutation($review: ID!, $replyTo: ID!, $body: String!) {
          addPullRequestReviewComment(input: {pullRequestReviewId: $review, inReplyTo: $replyTo, body: $body}) { comment { id } }
        }
        """;

    /// <summary>Reads which review threads of a pull request are resolved.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request's number.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>Each thread's state, by the id of its first comment.</returns>
    public async Task<IReadOnlyDictionary<long, ReviewThreadState>> GetReviewThreadStatesAsync(string owner, string repository, int number, CancellationToken cancellationToken = default)
    {
        var states = new Dictionary<long, ReviewThreadState>();
        string? after = null;
        for (var page = 0; page < 10; page++)
        {
            var variables = new JsonObject { ["owner"] = owner, ["name"] = repository, ["number"] = number, ["after"] = after };
            var data = await SendGraphQLAsync(ThreadStatesQuery, variables, cancellationToken).ConfigureAwait(false);
            var threads = data["repository"]?["pullRequest"]?["reviewThreads"];
            foreach (var thread in threads?["nodes"]?.AsArray() ?? new JsonArray())
            {
                if (thread?["comments"]?["nodes"]?.AsArray() is [{ } first, ..] && first["databaseId"]?.GetValue<long>() is { } rootId)
                {
                    var resolved = thread["isResolved"]?.GetValue<bool>() ?? false;
                    var canChange = (resolved ? thread["viewerCanUnresolve"] : thread["viewerCanResolve"])?.GetValue<bool>() ?? false;
                    states[rootId] = new ReviewThreadState(thread["id"]!.GetValue<string>(), resolved, canChange);
                }
            }

            if (threads?["pageInfo"]?["hasNextPage"]?.GetValue<bool>() != true)
            {
                break;
            }

            after = threads["pageInfo"]?["endCursor"]?.GetValue<string>();
        }

        return states;
    }

    /// <summary>Marks a review thread resolved, or opens it again.</summary>
    /// <param name="threadId">The thread's GraphQL id.</param>
    /// <param name="resolved">Whether it is resolved.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task SetReviewThreadResolvedAsync(string threadId, bool resolved, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(threadId);
        return SendGraphQLAsync(resolved ? ResolveThreadMutation : UnresolveThreadMutation, new JsonObject { ["id"] = threadId }, cancellationToken);
    }

    /// <summary>Takes a pull request out of draft, so it can be reviewed and merged.</summary>
    /// <param name="pullRequestNodeId">The pull request's GraphQL id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task MarkReadyForReviewAsync(string pullRequestNodeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pullRequestNodeId);
        return SendGraphQLAsync(ReadyForReviewMutation, new JsonObject { ["id"] = pullRequestNodeId }, cancellationToken);
    }

    /// <summary>Adds a comment on a line to a pending review. The REST API can only create a review with comments, not add to one.</summary>
    /// <param name="reviewNodeId">The pending review's GraphQL id.</param>
    /// <param name="comment">The comment.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task AddPendingReviewCommentAsync(string reviewNodeId, NewReviewComment comment, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reviewNodeId);
        ArgumentNullException.ThrowIfNull(comment);
        var variables = new JsonObject
        {
            ["review"] = reviewNodeId,
            ["path"] = comment.Path,
            ["body"] = comment.Body,
            ["line"] = comment.Line,
            ["side"] = SideParameter(comment.Side),
            ["startLine"] = comment.StartLine,
            ["startSide"] = comment.StartLine is null ? null : SideParameter(comment.StartSide ?? comment.Side),
        };
        return SendGraphQLAsync(AddThreadMutation, variables, cancellationToken);
    }

    /// <summary>Adds a reply to a thread to a pending review, where it waits with the review's other comments.</summary>
    /// <param name="reviewNodeId">The pending review's GraphQL id.</param>
    /// <param name="commentNodeId">The GraphQL id of the comment replied to.</param>
    /// <param name="body">The text, in Markdown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task AddPendingReplyAsync(string reviewNodeId, string commentNodeId, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reviewNodeId);
        ArgumentException.ThrowIfNullOrEmpty(commentNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        return SendGraphQLAsync(AddReplyMutation, new JsonObject { ["review"] = reviewNodeId, ["replyTo"] = commentNodeId, ["body"] = body }, cancellationToken);
    }

    /// <summary>Sends a GraphQL query or mutation and returns its data; GraphQL's errors come back as exceptions.</summary>
    private async Task<JsonNode> SendGraphQLAsync(string query, JsonObject variables, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["query"] = query, ["variables"] = variables };
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var request = CreateRequest(HttpMethod.Post, GraphQLPath, content);
        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        JsonNode? answer;
        try
        {
            answer = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (JsonException ex)
        {
            throw new GitHubApiException("GitHub sent an answer TabStudio could not read.", ex);
        }

        if (answer?["errors"] is JsonArray { Count: > 0 } errors)
        {
            var message = errors[0]?["message"]?.GetValue<string>() ?? "GitHub refused the request.";
            var status = errors[0]?["type"]?.GetValue<string>() switch
            {
                "FORBIDDEN" => HttpStatusCode.Forbidden,
                "NOT_FOUND" => HttpStatusCode.NotFound,
                _ => HttpStatusCode.UnprocessableEntity,
            };
            throw new GitHubApiException(status, message);
        }

        return answer?["data"] ?? throw new GitHubApiException("GitHub sent an empty answer.");
    }

    /// <summary>GraphQL's address: <c>/graphql</c> on github.com, <c>/api/graphql</c> beside a GitHub Enterprise server's <c>/api/v3</c>.</summary>
    private string GraphQLPath => _apiAddress.AbsolutePath.EndsWith("/v3/", StringComparison.Ordinal) ? "../graphql" : "graphql";
}
