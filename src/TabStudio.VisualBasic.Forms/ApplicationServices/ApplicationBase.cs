using System.Globalization;
using Microsoft.VisualBasic.Logging;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>
/// The base of <c>My.Application</c>: the program's assembly information, cultures, log and environment. On the
/// tablet it describes the program TabStudio runs, not TabStudio itself.
/// </summary>
public class ApplicationBase
{
    private AssemblyInfo? _info;
    private Log? _log;

    /// <summary>Creates the application object, as the My namespace does.</summary>
    public ApplicationBase()
    {
    }

    /// <summary>The culture of the current thread, which formats numbers and dates.</summary>
    public CultureInfo Culture => Thread.CurrentThread.CurrentCulture;

    /// <summary>The culture of the current thread that chooses resources.</summary>
    public CultureInfo UICulture => Thread.CurrentThread.CurrentUICulture;

    /// <summary>The program's assembly: its title, version, company and the other attributes of its project.</summary>
    public AssemblyInfo Info => _info ??= new AssemblyInfo(ProgramAssembly.Find(this));

    /// <summary>The program's log, which writes to a file in the program's data folder on the device.</summary>
    public Log Log => _log ??= new Log();

    /// <summary>Changes the culture of the current thread, which formats numbers and dates.</summary>
    /// <param name="cultureName">The culture's name, such as <c>en-US</c>.</param>
    public void ChangeCulture(string cultureName) => Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);

    /// <summary>Changes the culture of the current thread that chooses resources.</summary>
    /// <param name="cultureName">The culture's name, such as <c>he-IL</c>.</param>
    public void ChangeUICulture(string cultureName) => Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);

    /// <summary>The value of an environment variable of the program.</summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>Its value.</returns>
    /// <exception cref="ArgumentException">The variable is not defined.</exception>
    public string GetEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new ArgumentException($"Environment variable is not defined: '{name}'.", nameof(name));
}
