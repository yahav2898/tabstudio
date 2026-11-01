#!/usr/bin/env python3
"""Allow one retry only when every reported failure is outside the merged 0.14 and 0.15 features.

An aborted host or a build failure has no complete set of test failures, so it
must fail rather than hiding a possible feature regression behind another run.
"""
import re
import sys
from pathlib import Path

FEATURE_PROJECTS = frozenset({
    "TabStudio.Wpf.Tests",
    "TabStudio.Uno.Tests",
    "TabStudio.Porting.Tests",
    "TabStudio.Database.Tests",
    "TabStudio.Remote.Tests",
})

# Shared test projects contain both feature and unrelated tests. Keep their
# feature classes explicit so an unrelated screenshot/GC failure can still retry.
FEATURE_CLASSES = frozenset({
    "ArchiveExtractorTests",
    "BusinessAppTests",
    "ClientCapabilitiesTests",
    "CompatibilityDoctorTests",
    "CppProjectTests",
    "DebugAdapterSessionTests",
    "DebugAdapterUnixSocketTests",
    "GoProjectTests",
    "JavaInitializationTests",
    "JavaProjectTests",
    "JavaProotTests",
    "ProblemMatcherTests",
    "PackageDownloadTests",
    "ToolchainBatchTests",
    "ToolchainManagerTests",
    "RustProjectTests",
    "ToolchainCatalogTests",
    "ToolchainLanguageToolsTests",
    "ToolchainLaunchTests",
    "ToolchainProjectDetectorTests",
    "ToolchainServerLauncherTests",
    "UnixNamedPipeTests",
    "UnoAppTests",
    "UnoHostCompilerTests",
    "UnoRuntimeTests",
    "UnoTemplateTests",
    "UnoXamlBuildTests",
    "WebAssemblyAppHostTests",
    "WinUIAppTests",
    "WindowsBridgeIdeTests",
    "WindowsBridgeOnWindowsTests",
    "WpfApiCatalogTests",
    "WpfBrowserTests",
    "WpfProjectTests",
    "WpfTemplateTests",
    "WpfXamlEditingTests",
    "WpfXamlEditorTests",
    # 0.15 repository setup and static sites.
    "NodeRunTests",
    "ProjectSetupDeviceTests",
    "ProjectSetupTests",
    "ToolchainServerInstallTests",
    "ToolchainStoreTests",
    "StaticSiteTests",
    "StackDetectorLayoutTests",
    "RegistryTests",
    "WorkspaceConfigurationTests",
    "LaunchConfigurationTests",
    "InitializationOptionsTests",
    "RealLanguageServerTests",
    "LanguageServerHostTests",
    "LanguageServerScreenshotTests",
    "ProjectSetupPlanTests",
    "ProjectTemplatesTests",
    "RunPlannerTests",
    "StaticSiteServerTests",
    # Porting, database and remote workspaces.
    "PortingTests",
    "RuntimeXamlIncludeTests",
    "AvaloniaXamlGeneratorTests",
    "DatabaseExplorerTests",
    "EfCoreTests",
    "RemoteSshTests",
    # Local runs and mobile workflows.
    "ProgramRunnerTests",
    "MobileWorkflowTests",
    "IosWorkflowGeneratorTests",
    "ListeningPortsTests",
    "MobileProjectTests",
    "GradleCoexistenceTests",
    # Hot Reload, recording and on-device AI.
    "HotReloadTests",
    "HostRuntimeTests",
    "RecordingTests",
    "RecordingFormatTests",
    "DebuggerScreenshotTests",
    "PathMapTests",
    "LocalAITests",
    "LocalNativeTests",
    "LocalAIEditorTests",
    # Microchip toolchains, firmware metadata and inherited USB descriptors.
    "MplabProjectTests",
    "MplabTests",
    "MicrochipInstallationTests",
    "DevicePackTests",
    "PicProjectDetectionTests",
    "PicMemoryUsageTests",
    "PicCompilerDiagnosticTests",
    "UsbDescriptorInheritanceTests",
    "PicFlashProfileTests",
    "BuildOutputTests",

})


def retry_allowed(log: str, project: str) -> tuple[bool, str]:
    if project in FEATURE_PROJECTS:
        return False, "merged feature libraries must pass on the first attempt"
    # MSBuild writes every build error with the project it belongs to: "a.cs(1,2): error CS1002: ; expected [a.csproj]".
    # A test's own output quotes compiler errors too (the Error List tests do), without the project; those are not
    # build failures, and counting them made an unrelated test's failure in the light theme unretriable.
    if re.search(r"\berror (?:CS|MSB|NU)\d+:.*\[[^\]]+\.(?:cs|vb|fs)proj(?:::[^\]]*)?\]\s*$", log, re.M) or re.search(r"^Build FAILED\.", log, re.M):
        return False, "a build failure cannot be retried"
    if re.search(r"test run (?:was )?aborted|test host process crashed|testhost.*exited unexpectedly", log, re.I):
        return False, "an aborted test host cannot prove that only unrelated tests failed"
    failures = re.findall(r"^.*?\]\s+(.+?)\s+\[FAIL\]", log, re.M)
    failures += re.findall(r"^\s*Failed (.+?) \[", log, re.M)
    if not failures:
        return False, "no complete test failure names were reported"
    for name in failures:
        if any(re.search(r"(?:^|\.)" + re.escape(cls) + r"\.", name) for cls in FEATURE_CLASSES):
            return False, "merged feature failed on its first attempt: " + name
    return True, "only unrelated test failures: " + "; ".join(dict.fromkeys(failures))


if __name__ == "__main__":
    allowed, reason = retry_allowed(Path(sys.argv[1]).read_text(errors="replace"), sys.argv[2])
    print(reason)
    sys.exit(0 if allowed else 1)
