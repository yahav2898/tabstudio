using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;

namespace TabStudio.App.Services.Search;

/// <summary>A command the search can run: its title, where it is in the menus, and its key gesture.</summary>
/// <param name="Title">Its name, such as "Build Solution".</param>
/// <param name="Path">Its menu path, such as "Build", or <see langword="null"/> for one not in a menu.</param>
/// <param name="Gesture">Its key gesture, such as "Ctrl+Shift+B", or <see langword="null"/>.</param>
/// <param name="Command">What it runs.</param>
/// <param name="Parameter">The command's parameter.</param>
public sealed record CommandEntry(string Title, string? Path, string? Gesture, ICommand Command, object? Parameter)
{
    /// <summary>Whether it applies now: its menu item shows and the command can run.</summary>
    public Func<bool> IsAvailable { get; init; } = () => true;
}

/// <summary>
/// The IDE's commands for the search (Ctrl+Q), gathered from where they already are, the menus and the key bindings,
/// without moving them: a menu item gives a title, a menu path and a gesture; a key binding gives the gesture of a
/// command whose menu item does not show one. Commands that are in no menu can be added by name.
/// </summary>
public sealed partial class CommandRegistry
{
    private readonly List<CommandEntry> _commands = [];

    /// <summary>Raised after commands were added.</summary>
    public event EventHandler? Changed;

    /// <summary>The commands, in the menus' order.</summary>
    public IReadOnlyList<CommandEntry> Commands => _commands;

    /// <summary>Adds the commands of a menu bar, such as the IDE's.</summary>
    /// <param name="menu">The menu bar.</param>
    public void AddMenu(Menu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        AddMenuItems(menu.Items.OfType<MenuItem>(), null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds menu items and their submenus' items.</summary>
    /// <param name="items">The items.</param>
    /// <param name="path">The menu path they are under, such as "Edit &gt; IntelliSense", or <see langword="null"/> at the top.</param>
    public void AddMenuItems(IEnumerable<MenuItem> items, string? path)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            if (item.Header is not string header)
            {
                continue;
            }

            var (title, gestureInTitle) = CleanHeader(header);
            var children = item.Items.OfType<MenuItem>().ToList();
            if (children.Count > 0)
            {
                AddMenuItems(children, path is null ? title : $"{path} > {title}");
                continue;
            }

            if (item.Command is not { } command)
            {
                continue;
            }

            var source = item;
            _commands.Add(new CommandEntry(title, path, item.InputGesture?.ToString() ?? gestureInTitle, command, item.CommandParameter)
            {
                IsAvailable = () => source.IsVisible && command.CanExecute(source.CommandParameter),
            });
        }
    }

    /// <summary>Gives commands found in the menus the gestures of their key bindings, where the menu item shows none.</summary>
    /// <param name="bindings">The key bindings, such as a view's.</param>
    public void AddKeyBindings(IEnumerable<KeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        foreach (var binding in bindings)
        {
            if (binding.Command is not { } command || binding.Gesture is not { } gesture)
            {
                continue;
            }

            for (var i = 0; i < _commands.Count; i++)
            {
                var entry = _commands[i];
                if (entry.Gesture is null && ReferenceEquals(entry.Command, command) && Equals(entry.Parameter, binding.CommandParameter))
                {
                    _commands[i] = entry with { Gesture = gesture.ToString() };
                }
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds a command that is in no menu.</summary>
    /// <param name="title">Its name.</param>
    /// <param name="command">What it runs.</param>
    /// <param name="parameter">Its parameter.</param>
    /// <param name="gesture">Its key gesture, or <see langword="null"/>.</param>
    /// <param name="path">Where it belongs, such as "View", or <see langword="null"/>.</param>
    public void Add(string title, ICommand command, object? parameter = null, string? gesture = null, string? path = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(new CommandEntry(title, path, gesture, command, parameter)
        {
            IsAvailable = () => command.CanExecute(parameter),
        });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A menu header as a title: without the access key's underscore ("Save A_ll" is "Save All"), and with a key gesture
    /// written in the header taken out ("Run _All Tests (Ctrl+R, A)" is "Run All Tests" and "Ctrl+R, A").
    /// </summary>
    /// <param name="header">The header.</param>
    public static (string Title, string? Gesture) CleanHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);
        var text = new StringBuilder(header.Length);
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i] == '_')
            {
                if (i + 1 < header.Length && header[i + 1] == '_')
                {
                    text.Append('_');
                    i++;
                }

                continue;
            }

            text.Append(header[i]);
        }

        var title = text.ToString().Trim();
        if (GestureInTitle().Match(title) is { Success: true } match)
        {
            return (match.Groups["title"].Value.Trim(), match.Groups["gesture"].Value.Trim());
        }

        return (title.TrimEnd('.', '…').Trim(), null);
    }

    [GeneratedRegex(@"^(?<title>.+?)\.*\s*\((?<gesture>(?:Ctrl|Alt|Shift|F\d)[^)]*)\)$")]
    private static partial Regex GestureInTitle();
}
