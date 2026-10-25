using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;

namespace TabStudio.App.Editing;

/// <summary>
/// Keeps TextMate highlighting from tokenizing and redrawing lines on every frame of a scroll when their colors are
/// already known: the view's lines are tokenized on a scroll only while one of them has no current tokens yet.
/// </summary>
/// <remarks>
/// <para>
/// AvaloniaEdit's TextMate model handles every change of the scroll offset by tokenizing the lines in view again on the
/// UI thread: at least the first visible line, with a walk over the lines after it, and the report that it changed,
/// so the editor builds and draws that line again on the next frame. That is every frame of a drag or a fling, for
/// nothing when the background tokenizer has already colored the lines.
/// </para>
/// <para>
/// This is a small workaround of AvaloniaEdit.TextMate's internals, kept here: the model's scroll handler (a private
/// method, found by its name) is taken off the text view, and called by this class only when a line in view still has
/// no current tokens, at most once per batch of scroll events, so a line scrolled into view before the background
/// tokenizer reached it is colored at once, as before. When the handler is not found, as in a later
/// AvaloniaEdit.TextMate, nothing is changed. The installation's own disposal is unaffected.
/// </para>
/// </remarks>
public sealed class TextMateScrollTokenizing
{
    private const string ScrollHandlerName = "TextView_ScrollOffsetChanged";

    private static readonly MethodInfo? ScrollHandler =
        typeof(TextEditorModel).GetMethod(ScrollHandlerName, BindingFlags.Instance | BindingFlags.NonPublic, [typeof(object), typeof(EventArgs)]);

    private static readonly ConditionalWeakTable<TextView, TextMateScrollTokenizing> Installed = new();

    private readonly TextMate.Installation _installation;
    private readonly TextView _textView;
    private readonly IVisualLineTransformer _transformer;
    private TextEditorModel? _model;
    private EventHandler? _tokenizeView;
    private bool _checkPending;
    private bool _uninstalled;

    private TextMateScrollTokenizing(TextMate.Installation installation, TextView textView, IVisualLineTransformer transformer)
    {
        _installation = installation;
        _textView = textView;
        _transformer = transformer;
    }

    /// <summary>How many times a scroll tokenized the view because a line in it had no tokens yet, for tests.</summary>
    internal int ViewTokenizations { get; private set; }

    /// <summary>Takes over an installation's tokenizing on scroll. Call right after installing TextMate on the editor.</summary>
    /// <param name="installation">The editor's TextMate highlighting.</param>
    /// <param name="textView">The editor's text view.</param>
    /// <returns>The takeover, or <see langword="null"/> when AvaloniaEdit.TextMate does not work as this class expects.</returns>
    public static TextMateScrollTokenizing? Install(TextMate.Installation installation, TextView textView)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(textView);
        if (ScrollHandler is null || installation.EditorModel is null
            || textView.LineTransformers.OfType<TextMateColoringTransformer>().FirstOrDefault() is not { } transformer)
        {
            return null;
        }

        // A view colored again, as when an extension's grammar replaces another, has one takeover: the newest.
        if (Installed.TryGetValue(textView, out var previous))
        {
            previous.Detach();
        }

        var takeover = new TextMateScrollTokenizing(installation, textView, transformer);
        takeover.TakeModel();
        textView.ScrollOffsetChanged += takeover.OnScrollOffsetChanged;
        Installed.AddOrUpdate(textView, takeover);
        return takeover;
    }

    /// <summary>The takeover on a text view, if any, for tests.</summary>
    /// <param name="textView">The text view.</param>
    internal static TextMateScrollTokenizing? On(TextView textView) => Installed.TryGetValue(textView, out var takeover) && !takeover._uninstalled ? takeover : null;

    /// <summary>Gives the scroll handling back to AvaloniaEdit.TextMate, as it was installed.</summary>
    public void Uninstall()
    {
        if (_uninstalled)
        {
            return;
        }

        Detach();
        if (_tokenizeView is not null && ReferenceEquals(_installation.EditorModel, _model) && _textView.LineTransformers.Contains(_transformer))
        {
            _textView.ScrollOffsetChanged += _tokenizeView;
        }
    }

    private void Detach()
    {
        _uninstalled = true;
        _checkPending = false;
        _textView.ScrollOffsetChanged -= OnScrollOffsetChanged;
    }

    /// <summary>Takes the scroll handler of the installation's current model off the text view; a new document gets a new model.</summary>
    private void TakeModel()
    {
        _model = _installation.EditorModel;
        _tokenizeView = _model is null ? null : ScrollHandler!.CreateDelegate<EventHandler>(_model);
        if (_tokenizeView is not null)
        {
            _textView.ScrollOffsetChanged -= _tokenizeView;
        }
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        // TextMate does not color the view now: its installation was disposed.
        if (!_textView.LineTransformers.Contains(_transformer))
        {
            return;
        }

        if (!ReferenceEquals(_installation.EditorModel, _model))
        {
            TakeModel();
        }

        // After the layout that the scroll causes, once for all the scroll events before it.
        if (!_checkPending)
        {
            _checkPending = true;
            Dispatcher.UIThread.Post(CheckView, DispatcherPriority.Default);
        }
    }

    private void CheckView()
    {
        _checkPending = false;
        if (_uninstalled || _model is not { } model || _tokenizeView is not { } tokenizeView
            || !_textView.VisualLinesValid || _textView.VisualLines.Count == 0)
        {
            return;
        }

        var first = _textView.VisualLines[0].FirstDocumentLine.LineNumber - 1;
        var last = _textView.VisualLines[^1].LastDocumentLine.LineNumber - 1;
        for (var index = first; index <= last; index++)
        {
            if (model.Get(index) is not { IsInvalid: false, Tokens: not null })
            {
                ViewTokenizations++;
                tokenizeView(_textView, EventArgs.Empty);
                return;
            }
        }
    }
}
