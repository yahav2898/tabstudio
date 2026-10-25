using TabStudio.Core.Workspaces;

namespace TabStudio.Packages.Uno;

/// <summary>
/// The packages Uno.Sdk adds to a project by itself, for the target TabStudio builds Uno apps for: Uno's Skia
/// renderer, run by TabStudio's own host, as on Uno's desktop target. They follow Uno.Sdk's
/// <c>Uno.Implicit.Packages.ProjectSystem*.targets</c> and its <c>UnoFeatures</c>, without the packages a device does not
/// use: build tools (Uno.Resizetizer, Uno.Sdk.Extras), the IDE's (Hot Design, the dev server, Uno's MCP client) and
/// the desktop hosts with their native libraries, which TabStudio's host replaces.
/// </summary>
/// <remarks>
/// A WinUI 3 project gets the same packages as an Uno app without features, in place of the Windows App SDK packages
/// it references, which only Windows can run.
/// </remarks>
public static class UnoImplicitPackages
{
    /// <summary>The packages of a WinUI 3 project that only Windows can use, which Uno Platform's packages replace.</summary>
    public static IReadOnlySet<string> WindowsOnlyPackages { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        UnoProjects.WindowsAppSdkPackage,
        "Microsoft.WindowsAppSDK.Base",
        "Microsoft.WindowsAppSDK.Foundation",
        "Microsoft.WindowsAppSDK.WinUI",
        "Microsoft.WindowsAppSDK.Runtime",
        "Microsoft.Windows.SDK.BuildTools",
        "Microsoft.Windows.SDK.BuildTools.WinApp",
        "Microsoft.Windows.CsWinRT",
        "Microsoft.Windows.CsWin32",
    };

    /// <summary>The features whose packages draw with SkiaSharp's views, as Uno.Sdk adds them.</summary>
    private static readonly string[] SkiaViewsFeatures = ["skia", "lottie", "svg", "material", "cupertino", "simpletheme", "skiarenderer"];

