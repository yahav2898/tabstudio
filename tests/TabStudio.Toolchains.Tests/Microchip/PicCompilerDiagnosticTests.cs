using TabStudio.Core.Diagnostics;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class PicCompilerDiagnosticTests
{
    [Fact]
    public void Retains_all_errors_warnings_and_notes_with_columns_and_windows_paths()
    {
        var diagnostics = PicCompilerDiagnostic.Parse("""
            main.c:7:3: error: (192) undefined identifier led
            main.c:8: error: expected ';'
            C:\Firmware\main.c:11:4: warning: implicit conversion
            main.c:12:1: note: declared here
            main.c:7:3: error: (192) undefined identifier led
            """);

        Assert.Equal(4, diagnostics.Count);
        Assert.Equal((7, 3, DiagnosticSeverityLevel.Error), (diagnostics[0].Line, diagnostics[0].Column, diagnostics[0].Severity));
        Assert.Equal(1, diagnostics[1].Column);
        Assert.Equal(@"C:\Firmware\main.c", diagnostics[2].File);
        Assert.Equal(DiagnosticSeverityLevel.Warning, diagnostics[2].Severity);
        Assert.Equal(DiagnosticSeverityLevel.Info, diagnostics[3].Severity);
    }
}
