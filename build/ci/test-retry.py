#!/usr/bin/env python3
"""Allow one retry only when every reported failure is outside the 0.14 features.

An aborted host or a build failure has no complete set of test failures, so it
must fail rather than hiding a possible feature regression behind another run.
"""
import re
import sys
from pathlib import Path

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
    "ToolchainStoreTests",
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
})


def retry_allowed(log: str, project: str) -> tuple[bool, str]:
    if project in {"TabStudio.Wpf.Tests", "TabStudio.Uno.Tests"}:
        return False, "WPF and WinUI tests must pass on the first attempt"
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
