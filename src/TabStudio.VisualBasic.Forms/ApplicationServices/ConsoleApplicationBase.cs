using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>The base of <c>My.Application</c> in a console app: the application object with the command line.</summary>
public class ConsoleApplicationBase : ApplicationBase
{
    private ReadOnlyCollection<string>? _commandLineArgs;

    /// <summary>Creates the application object, as the My namespace does.</summary>
    public ConsoleApplicationBase()
    {
    }

    /// <summary>
    /// The program's command-line arguments, without the program's own name. A program TabStudio runs gets the
    /// arguments its run passes to it, which are none for a console app started with F5.
    /// </summary>
    public ReadOnlyCollection<string> CommandLineArgs => _commandLineArgs ??= new ReadOnlyCollection<string>(ProcessArguments());

    /// <summary>Sets the command-line arguments, as <c>WindowsFormsApplicationBase.Run</c> does with the ones Main got.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected ReadOnlyCollection<string> InternalCommandLine
    {
        set => _commandLineArgs = value;
    }

    private string[] ProcessArguments()
    {
        // In TabStudio the process is the IDE, whose command line is not the program's.
        if (ProgramAssembly.IsHosted(GetType().Assembly))
        {
            return [];
        }

        var arguments = Environment.GetCommandLineArgs();
        return arguments.Length >= 2 ? arguments[1..] : [];
    }
}
