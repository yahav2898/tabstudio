using Avalonia.Headless.XUnit;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels.Extensions;
using TabStudio.App.Views.Extensions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Testing.Extensions;

namespace TabStudio.App.Tests.Extensions;

/// <summary>
/// The Extensions window against the fake Open VSX registry: Browse, search, an extension's details with its README
/// and features, installing with verification, Installed, Updates, Install from VSIX, enabling, disabling and
/// uninstalling, the registry setting and being offline. The screenshots show it on a landscape and a portrait tablet.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class ExtensionsWindowTests
{
    private static readonly ExtensionIdentifier Perl = new("vscode", "perl");
    private static readonly ExtensionIdentifier Lumen = new(LumenExtension.Publisher, LumenExtension.Name);

    [AvaloniaFact]
    public async Task Browse_shows_popular_extensions_and_searches_the_registry()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.OpenFolderAsync();
        var window = fixture.Show(bottomPanel: false);

        var page = fixture.Workspace.OpenExtensions()!;

        Assert.True(fixture.Workspace.ActiveDocument!.IsExtensionsPage);
        Assert.Equal("Extensions", fixture.Workspace.ActiveDocument.Title);
        await UiTestHelpers.WaitUntilAsync(() => page.BrowseResults.Count > 0 && !page.IsSearching, because: "the popular extensions");
        Assert.Equal("Popular", page.ResultsHeading);
        Assert.True(page.CanLoadMore);
        await UiTestHelpers.WaitUntilAsync(() => page.BrowseResults.Take(3).All(i => i.HasIcon), because: "the icons of the rows on screen");
        UiTestHelpers.SaveScreenshot(window, "ext-01-browse");

        page.SearchText = "perl";

        await UiTestHelpers.WaitUntilAsync(() => page.BrowseResults.Any(i => i.Id == Perl), because: "the search results");
        Assert.Contains("result", page.ResultsHeading, StringComparison.Ordinal);
        var perl = page.BrowseResults.First(i => i.Id == Perl);
        Assert.True(perl.CanInstall);
        Assert.True(perl.IsVerified);

        page.SearchText = "no-such-extension";
        await UiTestHelpers.WaitUntilAsync(() => page.HasBrowseMessage, because: "the empty search");
        Assert.Contains("No extensions match", page.BrowseMessage, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_details_page_shows_the_readme_and_what_works_and_installs_a_verified_package()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.OpenFolderAsync();
        var window = fixture.Show(bottomPanel: false);
        var page = fixture.Workspace.OpenExtensions()!;
        page.SearchText = "perl";
        await UiTestHelpers.WaitUntilAsync(() => page.BrowseResults.Any(i => i.Id == Perl), because: "the search results");

        page.SelectedItem = page.BrowseResults.First(i => i.Id == Perl);

        var details = page.Details!;
        await UiTestHelpers.WaitUntilAsync(() => details.Registry is not null && details.Readme.Count > 0 && details.Features.Count > 0, because: "the details");
        Assert.True(details.CanInstall);
        Assert.Contains(details.Readme.OfType<ReadmeTextViewModel>().SelectMany(t => t.Blocks), b => b is TabStudio.App.Notebooks.Markdown.MarkdownHeading);
        Assert.Contains(details.Features, f => f.Title == "Syntax coloring for Perl" && f.State == ExtensionFeatureState.Inactive);
        Assert.Contains(details.Info, r => r.Label == "Identifier" && r.Value == "vscode.perl");
        Assert.Contains(details.Info, r => r.Label == "License");
        UiTestHelpers.SaveScreenshot(window, "ext-02-details");

        await details.InstallCommand.ExecuteAsync(null);

        var installed = fixture.Service.Store.Find(Perl);
        Assert.NotNull(installed);
        Assert.Equal(PackageVerification.Signature, installed.Source.Verification);
        Assert.False(details.CanInstall);
        Assert.True(details.CanUninstall);
        Assert.Contains(details.Features, f => f.Title == "Syntax coloring for Perl" && f.IsActive);
        Assert.Equal("Perl", FileTypes.For("script.pl").DisplayName);
        Assert.Contains("Installed", page.Notice, StringComparison.Ordinal);

        details.SelectTabCommand.Execute(ExtensionDetailsTab.Features);
        UiTestHelpers.SaveScreenshot(window, "ext-02b-features");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Installed_lists_extensions_that_can_be_disabled_enabled_and_uninstalled()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.OpenFolderAsync();
        var window = fixture.Show(bottomPanel: false);
        var page = fixture.Workspace.OpenExtensions(ExtensionsTab.Installed)!;
        Assert.True(page.HasInstalledMessage);

        fixture.Picker.Next = LumenExtension.Build();
        await page.InstallFromFileCommand.ExecuteAsync(null);

        Assert.Equal(ExtensionsTab.Installed, page.SelectedTab);
        var item = Assert.Single(page.InstalledItems);
        Assert.Equal(LumenExtension.DisplayName, item.DisplayName);
        Assert.Equal("Set Color Theme", page.NoticeActionText);
        var details = page.Details!;
        Assert.Equal(Lumen, details.Item.Id);
        await UiTestHelpers.WaitUntilAsync(() => details.Readme.Count > 0 && !details.IsLoading, because: "the README from the package");
        Assert.Contains(details.Readme, s => s is ReadmeImageViewModel);
        Assert.True(details.CanUseColorTheme);
        Assert.Contains(details.Info, r => r.Label == "Installed from" && r.Value.Contains("lumen-1.0.0.vsix", StringComparison.Ordinal));
        UiTestHelpers.SaveScreenshot(window, "ext-03-installed");

        await page.SetEnabledAsync(Lumen, enabled: false);
        Assert.Equal("Disabled", page.InstalledItems.Single().StateText);
        Assert.Same(FileTypes.PlainText, FileTypes.For("hello.lumen"));
        Assert.True(details.CanEnable);
        Assert.All(details.Features.Where(f => !f.IsUnavailable), f => Assert.True(f.IsInactive));

        await page.SetEnabledAsync(Lumen, enabled: true);
        Assert.Equal("Lumen", FileTypes.For("hello.lumen").DisplayName);

        await page.UninstallAsync(Lumen);
        Assert.Empty(page.InstalledItems);
        Assert.Null(fixture.Service.Store.Find(Lumen));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Updates_lists_newer_versions_and_updates_them()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show(bottomPanel: false);
        fixture.Registry.LumenVersion = "1.1.0";

        var page = fixture.Workspace.OpenExtensions(ExtensionsTab.Updates)!;
        await page.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(1, page.UpdateCount);
        var update = Assert.Single(page.UpdateItems);
        Assert.True(update.CanUpdate);
        Assert.Equal("1.1.0", update.UpdateVersion);
        UiTestHelpers.SaveScreenshot(window, "ext-05-updates");

        await page.UpdateAllCommand.ExecuteAsync(null);

        Assert.Equal("1.1.0", fixture.Service.Store.Find(Lumen)!.Version);
        Assert.Equal(0, page.UpdateCount);
        Assert.Empty(page.UpdateItems);
        Assert.Contains("Updated", page.Notice, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_damaged_download_is_refused_and_an_offline_registry_is_reported()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.OpenFolderAsync();
        var window = fixture.Show(bottomPanel: false);
        var page = fixture.Workspace.OpenExtensions()!;
        page.SearchText = "lumen";
        await UiTestHelpers.WaitUntilAsync(() => page.BrowseResults.Any(i => i.Id == Lumen), because: "the search result");
        fixture.Registry.CorruptsLumenPackage = true;

        await page.InstallAsync(page.BrowseResults.First(i => i.Id == Lumen));

        Assert.True(page.IsNoticeError);
        Assert.Contains("was not installed", page.Notice, StringComparison.Ordinal);
        Assert.Null(fixture.Service.Store.Find(Lumen));

        fixture.Registry.IsOffline = true;
        page.SearchText = "monokai";
        await UiTestHelpers.WaitUntilAsync(() => page.HasBrowseMessage, because: "the offline message");
        Assert.True(page.CanRetry);
        Assert.Contains("cannot be reached", page.BrowseMessage, StringComparison.Ordinal);

        fixture.Registry.IsOffline = false;
        await page.RetryCommand.ExecuteAsync(null);
        Assert.False(page.HasBrowseMessage);
        Assert.NotEmpty(page.BrowseResults);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_registry_can_be_a_company_server()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.OpenFolderAsync();
        var page = fixture.Workspace.OpenExtensions()!;
        page.ToggleSettingsCommand.Execute(null);

        page.RegistryText = "ftp://files.example.com";
        await page.ApplyRegistryCommand.ExecuteAsync(null);
        Assert.True(page.HasRegistryError);
        Assert.True(page.IsSettingsOpen);

        page.RegistryText = "intranet.example.com/openvsx/api";
        await page.ApplyRegistryCommand.ExecuteAsync(null);

        Assert.False(page.IsSettingsOpen);
        Assert.Equal(new Uri("https://intranet.example.com/openvsx/"), fixture.Service.Client.Registry);
        Assert.Equal("https://intranet.example.com/openvsx/", fixture.Service.Settings.Registry);
        Assert.Contains("intranet.example.com", page.SearchWatermark, StringComparison.Ordinal);
        Assert.NotEmpty(page.BrowseResults);

        await page.ResetRegistryCommand.ExecuteAsync(null);
        Assert.Null(fixture.Service.Settings.Registry);
    }

    [AvaloniaFact]
    public async Task On_a_portrait_tablet_the_list_and_the_details_take_turns()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show(800, 1280, bottomPanel: false);
        var page = fixture.Workspace.OpenExtensions(ExtensionsTab.Installed)!;
        await UiTestHelpers.WaitUntilAsync(() => true);
        var view = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<ExtensionsView>().Single();
        Assert.True(view.IsNarrow);
        Assert.True(view.ListColumn.IsVisible);
        Assert.False(view.DetailsColumn.IsVisible);

        page.ShowDetails(Lumen);
        await UiTestHelpers.WaitUntilAsync(() => page.Details is { IsLoading: false, Readme.Count: > 0 }, because: "the details");

        Assert.False(view.ListColumn.IsVisible);
        Assert.True(view.DetailsColumn.IsVisible);
        Assert.True(view.BackBar.IsVisible);
        UiTestHelpers.SaveScreenshot(window, "ext-09-portrait-details");

        page.CloseDetailsCommand.Execute(null);
        Assert.True(view.ListColumn.IsVisible);
        window.Close();
    }
}
