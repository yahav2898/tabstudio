namespace TabStudio.App.Swarm.Recording;

/// <summary>
/// The C# of <c>RecordedApp</c>, the small driver recorded UI tests use, as <see cref="UiTestWriter.Driver"/> writes it
/// into the test project once. It finds elements by the same rules as <see cref="ElementPaths"/>, so a path recorded on a
/// device finds the same element in the test.
/// </summary>
internal static class UiTestDriverSource
{
    /// <summary>The driver, with <c>$NAMESPACE$</c> for the test project's namespace and <c>$APP$</c> for the app's <c>Application</c> class.</summary>
    public const string Text = """
        using System.Reflection;
        using Avalonia;
        using Avalonia.Controls;
        using Avalonia.Controls.ApplicationLifetimes;
        using Avalonia.Headless;
        using Avalonia.Input;
        using Avalonia.LogicalTree;
        using Avalonia.Styling;
        using Avalonia.Threading;

        namespace $NAMESPACE$;

        /// <summary>
        /// Runs the app for the UI tests TabStudio records: starts it as on a phone, with its main view created the way
        /// the app creates it for a single-view lifetime, in a headless window of the recorded size, and drives it as a person does, with real
        /// pointer and key input aimed at elements by their names or their paths, such as "CountButton" or
        /// "MainView/StackPanel[0]/TextBlock[2]". TabStudio wrote it with the first recorded test; change it as you like.
        /// </summary>
        internal sealed class RecordedApp : IDisposable
        {
            private readonly Window _window;
            private readonly Control _view;

            private RecordedApp(Window window, Control view)
            {
                _window = window;
                _view = view;
            }

            /// <summary>Starts the app in a window of the given size, in device-independent pixels.</summary>
            public static RecordedApp Start(double width, double height)
            {
                // Avalonia gives an app its lifetime only before it is set up, and the app TestAppBuilder set up has
                // none; so each test starts a new instance of the app, which creates its main view for a phone's
                // single-view lifetime, as in App.axaml.cs.
                var lifetime = PhoneLifetime.Create();
                var app = new global::$APP$ { ApplicationLifetime = lifetime };
                app.Initialize();
                app.OnFrameworkInitializationCompleted();
                var view = lifetime.MainView
                    ?? throw new InvalidOperationException("The app sets no MainView for a single-view lifetime, as Avalonia's cross-platform template does in App.axaml.cs.");

                // Avalonia applies the styles of the app it runs; when that is another Application, the window takes
                // this app's styles, templates and resources.
                var window = new Window { Width = width, Height = height, Content = view };
                if (Application.Current is not global::$APP$)
                {
                    window.RequestedThemeVariant = app.RequestedThemeVariant ?? ThemeVariant.Default;
                    foreach (var style in app.Styles.ToList())
                    {
                        app.Styles.Remove(style);
                        window.Styles.Add(style);
                    }

                    foreach (var template in app.DataTemplates.ToList())
                    {
                        app.DataTemplates.Remove(template);
                        window.DataTemplates.Add(template);
                    }

                    foreach (var dictionary in app.Resources.MergedDictionaries.ToList())
                    {
                        app.Resources.MergedDictionaries.Remove(dictionary);
                        window.Resources.MergedDictionaries.Add(dictionary);
                    }
                }

                window.Show();
                Dispatcher.UIThread.RunJobs();
                return new RecordedApp(window, view);
            }

            /// <summary>Taps an element in its middle.</summary>
            public void Tap(string element) => Click(element, MouseButton.Left);

            /// <summary>Long-presses an element, as a right click does with a mouse.</summary>
            public void LongPress(string element) => Click(element, MouseButton.Right);

            /// <summary>Scrolls the element under the pointer by a distance, in device-independent pixels.</summary>
            public void Scroll(string element, double deltaX, double deltaY)
            {
                // A wheel step scrolls 50 pixels; scrolling down is a negative step.
                _window.MouseWheel(MiddleOf(element), new Vector(-deltaX / 50, -deltaY / 50));
                Settle();
            }

            /// <summary>Types text into the focused element.</summary>
            public void Type(string text)
            {
                _window.KeyTextInput(text);
                Settle();
            }

            /// <summary>Presses a key, with modifiers, on the focused element.</summary>
            public void Press(Key key, KeyModifiers modifiers = KeyModifiers.None)
            {
                _window.KeyPress(key, (RawInputModifiers)modifiers, PhysicalKey.None, null);
                _window.KeyRelease(key, (RawInputModifiers)modifiers, PhysicalKey.None, null);
                Settle();
            }

            /// <summary>Checks that an element shows a text: its own, or the texts inside it joined by spaces.</summary>
            public void ShouldShow(string element, string text) => Assert.Equal(text, TextOf(Find(element)));

            /// <summary>Finds an element by its name, or by its path from the app's main view.</summary>
            public Control Find(string element) =>
                FindIn(_view, element) ?? throw new Xunit.Sdk.XunitException($"There is no element '{element}' on the screen.");

            /// <inheritdoc />
            public void Dispose() => _window.Close();

            private void Click(string element, MouseButton button)
            {
                var point = MiddleOf(element);
                _window.MouseMove(point);
                _window.MouseDown(point, button);
                _window.MouseUp(point, button);
                Settle();
            }

            private Point MiddleOf(string element)
            {
                var control = Find(element);
                return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), _window)
                    ?? throw new Xunit.Sdk.XunitException($"'{element}' is not on the screen.");
            }

            private static void Settle() => Dispatcher.UIThread.RunJobs();

            private static string TextOf(Control element) => element switch
            {
                TextBlock text => text.Text ?? string.Empty,
                TextBox box => box.Text ?? string.Empty,
                ContentControl { Content: string content } => content,
                _ => string.Join(' ', element.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t))),
            };

            /// <summary>
            /// A bare name is looked for anywhere. A path is followed from the root through logical children, each segment
            /// being Type#Name or Type[index among children of exactly that type, or among a list's items]; when that
            /// fails, an element with the last segment's name is looked for anywhere, if there is only one.
            /// </summary>
            private static Control? FindIn(Control root, string path)
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                {
                    return null;
                }

                if (segments.Length == 1 && !segments[0].Contains('#') && !segments[0].Contains('['))
                {
                    return Named(root, segments[0]) ?? (IsType(root, segments[0]) ? root : null);
                }

                // The path starts at the app's root view, which is where the search starts, or under it.
                Control? current = root;
                var first = Parse(segments[0]);
                var start = 0;
                if (IsType(root, first.Type) && (first.Name is null || root.Name == first.Name))
                {
                    start = 1;
                }
                else if (!segments[0].Contains('[') && root.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => IsType(c, first.Type) && (first.Name is null || c.Name == first.Name)) is { } view)
                {
                    current = view;
                    start = 1;
                }

                for (var i = start; i < segments.Length && current is not null; i++)
                {
                    var (type, name, index) = Parse(segments[i]);
                    var children = current.GetLogicalChildren().OfType<Control>().ToList();
                    if (name is not null)
                    {
                        current = children.FirstOrDefault(c => c.Name == name);
                    }
                    else
                    {
                        var ofType = children.Where(c => TypeName(c.GetType()) == type).ToList();
                        current = Item(current, type, index) ?? (index < ofType.Count ? ofType[index] : null);
                    }
                }

                if (current is not null)
                {
                    return current;
                }

                var last = Parse(segments[^1]);
                if (last.Name is not { } lastName)
                {
                    return null;
                }

                var named = root.GetLogicalDescendants().OfType<Control>().Where(c => c.Name == lastName && IsType(c, last.Type)).Take(2).ToList();
                return named.Count == 1 ? named[0] : null;
            }

            /// <summary>An item of a list by its index among the list's items, scrolled into view when it is not shown yet.</summary>
            private static Control? Item(Control parent, string? type, int index)
            {
                if (parent is not ItemsControl items || index >= items.ItemCount)
                {
                    return null;
                }

                if (items.ContainerFromIndex(index) is null)
                {
                    items.ScrollIntoView(index);
                    items.UpdateLayout();
                }

                return items.ContainerFromIndex(index) is { } container && TypeName(container.GetType()) == type ? container : null;
            }

            /// <summary>A type's name without its generic arity, as paths write it.</summary>
            private static string TypeName(Type type)
            {
                var tick = type.Name.IndexOf('`');
                return tick < 0 ? type.Name : type.Name[..tick];
            }

            private static Control? Named(Control root, string name) =>
                root.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);

            private static bool IsType(Control control, string? type)
            {
                if (type is null)
                {
                    return true;
                }

                for (var current = control.GetType(); current is not null && current != typeof(StyledElement); current = current.BaseType)
                {
                    if (TypeName(current) == type)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static (string? Type, string? Name, int Index) Parse(string segment)
            {
                var hash = segment.IndexOf('#');
                if (hash >= 0)
                {
                    return (hash == 0 ? null : segment[..hash], segment[(hash + 1)..], 0);
                }

                var open = segment.IndexOf('[');
                return open > 0 && segment.EndsWith(']') && int.TryParse(segment[(open + 1)..^1], out var index)
                    ? (segment[..open], null, index)
                    : (segment, null, 0);
            }
        }

        /// <summary>
        /// The lifetime a phone gives an app: one view, without a window. Avalonia's lifetime interfaces cannot be
        /// implemented in code, so it is a proxy made at run time, where the interface has only MainView.
        /// </summary>
        internal class PhoneLifetime : DispatchProxy
        {
            public Control? MainView { get; private set; }

            public static ISingleViewApplicationLifetime Create() => Create<ISingleViewApplicationLifetime, PhoneLifetime>();

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                switch (targetMethod?.Name)
                {
                    case "get_MainView":
                        return MainView;
                    case "set_MainView":
                        MainView = args?[0] as Control;
                        return null;
                    default:
                        throw new NotSupportedException($"A phone's lifetime has no {targetMethod?.Name}.");
                }
            }
        }

        """;
}
