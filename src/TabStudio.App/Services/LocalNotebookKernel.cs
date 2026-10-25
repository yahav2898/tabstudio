using TabStudio.Notebooks;
using TabStudio.Notebooks.Execution;

namespace TabStudio.App.Services;

/// <summary>
/// The kernel inside TabStudio: a <see cref="CSharpKernel"/> that compiles against the app's memory-mapped
/// reference assemblies, created when the first cell runs.
/// </summary>
public sealed class LocalNotebookKernel : INotebookKernel
{
    private readonly CompilerServices _compiler;
    private readonly INotebookReferenceResolver? _references;
    private readonly Lock _gate = new();
    private Task<CSharpKernel>? _kernel;
    private bool _disposed;

    /// <summary>Creates the kernel. Nothing is loaded until the first cell runs.</summary>
    /// <param name="compiler">The app's compiler services, for the reference assemblies.</param>
    /// <param name="references">Resolves <c>#r</c> references such as NuGet packages, or <see langword="null"/>.</param>
    public LocalNotebookKernel(CompilerServices compiler, INotebookReferenceResolver? references = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        _compiler = compiler;
        _references = references;
    }

    /// <inheritdoc />
    public event EventHandler<CellOutputEventArgs>? OutputReceived;

    /// <inheritdoc />
    public async Task<CellExecutionResult> ExecuteAsync(NotebookCell cell, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var kernel = await GetKernelAsync().ConfigureAwait(false);
        return await kernel.ExecuteAsync(cell, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Interrupt()
    {
        if (TryGetKernel() is { } kernel)
        {
            kernel.Interrupt();
        }
    }

    /// <inheritdoc />
    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        if (TryGetKernel() is { } kernel)
        {
            await kernel.RestartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        if (TryGetKernel() is { } kernel)
        {
            kernel.OutputReceived -= OnOutputReceived;
            kernel.Dispose();
        }
    }

    private Task<CSharpKernel> GetKernelAsync()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // A kernel that failed to start, for example because the references could not be read, is tried again.
            if (_kernel is not { IsFaulted: false } kernel)
            {
                kernel = Task.Run(() => CreateKernelAsync());
                _kernel = kernel;
            }

            return kernel;
        }
    }

    private CSharpKernel? TryGetKernel()
    {
        lock (_gate)
        {
            return _kernel is { IsCompletedSuccessfully: true } created ? created.Result : null;
        }
    }

    private async Task<CSharpKernel> CreateKernelAsync()
    {
        var frameworkReferences = await _compiler.GetReferencesAsync().ConfigureAwait(false);
        var kernel = new CSharpKernel(new CSharpKernelOptions
        {
            FrameworkReferences = frameworkReferences,
            ReferenceResolver = _references,
        });

        lock (_gate)
        {
            if (_disposed)
            {
                // The notebook closed while the references were loading.
                kernel.Dispose();
                throw new ObjectDisposedException(nameof(LocalNotebookKernel));
            }

            kernel.OutputReceived += OnOutputReceived;
        }

        return kernel;
    }

    private void OnOutputReceived(object? sender, CellOutputEventArgs e) => OutputReceived?.Invoke(this, e);
}
