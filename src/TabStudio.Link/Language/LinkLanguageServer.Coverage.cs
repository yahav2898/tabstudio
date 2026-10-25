using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;
using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Coverage;
using TabStudio.Link.Messaging;

namespace TabStudio.Link.Language;

/// <summary>
/// Live Unit Testing builds on the host: the test project is built with the projects it tests instrumented for the
/// tablet's coverage recorder, and the tablet runs the tests.
/// </summary>
public sealed partial class LinkLanguageServer
{
    /// <summary>The hash of each assembly sent, so an assembly the coverage builder kept is not hashed again.</summary>
    private static readonly ConditionalWeakTable<CompiledAssembly, string> AssemblyHashes = new();

    private async Task<LinkReply> BuildForCoverageAsync(LinkPayload payload, CancellationToken cancellationToken)
    {
        var request = payload.Read(LinkLanguageJson.Info<CoverageBuildRequest>());
        var project = FindProject(request.ProjectPath);
        var (backend, map) = Ready();
        var uninstrumented = request.Uninstrumented.Select(map.ToHost).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var slot in request.Slots)
        {
            if (slot.Slot >= 0 && map.ToHost(slot.ProjectPath) is { } path)
            {
                slots[path] = slot.Slot;
            }
        }

        using var activity = Begin($"Building {project.Name} for Live Unit Testing");
        var build = await backend.BuildForCoverageAsync(project, uninstrumented, slots, cancellationToken).ConfigureAwait(false);
        var attachments = new List<ReadOnlyMemory<byte>>();
        var response = EncodeCoverageBuild(build, map, request.Known.ToHashSet(StringComparer.Ordinal), attachments);
        return LinkReply.Of(response, LinkLanguageJson.Info<CoverageBuildResponse>(), attachments);
    }

    /// <summary>A coverage build on the tablet's paths, without what the tablet already has.</summary>
    /// <param name="build">The build.</param>
    /// <param name="map">The path map.</param>
    /// <param name="known">The hashes the tablet has; what is sent is added, so nothing goes twice.</param>
    /// <param name="attachments">Receives the assemblies' bytes.</param>
    private static CoverageBuildResponse EncodeCoverageBuild(CoverageBuild build, LinkPathMap map, HashSet<string> known, List<ReadOnlyMemory<byte>> attachments)
    {
        CoverageAssembly Assembly(CompiledAssembly assembly)
        {
            var hash = AssemblyHashes.GetValue(assembly, HashOf);
            if (!known.Add(hash))
            {
                return new CoverageAssembly(assembly.Name, hash, null, null);
            }

            var image = attachments.Count;
            attachments.Add(assembly.Image);
            int? symbols = null;
            if (assembly.Symbols is { } pdb)
            {
                symbols = attachments.Count;
                attachments.Add(pdb);
            }

            return new CoverageAssembly(assembly.Name, hash, image, symbols);
        }

        CoverageSource Source(string path, SourceText text)
        {
            var hash = "src-" + Convert.ToHexString(text.GetContentHash().AsSpan());
            return new CoverageSource(map.ToClient(path), hash, known.Add(hash) ? text.ToString() : null);
        }

        var result = build.Result;
        var entry = result.EntryAssembly is { } assembly ? Assembly(assembly) : null;
        var dependencies = result.Dependencies.Select(Assembly).ToList();
        var projects = build.Projects
            .Select(p => new CoverageProject(
                map.ToClient(p.Project.FilePath),
                p.IsInstrumented,
                [.. p.Sources.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => Source(s.Key, s.Value))]))
            .ToList();
        var maps = build.Modules
            .Select(m => new CoverageMap(
                map.ToClient(m.ProjectFilePath),
                m.Index,
                [.. m.Members.Select(member => member with { FilePath = map.ToClient(member.FilePath) })],
                Flatten(m.Statements),
                m.PointMembers))
            .ToList();
        return new CoverageBuildResponse(result.ProjectName, map.ToClient(result.Diagnostics), entry, dependencies, result.Duration, projects, maps);
    }

    /// <summary>Statements as point, member, ordinal and line in a row.</summary>
    private static int[] Flatten(IReadOnlyList<CoverageStatement> statements)
    {
        var values = new int[statements.Count * 4];
        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            values[i * 4] = statement.Point;
            values[(i * 4) + 1] = statement.Member;
            values[(i * 4) + 2] = statement.Ordinal;
            values[(i * 4) + 3] = statement.Line;
        }

        return values;
    }

    /// <summary>The hash of an assembly's image and PDB.</summary>
    private static string HashOf(CompiledAssembly assembly)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(assembly.Image);
        if (assembly.Symbols is { } symbols)
        {
            hash.AppendData(symbols);
        }

        return "asm-" + Convert.ToHexString(hash.GetHashAndReset().AsSpan(0, 16));
    }
}
