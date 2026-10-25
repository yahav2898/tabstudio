using System.Reflection;

namespace TabStudio.Uno.Hosting;

/// <summary>
/// The host's entry points (<c>TabStudio.UnoHost.PreviewHost</c>), bound once to delegates. The host is compiled on the
/// device, so TabStudio knows it by name; its members use .NET types only, and a test checks that every one binds.
/// </summary>
internal sealed class UnoHostBridge
{
    private UnoHostBridge(Type host)
    {
        Start = Bind<Func<string, bool, double, double, float, Action, Action<int, string, Exception?>, Action<Exception>, Task>>(host, "StartAsync");
        Launch = Bind<Func<Assembly, Task>>(host, "LaunchAsync");
        Close = Bind<Func<Task>>(host, "CloseAsync");
        Resize = Bind<Action<double, double, float>>(host, "Resize");
        SetTheme = Bind<Action<bool>>(host, "SetTheme");
        CopyFrame = Bind<Func<IntPtr, int, int, int, bool>>(host, "CopyFrame");
        FrameWidth = BindGetter<int>(host, "FrameWidth");
        FrameHeight = BindGetter<int>(host, "FrameHeight");
        Pointer = Bind<Action<int, uint, int, double, double, bool, bool, bool, int, bool, int, ulong>>(host, "Pointer");
        Key = Bind<Action<bool, int, int, uint, string?>>(host, "Key");
        Text = Bind<Action<string, int>>(host, "Text");
        GetTexts = Bind<Func<Task<string[]>>>(host, "GetTextsAsync");
        FindElement = Bind<Func<string, Task<double[]?>>>(host, "FindElementAsync");
        GetTitle = Bind<Func<Task<string?>>>(host, "GetTitleAsync");
        IsEditingText = Bind<Func<Task<bool>>>(host, "IsEditingTextAsync");
    }

    public Func<string, bool, double, double, float, Action, Action<int, string, Exception?>, Action<Exception>, Task> Start { get; }

    public Func<Assembly, Task> Launch { get; }

    public Func<Task> Close { get; }

    public Action<double, double, float> Resize { get; }

    public Action<bool> SetTheme { get; }

    public Func<IntPtr, int, int, int, bool> CopyFrame { get; }

    public Func<int> FrameWidth { get; }

    public Func<int> FrameHeight { get; }

    public Action<int, uint, int, double, double, bool, bool, bool, int, bool, int, ulong> Pointer { get; }

    public Action<bool, int, int, uint, string?> Key { get; }

    public Action<string, int> Text { get; }

    public Func<Task<string[]>> GetTexts { get; }

    public Func<string, Task<double[]?>> FindElement { get; }

    public Func<Task<string?>> GetTitle { get; }

    public Func<Task<bool>> IsEditingText { get; }

    /// <summary>Binds the entry points of a loaded host assembly.</summary>
    /// <exception cref="UnoHostException">The assembly is not TabStudio's host, or not the version TabStudio expects.</exception>
    public static UnoHostBridge For(Assembly host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var type = host.GetType(UnoHostCompiler.HostTypeName)
            ?? throw new UnoHostException($"{host.GetName().Name} has no {UnoHostCompiler.HostTypeName}.");
        return new UnoHostBridge(type);
    }

    private static T Bind<T>(Type host, string name)
        where T : Delegate
    {
        var signature = typeof(T).GetMethod("Invoke")!;
        var method = host.GetMethod(name, BindingFlags.Public | BindingFlags.Static, [.. signature.GetParameters().Select(p => p.ParameterType)])
            ?? throw new UnoHostException($"TabStudio's Uno Platform host has no {name} with the parameters TabStudio calls it with.");
        return method.CreateDelegate<T>();
    }

    private static Func<T> BindGetter<T>(Type host, string name) =>
        host.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetMethod?.CreateDelegate<Func<T>>()
        ?? throw new UnoHostException($"TabStudio's Uno Platform host has no {name}.");
}
