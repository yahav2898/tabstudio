using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace TabStudio.App.Previews;

/// <summary>
/// The lifetime an app starts with in the app preview: a single view, as on a phone, which the app's
/// <c>OnFrameworkInitializationCompleted</c> sets.
/// </summary>
/// <remarks>
/// Avalonia's lifetime interfaces cannot be implemented by code compiled against its reference assemblies, which
/// add a member no class can declare. At run time the interface has only <c>MainView</c>, so the lifetime is a
/// <see cref="DispatchProxy"/>, generated then.
/// </remarks>
internal class PreviewLifetime : DispatchProxy
{
    /// <summary>The view the app set, if any.</summary>
    public Control? MainView { get; private set; }

    /// <summary>Creates a lifetime for one app.</summary>
    /// <returns>The lifetime to give the app, and the object that records what the app sets.</returns>
    public static (ISingleViewApplicationLifetime Lifetime, PreviewLifetime State) Create()
    {
        var lifetime = Create<ISingleViewApplicationLifetime, PreviewLifetime>();
        return (lifetime, (PreviewLifetime)(object)lifetime);
    }

    /// <inheritdoc />
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
                throw new NotSupportedException($"The app preview's lifetime has no {targetMethod?.Name}.");
        }
    }
}
