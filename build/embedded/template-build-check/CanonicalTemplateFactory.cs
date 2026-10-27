namespace TabStudio.App.Services;

/// <summary>The canonical generator's metadata shape, without starting Avalonia or an Android application.</summary>
public sealed record ProjectTemplate(string Id, string Name, string Description, IReadOnlyList<string> Tags);

/// <summary>Runs the exact linked app generator inside a host-only build proof.</summary>
public sealed partial class ProjectTemplates
{
    /// <summary>The same embedded descriptors used by the app's start screen.</summary>
    public static IReadOnlyList<ProjectTemplate> CanonicalEmbeddedTemplates => EmbeddedTemplates.ToArray();

    /// <summary>Creates exactly the app's generated embedded files in an already selected empty directory.</summary>
    public static string CreateForVerification(string id, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        return CreateEmbeddedProject(id, directory, name) ?? throw new ArgumentException("Unknown embedded template: " + id, nameof(id));
    }
}
