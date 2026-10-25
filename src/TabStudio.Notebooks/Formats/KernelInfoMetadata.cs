using System.Text.Json.Nodes;

namespace TabStudio.Notebooks.Formats;

/// <summary>
/// Reads and writes the metadata through which Polyglot Notebooks and Jupyter say which languages a notebook
/// uses.
/// </summary>
/// <remarks>
/// Polyglot Notebooks lists its kernels in a <c>kernelInfo</c> object: at the top of a <c>.dib</c> file's
/// <c>#!meta</c> block, and under <c>polyglot_notebook</c> in an <c>.ipynb</c> file. Jupyter itself names one
/// kernel in <c>kernelspec</c>.
/// </remarks>
internal static class KernelInfoMetadata
{
    public const string KernelInfoKey = "kernelInfo";
    public const string PolyglotKey = "polyglot_notebook";
    public const string DotNetInteractiveKey = "dotnet_interactive";
    public const string KernelSpecKey = "kernelspec";
    public const string LanguageInfoKey = "language_info";
    public const string DefaultKernelNameKey = "defaultKernelName";
    public const string ItemsKey = "items";

    /// <summary>The <c>kernelInfo</c> object, from either format, or <see langword="null"/>.</summary>
    public static JsonObject? FindKernelInfo(NotebookMetadata metadata) =>
        metadata.Peek(KernelInfoKey) as JsonObject ?? metadata.Peek(PolyglotKey, KernelInfoKey) as JsonObject;

    /// <summary>The language of cells that do not name one, as a kernel name.</summary>
    public static string GetDefaultLanguage(NotebookMetadata metadata)
    {
        var name = JsonNodes.TextOf(FindKernelInfo(metadata)?[DefaultKernelNameKey])
            ?? metadata.GetText(PolyglotKey, DefaultKernelNameKey)
            ?? metadata.GetText(DotNetInteractiveKey, DefaultKernelNameKey)
            ?? NotebookLanguages.FromJupyterLanguage(metadata.GetText(KernelSpecKey, "language"))
            ?? NotebookLanguages.FromJupyterLanguage(metadata.GetText(LanguageInfoKey, "name"));

        return name is null ? NotebookLanguages.CSharp : NotebookLanguages.Normalize(name);
    }

    /// <summary>The kernel names and aliases a notebook declares, mapped to the kernel's name.</summary>
    public static Dictionary<string, string> GetDeclaredKernels(NotebookMetadata metadata)
    {
        var kernels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (FindKernelInfo(metadata)?[ItemsKey] is not JsonArray items)
        {
            return kernels;
        }

        foreach (var item in items.OfType<JsonObject>())
        {
            if (JsonNodes.TextOf(item["name"]) is not { Length: > 0 } name)
            {
                continue;
            }

            kernels[name] = name;
            foreach (var alias in JsonNodes.ReadList(item["aliases"]).Where(a => a.Length > 0))
            {
                kernels[alias] = name;
            }
        }

        return kernels;
    }

    /// <summary>Creates a <c>kernelInfo</c> object as VS Code writes it for a notebook in one language.</summary>
    public static JsonObject CreateKernelInfo(string language) => new()
    {
        [DefaultKernelNameKey] = language,
        [ItemsKey] = new JsonArray(new JsonObject { ["aliases"] = new JsonArray(), ["name"] = language }),
    };

    /// <summary>Creates a Jupyter <c>kernelspec</c> for the .NET kernel of a language.</summary>
    public static JsonObject CreateKernelSpec(string language)
    {
        var (name, displayName) = DescribeDotNetLanguage(language);
        return new JsonObject
        {
            ["display_name"] = $".NET ({displayName})",
            ["language"] = displayName,
            ["name"] = $".net-{name}",
        };
    }

    /// <summary>Creates a Jupyter <c>language_info</c> for a .NET language.</summary>
    public static JsonObject CreateLanguageInfo(string language) => DescribeDotNetLanguage(language).Name switch
    {
        NotebookLanguages.FSharp => LanguageInfo(".fs", "text/x-fsharp", "F#", "fsharp"),
        NotebookLanguages.PowerShell => LanguageInfo(".ps1", "text/x-powershell", "PowerShell", "powershell"),
        _ => LanguageInfo(".cs", "text/x-csharp", "C#", "csharp"),
    };

    private static JsonObject LanguageInfo(string extension, string mimeType, string name, string lexer) => new()
    {
        ["file_extension"] = extension,
        ["mimetype"] = mimeType,
        ["name"] = name,
        ["pygments_lexer"] = lexer,
    };

    /// <summary>The .NET kernels Jupyter knows: C#, F# and PowerShell. Anything else is described as C#.</summary>
    private static (string Name, string DisplayName) DescribeDotNetLanguage(string language) =>
        NotebookLanguages.Normalize(language) switch
        {
            NotebookLanguages.FSharp => (NotebookLanguages.FSharp, "F#"),
            NotebookLanguages.PowerShell => (NotebookLanguages.PowerShell, "PowerShell"),
            _ => (NotebookLanguages.CSharp, "C#"),
        };
}
