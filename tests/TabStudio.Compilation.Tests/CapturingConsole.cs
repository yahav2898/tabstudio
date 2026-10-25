using System.Text;
using TabStudio.Compilation.Running;

namespace TabStudio.Compilation.Tests;

/// <summary>Collects a program's console output for assertions.</summary>
internal sealed class CapturingConsole : IConsoleSink
{
    private readonly Lock _gate = new();
    private readonly StringBuilder _output = new();
    private readonly StringBuilder _error = new();

    public string Output
    {
        get
        {
            lock (_gate)
            {
                return _output.ToString().ReplaceLineEndings("\n");
            }
        }
    }

    public string Error
    {
        get
        {
            lock (_gate)
            {
                return _error.ToString().ReplaceLineEndings("\n");
            }
        }
    }

    public void WriteOutput(string text)
    {
        lock (_gate)
        {
            _output.Append(text);
        }
    }

    public void WriteError(string text)
    {
        lock (_gate)
        {
            _error.Append(text);
        }
    }
}
