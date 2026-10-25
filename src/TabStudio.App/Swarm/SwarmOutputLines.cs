using System.Text;
using TabStudio.App.ViewModels;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.Swarm;

/// <summary>
/// Writes the console output of every device into one Output pane, each line under the device's name, such as
/// <c>[Pixel 7] Hello, world!</c>. Text arrives in pieces; a line is written once it ends, so the lines of two devices
/// never mix. UI thread.
/// </summary>
/// <param name="output">The Swarm pane of the Output panel.</param>
public sealed class SwarmOutputLines(OutputViewModel output)
{
    private readonly Dictionary<SwarmDevice, (StringBuilder Line, bool IsError)> _pending = [];

    /// <summary>Writes a piece of a device's output.</summary>
    /// <param name="device">The device.</param>
    /// <param name="text">The text, in any number of lines and pieces of lines.</param>
    /// <param name="isError">Whether it went to standard error.</param>
    public void Write(SwarmDevice device, string text, bool isError)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(text);
        if (!_pending.TryGetValue(device, out var pending) || pending.IsError != isError)
        {
            Flush(device);
            pending = (new StringBuilder(), isError);
            _pending[device] = pending;
        }

        foreach (var c in text)
        {
            if (c == '\n')
            {
                WriteLine(device, pending.Line.ToString().TrimEnd('\r'), isError);
                pending.Line.Clear();
            }
            else
            {
                pending.Line.Append(c);
            }
        }
    }

    /// <summary>Writes a line of TabStudio's own about a device, such as how its app ended.</summary>
    /// <param name="device">The device.</param>
    /// <param name="line">The line.</param>
    /// <param name="kind">How it is colored.</param>
    public void Note(SwarmDevice device, string line, OutputKind kind)
    {
        ArgumentNullException.ThrowIfNull(device);
        Flush(device);
        output.WriteLine($"[{device.Name}] {line}", kind);
    }

    /// <summary>Writes what is left of a device's last line, when its app ended.</summary>
    /// <param name="device">The device.</param>
    public void Flush(SwarmDevice device)
    {
        if (_pending.Remove(device, out var pending) && pending.Line.Length > 0)
        {
            WriteLine(device, pending.Line.ToString(), pending.IsError);
        }
    }

    private void WriteLine(SwarmDevice device, string line, bool isError) =>
        output.WriteLine($"[{device.Name}] {line}", isError ? OutputKind.Error : OutputKind.Program);
}
