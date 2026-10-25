using System.Text;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Collects everything one test writes. Thread-safe, because a test may write from tasks it starts.
/// </summary>
/// <remarks>
/// Once the test's result is reported the sink is closed, and writes from a test that was abandoned after a
/// timeout are dropped instead of leaking into the next test's output.
/// </remarks>
internal sealed class TestOutputSink(int maxLength)
{
    private const string TruncatedNotice = "\n[Output truncated: the test wrote more than TabStudio keeps per test.]\n";

    private readonly Lock _gate = new();
    private readonly StringBuilder _text = new();
    private bool _closed;
    private bool _truncated;

    /// <summary>The text written so far.</summary>
    public string Text
    {
        get
        {
            lock (_gate)
            {
                return _text.ToString();
            }
        }
    }

    /// <summary>Appends text, unless the sink is closed or full.</summary>
    /// <param name="text">The text.</param>
    public void Write(string text)
    {
        lock (_gate)
        {
            if (_closed || _truncated)
            {
                return;
            }

            var room = maxLength - _text.Length;
            if (text.Length <= room)
            {
                _text.Append(text);
                return;
            }

            _text.Append(text, 0, Math.Max(room, 0)).Append(TruncatedNotice);
            _truncated = true;
        }
    }

    /// <summary>Stops collecting and returns everything collected.</summary>
    public string Close()
    {
        lock (_gate)
        {
            _closed = true;
            return _text.ToString();
        }
    }
}
