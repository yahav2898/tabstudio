namespace TabStudio.SourceControl;

/// <summary>Why a source control operation could not go ahead.</summary>
public enum SyncFailure
{
    /// <summary>Something else went wrong; see the message.</summary>
    Other,

    /// <summary>The folder is not linked to a GitHub repository.</summary>
    NotLinked,

    /// <summary>The operation needs a GitHub account.</summary>
    NotSignedIn,

    /// <summary>There are no changes to commit.</summary>
    NothingToCommit,

    /// <summary>GitHub has commits the folder does not have yet; pull first.</summary>
    BehindRemote,

    /// <summary>Files still hold conflict markers.</summary>
    UnresolvedConflicts,

    /// <summary>The operation would overwrite uncommitted changes.</summary>
    UncommittedChanges,

    /// <summary>The folder holds a tag or commit, not a branch.</summary>
    Detached,
}

/// <summary>A source control operation could not go ahead. The message is written for the user.</summary>
public sealed class SyncException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SyncException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, for the user.</param>
    public SyncException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, for the user.</param>
    /// <param name="innerException">The cause.</param>
    public SyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a known reason.</summary>
    /// <param name="failure">Why the operation could not go ahead.</param>
    /// <param name="message">What went wrong, for the user.</param>
    public SyncException(SyncFailure failure, string message)
        : base(message) => Failure = failure;

    /// <summary>Why the operation could not go ahead.</summary>
    public SyncFailure Failure { get; }
}
