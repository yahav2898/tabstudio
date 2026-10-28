namespace Microsoft.VisualBasic;

/// <summary>The exceptions of members that cannot work on the tablet, which name the member and say what to do instead.</summary>
internal static class Unsupported
{
    /// <summary>The exception for a member that is not supported.</summary>
    /// <param name="member">The member, such as <c>My.Computer.Registry</c>.</param>
    /// <param name="reason">Why, and what to use instead.</param>
    public static NotSupportedException Member(string member, string reason) =>
        new($"{member} is not supported by TabStudio's Visual Basic runtime: {reason}");
}
