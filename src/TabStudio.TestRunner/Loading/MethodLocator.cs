using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace TabStudio.TestRunner.Loading;

/// <summary>
/// Finds where a method's code starts, using the portable PDB that the compiler emitted next to the assembly.
/// </summary>
/// <remarks>
/// Async and iterator methods keep their code, and so their sequence points, in a compiler-generated
/// <c>MoveNext</c> method; the PDB's state machine table leads from the method to it.
/// </remarks>
internal sealed class MethodLocator : IDisposable
{
    private readonly MetadataReaderProvider _provider;
    private readonly MetadataReader _reader;
    private readonly Lock _gate = new();
    private Dictionary<MethodDefinitionHandle, MethodDefinitionHandle>? _moveNextByKickoff;

    private MethodLocator(MetadataReaderProvider provider)
    {
        _provider = provider;
        _reader = provider.GetMetadataReader();
    }

    /// <summary>Opens a portable PDB, or returns <see langword="null"/> when there is none or it cannot be read.</summary>
    /// <param name="symbols">The PDB bytes.</param>
    public static MethodLocator? TryCreate(byte[]? symbols)
    {
        if (symbols is null || symbols.Length == 0)
        {
            return null;
        }

        MetadataReaderProvider? provider = null;
        try
        {
            provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(symbols, writable: false));
            return new MethodLocator(provider);
        }
        catch (BadImageFormatException)
        {
            provider?.Dispose();
            return null;
        }
    }

    /// <summary>Returns the first line of a method's code, or <see langword="null"/> when the PDB does not cover it.</summary>
    /// <param name="method">A method of the assembly this PDB belongs to.</param>
    public TestSourceLocation? Find(MethodBase method)
    {
        lock (_gate)
        {
            try
            {
                var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken);
                return FirstSequencePoint(handle)
                    ?? (MoveNextByKickoff().TryGetValue(handle, out var moveNext) ? FirstSequencePoint(moveNext) : null);
            }
            catch (Exception ex) when (ex is BadImageFormatException or ArgumentException or InvalidOperationException or InvalidCastException)
            {
                return null;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _provider.Dispose();

    private TestSourceLocation? FirstSequencePoint(MethodDefinitionHandle handle)
    {
        if (MetadataTokens.GetRowNumber(handle) > _reader.MethodDebugInformation.Count)
        {
            return null;
        }

        var information = _reader.GetMethodDebugInformation(handle);
        foreach (var point in information.GetSequencePoints())
        {
            if (!point.IsHidden)
            {
                var document = _reader.GetDocument(point.Document);
                return new TestSourceLocation(_reader.GetString(document.Name), point.StartLine, point.StartColumn);
            }
        }

        return null;
    }

    private Dictionary<MethodDefinitionHandle, MethodDefinitionHandle> MoveNextByKickoff()
    {
        if (_moveNextByKickoff is null)
        {
            _moveNextByKickoff = [];
            foreach (var handle in _reader.MethodDebugInformation)
            {
                var kickoff = _reader.GetMethodDebugInformation(handle).GetStateMachineKickoffMethod();
                if (!kickoff.IsNil)
                {
                    _moveNextByKickoff[kickoff] = handle.ToDefinitionHandle();
                }
            }
        }

        return _moveNextByKickoff;
    }
}
