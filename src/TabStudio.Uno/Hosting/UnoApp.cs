using System.Reflection;
using TabStudio.Compilation.Running;

namespace TabStudio.Uno.Hosting;

/// <summary>
/// One build of an Uno Platform or WinUI app running in the app preview, from <see cref="UnoRuntime.LaunchAsync"/>
/// until it is disposed, which closes its windows.
/// </summary>
public sealed class UnoApp : IAsyncDisposable
{
    private readonly UnoRuntime _runtime;
    private readonly LoadedProgram _program;
    private int _disposed;

    internal UnoApp(UnoRuntime runtime, LoadedProgram program)
    {
        _runtime = runtime;
        _program = program;
    }

    /// <summary>
    /// Raised, on Uno's UI thread, for an exception the app's code threw and did not handle, such as in a click handler.
    /// The app keeps running, as it would on Uno's own targets, unless the host stops it.
    /// </summary>
    public event EventHandler<UnhandledExceptionEventArgs>? UnhandledException;

    /// <summary>The app's assembly.</summary>
    public Assembly Assembly => _program.EntryAssembly;

    /// <summary>Whether the app has been closed.</summary>
    public bool IsClosed => Volatile.Read(ref _disposed) != 0;

    /// <summary>The texts the app shows, in the order of its visual tree.</summary>
    public async Task<IReadOnlyList<string>> GetTextsAsync() => await _runtime.Host.GetTexts().ConfigureAwait(false);

    /// <summary>Where an element named <paramref name="name"/> (its <c>x:Name</c> or automation id) is in the preview.</summary>
    /// <returns>Its bounds in device-independent pixels, or <see langword="null"/> when the app shows none.</returns>
    public async Task<UnoElementBounds?> FindElementAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return await _runtime.Host.FindElement(name).ConfigureAwait(false) is [var x, var y, var width, var height]
            ? new UnoElementBounds(x, y, width, height)
            : null;
    }

    /// <summary>The title of the app's newest window, such as a WinUI 3 window's <c>Title</c>.</summary>
    public Task<string?> GetTitleAsync() => _runtime.Host.GetTitle();

    /// <summary>Closes the app's windows and lets go of its assemblies.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _runtime.CloseAsync(this).ConfigureAwait(false);
        }
        finally
        {
            _program.Dispose();
        }
    }

    /// <summary>Reports an exception from the app's code.</summary>
    internal void OnUnhandled(Exception exception)
    {
        if (!IsClosed)
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(exception, isTerminating: false));
        }
    }
}

/// <summary>Where an element is in the app preview, in device-independent pixels.</summary>
/// <param name="X">Its left edge.</param>
/// <param name="Y">Its top edge.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public sealed record UnoElementBounds(double X, double Y, double Width, double Height)
{
    /// <summary>Its center, where a tap on it lands.</summary>
    public (double X, double Y) Center => (X + (Width / 2), Y + (Height / 2));
}
