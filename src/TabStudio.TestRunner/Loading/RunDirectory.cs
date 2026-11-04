namespace TabStudio.TestRunner.Loading;

/// <summary>
/// Makes a test project's output folder the current directory and <see cref="AppContext.BaseDirectory"/> while its
/// tests are found and run, as <c>dotnet test</c> runs them from there: scriban's tests find their files from
/// <see cref="AppContext.BaseDirectory"/>, and YamlDotNet's look for theirs from the current directory up.
/// </summary>
/// <remarks>
/// Both are the process's, and tests run in TabStudio's own process: they are set while any session that has an
/// output folder loads or runs and put back when the last one ends. When two such runs overlap, the later one's
/// folder holds until both end.
/// </remarks>
internal static class RunDirectory
{
    private const string BaseDirectoryKey = "APP_CONTEXT_BASE_DIRECTORY";

    private static readonly Lock Gate = new();
    private static int s_depth;
    private static string? s_savedCurrent;
    private static object? s_savedBase;

    /// <summary>Enters a folder until the result is disposed; does nothing for <see langword="null"/>.</summary>
    /// <param name="folder">The output folder.</param>
    public static IDisposable Enter(string? folder)
    {
        if (folder is null || !Directory.Exists(folder))
        {
            return Scope.None;
        }

        lock (Gate)
        {
            if (s_depth++ == 0)
            {
                s_savedBase = AppContext.GetData(BaseDirectoryKey);
                try
                {
                    s_savedCurrent = Directory.GetCurrentDirectory();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    s_savedCurrent = null;
                }
            }

            // The scope is counted from here, so a folder that cannot be entered still leaves, and puts back, the rest.
            try
            {
                Directory.SetCurrentDirectory(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Removed since it was checked: the tests run from where the process is.
            }

            AppContext.SetData(BaseDirectoryKey, Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar);
        }

        return new Scope();
    }

    private static void Leave()
    {
        lock (Gate)
        {
            if (--s_depth > 0)
            {
                return;
            }

            AppContext.SetData(BaseDirectoryKey, s_savedBase);
            if (s_savedCurrent is { } current)
            {
                try
                {
                    Directory.SetCurrentDirectory(current);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The folder TabStudio started in is gone; the tests' folder stays current.
                }
            }
        }
    }

    private sealed class Scope : IDisposable
    {
        public static readonly IDisposable None = new NoScope();
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Leave();
            }
        }

        private sealed class NoScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
