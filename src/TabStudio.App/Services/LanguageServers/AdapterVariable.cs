using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.DebugAdapters;

namespace TabStudio.App.Services.LanguageServers;

/// <summary>
/// A value of a program a debug adapter paused, for the Locals and Watch panels and DataTips: a variable, a member, an
/// evaluated expression or a scope such as Globals. Its children are read from the adapter when first shown.
/// </summary>
internal sealed class AdapterVariable : IDebugVariable
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    private readonly DebugAdapterSession _session;
    private readonly string _name;
    private readonly int _reference;
    private readonly int _container;
    private readonly int? _frameId;

    /// <summary>Creates the value.</summary>
    /// <param name="session">The session of the paused program.</param>
    /// <param name="name">Its name, as <c>setVariable</c> names it.</param>
    /// <param name="value">The value, as the adapter formats it.</param>
    /// <param name="type">Its type, if known.</param>
    /// <param name="evaluateName">An expression that finds it again, if known.</param>
    /// <param name="reference">The reference of its children; 0 when it has none.</param>
    /// <param name="container">The reference of the scope or value it belongs to; 0 for an evaluated expression.</param>
    /// <param name="frameId">The frame it was read in.</param>
    public AdapterVariable(DebugAdapterSession session, string name, string value, string? type, string? evaluateName, int reference, int container, int? frameId)
    {
        _session = session;
        _name = name;
        Value = value;
        Type = type ?? string.Empty;
        EvaluateName = evaluateName;
        _reference = reference;
        _container = container;
        _frameId = frameId;
    }

    /// <inheritdoc />
    public string Value { get; private set; }

    /// <inheritdoc />
    public string Type { get; }

    /// <inheritdoc />
    public bool HasChildren => _reference > 0;

    /// <inheritdoc />
    public string? EvaluateName { get; }

    /// <inheritdoc />
    public bool CanSetValue =>
        (_container > 0 && _session.Capabilities.SetVariable) || (EvaluateName is not null && _session.Capabilities.SetExpression);

    /// <summary>A scope of a frame, such as Globals, as a row whose children are its variables.</summary>
    /// <param name="session">The session.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="frameId">The frame.</param>
    public static AdapterVariable ForScope(DebugAdapterSession session, DapScope scope, int frameId) =>
        new(session, scope.Name, string.Empty, null, null, scope.VariablesReference, 0, frameId);

    /// <summary>A variable of a scope or a value.</summary>
    /// <param name="session">The session.</param>
    /// <param name="variable">The variable.</param>
    /// <param name="container">The reference of the scope or value it belongs to.</param>
    /// <param name="frameId">The frame.</param>
    public static AdapterVariable ForVariable(DebugAdapterSession session, DapVariable variable, int container, int? frameId) =>
        new(session, variable.Name, variable.Value, variable.Type, variable.EvaluateName, variable.VariablesReference, container, frameId);

    /// <inheritdoc />
    public IReadOnlyList<(string Name, IDebugVariable Value)> ReadChildren()
    {
        using var timeout = new CancellationTokenSource(ReadTimeout);
        var children = _session.GetVariablesAsync(_reference, timeout.Token).GetAwaiter().GetResult();
        return children
            .Select(c => (c.Name, (IDebugVariable)ForVariable(_session, c, _reference, _frameId)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<string> SetValueAsync(string expression, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (_container > 0 && _session.Capabilities.SetVariable)
        {
            var changed = await _session.SetVariableAsync(_container, _name, expression, cancellationToken);
            Value = changed.Value;
        }
        else
        {
            var changed = await _session.SetExpressionAsync(EvaluateName ?? _name, expression, _frameId, cancellationToken);
            Value = changed.Result;
        }

        return Value;
    }
}
