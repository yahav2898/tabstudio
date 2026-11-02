using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace TabStudio.Compilation.Wpf;

/// <summary>
/// WPF API that OpenSilver does not have and that apps such as Microsoft's ExpenseIt walkthrough need to build and show
/// their window, written into the project as the rest of the generator's compatibility code is: WPF's
/// <c>NavigationWindow</c> on OpenSilver's <c>Frame</c>, <c>NavigationService.Navigate(object)</c>, and the
/// <c>AutomationEvents</c> members OpenSilver's enumeration lacks.
/// </summary>
public sealed partial class WpfXamlGenerator
{
    /// <summary>WPF's <c>AutomationEvents</c> members that OpenSilver's lacks, with their values in WPF.</summary>
    internal static readonly (string Name, int Value)[] WpfAutomationEvents =
    [
        ("InputReachedTarget", 15),
        ("InputReachedOtherElement", 16),
        ("InputDiscarded", 17),
        ("LiveRegionChanged", 18),
        ("Notification", 19),
        ("ActiveTextPositionChanged", 20),
    ];

    private static bool IsVisualBasic(RoslynCompilation compilation) => compilation.Language == LanguageNames.VisualBasic;

    /// <summary>Whether the project needs TabStudio's <c>NavigationWindow</c>: OpenSilver has its frame, but not the window.</summary>
    internal static bool NeedsNavigationWindow(RoslynCompilation compilation) =>
        compilation.GetTypeByMetadataName("System.Windows.Navigation.NavigationWindow") is null
        && compilation.GetTypeByMetadataName("System.Windows.Controls.Frame") is not null
        && compilation.GetTypeByMetadataName("System.Windows.Navigation.INavigationContentLoader") is not null
        && compilation.GetTypeByMetadataName("System.Windows.Navigation.PageResourceContentLoader") is not null;

    /// <summary>The <c>AutomationEvents</c> members the project's OpenSilver lacks.</summary>
    internal static IEnumerable<(string Name, int Value)> MissingAutomationEvents(RoslynCompilation compilation) =>
        compilation.GetTypeByMetadataName("System.Windows.Automation.Peers.AutomationEvents") is { TypeKind: TypeKind.Enum } events
            ? WpfAutomationEvents.Where(e => events.GetMembers(e.Name).IsEmpty)
            : [];

