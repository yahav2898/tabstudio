using TabStudio.App.ViewModels;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// Android's sunflower, architecture-samples and JetNews built with 3, 7 and 1 errors in the Error List, all of them
/// Kotlin's deprecation warnings: every message of an Android build was listed as an error.
/// </summary>
public sealed class AndroidBuildSeverityTests
{
    [Theory]
    [InlineData("w", DiagnosticSeverityLevel.Warning)]
    [InlineData("warning", DiagnosticSeverityLevel.Warning)]
    [InlineData("e", DiagnosticSeverityLevel.Error)]
    [InlineData("error", DiagnosticSeverityLevel.Error)]
    [InlineData(null, DiagnosticSeverityLevel.Error)]
    public void Kotlin_and_javac_warnings_of_an_Android_build_are_warnings(string? severity, DiagnosticSeverityLevel expected) =>
        Assert.Equal(expected, WorkspaceViewModel.AndroidSeverity(severity));
}
