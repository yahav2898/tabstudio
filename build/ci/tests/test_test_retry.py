import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("retry_policy", Path(__file__).parents[1] / "test-retry.py")
policy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(policy)


class RetryPolicyTests(unittest.TestCase):
    def check(self, name, expected):
        log = f"[xUnit.net 00:01:00]     TabStudio.App.Tests.{name} [FAIL]\n  Failed TabStudio.App.Tests.{name} [1 s]"
        self.assertEqual(expected, policy.retry_allowed(log, "TabStudio.App.Tests")[0])

    def test_all_merged_feature_classes_are_protected(self):
        for name in policy.FEATURE_CLASSES:
            with self.subTest(name=name):
                self.check(name + ".Example", False)

    def test_unrelated_failure_can_retry(self):
        self.check("ScreenshotLifetimeTests.Capturing_is_safe", True)

    def test_mixed_failures_cannot_retry(self):
        log = "  Failed TabStudio.App.Tests.Baseline.Test [1 ms]\n  Failed TabStudio.App.Tests.GoProjectTests.Test [1 ms]"
        self.assertFalse(policy.retry_allowed(log, "TabStudio.App.Tests")[0])

    def test_crashes_and_build_failures_cannot_retry(self):
        for log in ("error CS1234: build failed", "Test Run Aborted.", "  Failed Baseline.Test [1 ms]\nThe active test run was aborted."):
            self.assertFalse(policy.retry_allowed(log, "TabStudio.App.Tests")[0])

    def test_feature_libraries_never_retry(self):
        for project in policy.FEATURE_PROJECTS:
            self.assertFalse(policy.retry_allowed("  Failed Other.Test [1 ms]", project)[0])

    def test_015_feature_regressions_cannot_retry_in_shared_projects(self):
        cases = {
            "TabStudio.App.Tests": [
                "Toolchains.ProjectSetupTests.Setup_is_cancelable", "ViewModels.StaticSiteTests.Saving_reloads",
                "ViewModels.PortingTests.Convert_and_run", "Database.DatabaseExplorerTests.Query",
                "RemoteSsh.RemoteSshTests.Save_conflict",
                "Toolchains.MobileWorkflowTests.Listening_notice", "ViewModels.HotReloadTests.Apply_current_edits",
                "ViewModels.RecordingTests.Rewind", "Views.LocalAIEditorTests.Ghost_text",
                "Views.LanguageServerScreenshotTests.Quick_info_of_a_name",
            ],
            "TabStudio.Compilation.Tests": ["HotReloadTests.Second_generation", "ProgramRunnerTests.Environment"],
            "TabStudio.Debugging.Tests": ["RecordingFormatTests.Evict_complete_records"],
            "TabStudio.Toolchains.Tests": ["Projects.ProjectSetupPlanTests.Manifest_change", "Networking.ListeningPortsTests.Owned_socket", "Microchip.PicProjectDetectionTests.Configuration", "Microchip.UsbDescriptorInheritanceTests.Only_selected_device"],
            "TabStudio.Agent.Tests": ["LocalAITests.Model_checksum", "LocalNativeTests.Inference"],
            "TabStudio.LanguageServers.Tests": ["Hosting.LanguageServerHostTests.Startup_EOF_recovers"],
        }
        for project, names in cases.items():
            for name in names:
                with self.subTest(project=project, name=name):
                    log = f"[xUnit.net 00:01:00]     {project}.{name} [FAIL]"
                    self.assertFalse(policy.retry_allowed(log, project)[0])

    def test_build_error_cannot_hide_behind_an_unrelated_test_failure(self):
        for error in ("error CS1002: ; expected", "error MSB1009: Project file does not exist", "error NU1101: package missing"):
            with self.subTest(error=error):
                log = f"  Failed Baseline.Test [1 ms]\n{error}"
                self.assertFalse(policy.retry_allowed(log, "TabStudio.App.Tests")[0])

    def test_similarly_named_unrelated_class_can_retry(self):
        self.check("NotHotReloadTests.Baseline", True)
