using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Services;
using TabStudio.Web.Templates;

namespace TabStudio.App.ViewModels;

/// <summary>A choice of where a new Blazor Web App's components run, as Visual Studio's "Interactive render mode" offers it.</summary>
/// <param name="Interactivity">Where the components run.</param>
/// <param name="Name">The choice, as Visual Studio names it.</param>
/// <param name="Description">What it means for the app.</param>
public sealed record BlazorRenderModeOption(BlazorInteractivity Interactivity, string Name, string Description)
{
    /// <summary>The choices, the default first.</summary>
    public static IReadOnlyList<BlazorRenderModeOption> All { get; } =
    [
        new(BlazorInteractivity.Server, "Server", "Components run on the tablet's server, over a live connection to the page."),
        new(BlazorInteractivity.WebAssembly, "WebAssembly", "Components run in the browser on .NET for WebAssembly, from a .Client project."),
        new(BlazorInteractivity.Auto, "Auto (Server and WebAssembly)", "Components start on the server and move to WebAssembly once it has downloaded."),
        new(BlazorInteractivity.None, "None", "Pages are rendered on the server, without interactivity."),
    ];
}

/// <summary>The start screen's option for the Blazor Web App template.</summary>
public sealed partial class StartViewModel
{
    /// <summary>Where a new Blazor Web App's components run, Server unless the user picks another.</summary>
    [ObservableProperty]
    public partial BlazorRenderModeOption SelectedBlazorRenderMode { get; set; } = BlazorRenderModeOption.All[0];

    /// <summary>Whether the Blazor Web App template is selected, which shows its render mode choice.</summary>
    public bool IsBlazorWebAppSelected => SelectedTemplate.Id == WebTemplates.BlazorWebAppId;

    /// <summary>The template to create: the selected one, with the Blazor Web App's render mode.</summary>
    private string TemplateIdToCreate => IsBlazorWebAppSelected
        ? WebTemplates.BlazorWebAppIdFor(SelectedBlazorRenderMode.Interactivity)
        : SelectedTemplate.Id;

    partial void OnSelectedTemplateChanged(ProjectTemplate value) => OnPropertyChanged(nameof(IsBlazorWebAppSelected));
}
