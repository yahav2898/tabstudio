using System.Globalization;
using System.Text;

namespace TabStudio.SourceControl.Git;

/// <summary>Who made a commit and when, as a commit object records it.</summary>
/// <param name="Name">The person's name.</param>
/// <param name="Email">Their email address.</param>
/// <param name="When">The time, with its offset from UTC.</param>
internal sealed record CommitPerson(string Name, string Email, DateTimeOffset When)
{
    /// <summary>The form a commit object writes: <c>Name &lt;email&gt; seconds +hhmm</c>.</summary>
    public string Format()
    {
        var offset = When.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var absolute = offset.Duration();
        return string.Create(CultureInfo.InvariantCulture, $"{Clean(Name)} <{Clean(Email)}> {When.ToUnixTimeSeconds()} {sign}{absolute.Hours:00}{absolute.Minutes:00}");
    }

    /// <summary>Reads the form a commit object writes; <see langword="null"/> when it is not one.</summary>
    public static CommitPerson? Parse(string text)
    {
        var open = text.IndexOf('<', StringComparison.Ordinal);
        var close = text.IndexOf('>', open + 1);
        if (open < 0 || close < 0)
        {
            return null;
        }

        var name = text[..open].Trim();
        var email = text[(open + 1)..close];
        var rest = text[(close + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var when = DateTimeOffset.UnixEpoch;
        if (rest.Length >= 1 && long.TryParse(rest[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            var offset = TimeSpan.Zero;
            if (rest.Length >= 2 && rest[1].Length == 5 && int.TryParse(rest[1].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var hhmm))
            {
                offset = new TimeSpan(hhmm / 100, hhmm % 100, 0);
                if (rest[1][0] == '-')
                {
                    offset = -offset;
                }
            }

            try
            {
                when = DateTimeOffset.FromUnixTimeSeconds(seconds).ToOffset(offset);
            }
            catch (ArgumentOutOfRangeException)
            {
                when = DateTimeOffset.UnixEpoch;
            }
        }

        return new CommitPerson(name, email, when);
    }

    private static string Clean(string text) => text.Replace("<", string.Empty, StringComparison.Ordinal).Replace(">", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
}

/// <summary>A commit object's fields.</summary>
/// <param name="Tree">The root tree.</param>
/// <param name="Parents">The parent commits.</param>
/// <param name="Author">Who wrote the change.</param>
/// <param name="Committer">Who committed it.</param>
/// <param name="Message">The message.</param>
internal sealed record CommitData(string Tree, IReadOnlyList<string> Parents, CommitPerson? Author, CommitPerson? Committer, string Message);

/// <summary>Reads and writes Git commit objects.</summary>
internal static class CommitCodec
{
    /// <summary>Reads a commit object.</summary>
    public static CommitData Parse(ReadOnlySpan<byte> content)
    {
        var text = Encoding.UTF8.GetString(content);
        var split = text.IndexOf("\n\n", StringComparison.Ordinal);
        var header = split < 0 ? text : text[..split];
        var message = split < 0 ? string.Empty : text[(split + 2)..];
        string? tree = null;
        var parents = new List<string>();
        CommitPerson? author = null;
        CommitPerson? committer = null;
        foreach (var line in header.Split('\n'))
        {
            if (line.StartsWith("tree ", StringComparison.Ordinal))
            {
                tree = line[5..].Trim();
            }
            else if (line.StartsWith("parent ", StringComparison.Ordinal))
            {
                parents.Add(line[7..].Trim());
            }
            else if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                author = CommitPerson.Parse(line[7..]);
            }
            else if (line.StartsWith("committer ", StringComparison.Ordinal))
            {
                committer = CommitPerson.Parse(line[10..]);
            }
        }

        return new CommitData(tree ?? throw new InvalidDataException("A commit object has no tree."), parents, author, committer, message);
    }

    /// <summary>Writes a commit object.</summary>
    public static byte[] Write(CommitData commit)
    {
        var text = new StringBuilder();
        text.Append("tree ").Append(commit.Tree).Append('\n');
        foreach (var parent in commit.Parents)
        {
            text.Append("parent ").Append(parent).Append('\n');
        }

        if (commit.Author is { } author)
        {
            text.Append("author ").Append(author.Format()).Append('\n');
        }

        if ((commit.Committer ?? commit.Author) is { } committer)
        {
            text.Append("committer ").Append(committer.Format()).Append('\n');
        }

        text.Append('\n').Append(commit.Message);
        if (!commit.Message.EndsWith('\n'))
        {
            text.Append('\n');
        }

        return Encoding.UTF8.GetBytes(text.ToString());
    }
}
