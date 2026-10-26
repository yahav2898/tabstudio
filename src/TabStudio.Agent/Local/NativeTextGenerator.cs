using System.Runtime.InteropServices;
using System.Text;

namespace TabStudio.Agent.Local;

/// <summary>Owns a llama.cpp model and context through TabStudio's small, pinned native C ABI.</summary>
public sealed class NativeTextGenerator : ILocalTextGenerator, IDisposable
{
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
                var text = new StringBuilder();
                var decoder = Encoding.UTF8.GetDecoder();
                var bytes = new byte[64 * 1024];
                var chars = new char[64 * 1024];
                Exception? callbackError = null;
                Piece callback = (data, length, _) =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return 1;
                        if (length > bytes.Length) throw new InvalidDataException("The model produced an oversized token.");
                        Marshal.Copy(data, bytes, 0, length);
                        var count = decoder.GetChars(bytes.AsSpan(0, length), chars, flush: false);
                        var piece = new string(chars, 0, count);
                        text.Append(piece);
                        onText?.Invoke(piece);
                        return 0;
                    }
                    catch (Exception ex) { callbackError = ex; return 1; }
                };
                var count = Generate(_engine, prompt, maxTokens, 0, callback, 0, out var speed);
                GC.KeepAlive(callback);
                cancellationToken.ThrowIfCancellationRequested();
                if (callbackError is not null) throw new InvalidOperationException("Local inference callback failed.", callbackError);
                if (count < 0) throw new InvalidOperationException(Marshal.PtrToStringUTF8(Error()) ?? "Local inference failed.");
                return new LocalCompletion(text.ToString(), count, speed);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
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
