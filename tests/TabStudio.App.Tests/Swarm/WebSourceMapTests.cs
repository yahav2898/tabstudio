using TabStudio.App.Services;
using TabStudio.App.Swarm.Web;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Swarm;

/// <summary>A web screen's element paths lead to the elements in the web template's page, and in pages like it.</summary>
public sealed class WebSourceMapTests : IDisposable
{
    private static readonly Uri Home = new("http://127.0.0.1:5000/");

    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData("body/main[0]/form#form/button[0]", "<button type=\"submit\">Greet</button>")]
    [InlineData("body/main[0]/form#form/input#name", "<input id=\"name\" placeholder=\"Your name\" autocomplete=\"off\" />")]
    [InlineData("body/main[0]/h1[0]", "<h1>Shop</h1>")]
    [InlineData("body/main[0]/div#answer", "<div id=\"answer\"></div>")]
    public void An_element_of_the_template_s_page_is_found_on_its_line(string path, string line)
    {
        var project = WebProject();
        var page = Path.Combine(project, "wwwroot", "index.html");

        var found = WebSourceMap.Find(project, Home, path);

        Assert.NotNull(found);
        Assert.Equal(page, found.FilePath);
        var lines = File.ReadAllLines(page);
        Assert.Equal(line, lines[found.Line - 1].Trim());
        Assert.Equal(lines[found.Line - 1].IndexOf('<', StringComparison.Ordinal) + 1, found.Column);
        Assert.StartsWith(line.Split(' ', '>')[0], found.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Children_are_counted_by_tag_past_comments_scripts_and_void_elements()
    {
        _temp.Write("Site/wwwroot/list.html", """
            <html><body>
              <!-- <section>not one</section> -->
              <script>if (a <b) { document.write("<section>"); }</script>
              <section><img src="a.png"><p>One</p><br/><p class="second">Two <b>bold</b></p></section>
              <section id="last"><p>Three</p></section>
            </body></html>
            """);
        var project = Path.Combine(_temp.Path, "Site");

        var second = WebSourceMap.Find(project, new Uri("http://127.0.0.1:5000/list.html"), "body/section[0]/p[1]");
        var third = WebSourceMap.Find(project, new Uri("http://127.0.0.1:5000/list.html"), "body/section#last/p[0]");

        Assert.Equal("<p class=\"second\">Two <b>bold</b></p>", second?.Snippet);
        Assert.Equal("<p>Three</p>", third?.Snippet);
        Assert.Null(WebSourceMap.Find(project, null, "body/section[0]/p[2]"));
        Assert.Null(WebSourceMap.Find(project, null, "body/nav#missing"));
    }

    [Fact]
    public void The_page_shown_is_searched_before_the_project_s_other_pages()
    {
        _temp.Write("Site/wwwroot/about.html", """<html><body><button id="go">About</button></body></html>""");
        _temp.Write("Site/wwwroot/index.html", """<html><body><button id="go">Home</button></body></html>""");
        _temp.Write("Site/bin/Debug/wwwroot/index.html", """<html><body><button id="go">Built</button></body></html>""");
        var project = Path.Combine(_temp.Path, "Site");

        Assert.Equal("<button id=\"go\">About</button>", WebSourceMap.Find(project, new Uri("http://127.0.0.1:5000/about.html"), "body/button#go")?.Snippet);
        Assert.Equal("<button id=\"go\">Home</button>", WebSourceMap.Find(project, Home, "body/button#go")?.Snippet);
        Assert.Equal("<button id=\"go\">About</button>", WebSourceMap.Find(project, null, "body/button#go")?.Snippet);
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>The web template, as File > New makes it.</summary>
    private string WebProject()
    {
        var solution = new ProjectTemplates(new AppPaths(Path.Combine(_temp.Path, "app"))).Create(ProjectTemplates.WebId, "Shop");
        return Path.Combine(Path.GetDirectoryName(solution)!, "Shop");
    }
}