    /// <summary>The packages Uno.Sdk would add, with their versions from its package list.</summary>
    /// <param name="project">An Uno.Sdk or WinUI 3 project.</param>
    /// <param name="manifest">The package list of its Uno.Sdk version.</param>
    /// <returns>The packages, without those the project references itself.</returns>
    public static IReadOnlyList<PackageReferenceInfo> For(ProjectInfo project, UnoSdkManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(manifest);

        var features = UnoProjects.UsesUnoSdk(project) ? UnoProjects.GetFeatures(project) : new HashSet<string>(StringComparer.Ordinal);
        bool Has(string feature) => features.Contains(feature);
        var head = project.IsExecutable;

        var ids = new List<string> { "Uno.WinUI" };
        if (head)
        {
            ids.Add("Uno.UI.Adapter.Microsoft.Extensions.Logging");
            ids.Add("Microsoft.Extensions.Logging.Console");
            if (UsesOpenSans(project))
            {
                ids.Add("Uno.Fonts.OpenSans");
            }
        }

        if (SkiaViewsFeatures.Any(Has))
        {
            ids.Add("SkiaSharp.Views.Uno.WinUI");
        }

        if (Has("skia") || Has("skiarenderer"))
        {
            ids.Add("Uno.WinUI.Graphics2DSK");
        }

        if (Has("lottie") || Has("material") || Has("cupertino") || Has("simpletheme") || Has("skiarenderer"))
        {
            ids.Add("Uno.WinUI.Lottie");
            ids.Add("SkiaSharp.Skottie");
        }

        if (Has("svg"))
        {
            ids.Add("Uno.WinUI.Svg");
            if (head)
            {
                ids.Add("Svg.Skia");
            }
        }

        if (Has("csharpmarkup"))
        {
            ids.Add("Uno.WinUI.Markup");
            ids.Add("Uno.Extensions.Markup.Generators");
        }

        AddThemes(ids, Has);
        AddExtensions(ids, Has);
        if (Has("mvvm"))
        {
            ids.Add("CommunityToolkit.Mvvm");
        }

        if (Has("prism"))
        {
            ids.Add(head ? "Prism.DryIoc.Uno.WinUI" : "Prism.Uno.WinUI");
            if (Has("csharpmarkup"))
            {
                ids.Add("Prism.Uno.WinUI.Markup");
            }
        }

        return ids
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id => !project.HasPackage(id))
            .Select(id => manifest.GetVersion(id, project) is { } version ? new PackageReferenceInfo(id, version) : null)
            .OfType<PackageReferenceInfo>()
            .ToList();
    }

    /// <summary>Whether the app's default font is Open Sans, Uno.Sdk's default, which <c>UnoDefaultFont</c> can change.</summary>
    /// <param name="project">The project.</param>
    public static bool UsesOpenSans(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.GetProperty("UnoDefaultFont") is not { Length: > 0 } font || font.Trim().Equals("OpenSans", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddThemes(List<string> ids, Func<string, bool> has)
    {
        var markup = has("csharpmarkup");
        if (has("toolkit"))
        {
            ids.Add("Uno.Toolkit.WinUI");
            AddIf(ids, markup, "Uno.Toolkit.WinUI.Markup");
            AddIf(ids, has("skia"), "Uno.Toolkit.Skia.WinUI");
            AddIf(ids, has("cupertino"), "Uno.Toolkit.WinUI.Cupertino");
            AddIf(ids, has("material"), "Uno.Toolkit.WinUI.Material");
            AddIf(ids, has("material") && markup, "Uno.Toolkit.WinUI.Material.Markup");
            AddIf(ids, has("simpletheme"), "Uno.Toolkit.WinUI.Simple");
        }

        if (has("material"))
        {
            ids.Add("Uno.Material.WinUI");
            AddIf(ids, markup, "Uno.Material.WinUI.Markup");
            AddIf(ids, markup, "Uno.Themes.WinUI.Markup");
        }

        AddIf(ids, has("cupertino"), "Uno.Cupertino.WinUI");
        if (has("simpletheme"))
        {
            ids.Add("Uno.Simple.WinUI");
            AddIf(ids, markup, "Uno.Simple.WinUI.Markup");
            AddIf(ids, markup, "Uno.Themes.WinUI.Markup");
        }
    }

    /// <summary>Uno.Extensions, as <c>Uno.Extensions.Implicit.Packages.ProjectSystem.targets</c> adds it.</summary>
    private static void AddExtensions(List<string> ids, Func<string, bool> has)
    {
        var markup = has("csharpmarkup");
        AddIf(ids, has("authenticationmsal"), "Uno.Extensions.Authentication.MSAL.WinUI");
        AddIf(ids, has("authenticationoidc"), "Uno.Extensions.Authentication.Oidc.WinUI");
        AddIf(ids, has("authentication") || has("authenticationmsal") || has("authenticationoidc"), "Uno.Extensions.Authentication.WinUI");
        AddIf(ids, has("configuration"), "Uno.Extensions.Configuration");
        AddIf(ids, has("extensionscore") || has("themeservice"), "Uno.Extensions.Core.WinUI");
        AddIf(ids, has("hosting"), "Uno.Extensions.Hosting.WinUI");
        AddIf(ids, has("http") || has("httpkiota") || has("httprefit"), "Uno.Extensions.Http.WinUI");
        AddIf(ids, has("httpkiota"), "Uno.Extensions.Http.Kiota");
        AddIf(ids, has("httprefit"), "Uno.Extensions.Http.Refit");
        AddIf(ids, has("localization"), "Uno.Extensions.Localization.WinUI");
        AddIf(ids, has("logging"), "Uno.Extensions.Logging.WinUI");
        AddIf(ids, has("loggingserilog"), "Uno.Extensions.Logging.Serilog");
        if (has("navigation") || has("prism"))
        {
            ids.Add("Uno.Extensions.Navigation.WinUI");
            AddIf(ids, markup, "Uno.Extensions.Navigation.WinUI.Markup");
            AddIf(ids, has("toolkit"), "Uno.Extensions.Navigation.Toolkit.WinUI");
        }

        if (has("mvux"))
        {
            ids.Add("Uno.Extensions.Reactive.WinUI");
            ids.Add("Uno.Extensions.Reactive.Messaging");
            AddIf(ids, markup, "Uno.Extensions.Reactive.WinUI.Markup");
        }

        if (has("serialization"))
        {
            ids.Add("Uno.Extensions.Serialization.Http");
            AddIf(ids, has("httprefit"), "Uno.Extensions.Serialization.Refit");
        }

        AddIf(ids, has("storage"), "Uno.Extensions.Storage.WinUI");
    }

    private static void AddIf(List<string> ids, bool condition, string id)
    {
        if (condition)
        {
            ids.Add(id);
        }
    }
}
