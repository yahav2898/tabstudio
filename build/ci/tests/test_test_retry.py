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
        for project in ("TabStudio.Wpf.Tests", "TabStudio.Uno.Tests"):
            self.assertFalse(policy.retry_allowed("  Failed Other.Test [1 ms]", project)[0])
