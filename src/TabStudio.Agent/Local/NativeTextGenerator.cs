using System.Runtime.InteropServices;
using System.Text;

namespace TabStudio.Agent.Local;

/// <summary>Owns a llama.cpp model and context through TabStudio's small, pinned native C ABI.</summary>
public sealed class NativeTextGenerator : ILocalTextGenerator, IDisposable
{
    private static readonly Piece PieceCallback = ReceivePiece;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private nint _engine;
    private bool _disposed;

    static NativeTextGenerator()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeTextGenerator).Assembly, (name, assembly, paths) =>
        {
            if (name == "tabstudio-ai" && Environment.GetEnvironmentVariable("TABSTUDIO_LOCAL_AI_LIBRARY") is { Length: > 0 } library)
                return NativeLibrary.Load(library);
            return 0;
        });
    }

    /// <summary>Loads a verified GGUF. Call on a worker thread; a 4096-token context bounds RAM.</summary>
    public NativeTextGenerator(string path, int context = 4096, int threads = 2, int gpuLayers = -1)
    {
        try { _engine = Load(path, context, threads, gpuLayers); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new InvalidOperationException("This build has no compatible local AI runtime. Install a 64-bit TabStudio build with llama.cpp included.", ex);
        }
        if (_engine == 0) throw new InvalidOperationException(Marshal.PtrToStringUTF8(Error()) ?? "The local model could not be loaded.");
    }

    /// <inheritdoc />
    public async Task<LocalCompletion> GenerateAsync(string prompt, int maxTokens, Action<string>? onText = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokens);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxTokens, 1024);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return await Task.Run(() =>
            {
                ResetCancel(_engine);
                using var registration = cancellationToken.Register(() => Cancel(_engine));
                var state = new GenerationState(onText, cancellationToken);
                // A static reverse P/Invoke target and explicit state handle also work when Android's Mono
                // interpreter is enabled for Hot Reload. No capturing delegate crosses the native boundary.
                var handle = GCHandle.Alloc(state);
                try
                {
                    var count = Generate(_engine, prompt, maxTokens, 0, PieceCallback, GCHandle.ToIntPtr(handle), out var speed);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (state.Error is not null) throw new InvalidOperationException("Local inference callback failed.", state.Error);
                    if (count < 0) throw new InvalidOperationException(Marshal.PtrToStringUTF8(Error()) ?? "Local inference failed.");
                    if (count > 0 && state.Text.Length == 0)
                        throw new InvalidOperationException(FormattableString.Invariant($"Local inference generated {count} tokens but delivered no text ({state.CallbackCount} callbacks, {state.ByteCount} UTF-8 bytes, {state.Text.Length} characters)."));
                    return new LocalCompletion(state.Text.ToString(), count, speed);
                }
                finally { handle.Free(); }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private static int ReceivePiece(nint data, int length, nint user) =>
        ((GenerationState)GCHandle.FromIntPtr(user).Target!).Receive(data, length);

    private sealed class GenerationState(Action<string>? onText, CancellationToken cancellationToken)
    {
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
        private readonly byte[] _bytes = new byte[64 * 1024];
        private readonly char[] _chars = new char[64 * 1024];
        public StringBuilder Text { get; } = new();
        public Exception? Error { get; private set; }
        public int CallbackCount { get; private set; }
        public int ByteCount { get; private set; }

        public int Receive(nint data, int length)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested) return 1;
                CallbackCount++;
                if (length < 0 || length > _bytes.Length) throw new InvalidDataException("The model produced an oversized token.");
                ByteCount += length;
                Marshal.Copy(data, _bytes, 0, length);
                // Tokens may split a UTF-8 character. Keep the decoder across pieces and use its array overload
                // inside the reverse P/Invoke callback, including when Mono runs the interpreter.
                var count = _decoder.GetChars(_bytes, 0, length, _chars, 0, flush: false);
                var piece = new string(_chars, 0, count);
                Text.Append(piece);
                onText?.Invoke(piece);
                return 0;
            }
            catch (Exception ex) { Error = ex; return 1; }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_engine != 0) Free(_engine);
            _engine = 0;
        }
        finally { _gate.Release(); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Piece(nint data, int length, nint user);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2101", Justification = "The C ABI requires UTF-8; LPUTF8Str explicitly matches it.")]
    [DllImport("tabstudio-ai", EntryPoint = "tsai_load", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Load([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int context, int threads, int gpuLayers);
    [DllImport("tabstudio-ai", EntryPoint = "tsai_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Free(nint engine);
    [DllImport("tabstudio-ai", EntryPoint = "tsai_cancel", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Cancel(nint engine);
    [DllImport("tabstudio-ai", EntryPoint = "tsai_reset_cancel", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ResetCancel(nint engine);
    [DllImport("tabstudio-ai", EntryPoint = "tsai_error", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Error();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2101", Justification = "The C ABI requires UTF-8; LPUTF8Str explicitly matches it.")]
    [DllImport("tabstudio-ai", EntryPoint = "tsai_generate", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Generate(nint engine, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, int maxTokens, int ignoreEos, Piece callback, nint user, out double tokensPerSecond);
}
