using System.Xml.Linq;
using TabStudio.App.Previews;

namespace TabStudio.App.Tests.ViewModels;

public sealed class RuntimeXamlIncludeTests
{
    [Fact]
    public void Local_includes_expand_recursively_and_external_package_styles_stay_includes()
    {
        var files = new Dictionary<string, string>
        {
            ["/Themes/One.axaml"] = "<Styles xmlns=\"https://github.com/avaloniaui\"><StyleInclude Source=\"Two.axaml\" /></Styles>",
            ["/Themes/Two.axaml"] = "<Styles xmlns=\"https://github.com/avaloniaui\"><Style Selector=\"Button\"><Setter Property=\"Opacity\" Value=\"0.5\" /></Style></Styles>",
        };
        var text = RuntimeXamlIncludes.Expand("<Application xmlns=\"https://github.com/avaloniaui\"><Application.Styles><StyleInclude Source=\"/Themes/One.axaml\"/><StyleInclude Source=\"avares://Package/Theme.axaml\"/></Application.Styles></Application>",
            new Uri("avares://App/App.axaml"), uri => uri.Host.Equals("App", StringComparison.OrdinalIgnoreCase) ? files.GetValueOrDefault(uri.AbsolutePath) : null);
        var result = XDocument.Parse(text);
        Assert.DoesNotContain(result.Root!.Descendants().Attributes(), a => a.IsNamespaceDeclaration);
        Assert.Single(result.Descendants(), e => e.Name.LocalName == "Style");
        Assert.Single(result.Descendants(), e => e.Name.LocalName == "StyleInclude");
    }

    [Fact]
    public void Namespace_prefix_collisions_keep_each_included_selector_bound_to_its_own_type()
    {
        var text = RuntimeXamlIncludes.Expand("<Application xmlns=\"https://github.com/avaloniaui\" xmlns:local=\"using:App\"><Application.Styles><StyleInclude Source=\"/Theme.axaml\"/></Application.Styles></Application>",
            new Uri("avares://App/App.axaml"), _ => "<Styles xmlns=\"https://github.com/avaloniaui\" xmlns:local=\"using:Controls\"><Style Selector=\"local|Rating\" /></Styles>");
        var result = XDocument.Parse(text);
        var selector = result.Descendants().Single(e => e.Name.LocalName == "Style").Attribute("Selector")!.Value;
        var prefix = selector.Split('|')[0];
        Assert.Equal("using:Controls", result.Root!.GetNamespaceOfPrefix(prefix)?.NamespaceName);
        Assert.Equal("using:App", result.Root.GetNamespaceOfPrefix("local")?.NamespaceName);
        Assert.DoesNotContain(result.Root.Descendants().Attributes(), a => a.IsNamespaceDeclaration);
    }

    [Fact]
    public void Cycles_report_the_included_file()
    {
        const string xaml = "<Styles xmlns=\"https://github.com/avaloniaui\"><StyleInclude Source=\"/Loop.axaml\" /></Styles>";
        var exception = Assert.Throws<InvalidOperationException>(() => RuntimeXamlIncludes.Expand(xaml, new Uri("avares://App/Loop.axaml"), _ => xaml));
        Assert.Contains("Loop.axaml", exception.Message, StringComparison.Ordinal);
    }
}
