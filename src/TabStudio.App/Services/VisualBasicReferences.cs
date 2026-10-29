using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TabStudio.App.Services;

/// <summary>
/// The assemblies every Visual Basic project compiles against beside the framework: the reference assembly of
/// TabStudio's Microsoft.VisualBasic.Forms, which has the base classes of <c>My.Application</c>, <c>My.Computer</c>
/// and <c>My.User</c> and the Windows Forms application framework, with its documentation for IntelliSense. The app
/// carries them as resources; a development build without them uses the assembly itself.
/// </summary>
public static class VisualBasicReferences
{
    private const string ImageResource = "VisualBasic/Microsoft.VisualBasic.Forms.dll";
    private const string DocumentationResource = "VisualBasic/Microsoft.VisualBasic.Forms.xml";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> LazyReferences = new(Load);

    /// <summary>The references, loaded once. Loading them also lets programs find the assembly at run time.</summary>
    public static IReadOnlyList<MetadataReference> References => LazyReferences.Value;

    private static IReadOnlyList<MetadataReference> Load()
    {
        // Every Visual Basic program is compiled before it runs, so this is early enough for its run.
        VisualBasicRuntime.Register();

        var assembly = typeof(VisualBasicReferences).Assembly;
        var location = VisualBasicRuntime.Shim.Location;
        var image = Read(assembly.GetManifestResourceStream(ImageResource)) ?? ReadFile(location);
        if (image is null)
        {
            return [];
        }

        var documentation = Read(assembly.GetManifestResourceStream(DocumentationResource))
            ?? (location.Length > 0 ? ReadFile(Path.ChangeExtension(location, ".xml")) : null);
        var provider = documentation is null ? null : XmlDocumentationProvider.CreateFromBytes(documentation);
        return [MetadataReference.CreateFromImage(ImmutableArray.Create(image), documentation: provider, filePath: "Microsoft.VisualBasic.Forms.dll")];
    }

    private static byte[]? Read(Stream? stream)
    {
        if (stream is null)
        {
            return null;
        }

        using (stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    private static byte[]? ReadFile(string path) => path.Length > 0 && File.Exists(path) ? File.ReadAllBytes(path) : null;
}
