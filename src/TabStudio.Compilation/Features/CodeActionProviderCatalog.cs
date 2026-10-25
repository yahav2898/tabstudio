using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace TabStudio.Compilation.Features;

/// <summary>
/// The C# code fix and refactoring providers that ship in Roslyn's Features assemblies, found once by reflection.
/// </summary>
/// <remarks>
/// Visual Studio composes these providers with MEF. TabStudio instead looks for the types exported for C#
/// (<see cref="ExportCodeFixProviderAttribute"/> and <see cref="ExportCodeRefactoringProviderAttribute"/>) and
/// creates the ones with a parameterless constructor. Providers that need constructor arguments or fail to start
/// are skipped. Discovery takes a moment, so it happens on first use and is shared by every host.
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
    };

    private static readonly Lazy<CodeActionProviderCatalog> LazyInstance = new(Discover, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Dictionary<string, List<CodeFixProvider>> _fixesById;

    private CodeActionProviderCatalog(Dictionary<string, List<CodeFixProvider>> fixesById, List<CodeRefactoringProvider> refactorings)
    {
        _fixesById = fixesById;
        Refactorings = refactorings;
    }

    /// <summary>The shared catalog.</summary>
    public static CodeActionProviderCatalog Instance => LazyInstance.Value;

    /// <summary>Every C# refactoring provider.</summary>
    public IReadOnlyList<CodeRefactoringProvider> Refactorings { get; }

    /// <summary>The fix providers for a diagnostic id.</summary>
    public IReadOnlyList<CodeFixProvider> GetFixProviders(string diagnosticId)
    {
        var providers = _fixesById.GetValueOrDefault(diagnosticId) ?? [];
        if (DiagnosticAliases.TryGetValue(diagnosticId, out var alias) && _fixesById.TryGetValue(alias, out var aliased))
        {
            providers = [.. providers, .. aliased];
        }

        return providers;
    }

    private static CodeActionProviderCatalog Discover()
    {
        var fixesById = new Dictionary<string, List<CodeFixProvider>>(StringComparer.Ordinal);
        var refactorings = new List<CodeRefactoringProvider>();

        foreach (var type in FeatureAssemblies().SelectMany(LoadableTypes).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.IsAbstract || type.ContainsGenericParameters)
            {
                continue;
            }

            if (typeof(CodeFixProvider).IsAssignableFrom(type)
                && IsExportedForCSharp<ExportCodeFixProviderAttribute>(type, a => a.Languages)
                && TryCreate<CodeFixProvider>(type) is { } fix)
            {
                foreach (var id in FixableIds(fix))
                {
                    if (!fixesById.TryGetValue(id, out var list))
                    {
                        list = [];
                        fixesById[id] = list;
                    }

                    list.Add(fix);
                }
            }
            else if (typeof(CodeRefactoringProvider).IsAssignableFrom(type)
                && IsExportedForCSharp<ExportCodeRefactoringProviderAttribute>(type, a => a.Languages)
                && TryCreate<CodeRefactoringProvider>(type) is { } refactoring)
            {
                refactorings.Add(refactoring);
            }
        }

        return new CodeActionProviderCatalog(fixesById, refactorings);
    }

    private static IEnumerable<Assembly> FeatureAssemblies()
    {
        foreach (var name in new[] { "Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.CSharp.Features" })
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

    private static bool IsExportedForCSharp<TAttribute>(Type type, Func<TAttribute, string[]> languages)
        where TAttribute : Attribute =>
        type.GetCustomAttributes<TAttribute>(inherit: false).Any(a => languages(a).Contains(LanguageNames.CSharp, StringComparer.Ordinal));

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
}
