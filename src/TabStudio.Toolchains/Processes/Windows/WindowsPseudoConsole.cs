using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TabStudio.Toolchains.Processes.Windows;

/// <summary>
/// A process in a Windows pseudo console (ConPTY, Windows 10 1809 and later): the Windows counterpart of a Unix
/// pseudo terminal, for the desktop build's Terminal on Windows.
/// </summary>
/// <remarks>
/// The console translates the program's console calls into the same escape sequences a Unix terminal receives,
/// so the Terminal's emulator draws Windows shells too.
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
internal sealed partial class WindowsPseudoConsole : IProcessHandle
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const nint PseudoConsoleAttribute = 0x00020016;
    private const uint Infinite = 0xFFFFFFFF;

    private readonly nint _console;
    private readonly SafeProcessHandle _process;
    private readonly Task<(int ExitCode, int? Signal)> _exit;
    private int _closed;

    private WindowsPseudoConsole(nint console, SafeProcessHandle process, int pid, Stream input, Stream output)
    {
        _console = console;
        _process = process;
        Id = pid;
        Input = input;
        Output = output;
        _exit = Task.Factory.StartNew(Wait, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <inheritdoc />
    public int Id { get; }

    /// <inheritdoc />
    public bool IsTerminal => true;

    /// <inheritdoc />
    public Stream Input { get; }

    /// <inheritdoc />
    public Stream Output { get; }

    /// <inheritdoc />
    public Stream? Error => null;

    /// <summary>Starts a program in a new pseudo console.</summary>
    /// <param name="path">The program.</param>
    /// <param name="arguments">Its arguments, without its name.</param>
    /// <param name="environment">The complete environment.</param>
    /// <param name="workingDirectory">The folder to start in, or <see langword="null"/>.</param>
    /// <param name="size">The console's size.</param>
    /// <exception cref="ToolchainRuntimeException">It could not be started.</exception>
    public static IProcessHandle Start(string path, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string? workingDirectory, TerminalSize size)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            throw new ToolchainRuntimeException("The Terminal needs Windows 10 version 1809 or later.");
        }

        if (!CreatePipe(out var inputRead, out var inputWrite, 0, 0) || !CreatePipe(out var outputRead, out var outputWrite, 0, 0))
        {
            throw Failure("Could not create the console's pipes");
        }

        var result = CreatePseudoConsole(Coordinates(size), inputRead, outputWrite, 0, out var console);
        // The console has its own handles to these ends now.
        inputRead.Dispose();
        outputWrite.Dispose();
        if (result != 0)
        {
            inputWrite.Dispose();
            outputRead.Dispose();
            throw new ToolchainRuntimeException($"Could not create a pseudo console (error 0x{result:x8}).", result);
        }

        var attributeListSize = (nint)0;
        _ = InitializeProcThreadAttributeList(0, 1, 0, ref attributeListSize);
        var attributeList = Marshal.AllocHGlobal(attributeListSize);
        try
        {
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize)
                || !UpdateProcThreadAttribute(attributeList, 0, PseudoConsoleAttribute, console, nint.Size, 0, 0))
            {
                throw Failure("Could not attach the pseudo console");
            }

            var startup = new StartupInfoEx { StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>() }, AttributeList = attributeList };
            var commandLine = BuildCommandLine(path, arguments);
            var block = BuildEnvironmentBlock(environment);
            if (!CreateProcessW(null, commandLine, 0, 0, false, ExtendedStartupInfoPresent | CreateUnicodeEnvironment, block, workingDirectory, ref startup, out var information))
            {
                throw Failure($"Could not start {path}");
            }

            _ = CloseHandle(information.Thread);
            return new WindowsPseudoConsole(
                console,
                new SafeProcessHandle(information.Process, ownsHandle: true),
                information.ProcessId,
                new FileStream(inputWrite, FileAccess.Write, 1),
                new FileStream(outputRead, FileAccess.Read, 1));
        }
        catch
        {
            ClosePseudoConsole(console);
            inputWrite.Dispose();
            outputRead.Dispose();
            throw;
        }
        finally
        {
            DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
        }
    }

    /// <inheritdoc />
    public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit;

    /// <inheritdoc />
    public void Resize(TerminalSize size) => _ = ResizePseudoConsole(_console, Coordinates(size));

    /// <inheritdoc />
    public void CloseInput() => Input.Dispose();

    /// <inheritdoc />
    public void KillTree()
    {
        try
        {
            using var process = Process.GetProcessById(Id);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // It has already exited.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            // Closing the console ends its output, which ends the reader.
            ClosePseudoConsole(_console);
            Input.Dispose();
            Output.Dispose();
            _process.Dispose();
        }
    }

    /// <summary>Quotes arguments so the program's C runtime splits them back exactly as given.</summary>
    internal static string BuildCommandLine(string path, IEnumerable<string> arguments)
    {
        var line = new StringBuilder();
        foreach (var argument in new[] { path }.Concat(arguments))
        {
            if (line.Length > 0)
            {
                line.Append(' ');
            }

            if (argument.Length > 0 && !argument.Any(c => c is ' ' or '\t' or '"'))
            {
                line.Append(argument);
                continue;
            }

            line.Append('"');
            var backslashes = 0;
            foreach (var c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                line.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes);
                backslashes = 0;
                line.Append(c);
            }

            line.Append('\\', backslashes * 2).Append('"');
        }

        return line.ToString();
    }

    private static string BuildEnvironmentBlock(IReadOnlyDictionary<string, string> environment)
    {
        var block = new StringBuilder();
        foreach (var (name, value) in environment.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(name).Append('=').Append(value).Append('\0');
        }

        return block.Append('\0').ToString();
    }

    private static int Coordinates(TerminalSize size) => (size.Rows << 16) | (size.Columns & 0xFFFF);

    private static ToolchainRuntimeException Failure(string message)
    {
        var error = Marshal.GetLastPInvokeError();
        return new ToolchainRuntimeException($"{message}: {new Win32Exception(error).Message}", error);
    }

    private (int ExitCode, int? Signal) Wait()
    {
        _ = WaitForSingleObject(_process, Infinite);
        return (GetExitCodeProcess(_process, out var code) ? (int)code : -1, null);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public nint Reserved;
        public nint Desktop;
        public nint Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public nint Reserved2;
        public nint StandardInput;
        public nint StandardOutput;
        public nint StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process;
        public nint Thread;
        public int ProcessId;
        public int ThreadId;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, nint attributes, int size);

    [LibraryImport("kernel32.dll")]
    private static partial int CreatePseudoConsole(int size, SafeFileHandle input, SafeFileHandle output, uint flags, out nint console);

    [LibraryImport("kernel32.dll")]
    private static partial int ResizePseudoConsole(nint console, int size);

    [LibraryImport("kernel32.dll")]
    private static partial void ClosePseudoConsole(nint console);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitializeProcThreadAttributeList(nint list, int count, int flags, ref nint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateProcThreadAttribute(nint list, uint flags, nint attribute, nint value, nint size, nint previousValue, nint returnSize);

    [LibraryImport("kernel32.dll")]
    private static partial void DeleteProcThreadAttributeList(nint list);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateProcessW(
        string? applicationName,
        string commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        string environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
