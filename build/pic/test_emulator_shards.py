"""The native PIC probe supplements, rather than replaces, each IDE shard's verdict."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("android-emulator-test.sh").resolve()


class EmulatorShardTests(unittest.TestCase):
    def run_shard(self, shard=None, *, smoke=0, probe=0):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            log = folder / "calls"
            for name, kind, result in [("bash", "smoke", smoke), ("adb", "adb", 0), ("python3", "probe", probe)]:
                command = folder / name
                command.write_text(f'#!/bin/sh\nprintf "%s\\n" "{kind}:$*" >> "$PIC_CALLS"\nexit {result}\n')
                command.chmod(0o755)
            environment = {**os.environ, "PATH": str(folder) + os.pathsep + os.environ["PATH"], "PIC_CALLS": str(log)}
            arguments = ["/bin/bash", str(SCRIPT), "ide.apk", "results", "compiler-work", "probe.apk"]
            if shard is not None:
                arguments.append(shard)
            run = subprocess.run(arguments, env=environment, text=True, capture_output=True, check=False, cwd=folder)
            self.kept_log = (folder / "results" / "emulator-test.log").exists()
            return run.returncode, log.read_text().splitlines() if log.exists() else []

    def test_native_and_manual_all_require_both_the_ide_and_compiler_probe(self):
        for shard in [None, "all", "native"]:
            with self.subTest(shard=shard):
                code, calls = self.run_shard(shard)
                self.assertEqual(0, code)
                self.assertEqual(f"smoke:build/ci/android-smoke-test.sh ide.apk results 900 {shard or 'all'}", calls[0])
                self.assertEqual("adb:shell am force-stop com.tabstudio.ide", calls[1])
                self.assertEqual("adb:install -r probe.apk", calls[2])
                self.assertEqual("probe:build/pic/android_spike.py run --work compiler-work --output results/pic-android", calls[3])
                # The job's log cannot be read after the run, so the script keeps its own with the published files.
                self.assertTrue(self.kept_log)

    def test_other_shards_keep_their_own_verdict_without_repeating_the_pic_probe(self):
        for shard in ["managed", "ui"]:
            for failure in [0, 1]:
                with self.subTest(shard=shard, failure=failure):
                    code, calls = self.run_shard(shard, smoke=failure)
                    self.assertEqual(failure, code)
                    self.assertEqual([f"smoke:build/ci/android-smoke-test.sh ide.apk results 900 {shard}"], calls)

    def test_either_native_verdict_failing_keeps_the_combined_check_red(self):
        for smoke, probe in [(1, 0), (0, 1), (1, 1)]:
            with self.subTest(smoke=smoke, probe=probe):
                code, calls = self.run_shard("native", smoke=smoke, probe=probe)
                self.assertNotEqual(0, code)
                self.assertEqual(4, len(calls))

    def test_unknown_shard_cannot_silently_skip_assertions(self):
        code, calls = self.run_shard("misspelled")
        self.assertEqual(2, code)
        self.assertEqual([], calls)


if __name__ == "__main__":
    unittest.main()
