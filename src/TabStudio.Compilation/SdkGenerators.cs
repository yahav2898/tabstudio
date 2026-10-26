using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// What the .NET SDK decides about source generators before the compiler runs, which TabStudio has to decide
/// itself: which generators stay off unless a project turns them on, and which namespaces generators may put
/// interceptors in.
/// </summary>
/// <remarks>
/// It follows the SDK's <c>Microsoft.NET.Sdk.FrameworkReferenceResolution.targets</c>. ASP.NET Core's request delegate
/// generator and the configuration binding generator rewrite calls such as <c>app.MapGet</c> with interceptors;
/// the SDK only runs them for apps trimmed for publishing, or when the project asks for them.
/// </remarks>
internal static class SdkGenerators
{
    /// <summary>The generators that are off by default, by file name, with the property that turns each one on.</summary>
    private static readonly Dictionary<string, string> OffByDefault = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.AspNetCore.Http.RequestDelegateGenerator.dll"] = "EnableRequestDelegateGenerator",
        ["Microsoft.Extensions.Configuration.Binder.SourceGeneration.dll"] = "EnableConfigurationBindingGenerator",
    };

    /// <summary>The properties a project lists more interceptor namespaces in; the second is the older name.</summary>
    private static readonly string[] InterceptorsProperties = ["InterceptorsNamespaces", "InterceptorsPreviewNamespaces"];

    /// <summary>Whether a project runs an analyzer assembly, which is the case unless it is off by default and not turned on.</summary>
    /// <param name="project">The project.</param>
    /// <param name="analyzerPath">The analyzer assembly's path.</param>
    public static bool IsEnabled(TsProject project, string? analyzerPath) =>
        analyzerPath is null
        || ((!NewIn.TryGetValue(Path.GetFileName(analyzerPath), out var since) || TargetVersion(project) >= since)
            && (!OffByDefault.TryGetValue(Path.GetFileName(analyzerPath), out var property) || IsOn(project, property)));

    /// <summary>
    /// Generators of TabStudio's .NET 10 targeting packs that earlier .NET versions do not have, by file name, with the
    /// version that brought them. The SDK takes a project's generators from the targeting pack of its own framework, so
    /// a <c>net8.0</c> web app keeps an internal top-level <c>Program</c>, which ASP.NET Core 10's generator makes public.
    /// </summary>
    private static readonly Dictionary<string, int> NewIn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.AspNetCore.App.SourceGenerators.dll"] = 10,
    };

    /// <summary>The major .NET version a project targets, such as 8 for <c>net8.0</c>; 10, TabStudio's own, when it says none.</summary>
    private static int TargetVersion(TsProject project)
    {
        var framework = project.TargetFrameworks.Count > 0 ? project.TargetFrameworks[^1] : null;
        if (framework is null || !framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        var digits = new string([.. framework[3..].TakeWhile(char.IsAsciiDigit)]);
        return framework.Contains('.', StringComparison.Ordinal) && int.TryParse(digits, System.Globalization.CultureInfo.InvariantCulture, out var major) ? major : 10;
    }

    /// <summary>
    /// The namespaces a project's generators may put interceptors in: TabStudio's XAML loader's, those the SDK adds
    /// for .NET's generators and ASP.NET Core's OpenAPI package, and any the project lists itself.
    /// </summary>
    /// <param name="project">The project.</param>
    public static List<string> InterceptorsNamespaces(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        List<string> namespaces = [Xaml.AvaloniaXamlGenerator.GeneratedNamespace, "Microsoft.Extensions.Validation.Generated"];
        if (IsOn(project, "EnableRequestDelegateGenerator"))
        {
            namespaces.Add("Microsoft.AspNetCore.Http.Generated");
        }

        if (IsOn(project, "EnableConfigurationBindingGenerator"))
        {
            namespaces.Add("Microsoft.Extensions.Configuration.Binder.SourceGeneration");
        }

        if (project.HasPackage("Microsoft.AspNetCore.OpenApi"))
        {
            namespaces.Add("Microsoft.AspNetCore.OpenApi.Generated");
        }

        foreach (var property in InterceptorsProperties)
        {
            // Project files append to the property, as in "$(InterceptorsNamespaces);MyApp.Generated".
            var listed = (project.GetProperty(property) ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(name => !name.StartsWith("$(", StringComparison.Ordinal));
            namespaces.AddRange(listed);
        }

        return [.. namespaces.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Whether one of the off-by-default generators is on: the project sets its property to <c>true</c>, or leaves it
    /// unset and is trimmed for publishing, which turns them all on.
    /// </summary>
    private static bool IsOn(TsProject project, string property) => project.GetProperty(property) switch
    {
        { } value when value.Length > 0 => IsTrue(value),
        _ => IsTrue(project.GetProperty("PublishAot")) || IsTrue(project.GetProperty("PublishTrimmed")),
    };

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
