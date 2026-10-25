using AvaloniaEdit.Document;
using TabStudio.App.Services;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Every breakpoint in the workspace: set and cleared with F9 or a tap in the editor's margin, saved with the session,
/// following edits in open documents, and handed to the debug session while one runs.
/// </summary>
/// <remarks>Use it on the UI thread.</remarks>
public sealed class BreakpointsViewModel
{
    private readonly List<BreakpointViewModel> _breakpoints = [];
    private readonly Dictionary<TextDocument, DocumentViewModel> _documents = [];
    private DebugSession? _session;

    /// <summary>Raised after breakpoints were added, removed, moved or changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Every breakpoint, in the order they were set.</summary>
    public IReadOnlyList<BreakpointViewModel> All => _breakpoints;

    /// <summary>The breakpoints of one file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public IReadOnlyList<BreakpointViewModel> InFile(string filePath) =>
        _breakpoints.Where(b => string.Equals(b.FilePath, filePath, StringComparison.Ordinal)).ToList();

    /// <summary>The breakpoint on a line, if there is one.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    public BreakpointViewModel? Find(string filePath, int line) =>
        _breakpoints.Find(b => b.Line == line && string.Equals(b.FilePath, filePath, StringComparison.Ordinal));

    /// <summary>Sets a breakpoint on a line, or removes the one that is there (F9).</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    /// <returns>The new breakpoint, or <see langword="null"/> when one was removed.</returns>
    public BreakpointViewModel? Toggle(string filePath, int line)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (Find(filePath, line) is { } existing)
        {
            Remove(existing);
            return null;
        }

