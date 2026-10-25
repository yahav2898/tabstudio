using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// A Blazor Web App for the tests: an Interactive Server page using a component with parameters and an EventCallback,
/// bound with <c>@bind-Count</c>, and the component's scoped CSS.
/// </summary>
internal static class CountersApp
{
    public const string Counter = """
        <button class="counter" @onclick="IncrementAsync">@Label: @Count</button>

        @code {
            [Parameter] public string Label { get; set; } = "Count";

            [Parameter] public int Count { get; set; }

            [Parameter] public EventCallback<int> CountChanged { get; set; }

            private async Task IncrementAsync()
            {
                Count++;
                await CountChanged.InvokeAsync(Count);
            }
        }
        """;

    public const string Home = """
        @page "/"
        @rendermode InteractiveServer

        <h1>Clicks</h1>
        <Counter Label="Clicks" @bind-Count="clicks" />
        <p role="status">Total: @clicks</p>

        @code {
            private int clicks = 3;
        }
        """;

    /// <summary>A Blazor Web App with an Interactive Server page and a component with parameters and an EventCallback.</summary>
    public static string Write(TempDirectory temp)
    {
        var project = temp.Write("Counters/Counters.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>
              </PropertyGroup>
            </Project>
            """);
        temp.Write("Counters/Program.cs", """
            using Counters.Components;

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();

            var app = builder.Build();
            app.UseAntiforgery();
            app.MapStaticAssets();
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
            app.Run();
            """);
        temp.Write("Counters/Components/_Imports.razor", """
            @using Microsoft.AspNetCore.Components.Routing
            @using Microsoft.AspNetCore.Components.Web
            @using static Microsoft.AspNetCore.Components.Web.RenderMode
            @using Counters.Components
            """);
        temp.Write("Counters/Components/App.razor", """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <base href="/" />
                <link rel="stylesheet" href="@Assets["Counters.styles.css"]" />
                <ImportMap />
                <HeadOutlet />
            </head>
            <body>
                <Routes />
                <script src="@Assets["_framework/blazor.web.js"]"></script>
            </body>
            </html>
            """);
        temp.Write("Counters/Components/Routes.razor", """
            <Router AppAssembly="typeof(Program).Assembly">
                <Found Context="routeData">
                    <RouteView RouteData="routeData" />
                </Found>
            </Router>
            """);
        temp.Write("Counters/Components/Counter.razor", Counter);
        temp.Write("Counters/Components/Counter.razor.css", ".counter { font-weight: bold; }\n");
        temp.Write("Counters/Components/Home.razor", Home);
        return project;
    }
}
