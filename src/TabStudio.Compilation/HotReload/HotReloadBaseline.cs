using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace TabStudio.Compilation.HotReload;

/// <summary>A metadata, IL and portable-PDB delta, prepared without modifying the running program.</summary>
public sealed class HotReloadDelta
{
    internal HotReloadDelta(byte[] metadata, byte[] il, byte[] pdb, Microsoft.CodeAnalysis.Compilation compilation, EmitBaseline baseline, int generation, HotReloadBaseline owner)
    {
        Metadata = metadata;
        IL = il;
        Pdb = pdb;
        Compilation = compilation;
        Baseline = baseline;
        Generation = generation;
        Owner = owner;
    }

    /// <summary>The metadata delta.</summary>
    public byte[] Metadata { get; }
    /// <summary>The IL delta.</summary>
    public byte[] IL { get; }
    /// <summary>The portable-PDB delta.</summary>
    public byte[] Pdb { get; }
    internal Microsoft.CodeAnalysis.Compilation Compilation { get; }
    internal EmitBaseline Baseline { get; }
    internal int Generation { get; }
    internal HotReloadBaseline Owner { get; }
}

/// <summary>
/// Owns the original PE/PDB and Roslyn's chained EmitBaseline for one assembly. Keep it alive for the whole run;
/// advance it only after ApplyUpdate succeeds. Instances are serialized by <see cref="HotReloadSession"/>.
/// </summary>
public sealed class HotReloadBaseline : IDisposable
{
    // Roslyn's portable custom-debug-info identifiers (dotnet/roslyn, PortableCustomDebugInfoKinds.cs).
    private static readonly Guid LocalSlots = new("755F52A8-91C5-45BE-B4B8-209571E552BD");
    private static readonly Guid Lambdas = new("A643004C-0240-496F-A783-30D64F4979DE");
    private static readonly Guid States = new("8B78CD68-2EDE-420B-980B-E15884B8AAA3");
    private readonly ModuleMetadata _module;
    private readonly PEReader _pe;
    private readonly MetadataReaderProvider _pdb;
    private EmitBaseline _baseline;
    private Microsoft.CodeAnalysis.Compilation _compilation;
    private int _generation;

    /// <summary>Creates the baseline from the exact Debug compilation and the image that was loaded.</summary>
    /// <param name="assembly">A successful local Debug build, with its original compilation retained.</param>
    public HotReloadBaseline(CompiledAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _compilation = assembly.HotReloadCompilation ?? throw new ArgumentException("Hot Reload needs a local Debug build.", nameof(assembly));
        _module = ModuleMetadata.CreateFromImage(assembly.Image);
        _pe = new PEReader(ImmutableArray.Create(assembly.Image));
        _pdb = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(assembly.Symbols ?? throw new ArgumentException("Hot Reload needs portable symbols.", nameof(assembly))));
        _baseline = EmitBaseline.CreateInitialBaseline(_compilation, _module, ReadDebugInfo, ReadLocalSignature, hasPortableDebugInformation: true);
    }

    /// <summary>Analyzes edits and emits deltas; this never calls ApplyUpdate or advances the baseline.</summary>
    /// <param name="compilation">The edited compilation, including the same generators/instrumentation.</param>
    /// <param name="delta">The delta when there are supported changes, otherwise null.</param>
    /// <param name="cancellationToken">Cancels analysis and emission before the runtime is touched.</param>
    public HotReloadResult EmitDifference(Microsoft.CodeAnalysis.Compilation compilation, out HotReloadDelta? delta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        delta = null;
        var errors = DiagnosticMapper.Map(compilation.GetDiagnostics(cancellationToken), compilation.AssemblyName)
            .Where(d => d.Severity == Core.Diagnostics.DiagnosticSeverityLevel.Error).ToList();
        errors.AddRange(InProcessSafetyAnalyzer.Analyze(compilation, compilation.AssemblyName, cancellationToken)
            .Where(d => d.Severity == Core.Diagnostics.DiagnosticSeverityLevel.Error));
        if (errors.Count != 0)
        {
            return new(HotReloadStatus.CompilationErrors, "Fix the compilation errors before applying edits.", errors);
        }

        var (edits, rudeEdit) = HotReloadEdits.Analyze(_compilation, compilation, cancellationToken);
        if (rudeEdit is not null)
        {
            return new(HotReloadStatus.RestartRequired, rudeEdit, []);
        }

        if (edits.Count == 0)
        {
            return new(HotReloadStatus.NoChanges, compilation.Language == LanguageNames.VisualBasic ? "No executable Visual Basic changes to apply." : "No executable C# changes to apply.", []);
        }

        using var metadata = new MemoryStream();
        using var il = new MemoryStream();
        using var pdb = new MemoryStream();
        var result = compilation.EmitDifference(_baseline, edits, _ => false, metadata, il, pdb, cancellationToken);
        if (!result.Success)
        {
            return new(HotReloadStatus.RestartRequired, "Roslyn cannot apply these edits. Restart the app to use them.",
                DiagnosticMapper.Map(result.Diagnostics, compilation.AssemblyName));
        }

        delta = new HotReloadDelta(metadata.ToArray(), il.ToArray(), pdb.ToArray(), compilation, result.Baseline!, _generation, this);
        return new(HotReloadStatus.Applied, compilation.Language == LanguageNames.VisualBasic ? "Visual Basic edits are ready to apply." : "C# edits are ready to apply.", []);
    }

    /// <summary>Commits a prepared delta after the runtime accepted it.</summary>
    /// <param name="delta">The delta from this baseline's current generation.</param>
    public void Accept(HotReloadDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (delta.Generation != _generation || !ReferenceEquals(delta.Owner, this))
        {
            throw new InvalidOperationException("The Hot Reload delta is stale or belongs to another module.");
        }

        _compilation = delta.Compilation;
        _baseline = delta.Baseline;
        _generation++;
    }

    private EditAndContinueMethodDebugInformation ReadDebugInfo(MethodDefinitionHandle method)
    {
        var reader = _pdb.GetMetadataReader();
        ImmutableArray<byte> Read(Guid kind)
        {
            foreach (var handle in reader.GetCustomDebugInformation(method))
            {
                var info = reader.GetCustomDebugInformation(handle);
                if (reader.GetGuid(info.Kind) == kind)
                {
                    return ImmutableArray.Create(reader.GetBlobBytes(info.Value));
                }
            }

            return [];
        }

        return EditAndContinueMethodDebugInformation.Create(Read(LocalSlots), Read(Lambdas), Read(States));
    }

    private StandaloneSignatureHandle ReadLocalSignature(MethodDefinitionHandle method)
    {
        var definition = _pe.GetMetadataReader().GetMethodDefinition(method);
        return definition.RelativeVirtualAddress == 0 ? default : _pe.GetMethodBody(definition.RelativeVirtualAddress).LocalSignature;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _pdb.Dispose();
        _pe.Dispose();
        _module.Dispose();
    }
}
