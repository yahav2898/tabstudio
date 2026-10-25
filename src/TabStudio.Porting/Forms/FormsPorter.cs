using TabStudio.Porting.Output;

namespace TabStudio.Porting.Forms;

/// <summary>Ports a Windows Forms project.</summary>
internal static class FormsPorter
{
    /// <summary>Converts the project's forms and code.</summary>
    /// <param name="context">The port.</param>
    public static List<PortedFile> Port(PortContext context) =>
        throw new NotSupportedException($"Windows Forms is not ported yet: {context.Source.Name}.");
}