        return Add(filePath, line);
    }

    /// <summary>Sets a breakpoint on a line, or returns the one already there.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">One-based line.</param>
    /// <param name="condition">When to stop, or <see langword="null"/> to stop every time.</param>
    public BreakpointViewModel Add(string filePath, int line, string? condition = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (Find(filePath, line) is { } existing)
        {
            return existing;
        }

        var breakpoint = new BreakpointViewModel(filePath, line, condition);
        if (DocumentFor(filePath) is { } document)
        {
            breakpoint.Attach(document.Document);
        }

        _breakpoints.Add(breakpoint);
        if (_session is not null)
        {
            BindToSession(breakpoint);
        }

        OnChanged(filePath);
        return breakpoint;
    }

    /// <summary>Removes a breakpoint.</summary>
    /// <param name="breakpoint">The breakpoint.</param>
    public void Remove(BreakpointViewModel breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        if (!_breakpoints.Remove(breakpoint))
        {
            return;
        }

        if (breakpoint.Binding is { } binding)
        {
            _session?.RemoveBreakpoint(binding.FilePath, binding.Line);
        }

        breakpoint.Detach();
        OnChanged(breakpoint.FilePath);
    }

    /// <summary>Removes every breakpoint (Ctrl+Shift+F9).</summary>
    public void Clear()
    {
        var files = _breakpoints.Select(b => b.FilePath).Distinct(StringComparer.Ordinal).ToList();
        foreach (var breakpoint in _breakpoints)
        {
            breakpoint.Detach();
        }

        _breakpoints.Clear();
        _session?.ClearBreakpoints();
        foreach (var file in files)
        {
            OnChanged(file);
        }
    }

    /// <summary>Changes when a breakpoint stops.</summary>
    /// <param name="breakpoint">The breakpoint.</param>
    /// <param name="condition">The condition, or <see langword="null"/> to stop every time.</param>
    public void SetCondition(BreakpointViewModel breakpoint, string? condition)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        breakpoint.SetCondition(condition);
        if (_session is not null && breakpoint.Binding is { } binding)
        {
            // Setting the same line with another condition replaces the engine's breakpoint.
            breakpoint.Bind(_session.SetBreakpoint(binding.FilePath, binding.Line, breakpoint.Condition));
        }

        OnChanged(breakpoint.FilePath);
    }

    /// <summary>Starts tracking the breakpoints of a document that just opened, and shows them in its margin.</summary>
    /// <param name="document">The document.</param>
    public void Attach(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.TryAdd(document.Document, document))
        {
            return;
        }

        foreach (var breakpoint in InFile(document.FilePath))
        {
            breakpoint.Attach(document.Document);
        }

        document.Document.Changed += OnDocumentChanged;
        document.SetBreakpoints(InFile(document.FilePath));
    }

    /// <summary>Stops tracking a document that is closing; its breakpoints keep their lines.</summary>
    /// <param name="document">The document.</param>
    public void Detach(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.Remove(document.Document))
        {
            return;
        }

        document.Document.Changed -= OnDocumentChanged;
        foreach (var breakpoint in InFile(document.FilePath))
        {
            breakpoint.Detach();
        }

        document.SetBreakpoints([]);
    }

    /// <summary>
    /// Hands every breakpoint to a debug session that is about to run. A breakpoint on a line without code moves to
    /// the statement it binds to, as in Visual Studio.
    /// </summary>
    /// <param name="session">The session.</param>
    public void Bind(DebugSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        foreach (var breakpoint in _breakpoints.ToList())
        {
            BindToSession(breakpoint);
        }

        foreach (var file in _breakpoints.Select(b => b.FilePath).Distinct(StringComparer.Ordinal).ToList())
        {
            OnChanged(file);
        }
    }

    /// <summary>Forgets the debug session, when it has ended.</summary>
    public void Unbind()
    {
        _session = null;
        foreach (var breakpoint in _breakpoints)
        {
            breakpoint.Bind(null);
        }

        foreach (var file in _breakpoints.Select(b => b.FilePath).Distinct(StringComparer.Ordinal).ToList())
        {
            OnChanged(file);
        }
    }

    /// <summary>The breakpoints to save with the session.</summary>
    public IReadOnlyList<SessionBreakpoint> Save() =>
        _breakpoints.Select(b => new SessionBreakpoint(b.FilePath, b.Line, b.Condition)).ToList();

    /// <summary>Replaces the breakpoints with those of a saved session, skipping files that no longer exist.</summary>
    /// <param name="saved">The saved breakpoints, or <see langword="null"/> for none.</param>
    public void Load(IReadOnlyList<SessionBreakpoint>? saved)
    {
        Clear();
        foreach (var breakpoint in saved ?? [])
        {
            if (breakpoint.Line >= 1 && File.Exists(breakpoint.Path))
            {
                Add(breakpoint.Path, breakpoint.Line, breakpoint.Condition);
            }
        }
    }

    private void BindToSession(BreakpointViewModel breakpoint)
    {
        var binding = _session!.SetBreakpoint(breakpoint.FilePath, breakpoint.Line, breakpoint.Condition);
        breakpoint.Bind(binding);
        if (binding.Location is not { } location
            || location.Line == breakpoint.Line
            || !string.Equals(location.FilePath, breakpoint.FilePath, StringComparison.Ordinal))
        {
            return;
        }

        if (Find(breakpoint.FilePath, location.Line) is { } other && !ReferenceEquals(other, breakpoint))
        {
            // Another breakpoint is already on the line this one moves to: keep that one.
            Remove(breakpoint);
            return;
        }

        breakpoint.MoveTo(location.Line, DocumentFor(breakpoint.FilePath)?.Document);
    }

    private DocumentViewModel? DocumentFor(string filePath) =>
        _documents.Values.FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.Ordinal));

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (sender is not TextDocument text || !_documents.TryGetValue(text, out var document))
        {
            return;
        }

        foreach (var breakpoint in _breakpoints.Where(b => string.Equals(b.FilePath, document.FilePath, StringComparison.Ordinal)))
        {
            breakpoint.Refresh(text);
        }
    }

    private void OnChanged(string filePath)
    {
        if (DocumentFor(filePath) is { } document)
        {
            document.SetBreakpoints(InFile(filePath));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
