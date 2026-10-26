namespace TabStudio.Compilation.Running;

/// <summary>How <see cref="WebAppHost"/> starts a web app.</summary>
public sealed record WebAppOptions
{
    /// <summary>The hosting environment, <c>Development</c> by default as when Visual Studio starts an app.</summary>
    public string Environment { get; init; } = "Development";

    /// <summary>More command-line arguments, such as a launch profile's <c>commandLineArgs</c>.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Environment variables scoped to the app run, including native service connections.</summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>();

    /// <summary>The port to listen on when it is free, so a restarted app keeps its address; any free port otherwise.</summary>
    public int? Port { get; init; }

    /// <summary>How long to wait for the server to accept connections.</summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromMinutes(1);
}
