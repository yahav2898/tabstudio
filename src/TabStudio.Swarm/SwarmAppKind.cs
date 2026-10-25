namespace TabStudio.Swarm;

/// <summary>How the devices of a swarm run the app being built.</summary>
public enum SwarmAppKind
{
    /// <summary>An Avalonia app, shown full screen in each device's app preview.</summary>
    Avalonia,

    /// <summary>A console app, run on each device with its output streamed back to the tablet and its input sent from there.</summary>
    Console,

    /// <summary>A web app, served once by the tablet and opened in each device's web view through the swarm's tunnel.</summary>
    Web,
}
