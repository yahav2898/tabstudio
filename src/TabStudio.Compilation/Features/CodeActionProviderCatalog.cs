using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace TabStudio.Compilation.Features;

/// <summary>
/// The C# and Visual Basic code fix and refactoring providers that ship in Roslyn's Features assemblies, found once
/// by reflection.
/// </summary>
/// <remarks>
/// Visual Studio composes these providers with MEF. TabStudio instead looks for the types exported for C# or Visual
/// Basic (<see cref="ExportCodeFixProviderAttribute"/> and <see cref="ExportCodeRefactoringProviderAttribute"/>) and
/// creates the ones with a parameterless constructor. A provider exported for both languages is created once and
/// serves both. Providers that need constructor arguments or fail to start are skipped. Discovery takes a moment, so
/// it happens on first use and is shared by every host.
/// </remarks>
internal sealed class CodeActionProviderCatalog
{
    /// <summary>
    /// Compiler diagnostics that Roslyn's IDE analyzers re-report under their own ids before a fix can use them.
    /// The fixes do not depend on the id, so the compiler's diagnostic is passed on as it is.
    /// </summary>
    private static readonly Dictionary<string, string> DiagnosticAliases = new(StringComparer.Ordinal)
    {
        ["CS8019"] = "RemoveUnnecessaryImportsFixable", // Unnecessary using directive.
        ["CS0105"] = "RemoveUnnecessaryImportsFixable", // Duplicate using directive.
        ["BC50000"] = "RemoveUnnecessaryImportsFixable", // Unused Imports clause.
        ["BC50001"] = "RemoveUnnecessaryImportsFixable", // Unused Imports statement.
    };

    /// <summary>The languages whose providers are collected.</summary>
    private static readonly string[] Languages = [LanguageNames.CSharp, LanguageNames.VisualBasic];

    private static readonly Lazy<CodeActionProviderCatalog> LazyInstance = new(Discover, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Dictionary<string, LanguageProviders> _languages;

    private CodeActionProviderCatalog(Dictionary<string, LanguageProviders> languages) => _languages = languages;

    /// <summary>The shared catalog.</summary>
    public static CodeActionProviderCatalog Instance => LazyInstance.Value;

    /// <summary>Every refactoring provider of a language.</summary>
    /// <param name="language">The document's language, such as <see cref="LanguageNames.VisualBasic"/>.</param>
    public IReadOnlyList<CodeRefactoringProvider> GetRefactorings(string language) =>
        _languages.TryGetValue(language, out var providers) ? providers.Refactorings : [];

    /// <summary>The fix providers of a language for a diagnostic id.</summary>
    /// <param name="language">The document's language, such as <see cref="LanguageNames.VisualBasic"/>.</param>
    /// <param name="diagnosticId">The diagnostic, such as <c>CS0246</c> or <c>BC30002</c>.</param>
    public IReadOnlyList<CodeFixProvider> GetFixProviders(string language, string diagnosticId)
    {
        if (!_languages.TryGetValue(language, out var languageProviders))
        {
            return [];
        }

        var fixesById = languageProviders.FixesById;
        var providers = fixesById.GetValueOrDefault(diagnosticId) ?? [];
        if (DiagnosticAliases.TryGetValue(diagnosticId, out var alias) && fixesById.TryGetValue(alias, out var aliased))
        {
            providers = [.. providers, .. aliased];
        }

        return providers;
    }

    private static CodeActionProviderCatalog Discover()
    {
        var languages = Languages.ToDictionary(l => l, _ => new LanguageProviders(), StringComparer.Ordinal);

        foreach (var type in FeatureAssemblies().SelectMany(LoadableTypes).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.IsAbstract || type.ContainsGenericParameters)
            {
                continue;
            }

            if (typeof(CodeFixProvider).IsAssignableFrom(type))
            {
                var exportedFor = ExportedLanguages<ExportCodeFixProviderAttribute>(type, a => a.Languages);
                if (exportedFor.Count == 0 || TryCreate<CodeFixProvider>(type) is not { } fix)
                {
                    continue;
                }

                var ids = FixableIds(fix);
                foreach (var language in exportedFor)
                {
                    var fixesById = languages[language].FixesById;
                    foreach (var id in ids)
                    {
                        if (!fixesById.TryGetValue(id, out var list))
                        {
                            list = [];
                            fixesById[id] = list;
                        }

                        list.Add(fix);
                    }
                }
            }
            else if (typeof(CodeRefactoringProvider).IsAssignableFrom(type))
            {
                var exportedFor = ExportedLanguages<ExportCodeRefactoringProviderAttribute>(type, a => a.Languages);
                if (exportedFor.Count == 0 || TryCreate<CodeRefactoringProvider>(type) is not { } refactoring)
                {
                    continue;
                }

                foreach (var language in exportedFor)
                {
                    languages[language].Refactorings.Add(refactoring);
                }
            }
        }

        return new CodeActionProviderCatalog(languages);
    }

    private static IEnumerable<Assembly> FeatureAssemblies()
    {
        foreach (var name in new[] { "Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.CSharp.Features", "Microsoft.CodeAnalysis.VisualBasic.Features" })
        {
            Assembly? assembly;
            try
            {
                assembly = Assembly.Load(name);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                assembly = null;
            }

            if (assembly is not null)
            {
                yield return assembly;
            }
        }
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    /// <summary>The languages, of those TabStudio serves, that a provider type is exported for.</summary>
    private static List<string> ExportedLanguages<TAttribute>(Type type, Func<TAttribute, string[]> languages)
        where TAttribute : Attribute =>
        type.GetCustomAttributes<TAttribute>(inherit: false)
            .SelectMany(languages)
            .Where(l => Languages.Contains(l, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

#pragma warning disable CA1031 // A provider that cannot be created or queried is skipped, whatever the reason.
    private static T? TryCreate<T>(Type type)
        where T : class
    {
        if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) is null)
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(type, nonPublic: true) as T;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<string> FixableIds(CodeFixProvider provider)
    {
        try
        {
            return provider.FixableDiagnosticIds.Distinct(StringComparer.Ordinal).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
#pragma warning restore CA1031

    /// <summary>The providers of one language.</summary>
    private sealed class LanguageProviders
    {
        /// <summary>The fix providers by the diagnostic id they fix.</summary>
        public Dictionary<string, List<CodeFixProvider>> FixesById { get; } = new(StringComparer.Ordinal);

        /// <summary>The refactoring providers.</summary>
        public List<CodeRefactoringProvider> Refactorings { get; } = [];
    }
}
