using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TabStudio.App.Services;

/// <summary>
/// The assemblies Windows Forms projects compile against: the reference assembly of TabStudio's Forms layer, which
/// has the API of System.Windows.Forms and System.Drawing, with its documentation for IntelliSense. The app carries
/// them as resources; a development build without them uses the Forms layer's own assembly.
/// </summary>
public static class WindowsFormsReferences
{
    private const string ImageResource = "WindowsForms/TabStudio.Forms.dll";
    private const string DocumentationResource = "WindowsForms/TabStudio.Forms.xml";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> LazyReferences = new(Load);

    /// <summary>The references, loaded once.</summary>
    public static IReadOnlyList<MetadataReference> References => LazyReferences.Value;

    private static IReadOnlyList<MetadataReference> Load()
    {
        var assembly = typeof(WindowsFormsReferences).Assembly;
        var image = Read(assembly.GetManifestResourceStream(ImageResource)) ?? ReadFile(typeof(System.Windows.Forms.Form).Assembly.Location);
        if (image is null)
        {
            return [];
        }

        var documentation = Read(assembly.GetManifestResourceStream(DocumentationResource))
            ?? ReadFile(Path.ChangeExtension(typeof(System.Windows.Forms.Form).Assembly.Location, ".xml"));
        var provider = documentation is null ? null : XmlDocumentationProvider.CreateFromBytes(documentation);
        return [MetadataReference.CreateFromImage(ImmutableArray.Create(image), documentation: provider, filePath: "TabStudio.Forms.dll")];
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
