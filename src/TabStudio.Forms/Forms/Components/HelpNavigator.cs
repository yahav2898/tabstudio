namespace System.Windows.Forms;

/// <summary>Where in a help file help opens, with the values Windows Forms gives them.</summary>
#pragma warning disable CA1027, CA1008 // The values are Windows Forms' own, kept for code that stores them.
public enum HelpNavigator
{
    /// <summary>A topic, by its URL.</summary>
    Topic = -2147483647,

    /// <summary>The table of contents.</summary>
    TableOfContents = -2147483646,

    /// <summary>The index.</summary>
    Index = -2147483645,

    /// <summary>The search page.</summary>
    Find = -2147483644,

    /// <summary>The index entry for the first letter of the keyword.</summary>
    AssociateIndex = -2147483643,

    /// <summary>The index entry of the keyword.</summary>
    KeywordIndex = -2147483642,

    /// <summary>A topic, by its number.</summary>
    TopicId = -2147483641,
}
#pragma warning restore CA1027, CA1008
