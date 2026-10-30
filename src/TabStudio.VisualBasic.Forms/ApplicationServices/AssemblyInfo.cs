using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Forms;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>
/// The attributes of an assembly (<c>My.Application.Info</c>): title, description, company, product, copyright,
/// trademark and version, as the project sets them.
/// </summary>
public class AssemblyInfo
{
    private readonly Assembly _assembly;
    private string? _companyName;
    private string? _copyright;
    private string? _description;
    private string? _productName;
    private string? _title;
    private string? _trademark;

    /// <summary>Describes an assembly.</summary>
    /// <param name="currentAssembly">The assembly.</param>
    /// <exception cref="ArgumentNullException"><paramref name="currentAssembly"/> is <see langword="null"/>.</exception>
    public AssemblyInfo(Assembly currentAssembly)
    {
        ArgumentNullException.ThrowIfNull(currentAssembly);
        _assembly = currentAssembly;
    }

    /// <summary>The assembly's name, without its version: the project's assembly name.</summary>
    public string AssemblyName => _assembly.GetName().Name ?? string.Empty;

    /// <summary>The company of the assembly's <see cref="AssemblyCompanyAttribute"/>, or an empty string.</summary>
    public string CompanyName => _companyName ??= Find<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

    /// <summary>The copyright of the assembly's <see cref="AssemblyCopyrightAttribute"/>, or an empty string.</summary>
    public string Copyright => _copyright ??= Find<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    /// <summary>The description of the assembly's <see cref="AssemblyDescriptionAttribute"/>, or an empty string.</summary>
    public string Description => _description ??= Find<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;

    /// <summary>
    /// The folder the assembly was loaded from. A program TabStudio compiled is loaded from memory, so this is the
    /// folder the program runs in: its project's folder for a Windows Forms app.
    /// </summary>
    public string DirectoryPath =>
        _assembly.Location is { Length: > 0 } location && Path.GetDirectoryName(location) is { } directory
            ? directory
            : Application.StartupPath;

    /// <summary>The assemblies loaded in the process, TabStudio's included.</summary>
    public ReadOnlyCollection<Assembly> LoadedAssemblies => new(AppDomain.CurrentDomain.GetAssemblies());

    /// <summary>The product of the assembly's <see cref="AssemblyProductAttribute"/>, or an empty string.</summary>
    public string ProductName => _productName ??= Find<AssemblyProductAttribute>()?.Product ?? string.Empty;

    /// <summary>The current stack trace.</summary>
    public string StackTrace => Environment.StackTrace;

    /// <summary>The title of the assembly's <see cref="AssemblyTitleAttribute"/>, or an empty string.</summary>
    public string Title => _title ??= Find<AssemblyTitleAttribute>()?.Title ?? string.Empty;

    /// <summary>The trademark of the assembly's <see cref="AssemblyTrademarkAttribute"/>, or an empty string.</summary>
    public string Trademark => _trademark ??= Find<AssemblyTrademarkAttribute>()?.Trademark ?? string.Empty;

    /// <summary>The assembly's version.</summary>
    public Version? Version => _assembly.GetName().Version;

    /// <summary>The memory the process uses, in bytes: TabStudio's, which runs the program.</summary>
    public long WorkingSet => Environment.WorkingSet;

    private T? Find<T>()
        where T : Attribute =>
        _assembly.GetCustomAttributes(typeof(T), inherit: true) is { Length: > 0 } attributes ? (T)attributes[0] : null;
}
