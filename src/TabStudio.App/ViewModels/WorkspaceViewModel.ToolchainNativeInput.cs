using System.Text;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private FileStream? _nativeDebugInput;
    private string? _nativeDebugDirectory;

    /// <summary>LLDB 22 launches inside its own PRoot trace tree; a FIFO supplies the Output panel's input.</summary>
    private async Task<LaunchConfiguration?> PrepareNativeDebugInputAsync(LaunchConfiguration configuration)
    {
        if (configuration.Type != "lldb-dap" || _toolchains.Runtime.Kind != RuntimeKind.Linux)
        {
            return configuration;
        }

        StopNativeDebugInput();
        var cache = Path.Combine(Path.GetTempPath(), "tabstudio-native-debug");
        Directory.CreateDirectory(cache);
        _toolchains.Runtime.ShareFolder(cache);
        var folder = Path.Combine(cache, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        _nativeDebugDirectory = folder;
        var pipe = Path.Combine(folder, "stdin");
        try
        {
            var made = await CommandRunner.RunAsync(_toolchains.Runtime, new ProcessStartSpec
            {
                Program = "mkfifo", Arguments = ["-m", "600", pipe],
            }, null, CancellationToken.None);
            if (!made.Succeeded)
            {
                throw new IOException("Could not create the debugger input pipe: " + made.Tail(3));
            }

            // O_RDWR opens a FIFO without blocking before LLDB opens its reader. Keeping the writer open also
            // prevents EOF between answers. The adapter's stdin remains exclusively its DAP protocol stream.
            _nativeDebugInput = new FileStream(pipe, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1, useAsync: false);
            var properties = (JsonObject)configuration.Properties.DeepClone();
            properties["runInTerminal"] = false;
            properties["console"] = "internalConsole";
            properties["stdio"] = new JsonArray(pipe, null, null);
            return configuration with { Properties = properties };
        }
        catch (Exception ex) when (ex is IOException or ToolchainRuntimeException)
        {
            StopNativeDebugInput();
            Output.WriteLine(ex.Message, OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Debugger input could not start";
            return null;
        }
    }

    private bool StartNativeDebugInput()
    {
        if (_nativeDebugInput is not { } pipe)
        {
            return false;
        }

        var input = Output.BeginProcessInput();
        _ = Task.Run(async () =>
        {
            try
            {
                while (input.ReadLine() is { } line)
                {
                    await pipe.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
                    await pipe.FlushAsync();
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The session ended while input was being forwarded.
            }
        });
        return true;
    }

    private void StopNativeDebugInput()
    {
        if (_nativeDebugInput is { } input)
        {
            _nativeDebugInput = null;
            Output.EndInput();
            input.Dispose();
        }

        if (_nativeDebugDirectory is { } folder)
        {
            _nativeDebugDirectory = null;
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // A session still closing can leave a cache entry, removed with the app's other temporary files.
            }
        }
    }
}