    /// <summary>The C# of <c>NavigationWindow</c>, its content loader and <c>Navigate(object)</c>, inside <c>namespace System.Windows</c>.</summary>
    private static string NavigationCompatibility(RoslynCompilation compilation)
    {
        if (!NeedsNavigationWindow(compilation))
        {
            return string.Empty;
        }

        return """
                namespace Navigation
                {
                    /// <summary>
                    /// WPF's navigation window, which OpenSilver does not have: a window that shows pages in a frame, the
                    /// first from <see cref="Source"/>, the next from <see cref="Navigate(object)"/> or a page's
                    /// NavigationService, with the frame's back and forward journal. Added by TabStudio.
                    /// </summary>
                    public class NavigationWindow : global::System.Windows.Window
                    {
                        /// <summary>The page the window shows first.</summary>
                        public static readonly global::System.Windows.DependencyProperty SourceProperty = global::System.Windows.DependencyProperty.Register(
                            "Source", typeof(global::System.Uri), typeof(NavigationWindow), new global::System.Windows.PropertyMetadata(null, OnSourceChanged));

                        private readonly global::System.Windows.Controls.Frame _frame = new global::System.Windows.Controls.Frame { ContentLoader = new TabStudioNavigationContentLoader() };

                        /// <summary>Creates the window, with its frame as its content.</summary>
                        public NavigationWindow()
                        {
                            Content = _frame;
                        }

                        /// <summary>The page the window shows first, as a path in the project such as <c>HomePage.xaml</c>.</summary>
                        public global::System.Uri Source
                        {
                            get => (global::System.Uri)GetValue(SourceProperty);
                            set => SetValue(SourceProperty, value);
                        }

                        /// <summary>The address of the page shown now.</summary>
                        public global::System.Uri CurrentSource => _frame.CurrentSource;

                        /// <summary>Whether there is a page to go back to.</summary>
                        public bool CanGoBack => _frame.CanGoBack;

                        /// <summary>Whether there is a page to go forward to.</summary>
                        public bool CanGoForward => _frame.CanGoForward;

                        /// <summary>Whether WPF shows its back and forward buttons; the web preview's browser has its own.</summary>
                        public bool ShowsNavigationUI { get; set; } = true;

                        /// <summary>Goes back to the previous page.</summary>
                        public void GoBack() => _frame.GoBack();

                        /// <summary>Goes forward to the next page.</summary>
                        public void GoForward() => _frame.GoForward();

                        /// <summary>Shows the page at an address.</summary>
                        /// <param name="source">The page, such as <c>HomePage.xaml</c>.</param>
                        public bool Navigate(global::System.Uri source) => _frame.Navigate(TabStudioNavigationContentLoader.Resolve(source, GetType().Assembly));

                        /// <summary>Shows an object, such as a page made with <c>new</c>.</summary>
                        /// <param name="content">The page, or an address.</param>
                        public bool Navigate(object content) => content is global::System.Uri uri ? Navigate(uri) : _frame.Navigate(TabStudioNavigationContentLoader.Register(content));

                        private static void OnSourceChanged(global::System.Windows.DependencyObject d, global::System.Windows.DependencyPropertyChangedEventArgs e)
                        {
                            if (e.NewValue is global::System.Uri uri && d is NavigationWindow window)
                            {
                                window._frame.Source = TabStudioNavigationContentLoader.Resolve(uri, window.GetType().Assembly);
                            }
                        }
                    }

                    /// <summary>
                    /// Loads what a navigation window or a page's NavigationService goes to: an object it was given, under an
                    /// address of its own so the journal can go back to it, or a page of the app from its XAML file.
                    /// </summary>
                    internal sealed class TabStudioNavigationContentLoader : global::System.Windows.Navigation.INavigationContentLoader
                    {
                        private const string Prefix = "/TabStudio.Navigation/";
                        private static readonly global::System.Collections.Generic.Dictionary<string, object> Contents = new global::System.Collections.Generic.Dictionary<string, object>(global::System.StringComparer.Ordinal);
                        private static int _next;
                        private readonly global::System.Windows.Navigation.PageResourceContentLoader _pages = new global::System.Windows.Navigation.PageResourceContentLoader();

                        /// <summary>An address for an object to navigate to.</summary>
                        public static global::System.Uri Register(object content)
                        {
                            var key = Prefix + (++_next).ToString(global::System.Globalization.CultureInfo.InvariantCulture) + ".xaml";
                            Contents[key] = content;
                            return new global::System.Uri(key, global::System.UriKind.Relative);
                        }

                        /// <summary>
                        /// A WPF address of a page, such as <c>HomePage.xaml</c> or <c>pack://application:,,,/HomePage.xaml</c>, as
                        /// OpenSilver finds pages: in the app's assembly, which is not the browser host that starts it.
                        /// </summary>
                        public static global::System.Uri Resolve(global::System.Uri source, global::System.Reflection.Assembly assembly)
                        {
                            var path = source.OriginalString.Replace('\\', '/');
                            const string pack = "pack://application:,,,";
                            if (path.StartsWith(pack, global::System.StringComparison.OrdinalIgnoreCase))
                            {
                                path = path.Substring(pack.Length);
                            }

                            if (path.StartsWith(Prefix, global::System.StringComparison.Ordinal))
                            {
                                return new global::System.Uri(path, global::System.UriKind.Relative);
                            }

                            if (path.IndexOf(";component/", global::System.StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return new global::System.Uri(path.StartsWith("/", global::System.StringComparison.Ordinal) ? path : "/" + path, global::System.UriKind.Relative);
                            }

                            return new global::System.Uri("/" + assembly.GetName().Name + ";component/" + path.TrimStart('/'), global::System.UriKind.Relative);
                        }

                        /// <inheritdoc />
                        public bool CanLoad(global::System.Uri targetUri, global::System.Uri currentUri) => Contents.ContainsKey(Key(targetUri)) || _pages.CanLoad(targetUri, currentUri);

                        /// <inheritdoc />
                        public global::System.IAsyncResult BeginLoad(global::System.Uri targetUri, global::System.Uri currentUri, global::System.AsyncCallback userCallback, object asyncState)
                        {
                            if (!Contents.TryGetValue(Key(targetUri), out var content))
                            {
                                return _pages.BeginLoad(targetUri, currentUri, userCallback, asyncState);
                            }

                            var result = new Loaded(asyncState, content);
                            global::System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() => userCallback?.Invoke(result));
                            return result;
                        }

                        /// <inheritdoc />
                        public global::System.Windows.Navigation.LoadResult EndLoad(global::System.IAsyncResult asyncResult) =>
                            asyncResult is Loaded loaded ? new global::System.Windows.Navigation.LoadResult(loaded.Content) : _pages.EndLoad(asyncResult);

                        /// <inheritdoc />
                        public void CancelLoad(global::System.IAsyncResult asyncResult)
                        {
                            if (!(asyncResult is Loaded))
                            {
                                _pages.CancelLoad(asyncResult);
                            }
                        }

                        private static string Key(global::System.Uri uri)
                        {
                            var text = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
                            var end = text.IndexOfAny(new[] { '?', '#' });
                            return end < 0 ? text : text.Substring(0, end);
                        }

                        private sealed class Loaded : global::System.IAsyncResult
                        {
                            public Loaded(object asyncState, object content)
                            {
                                AsyncState = asyncState;
                                Content = content;
                            }

                            public object Content { get; }

                            public object AsyncState { get; }

                            public global::System.Threading.WaitHandle AsyncWaitHandle => null;

                            public bool CompletedSynchronously => false;

                            public bool IsCompleted => true;
                        }
                    }
                }

                /// <summary>WPF's <c>NavigationService.Navigate(object)</c>, which OpenSilver's NavigationService does not have.</summary>
                internal static class TabStudioNavigationServiceExtensions
                {
                    /// <summary>Shows an object, such as a page made with <c>new</c>, in the frame the service navigates.</summary>
                    /// <param name="navigationService">A page's NavigationService.</param>
                    /// <param name="root">The page, or an address.</param>
                    public static bool Navigate(this global::System.Windows.Navigation.NavigationService navigationService, object root)
                    {
                        if (root is global::System.Uri uri)
                        {
                            return navigationService.Navigate(uri);
                        }

                        // The frame the service belongs to, which OpenSilver does not make public, loads objects with TabStudio's loader.
                        var host = typeof(global::System.Windows.Navigation.NavigationService).GetProperty("Host", global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Public);
                        if (host?.GetValue(navigationService) is global::System.Windows.Controls.Frame frame && !(frame.ContentLoader is global::System.Windows.Navigation.TabStudioNavigationContentLoader))
                        {
                            frame.ContentLoader = new global::System.Windows.Navigation.TabStudioNavigationContentLoader();
                        }

                        return navigationService.Navigate(global::System.Windows.Navigation.TabStudioNavigationContentLoader.Register(root));
                    }
                }


            """;
    }

