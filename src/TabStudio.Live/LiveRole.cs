namespace TabStudio.Live;

/// <summary>What a participant may do in a session.</summary>
public enum LiveRole
{
    /// <summary>The device that shares its workspace: it runs, debugs and decides what everyone else may do.</summary>
    Host,

    /// <summary>A guest that edits the shared documents.</summary>
    Editor,

    /// <summary>A guest that watches: it sees everything, follows, and raises a hand, but edits nothing.</summary>
    Viewer,
}
