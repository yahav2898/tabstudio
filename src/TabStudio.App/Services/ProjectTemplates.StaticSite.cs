using System.Net;

namespace TabStudio.App.Services;

/// <summary>The Static Web Site template: <c>index.html</c>, <c>styles.css</c> and <c>script.js</c>.</summary>
public sealed partial class ProjectTemplates
{
    /// <summary>Writes a static web site into its folder, which is the workspace.</summary>
    /// <param name="root">The new folder.</param>
    /// <param name="name">The site's name, for its title.</param>
    private static void WriteStaticSite(string root, string name)
    {
        var title = WebUtility.HtmlEncode(name);
        File.WriteAllText(Path.Combine(root, "index.html"), $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{title}}</title>
              <link rel="stylesheet" href="styles.css">
            </head>
            <body>
              <!-- Press F5 to serve this site on your tablet and see it beside the code.
                   Saving a file reloads the page; saving styles.css applies it without a reload. -->
              <main class="card">
                <h1>{{title}}</h1>
                <p>A static web site made with TabStudio: HTML, CSS and JavaScript.</p>
                <button id="greet" type="button">Say hello</button>
                <p id="greeting" class="greeting" aria-live="polite"></p>
              </main>
              <script src="script.js"></script>
            </body>
            </html>

            """);
        File.WriteAllText(Path.Combine(root, "styles.css"), """
            :root {
              --accent: #0e639c;
              --surface: #ffffff;
              --text: #1e1e1e;
            }

            body {
              margin: 0;
              min-height: 100vh;
              display: grid;
              place-items: center;
              font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
              color: var(--text);
              background: linear-gradient(135deg, #e8f0fe, #f6f8fa);
            }

            .card {
              max-width: 28rem;
              padding: 2rem 2.5rem;
              border-radius: 12px;
              background: var(--surface);
              box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
            }

            h1 {
              margin-top: 0;
              color: var(--accent);
            }

            button {
              padding: 0.6rem 1.2rem;
              border: none;
              border-radius: 6px;
              font: inherit;
              color: #ffffff;
              background: var(--accent);
              cursor: pointer;
            }

            .greeting {
              min-height: 1.5em;
              font-weight: 600;
            }

            """);
        File.WriteAllText(Path.Combine(root, "script.js"), """
            // The page's script: it runs in the web preview, and in any browser that opens the site.
            const button = document.getElementById('greet');
            const greeting = document.getElementById('greeting');
            let count = 0;

            button.addEventListener('click', () => {
              count += 1;
              greeting.textContent = count === 1 ? 'Hello from TabStudio!' : `Hello again! (${count} times)`;
            });

            """);
    }
}