    /// <summary>
    /// The <c>AutomationEvents</c> members OpenSilver lacks, as C# 14 static extension members with WPF's values, inside
    /// <c>namespace System.Windows</c>: <c>AutomationEvents.LiveRegionChanged</c> compiles where the file imports the peers.
    /// </summary>
    private static string AutomationCompatibility(RoslynCompilation compilation)
    {
        var missing = MissingAutomationEvents(compilation).ToList();
        if (missing.Count == 0 || compilation is not Microsoft.CodeAnalysis.CSharp.CSharpCompilation { LanguageVersion: >= Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14 })
        {
            return string.Empty;
        }

        var text = new StringBuilder("""
                namespace Automation.Peers
                {
                    /// <summary>WPF's automation events that OpenSilver does not raise, so code that raises them compiles. Added by TabStudio.</summary>
                    internal static class TabStudioAutomationEvents
                    {
                        extension(global::System.Windows.Automation.Peers.AutomationEvents)
                        {

            """);
        foreach (var (name, value) in missing)
        {
            text.Append(CultureInfo.InvariantCulture, $"                /// <summary>WPF's <c>AutomationEvents.{name}</c>.</summary>\n");
            text.Append(CultureInfo.InvariantCulture, $"                public static global::System.Windows.Automation.Peers.AutomationEvents {name} => (global::System.Windows.Automation.Peers.AutomationEvents){value};\n\n");
        }

        text.Append("""
                        }
                    }
                }


            """);
        return text.ToString();
    }
}
